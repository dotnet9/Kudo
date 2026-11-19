namespace Kudo;

/// <summary>
/// Describes a successful history operation after the document has been updated.
/// </summary>
public enum GraphOperationOrigin
{
    Programmatic,
    User
}

public static class GraphOperationInteractions
{
    public const string DragDrop = "drag-drop";
    public const string Paste = "paste";
    public const string Duplicate = "duplicate";
}

public sealed record GraphOperationContext(
    GraphOperationOrigin Origin = GraphOperationOrigin.Programmatic,
    string? InteractionKey = null,
    string? OperationKey = null)
{
    public static GraphOperationContext Programmatic { get; } = new();

    public static GraphOperationContext User(
        string? interactionKey = null,
        string? operationKey = null) =>
        new(GraphOperationOrigin.User, interactionKey, operationKey);

    public bool IsUserInitiated => Origin == GraphOperationOrigin.User;
}

/// <summary>
/// Raised after a successful command execution, undo, or redo.
/// The context identifies whether the change came from a user interaction.
/// </summary>
public sealed class GraphHistoryOperationChangedEventArgs(
    GraphDocumentEditDirection direction,
    IGraphCommand command,
    GraphChangeSet changes,
    long beforeRevision,
    long afterRevision,
    GraphOperationContext? operationContext = null) : EventArgs
{
    public GraphDocumentEditDirection Direction { get; } = direction;

    public IGraphCommand Command { get; } = command ?? throw new ArgumentNullException(nameof(command));

    public GraphChangeSet Changes { get; } = changes ?? throw new ArgumentNullException(nameof(changes));

    public long BeforeRevision { get; } = beforeRevision;

    public long AfterRevision { get; } = afterRevision;

    public GraphOperationContext OperationContext { get; } =
        operationContext ?? GraphOperationContext.Programmatic;
}

/// <summary>
/// Context passed to a history description provider. The culture is a hint for providers
/// that own localized business text; Kudo itself does not resolve resources here.
/// </summary>
public sealed record GraphHistoryDescriptionContext(
    GraphDocument Document,
    IGraphCommand Command,
    GraphDocumentEditDirection Direction,
    GraphChangeSet Changes,
    string? CultureName = null,
    GraphOperationContext? OperationContext = null)
{
    public GraphDocument Document { get; } = Document ?? throw new ArgumentNullException(nameof(Document));

    public IGraphCommand Command { get; } = Command ?? throw new ArgumentNullException(nameof(Command));

    public GraphChangeSet Changes { get; } = Changes ?? throw new ArgumentNullException(nameof(Changes));

    public GraphOperationContext OperationContext { get; } =
        OperationContext ?? GraphOperationContext.Programmatic;
}

/// <summary>Stable operation keys used by built-in history descriptions.</summary>
public static class GraphHistoryOperationKeys
{
    public const string GraphEdit = "graph.edit";
    public const string BatchEdit = "graph.batch-edit";
    public const string AddNode = "node.add";
    public const string DropNode = "node.drop";
    public const string Paste = "graph.paste";
    public const string Duplicate = "graph.duplicate";
    public const string DeleteNode = "node.delete";
    public const string MoveNode = "node.move";
    public const string ResizeNode = "node.resize";
    public const string LockNode = "node.lock";
    public const string ArrangeNodes = "node.arrange";
    public const string EditNode = "node.edit";
    public const string EditNodeParameters = "node.edit-parameters";
    public const string EditNodeAppearance = "node.edit-appearance";
    public const string AddConnection = "connection.add";
    public const string DeleteConnection = "connection.delete";
    public const string EditConnection = "connection.edit";
    public const string ReconnectConnection = "connection.reconnect";
    public const string AddGroup = "group.add";
    public const string DeleteGroup = "group.delete";
    public const string EditGroup = "group.edit";
    public const string AddAnnotation = "annotation.add";
    public const string DeleteAnnotation = "annotation.delete";
    public const string MoveAnnotation = "annotation.move";
    public const string ResizeAnnotation = "annotation.resize";
    public const string EditAnnotation = "annotation.edit";
    public const string EditDocument = "document.edit";
    public const string EditCanvas = "canvas.edit";
}

