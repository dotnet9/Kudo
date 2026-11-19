namespace Kudo;

public enum GraphPortConflictPolicy
{
    Reject,
    RemoveConflictingEdges
}

public sealed class GraphNodeReconfigurationException : InvalidOperationException
{
    public GraphNodeReconfigurationException(string nodeId, IReadOnlyList<string> conflictingEdgeIds)
        : base(CreateMessage(nodeId, conflictingEdgeIds))
    {
        NodeId = nodeId;
        ConflictingEdgeIds = conflictingEdgeIds;
    }

    public string NodeId { get; }

    public IReadOnlyList<string> ConflictingEdgeIds { get; }

    private static string CreateMessage(string nodeId, IReadOnlyList<string> edgeIds) =>
        $"Node '{nodeId}' cannot be reconfigured because edges [{string.Join(", ", edgeIds)}] " +
        "are incompatible with the projected ports.";
}

/// <summary>
/// 原子替换扩展状态及其尺寸、端口投影。冲突连线要么阻止执行，要么与结构一起纳入撤销快照。
/// </summary>
public sealed class ReconfigureNodeCommand : GraphCommand
{
    private readonly GraphExtensionState _newState;
    private readonly GraphNodeProjection _newProjection;
    private readonly IConnectionValidationRule? _connectionValidationRule;
    private readonly IGraphValidationTextProvider? _validationTextProvider;
    private GraphExtensionState? _oldState;
    private GraphSize _oldSize;
    private IReadOnlyList<PortModel> _oldPorts = [];
    private IReadOnlyList<RemovedEdgeSnapshot> _removedEdgeSnapshots = [];
    private bool _isInitialized;
    private bool _isApplied;

    public ReconfigureNodeCommand(
        string nodeId,
        GraphExtensionState newState,
        GraphNodeProjection newProjection,
        GraphPortConflictPolicy conflictPolicy = GraphPortConflictPolicy.Reject,
        IConnectionValidationRule? connectionValidationRule = null,
        IGraphValidationTextProvider? validationTextProvider = null,
        string name = "Reconfigure node") : base(name)
    {
        NodeId = string.IsNullOrWhiteSpace(nodeId)
            ? throw new ArgumentException("A node ID is required.", nameof(nodeId))
            : nodeId;
        _newState = CloneAndValidateState(newState, nameof(newState));
        _newProjection = CloneAndValidateProjection(newProjection, nameof(newProjection));
        ConflictPolicy = Enum.IsDefined(conflictPolicy)
            ? conflictPolicy
            : throw new ArgumentOutOfRangeException(nameof(conflictPolicy));
        _connectionValidationRule = connectionValidationRule;
        _validationTextProvider = validationTextProvider;
    }

    public string NodeId { get; }

    public GraphPortConflictPolicy ConflictPolicy { get; }

    public GraphExtensionState? OldState => _oldState?.DeepClone();

    public GraphExtensionState NewState => _newState.DeepClone();

    public GraphNodeProjection NewProjection => _newProjection.DeepClone();

    public IReadOnlyList<EdgeModel> RemovedEdges => _removedEdgeSnapshots
        .Select(item => item.Edge)
        .ToArray();

    public override void Execute(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_isApplied)
        {
            throw new InvalidOperationException("The command is already applied.");
        }

        var node = document.FindNode(NodeId) ??
            throw new InvalidOperationException($"The node with ID '{NodeId}' does not exist.");
        if (!_isInitialized)
        {
            Initialize(document, node);
        }

