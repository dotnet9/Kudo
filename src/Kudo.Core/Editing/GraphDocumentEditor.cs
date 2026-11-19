// 文档编辑入口：统一执行命令、合并批量变更并向订阅方发布 GraphChangeSet。
using System.ComponentModel;

namespace Kudo;

public enum GraphDocumentEditDirection
{
    [Description("执行")]
    Execute,

    [Description("撤销")]
    Undo,

    [Description("重做")]
    Redo,

    [Description("外部同步")]
    Synchronize
}

public sealed class GraphDocumentChangedEventArgs(
    GraphChangeSet changes,
    GraphDocumentEditDirection direction,
    IGraphCommand? command = null) : EventArgs
{
    public GraphChangeSet Changes { get; } = changes ?? throw new ArgumentNullException(nameof(changes));

    public GraphDocumentEditDirection Direction { get; } = direction;

    public IGraphCommand? Command { get; } = command;
}

/// <summary>
/// GraphDocument 的统一命令写入入口。未知命令自动回退为全量同步，保证扩展命令不会留下过期缓存。
/// </summary>
public sealed class GraphDocumentEditor
{
    private int _batchDepth;
    private GraphChangeSet _batchedChanges = GraphChangeSet.Empty;
    private GraphDocumentEditDirection _batchedDirection = GraphDocumentEditDirection.Execute;
    private bool _batchContainsSynchronization;

    public GraphDocumentEditor(GraphDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public GraphDocument Document { get; }

    public GraphChangeSet LastChangeSet { get; private set; } = GraphChangeSet.Empty;

    public event EventHandler<GraphDocumentChangedEventArgs>? Changed;

    public GraphChangeSet Execute(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute(Document);
        return Publish(command, GraphDocumentEditDirection.Execute, reverse: false);
    }

    /// <summary>
    /// 发布已经由交互热路径写入模型的命令，避免为了记录历史再次执行命令。
    /// </summary>
    public GraphChangeSet RecordExecuted(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Publish(command, GraphDocumentEditDirection.Execute, reverse: false);
    }

    public GraphChangeSet Undo(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Undo(Document);
        return Publish(command, GraphDocumentEditDirection.Undo, reverse: true);
    }

    public GraphChangeSet Redo(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute(Document);
        return Publish(command, GraphDocumentEditDirection.Redo, reverse: false);
    }

    /// <summary>
    /// 为直接修改模型的调用方提供显式同步入口；未提供范围时安全回退为全量刷新。
    /// </summary>
    public GraphChangeSet Synchronize(GraphChangeSet? changes = null)
    {
        var normalized = changes ?? GraphChangeSet.FullRefresh;
        QueueOrPublish(normalized, GraphDocumentEditDirection.Synchronize, null);
        return normalized;
    }

    /// <summary>
    /// 合并作用域内的通知；嵌套 Batch 只在最外层释放时发布一次聚合变更。
    /// </summary>
    public IDisposable BeginBatch() => new BatchScope(this);

    private GraphChangeSet Publish(
        IGraphCommand command,
        GraphDocumentEditDirection direction,
        bool reverse)
    {
        var changes = GraphCommandChangeSetFactory.Create(command, reverse);
        QueueOrPublish(changes, direction, command);
        return changes;
    }

    private void QueueOrPublish(
        GraphChangeSet changes,
        GraphDocumentEditDirection direction,
        IGraphCommand? command)
    {
        LastChangeSet = changes;
        if (_batchDepth > 0)
        {
            _batchedChanges = _batchedChanges.Merge(changes);
            _batchedDirection = direction;
            _batchContainsSynchronization |= direction == GraphDocumentEditDirection.Synchronize;
            return;
        }

        Changed?.Invoke(this, new GraphDocumentChangedEventArgs(changes, direction, command));
    }

    private void EnterBatch()
    {
        if (_batchDepth == 0)
        {
            _batchedChanges = GraphChangeSet.Empty;
            _batchedDirection = GraphDocumentEditDirection.Execute;
            _batchContainsSynchronization = false;
        }

        _batchDepth++;
    }

    private void ExitBatch()
    {
        if (_batchDepth <= 0)
        {
            throw new InvalidOperationException("GraphDocumentEditor Batch scopes must be disposed in order.");
        }

        _batchDepth--;
        if (_batchDepth != 0 || _batchedChanges.IsEmpty)
        {
            return;
        }

        var changes = _batchedChanges;
        var direction = _batchContainsSynchronization
            ? GraphDocumentEditDirection.Synchronize
            : _batchedDirection;
        _batchedChanges = GraphChangeSet.Empty;
        _batchContainsSynchronization = false;
        LastChangeSet = changes;
        Changed?.Invoke(
            this,
            new GraphDocumentChangedEventArgs(
                changes,
                direction));
    }

    private sealed class BatchScope : IDisposable
    {
        private GraphDocumentEditor? _editor;

        public BatchScope(GraphDocumentEditor editor)
        {
            _editor = editor;
            editor.EnterBatch();
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _editor, null)?.ExitBatch();
        }
    }
}