/// <summary>Stable property keys emitted by Kudo's built-in history descriptions.</summary>
public static class GraphHistoryPropertyKeys
{
    public const string NodeNumber = "node.number";
    public const string NodeName = "node.name";
    public const string NodeDescription = "node.description";
    public const string NodePosition = "node.position";
    public const string NodeRotation = "node.rotation";
    public const string DocumentTitle = "document.title";
    public const string DocumentCreatedBy = "document.created-by";
    public const string DocumentCreatedAt = "document.created-at";
    public const string DocumentDescription = "document.description";
}

public sealed record GraphHistorySubject(
    string Kind,
    string? Id = null,
    string? Type = null,
    string? DisplayName = null,
    IReadOnlyDictionary<string, string?>? Metadata = null)
{
    public static GraphHistorySubject Empty { get; } = new("graph");

    public string Kind { get; } = string.IsNullOrWhiteSpace(Kind)
        ? throw new ArgumentException("A history subject kind is required.", nameof(Kind))
        : Kind;
}

public sealed record GraphHistoryPropertyChange(
    string Key,
    object? OldValue,
    object? NewValue,
    string? Unit = null)
{
    public string Key { get; } = string.IsNullOrWhiteSpace(Key)
        ? throw new ArgumentException("A history property key is required.", nameof(Key))
        : Key;
}

public sealed class GraphHistoryDescription
{
    public GraphHistoryDescription(
        string operationKey,
        GraphHistorySubject? subject = null,
        IEnumerable<GraphHistoryPropertyChange>? changes = null,
        bool suppressLog = false)
    {
        OperationKey = string.IsNullOrWhiteSpace(operationKey)
            ? throw new ArgumentException("A history operation key is required.", nameof(operationKey))
            : operationKey;
        Subject = subject ?? GraphHistorySubject.Empty;
        Changes = (changes ?? []).ToArray();
        SuppressLog = suppressLog;
    }

    public string OperationKey { get; }

    public GraphHistorySubject Subject { get; }

    public IReadOnlyList<GraphHistoryPropertyChange> Changes { get; }

    /// <summary>Suppresses user-facing output for known no-op changes.</summary>
    public bool SuppressLog { get; }
}

/// <summary>
/// Supplies host- or extension-specific history semantics without coupling Kudo to logging
/// or localization libraries. Return null when the provider does not own the command.
/// </summary>
public interface IGraphHistoryDescriptionProvider
{
    GraphHistoryDescription? Describe(GraphHistoryDescriptionContext context);
}

/// <summary>Combines built-in and extension history descriptions by provider priority.</summary>
public sealed class GraphHistoryDescriptionRegistry
{
    private readonly List<IGraphHistoryDescriptionProvider> _providers = [];

    public void Register(IGraphHistoryDescriptionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providers.Add(provider);
    }

    public bool TryDescribe(
        GraphHistoryDescriptionContext context,
        out GraphHistoryDescription? description)
    {
        ArgumentNullException.ThrowIfNull(context);
        var normalized = NormalizeComposite(context);
        for (var index = _providers.Count - 1; index >= 0; index--)
        {
            var candidate = _providers[index].Describe(normalized);
            if (candidate is null)
            {
                continue;
            }

            description = candidate;
            return true;
        }

        description = null;
        return false;
    }

    private static GraphHistoryDescriptionContext NormalizeComposite(
        GraphHistoryDescriptionContext context)
    {
        if (context.Command is not CompositeGraphCommand composite ||
            composite.Commands.Count != 1 ||
            context.OperationContext.InteractionKey is
                GraphOperationInteractions.Paste or
                GraphOperationInteractions.Duplicate)
        {
            return context;
        }

        return new GraphHistoryDescriptionContext(
            context.Document,
            composite.Commands[0],
            context.Direction,
            context.Changes,
            context.CultureName,
            context.OperationContext);
    }
}