        EnsureRemovedEdgesExist(document);
        ApplyProjection(node, _newState, _newProjection);
        RemoveConflictingEdges(document);
        _isApplied = true;
    }

    public override void Undo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_isInitialized || !_isApplied)
        {
            throw new InvalidOperationException("The command has not been executed.");
        }

        var node = document.FindNode(NodeId) ??
            throw new InvalidOperationException($"The node with ID '{NodeId}' does not exist.");
        foreach (var snapshot in _removedEdgeSnapshots)
        {
            if (document.Edges.Any(edge => string.Equals(edge.Id, snapshot.Edge.Id, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"An edge with ID '{snapshot.Edge.Id}' already exists and prevents undo.");
            }
        }

        node.ExtensionState = _oldState?.DeepClone();
        node.Size = _oldSize;
        node.Ports = GraphModelSnapshot.ClonePorts(_oldPorts).ToList();
        foreach (var snapshot in _removedEdgeSnapshots.OrderBy(item => item.Index))
        {
            document.Edges.Insert(Math.Min(snapshot.Index, document.Edges.Count), snapshot.Edge);
        }

        _isApplied = false;
    }

    private void Initialize(GraphDocument document, NodeModel node)
    {
        var conflicts = FindConflictingEdges(
            document,
            node,
            _newProjection,
            _connectionValidationRule,
            _validationTextProvider);
        if (conflicts.Count > 0 && ConflictPolicy == GraphPortConflictPolicy.Reject)
        {
            throw new GraphNodeReconfigurationException(
                NodeId,
                conflicts.Select(item => item.Edge.Id).ToArray());
        }

        _oldState = node.ExtensionState?.DeepClone();
        _oldSize = node.Size;
        _oldPorts = GraphModelSnapshot.ClonePorts(node.Ports);
        _removedEdgeSnapshots = ConflictPolicy == GraphPortConflictPolicy.RemoveConflictingEdges
            ? conflicts
            : [];
        _isInitialized = true;
    }

    private void EnsureRemovedEdgesExist(GraphDocument document)
    {
        foreach (var snapshot in _removedEdgeSnapshots)
        {
            if (!document.Edges.Any(edge => string.Equals(edge.Id, snapshot.Edge.Id, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"The conflicting edge with ID '{snapshot.Edge.Id}' no longer exists.");
            }
        }
    }

    private void RemoveConflictingEdges(GraphDocument document)
    {
        if (_removedEdgeSnapshots.Count == 0)
        {
            return;
        }

        var ids = _removedEdgeSnapshots
            .Select(item => item.Edge.Id)
            .ToHashSet(StringComparer.Ordinal);
        document.Edges.RemoveAll(edge => ids.Contains(edge.Id));
    }

    private static IReadOnlyList<RemovedEdgeSnapshot> FindConflictingEdges(
        GraphDocument document,
        NodeModel node,
        GraphNodeProjection projection,
        IConnectionValidationRule? connectionValidationRule,
        IGraphValidationTextProvider? validationTextProvider)
    {
        var projectedNode = new NodeModel
        {
            Id = node.Id,
            Type = node.Type,
            DefinitionId = node.DefinitionId,
            Number = node.Number,
            Name = node.Name,
            Description = node.Description,
            Position = node.Position,
            Size = projection.Size,
            Rotation = node.Rotation,
            Appearance = node.Appearance,
            ExtensionState = node.ExtensionState?.DeepClone(),
            Ports = GraphModelSnapshot.ClonePorts(projection.Ports).ToList(),
            Properties = node.Properties
        };
        var retainedEdges = new List<EdgeModel>();
        var validationDocument = new GraphDocument
        {
            Nodes = document.Nodes
                .Select(candidate => ReferenceEquals(candidate, node) ? projectedNode : candidate)
                .ToList(),
            Edges = retainedEdges
        };
        var conflicts = new List<RemovedEdgeSnapshot>();
        for (var index = 0; index < document.Edges.Count; index++)
        {
            var edge = document.Edges[index];
            var isIncident = string.Equals(edge.Source.NodeId, node.Id, StringComparison.Ordinal) ||
                             string.Equals(edge.Target.NodeId, node.Id, StringComparison.Ordinal);
            if (!isIncident)
            {
                retainedEdges.Add(edge);
                continue;
            }

            var validation = validationDocument.ValidateConnection(
                edge.Source,
                edge.Target,
                edge.RelationKind,
                connectionValidationRule,
                textProvider: validationTextProvider);
            if (validation.IsValid)
            {
                retainedEdges.Add(edge);
            }
            else
            {
                conflicts.Add(new RemovedEdgeSnapshot(edge, index));
            }
        }

        return conflicts;
    }

    private static void ApplyProjection(
        NodeModel node,
        GraphExtensionState state,
        GraphNodeProjection projection)
    {
        node.ExtensionState = state.DeepClone();
        node.Size = projection.Size;
        node.Ports = GraphModelSnapshot.ClonePorts(projection.Ports).ToList();
    }

    private static GraphExtensionState CloneAndValidateState(
        GraphExtensionState state,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(state, parameterName);
        if (string.IsNullOrWhiteSpace(state.SchemaId))
        {
            throw new ArgumentException("The extension state requires a schema ID.", parameterName);
        }

        if (state.SchemaVersion < 1)
        {
            throw new ArgumentException("The extension state requires a positive schema version.", parameterName);
        }

        if (state.Payload.ValueKind is System.Text.Json.JsonValueKind.Undefined or
            System.Text.Json.JsonValueKind.Null)
        {
            throw new ArgumentException("The extension state requires a JSON payload.", parameterName);
        }

        return state with
        {
            SchemaId = state.SchemaId.Trim(),
            Payload = state.Payload.Clone()
        };
    }

    private static GraphNodeProjection CloneAndValidateProjection(
        GraphNodeProjection projection,
        string parameterName)
    {
        GraphNodeProjectionValidator.Validate(projection, parameterName);
        return projection.DeepClone();
    }

    private sealed record RemovedEdgeSnapshot(EdgeModel Edge, int Index);
}