internal static class GraphCommandChangeSetFactory
{
    public static GraphChangeSet Create(IGraphCommand command, bool reverse)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command is CompositeGraphCommand composite)
        {
            var commands = reverse ? composite.Commands.Reverse() : composite.Commands;
            return commands.Aggregate(
                GraphChangeSet.Empty,
                (current, child) => current.Merge(Create(child, reverse)));
        }

        return command switch
        {
            SetDocumentTitleCommand or SetDocumentPropertiesCommand => Change(GraphChangeKind.Document),
            SetCanvasSettingsCommand => Change(GraphChangeKind.Settings),
            AddNodeCommand value => Change(
                GraphChangeKind.Structure | GraphChangeKind.Geometry,
                nodes: GraphElementChangeSet.Added(value.Node.Id, reverse)),
            SetNodeOrderCommand => Change(
                GraphChangeKind.Structure | GraphChangeKind.Geometry,
                nodes: GraphElementChangeSet.Reset),
            RemoveNodeCommand value => Change(
                GraphChangeKind.Structure | GraphChangeKind.Geometry | GraphChangeKind.Routing,
                nodes: GraphElementChangeSet.Removed(value.NodeId, reverse),
                edges: GraphElementChangeSet.Reset,
                groups: GraphElementChangeSet.Reset),
            MoveNodeCommand value => NodeGeometry(value.NodeId),
            ResizeNodeCommand value => NodeGeometry(value.NodeId),
            SetNodePositionsCommand value => Change(
                GraphChangeKind.Geometry | GraphChangeKind.Routing,
                nodes: GraphElementChangeSet.Modified(value.NewPositions.Keys)),
            SetNodePropertiesCommand value => NodeProperties(value),
            SetNodeAppearanceCommand value => Change(
                GraphChangeKind.Appearance,
                nodes: GraphElementChangeSet.Modified(value.NodeId)),
            SetNodeExtensionPropertiesCommand value => Change(
                GraphChangeKind.Properties,
                nodes: GraphElementChangeSet.Modified(value.NodeId)),
            ReconfigureNodeCommand value => Change(
                GraphChangeKind.Structure |
                GraphChangeKind.Geometry |
                GraphChangeKind.Routing |
                GraphChangeKind.Appearance |
                GraphChangeKind.Properties,
                nodes: GraphElementChangeSet.Modified(value.NodeId),
                edges: GraphElementChangeSet.Reset,
                groups: GraphElementChangeSet.Reset),
            AddEdgeCommand value => Change(
                GraphChangeKind.Structure | GraphChangeKind.Geometry | GraphChangeKind.Routing,
                edges: GraphElementChangeSet.Added(value.Edge.Id, reverse)),
            RemoveEdgeCommand value => Change(
                GraphChangeKind.Structure | GraphChangeKind.Geometry | GraphChangeKind.Routing,
                edges: GraphElementChangeSet.Removed(value.EdgeId, reverse)),
            SetEdgeRoutingStyleCommand value => EdgeGeometry(value.EdgeId),
            SetEdgeStylePresetCommand value => Change(
                GraphChangeKind.Appearance,
                edges: GraphElementChangeSet.Modified(value.EdgeId)),
            ReconnectEdgeCommand value => EdgeGeometry(value.EdgeId),
            SetEdgePropertiesCommand value => Change(
                GraphChangeKind.Geometry |
                GraphChangeKind.Routing |
                GraphChangeKind.Appearance |
                GraphChangeKind.Properties,
                edges: GraphElementChangeSet.Modified(value.EdgeId)),
            SetEdgeWaypointsCommand value => EdgeGeometry(value.EdgeId),
            AddEdgeWaypointCommand value => EdgeGeometry(value.EdgeId),
            MoveEdgeWaypointCommand value => EdgeGeometry(value.EdgeId),
            RemoveEdgeWaypointCommand value => EdgeGeometry(value.EdgeId),
            AddGroupCommand value => Change(
                GraphChangeKind.Structure | GraphChangeKind.Geometry,
                groups: GraphElementChangeSet.Added(value.Group.Id, reverse)),
            RemoveGroupCommand value => Change(
                GraphChangeKind.Structure | GraphChangeKind.Geometry,
                groups: GraphElementChangeSet.Removed(value.GroupId, reverse)),
            SetGroupCollapsedCommand value => Change(
                GraphChangeKind.Geometry | GraphChangeKind.Routing,
                groups: GraphElementChangeSet.Modified(value.GroupId),
                nodes: GraphElementChangeSet.Reset,
                edges: GraphElementChangeSet.Reset),
            SetGroupPropertiesCommand value => Change(
                GraphChangeKind.Geometry | GraphChangeKind.Properties,
                groups: GraphElementChangeSet.Modified(value.GroupId)),
            AddAnnotationCommand value => Change(
                GraphChangeKind.Structure | GraphChangeKind.Geometry,
                annotations: GraphElementChangeSet.Added(value.Annotation.Id, reverse)),
            RemoveAnnotationCommand value => Change(
                GraphChangeKind.Structure | GraphChangeKind.Geometry,
                annotations: GraphElementChangeSet.Removed(value.AnnotationId, reverse)),
            MoveAnnotationCommand value => AnnotationGeometry(value.AnnotationId),
            ResizeAnnotationCommand value => AnnotationGeometry(value.AnnotationId),
            SetAnnotationTextCommand value => Change(
                GraphChangeKind.Appearance,
                annotations: GraphElementChangeSet.Modified(value.AnnotationId)),
            SetAnnotationPropertiesCommand value => Change(
                GraphChangeKind.Geometry | GraphChangeKind.Appearance | GraphChangeKind.Properties,
                annotations: GraphElementChangeSet.Modified(value.AnnotationId)),
            _ => GraphChangeSet.FullRefresh
        };
    }

    private static GraphChangeSet NodeGeometry(string nodeId) => Change(
        GraphChangeKind.Geometry | GraphChangeKind.Routing,
        nodes: GraphElementChangeSet.Modified(nodeId),
        edges: GraphElementChangeSet.Reset,
        groups: GraphElementChangeSet.Reset);

    private static GraphChangeSet NodeProperties(SetNodePropertiesCommand command)
    {
        var kind = GraphChangeKind.Properties;
        if (!string.Equals(command.OldName, command.NewName, StringComparison.Ordinal) ||
            !string.Equals(command.OldDescription, command.NewDescription, StringComparison.Ordinal))
        {
            kind |= GraphChangeKind.Appearance;
        }

        var properties = Change(
            kind,
            nodes: GraphElementChangeSet.Modified(command.NodeId));
        return command.OldPosition != command.NewPosition ||
               command.UpdatesRotation && !command.OldRotation.Equals(command.NewRotation)
            ? properties.Merge(NodeGeometry(command.NodeId))
            : properties;
    }

    private static GraphChangeSet EdgeGeometry(string edgeId) => Change(
        GraphChangeKind.Geometry | GraphChangeKind.Routing,
        edges: GraphElementChangeSet.Modified(edgeId));

    private static GraphChangeSet AnnotationGeometry(string annotationId) => Change(
        GraphChangeKind.Geometry,
        annotations: GraphElementChangeSet.Modified(annotationId));

    private static GraphChangeSet Change(
        GraphChangeKind kind,
        GraphElementChangeSet? nodes = null,
        GraphElementChangeSet? edges = null,
        GraphElementChangeSet? groups = null,
        GraphElementChangeSet? annotations = null) => new(
        kind,
        nodes ?? GraphElementChangeSet.Empty,
        edges ?? GraphElementChangeSet.Empty,
        groups ?? GraphElementChangeSet.Empty,
        annotations ?? GraphElementChangeSet.Empty);
}