/// <summary>Describes the standard Kudo commands without producing localized text.</summary>
public sealed class GraphBuiltInHistoryDescriptionProvider : IGraphHistoryDescriptionProvider
{
    public GraphHistoryDescription? Describe(GraphHistoryDescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var command = context.Command;
        var operationOverride = context.OperationContext.OperationKey;
        if (command is CompositeGraphCommand)
        {
            var composite = (CompositeGraphCommand)command;
            var operationKey = operationOverride ?? context.OperationContext.InteractionKey switch
            {
                GraphOperationInteractions.Paste => GraphHistoryOperationKeys.Paste,
                GraphOperationInteractions.Duplicate => GraphHistoryOperationKeys.Duplicate,
                _ => DescribeCompositeOperation(composite)
            };
            return new GraphHistoryDescription(operationKey);
        }

        return command switch
        {
            AddNodeCommand value => Create(
                operationOverride ?? (context.OperationContext.InteractionKey == GraphOperationInteractions.DragDrop
                    ? GraphHistoryOperationKeys.DropNode
                    : GraphHistoryOperationKeys.AddNode),
                Node(context.Document, value.Node)),
            RemoveNodeCommand value => Create(
                GraphHistoryOperationKeys.DeleteNode,
                Node(context.Document, value.RemovedNode ?? context.Document.FindNode(value.NodeId))),
            MoveNodeCommand value => Create(operationOverride ?? GraphHistoryOperationKeys.MoveNode, Node(context.Document, value.NodeId)),
            ResizeNodeCommand value => Create(operationOverride ?? GraphHistoryOperationKeys.ResizeNode, Node(context.Document, value.NodeId)),
            SetNodeLockedCommand value => Create(operationOverride ?? GraphHistoryOperationKeys.LockNode, Node(context.Document, value.NodeId)),
            SetNodePositionsCommand or SetNodeOrderCommand => Create(operationOverride ?? GraphHistoryOperationKeys.ArrangeNodes),
            SetNodePropertiesCommand value => DescribeNodeProperties(context, value, operationOverride),
            SetNodeAppearanceCommand value => Create(operationOverride ?? GraphHistoryOperationKeys.EditNodeAppearance, Node(context.Document, value.NodeId)),
            SetNodeExtensionPropertiesCommand value => Create(operationOverride ?? GraphHistoryOperationKeys.EditNodeParameters, Node(context.Document, value.NodeId)),
            ReconfigureNodeCommand value => Create(operationOverride ?? GraphHistoryOperationKeys.EditNodeParameters, Node(context.Document, value.NodeId)),
            AddEdgeCommand value => Create(
                operationOverride ?? GraphHistoryOperationKeys.AddConnection,
                Connection(value.Edge)),
            RemoveEdgeCommand value => Create(
                operationOverride ?? GraphHistoryOperationKeys.DeleteConnection,
                Connection(value.RemovedEdge ?? context.Document.FindEdge(value.EdgeId))),
            SetEdgeRoutingStyleCommand or SetEdgeStylePresetCommand or SetEdgePropertiesCommand or
                SetEdgeWaypointsCommand or AddEdgeWaypointCommand or MoveEdgeWaypointCommand or
                RemoveEdgeWaypointCommand => Create(
                    operationOverride ?? GraphHistoryOperationKeys.EditConnection,
                    Connection(context.Document, command)),
            ReconnectEdgeCommand value => Create(
                operationOverride ?? GraphHistoryOperationKeys.ReconnectConnection,
                Connection(value, context.Direction)),
            AddGroupCommand => Create(operationOverride ?? GraphHistoryOperationKeys.AddGroup),
            RemoveGroupCommand => Create(operationOverride ?? GraphHistoryOperationKeys.DeleteGroup),
            SetGroupCollapsedCommand or SetGroupPropertiesCommand => Create(operationOverride ?? GraphHistoryOperationKeys.EditGroup),
            AddAnnotationCommand => Create(operationOverride ?? GraphHistoryOperationKeys.AddAnnotation),
            RemoveAnnotationCommand => Create(operationOverride ?? GraphHistoryOperationKeys.DeleteAnnotation),
            MoveAnnotationCommand => Create(operationOverride ?? GraphHistoryOperationKeys.MoveAnnotation),
            ResizeAnnotationCommand => Create(operationOverride ?? GraphHistoryOperationKeys.ResizeAnnotation),
            SetAnnotationTextCommand or SetAnnotationPropertiesCommand => Create(operationOverride ?? GraphHistoryOperationKeys.EditAnnotation),
            SetDocumentTitleCommand value => DescribeDocumentTitle(value, operationOverride),
            SetDocumentPropertiesCommand value => DescribeDocumentProperties(value, operationOverride),
            SetCanvasSettingsCommand => Create(operationOverride ?? GraphHistoryOperationKeys.EditCanvas),
            _ => Create(operationOverride ?? GraphHistoryOperationKeys.GraphEdit)
        };
    }

    private static GraphHistoryDescription Create(
        string operationKey,
        GraphHistorySubject? subject = null) =>
        new(operationKey, subject);

    private static GraphHistoryDescription DescribeNodeProperties(
        GraphHistoryDescriptionContext context,
        SetNodePropertiesCommand command,
        string? operationOverride)
    {
        var changes = new List<GraphHistoryPropertyChange>();
        AddChange(changes, GraphHistoryPropertyKeys.NodeNumber, command.OldNumber, command.NewNumber);
        AddChange(changes, GraphHistoryPropertyKeys.NodeName, command.OldName, command.NewName);
        AddChange(changes, GraphHistoryPropertyKeys.NodeDescription, command.OldDescription, command.NewDescription);
        AddChange(changes, GraphHistoryPropertyKeys.NodePosition, FormatPoint(command.OldPosition), FormatPoint(command.NewPosition));
        if (command.UpdatesRotation)
        {
            AddChange(
                changes,
                GraphHistoryPropertyKeys.NodeRotation,
                command.OldRotation,
                command.NewRotation,
                "°");
        }

        return new GraphHistoryDescription(
            operationOverride ?? GraphHistoryOperationKeys.EditNode,
            Node(context.Document, command.NodeId),
            changes,
            suppressLog: changes.Count == 0);
    }

    private static GraphHistoryDescription DescribeDocumentTitle(
        SetDocumentTitleCommand command,
        string? operationOverride) =>
        new(
            operationOverride ?? GraphHistoryOperationKeys.EditDocument,
            new GraphHistorySubject("document"),
            [new(GraphHistoryPropertyKeys.DocumentTitle, command.OldTitle, command.NewTitle)]);

    private static GraphHistoryDescription DescribeDocumentProperties(
        SetDocumentPropertiesCommand command,
        string? operationOverride)
    {
        var changes = new List<GraphHistoryPropertyChange>();
        AddChange(changes, GraphHistoryPropertyKeys.DocumentTitle, command.OldTitle, command.NewTitle);
        AddChange(changes, GraphHistoryPropertyKeys.DocumentCreatedBy, command.OldCreatedBy, command.NewCreatedBy);
        AddChange(changes, GraphHistoryPropertyKeys.DocumentCreatedAt, command.OldCreatedAt, command.NewCreatedAt);
        AddChange(changes, GraphHistoryPropertyKeys.DocumentDescription, command.OldDescription, command.NewDescription);
        return new GraphHistoryDescription(
            operationOverride ?? GraphHistoryOperationKeys.EditDocument,
            new GraphHistorySubject("document"),
            changes,
            suppressLog: changes.Count == 0);
    }

    private static void AddChange(
        ICollection<GraphHistoryPropertyChange> changes,
        string key,
        object? oldValue,
        object? newValue,
        string? unit = null)
    {
        if (!Equals(oldValue, newValue))
        {
            changes.Add(new GraphHistoryPropertyChange(key, oldValue, newValue, unit));
        }
    }

    private static string FormatPoint(GraphPoint point) =>
        point.X.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
        point.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static GraphHistorySubject Connection(EdgeModel? edge)
    {
        if (edge is null)
        {
            return new GraphHistorySubject("connection");
        }

        return Connection(edge.Id, edge.Source, edge.Target, edge.RelationKind.ToString());
    }

    private static GraphHistorySubject Connection(GraphDocument document, IGraphCommand command)
    {
        var edgeId = command switch
        {
            SetEdgeRoutingStyleCommand value => value.EdgeId,
            SetEdgeStylePresetCommand value => value.EdgeId,
            SetEdgePropertiesCommand value => value.EdgeId,
            SetEdgeWaypointsCommand value => value.EdgeId,
            AddEdgeWaypointCommand value => value.EdgeId,
            MoveEdgeWaypointCommand value => value.EdgeId,
            RemoveEdgeWaypointCommand value => value.EdgeId,
            _ => null
        };
        return Connection(document.FindEdge(edgeId ?? string.Empty));
    }

    private static GraphHistorySubject Connection(
        ReconnectEdgeCommand command,
        GraphDocumentEditDirection direction)
    {
        var source = direction == GraphDocumentEditDirection.Undo ? command.OldSource : command.NewSource;
        var target = direction == GraphDocumentEditDirection.Undo ? command.OldTarget : command.NewTarget;
        return Connection(command.EdgeId, source, target, null);
    }

    private static GraphHistorySubject Connection(
        string edgeId,
        PortReference source,
        PortReference target,
        string? relationKind)
    {
        var metadata = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["source-node-id"] = source.NodeId,
            ["source-port-id"] = source.PortId,
            ["target-node-id"] = target.NodeId,
            ["target-port-id"] = target.PortId
        };
        if (!string.IsNullOrWhiteSpace(relationKind))
        {
            metadata["relation-kind"] = relationKind;
        }

        return new GraphHistorySubject("connection", edgeId, relationKind, Metadata: metadata);
    }

    private static string DescribeCompositeOperation(CompositeGraphCommand command)
    {
        if (command.Commands.Count > 0 &&
            command.Commands.All(item => item is AddNodeCommand))
        {
            return GraphHistoryOperationKeys.AddNode;
        }

        if (command.Commands.Count > 0 &&
            command.Commands.All(item => item is RemoveNodeCommand))
        {
            return GraphHistoryOperationKeys.DeleteNode;
        }

        if (command.Commands.Count > 0 &&
            command.Commands.All(item => item is AddGroupCommand))
        {
            return GraphHistoryOperationKeys.AddGroup;
        }

        if (command.Commands.Count > 0 &&
            command.Commands.All(item => item is RemoveGroupCommand))
        {
            return GraphHistoryOperationKeys.DeleteGroup;
        }

        if (command.Commands.Count > 0 &&
            command.Commands.All(item => item is RemoveEdgeCommand))
        {
            return GraphHistoryOperationKeys.DeleteConnection;
        }

        return GraphHistoryOperationKeys.BatchEdit;
    }

    private static GraphHistorySubject Node(GraphDocument document, NodeModel? node) =>
        node is null
            ? new("node")
            : new("node", node.Id, node.Type, GraphHistoryIdentity.FormatNode(node));

    private static GraphHistorySubject Node(GraphDocument document, string nodeId) =>
        Node(document, document.FindNode(nodeId));
}
