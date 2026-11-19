// 节点命令：负责节点增删、移动、尺寸、属性和外观变更。
namespace Kudo;

public sealed class AddNodeCommand(NodeModel node, string name = "Add node") : GraphCommand(name)
{
    public NodeModel Node { get; } = node ?? throw new ArgumentNullException(nameof(node));

    public override void Execute(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FindNode(Node.Id) is not null)
        {
            throw new InvalidOperationException($"A node with ID '{Node.Id}' already exists.");
        }

        document.AddNode(Node);
    }

    public override void Undo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.RemoveNode(Node.Id))
        {
            throw new InvalidOperationException($"The node with ID '{Node.Id}' no longer exists.");
        }
    }
}

public sealed class SetNodeOrderCommand : GraphCommand
{
    public SetNodeOrderCommand(
        IReadOnlyList<string> oldOrder,
        IReadOnlyList<string> newOrder,
        IReadOnlyCollection<string>? affectedNodeIds = null,
        string name = "Arrange nodes") : base(name)
    {
        OldOrder = CopyAndValidate(oldOrder, nameof(oldOrder));
        NewOrder = CopyAndValidate(newOrder, nameof(newOrder));
        if (OldOrder.Count != NewOrder.Count ||
            !OldOrder.ToHashSet(StringComparer.Ordinal).SetEquals(NewOrder))
        {
            throw new ArgumentException(
                "Node orders must contain the same unique node IDs.",
                nameof(newOrder));
        }

        AffectedNodeIds = (affectedNodeIds ?? NewOrder)
            .ToArray();
        if (AffectedNodeIds.Count == 0 ||
            AffectedNodeIds.Any(string.IsNullOrWhiteSpace) ||
            AffectedNodeIds.Distinct(StringComparer.Ordinal).Count() != AffectedNodeIds.Count ||
            AffectedNodeIds.Any(nodeId => !OldOrder.Contains(nodeId, StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                "Affected node IDs must be unique and belong to the node order.",
                nameof(affectedNodeIds));
        }
    }

    public IReadOnlyList<string> OldOrder { get; }

    public IReadOnlyList<string> NewOrder { get; }

    /// <summary>
    /// Gets the nodes whose layer operation was requested. Other nodes may shift around them
    /// while preserving their own relative order.
    /// </summary>
    public IReadOnlyList<string> AffectedNodeIds { get; }

    public override void Execute(GraphDocument document) => Apply(document, NewOrder);

    public override void Undo(GraphDocument document) => Apply(document, OldOrder);

    private static void Apply(GraphDocument document, IReadOnlyList<string> order)
    {
        ArgumentNullException.ThrowIfNull(document);
        var nodesById = document.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        if (nodesById.Count != document.Nodes.Count ||
            nodesById.Count != order.Count ||
            order.Any(nodeId => !nodesById.ContainsKey(nodeId)) ||
            order.Distinct(StringComparer.Ordinal).Count() != order.Count)
        {
            throw new InvalidOperationException("The node order does not match the document.");
        }

        document.Nodes = order.Select(nodeId => nodesById[nodeId]).ToList();
    }

    private static IReadOnlyList<string> CopyAndValidate(
        IReadOnlyList<string> order,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(order, parameterName);
        if (order.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Node IDs cannot be empty.", parameterName);
        }

        return order.ToArray();
    }
}

public sealed class RemoveNodeCommand(string nodeId, string name = "Delete node") : GraphCommand(name)
{
    private readonly List<(EdgeModel Edge, int Index)> _edges = [];
    private readonly List<GroupMembershipSnapshot> _groupMemberships = [];
    private NodeModel? _node;
    private int _nodeIndex;

    public string NodeId { get; } = string.IsNullOrWhiteSpace(nodeId)
        ? throw new ArgumentException("A node ID is required.", nameof(nodeId))
        : nodeId;

    public NodeModel? RemovedNode => _node;

    public override void Execute(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var index = document.Nodes.FindIndex(node => node.Id == NodeId);
        if (index < 0)
        {
            throw new InvalidOperationException($"The node with ID '{NodeId}' does not exist.");
        }

        _nodeIndex = index;
        _node = document.Nodes[index];
        _edges.Clear();
        _groupMemberships.Clear();
        for (var edgeIndex = 0; edgeIndex < document.Edges.Count; edgeIndex++)
        {
            var edge = document.Edges[edgeIndex];
            if (edge.Source.NodeId == NodeId || edge.Target.NodeId == NodeId)
            {
                _edges.Add((edge, edgeIndex));
            }
        }

        for (var groupIndex = 0; groupIndex < document.Groups.Count; groupIndex++)
        {
            var group = document.Groups[groupIndex];
            var nodeIndex = group.NodeIds?.FindIndex(candidate =>
                string.Equals(candidate, NodeId, StringComparison.Ordinal)) ?? -1;
            if (nodeIndex >= 0)
            {
                _groupMemberships.Add(new GroupMembershipSnapshot(group, groupIndex, nodeIndex));
            }
        }

        document.RemoveNode(NodeId);
    }

    public override void Undo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_node is null)
        {
            throw new InvalidOperationException("The command has not been executed.");
        }

        document.Nodes.Insert(Math.Min(_nodeIndex, document.Nodes.Count), _node);
        foreach (var (edge, index) in _edges)
        {
            document.Edges.Insert(Math.Min(index, document.Edges.Count), edge);
        }

        foreach (var membership in _groupMemberships.OrderBy(item => item.GroupIndex))
        {
            var group = document.FindGroup(membership.Group.Id);
            if (group is null)
            {
                group = membership.Group;
                group.NodeIds ??= [];
                document.Groups.Insert(Math.Min(membership.GroupIndex, document.Groups.Count), group);
            }

            if (!group.NodeIds.Contains(NodeId, StringComparer.Ordinal))
            {
                group.NodeIds.Insert(Math.Min(membership.NodeIndex, group.NodeIds.Count), NodeId);
            }
        }
    }

    private sealed record GroupMembershipSnapshot(GroupModel Group, int GroupIndex, int NodeIndex);
}
public sealed class MoveNodeCommand(
    string nodeId,
    GraphPoint oldPosition,
    GraphPoint newPosition,
    string name = "Move node") : GraphCommand(name)
{
    public string NodeId { get; } = string.IsNullOrWhiteSpace(nodeId)
        ? throw new ArgumentException("A node ID is required.", nameof(nodeId))
        : nodeId;

    public GraphPoint OldPosition { get; } = oldPosition;

    public GraphPoint NewPosition { get; } = newPosition;

    public override void Execute(GraphDocument document) => SetPosition(document, NewPosition);

    public override void Undo(GraphDocument document) => SetPosition(document, OldPosition);

    private void SetPosition(GraphDocument document, GraphPoint position)
    {
        ArgumentNullException.ThrowIfNull(document);
        var node = document.FindNode(NodeId) ??
            throw new InvalidOperationException($"The node with ID '{NodeId}' does not exist.");
        node.Position = position;
    }
}

public sealed class ResizeNodeCommand : GraphCommand
{
    public ResizeNodeCommand(
        string nodeId,
        GraphPoint oldPosition,
        GraphSize oldSize,
        GraphPoint newPosition,
        GraphSize newSize,
        string name) : base(name)
    {
        NodeId = string.IsNullOrWhiteSpace(nodeId)
            ? throw new ArgumentException("A node ID is required.", nameof(nodeId))
            : nodeId;
        OldPosition = oldPosition;
        OldSize = oldSize;
        NewPosition = newPosition;
        NewSize = newSize;
    }

    public string NodeId { get; }

    public GraphPoint OldPosition { get; }

    public GraphSize OldSize { get; }

    public GraphPoint NewPosition { get; }

    public GraphSize NewSize { get; }

    public override void Execute(GraphDocument document) =>
        SetBounds(document, NewPosition, NewSize);

    public override void Undo(GraphDocument document) =>
        SetBounds(document, OldPosition, OldSize);

    private void SetBounds(
        GraphDocument document,
        GraphPoint position,
        GraphSize size)
    {
        ArgumentNullException.ThrowIfNull(document);
        var node = document.FindNode(NodeId) ??
            throw new InvalidOperationException($"The node with ID '{NodeId}' does not exist.");
        node.Position = position;
        node.Size = size;
    }
}

public sealed class SetNodeLockedCommand(
    string nodeId,
    bool oldValue,
    bool newValue,
    string name = "Set node lock") : GraphCommand(name)
{
    public string NodeId { get; } = string.IsNullOrWhiteSpace(nodeId)
        ? throw new ArgumentException("A node ID is required.", nameof(nodeId))
        : nodeId;

    public bool OldValue { get; } = oldValue;

    public bool NewValue { get; } = newValue;

    public override void Execute(GraphDocument document) => Apply(document, NewValue);

    public override void Undo(GraphDocument document) => Apply(document, OldValue);

    private void Apply(GraphDocument document, bool value)
    {
        ArgumentNullException.ThrowIfNull(document);
        var node = document.FindNode(NodeId) ??
            throw new InvalidOperationException($"The node with ID '{NodeId}' does not exist.");
        node.IsLocked = value;
    }
}

public sealed class SetNodePositionsCommand : GraphCommand
{
    public SetNodePositionsCommand(
        IReadOnlyDictionary<string, GraphPoint> oldPositions,
        IReadOnlyDictionary<string, GraphPoint> newPositions,
        string name = "Arrange nodes") : base(name)
    {
        ArgumentNullException.ThrowIfNull(oldPositions);
        ArgumentNullException.ThrowIfNull(newPositions);
        OldPositions = new Dictionary<string, GraphPoint>(oldPositions, StringComparer.Ordinal);
        NewPositions = new Dictionary<string, GraphPoint>(newPositions, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, GraphPoint> OldPositions { get; }

    public IReadOnlyDictionary<string, GraphPoint> NewPositions { get; }

    public override void Execute(GraphDocument document) => Apply(document, NewPositions);

    public override void Undo(GraphDocument document) => Apply(document, OldPositions);

    private static void Apply(GraphDocument document, IReadOnlyDictionary<string, GraphPoint> positions)
    {
        ArgumentNullException.ThrowIfNull(document);
        foreach (var node in document.Nodes)
        {
            if (positions.TryGetValue(node.Id, out var position))
            {
                node.Position = position;
            }
        }
    }
}

public sealed class SetNodePropertiesCommand : GraphCommand
{
    public SetNodePropertiesCommand(
        string nodeId,
        string oldNumber,
        string newNumber,
        string? oldName,
        string? newName,
        string? oldDescription,
        string? newDescription,
        GraphPoint oldPosition,
        GraphPoint newPosition,
        string name = "Edit node properties")
        : this(
            nodeId,
            oldNumber,
            newNumber,
            oldName,
            newName,
            oldDescription,
            newDescription,
            oldPosition,
            newPosition,
            0,
            0,
            false,
            name)
    {
    }

    public SetNodePropertiesCommand(
        string nodeId,
        string oldNumber,
        string newNumber,
        string? oldName,
        string? newName,
        string? oldDescription,
        string? newDescription,
        GraphPoint oldPosition,
        GraphPoint newPosition,
        double oldRotation,
        double newRotation,
        string name = "Edit node properties")
        : this(
            nodeId,
            oldNumber,
            newNumber,
            oldName,
            newName,
            oldDescription,
            newDescription,
            oldPosition,
            newPosition,
            oldRotation,
            newRotation,
            true,
            name)
    {
    }

    private SetNodePropertiesCommand(
        string nodeId,
        string oldNumber,
        string newNumber,
        string? oldName,
        string? newName,
        string? oldDescription,
        string? newDescription,
        GraphPoint oldPosition,
        GraphPoint newPosition,
        double oldRotation,
        double newRotation,
        bool updatesRotation,
        string name) : base(name)
    {
        NodeId = string.IsNullOrWhiteSpace(nodeId)
            ? throw new ArgumentException("A node ID is required.", nameof(nodeId))
            : nodeId;
        OldNumber = oldNumber ?? throw new ArgumentNullException(nameof(oldNumber));
        NewNumber = newNumber ?? throw new ArgumentNullException(nameof(newNumber));
        OldName = oldName;
        NewName = newName;
        OldDescription = oldDescription;
        NewDescription = newDescription;
        OldPosition = oldPosition;
        NewPosition = newPosition;
        OldRotation = GraphNodeGeometry.NormalizeRotation(oldRotation);
        NewRotation = GraphNodeGeometry.NormalizeRotation(newRotation);
        UpdatesRotation = updatesRotation;
    }

    public string NodeId { get; }

    public string OldNumber { get; }

    public string NewNumber { get; }

    public string? OldName { get; }

    public string? NewName { get; }

    public string? OldDescription { get; }

    public string? NewDescription { get; }

    public GraphPoint OldPosition { get; }

    public GraphPoint NewPosition { get; }

    public double OldRotation { get; }

    public double NewRotation { get; }

    public bool UpdatesRotation { get; }

    public override void Execute(GraphDocument document) =>
        Apply(document, NewNumber, NewName, NewDescription, NewPosition, NewRotation);

    public override void Undo(GraphDocument document) =>
        Apply(document, OldNumber, OldName, OldDescription, OldPosition, OldRotation);

    private void Apply(
        GraphDocument document,
        string number,
        string? name,
        string? description,
        GraphPoint position,
        double rotation)
    {
        ArgumentNullException.ThrowIfNull(document);
        var node = document.FindNode(NodeId) ??
            throw new InvalidOperationException($"The node with ID '{NodeId}' does not exist.");
        if (!string.IsNullOrWhiteSpace(number) && document.Nodes.Any(candidate =>
                candidate.Id != node.Id &&
                string.Equals(candidate.Number, number, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"The node number '{number}' is already in use.");
        }

        node.Number = number;
        node.Name = name;
        node.Description = description;
        node.Position = position;
        if (UpdatesRotation)
        {
            node.Rotation = rotation;
        }
    }
}

public sealed class SetNodeAppearanceCommand(
    string nodeId,
    GraphNodeAppearance oldAppearance,
    GraphNodeAppearance newAppearance,
    string name = "Edit node appearance") : GraphCommand(name)
{
    public string NodeId { get; } = string.IsNullOrWhiteSpace(nodeId)
        ? throw new ArgumentException("A node ID is required.", nameof(nodeId))
        : nodeId;

    public GraphNodeAppearance OldAppearance { get; } =
        (oldAppearance ?? throw new ArgumentNullException(nameof(oldAppearance))).Normalize();

    public GraphNodeAppearance NewAppearance { get; } =
        (newAppearance ?? throw new ArgumentNullException(nameof(newAppearance))).Normalize();

    public override void Execute(GraphDocument document) => Apply(document, NewAppearance);

    public override void Undo(GraphDocument document) => Apply(document, OldAppearance);

    private void Apply(GraphDocument document, GraphNodeAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(document);
        var node = document.FindNode(NodeId) ??
            throw new InvalidOperationException($"The node with ID '{NodeId}' does not exist.");
        node.Appearance = appearance;
    }
}

public sealed class SetNodeExtensionPropertiesCommand : GraphCommand
{
    private readonly Dictionary<string, object?> _oldProperties;
    private readonly Dictionary<string, object?> _newProperties;

    public SetNodeExtensionPropertiesCommand(
        string nodeId,
        IReadOnlyDictionary<string, object?> oldProperties,
        IReadOnlyDictionary<string, object?> newProperties,
        string name = "Edit node extension properties")
        : base(name)
    {
        NodeId = string.IsNullOrWhiteSpace(nodeId)
            ? throw new ArgumentException("A node ID is required.", nameof(nodeId))
            : nodeId;
        _oldProperties = CopyProperties(oldProperties, nameof(oldProperties));
        _newProperties = CopyProperties(newProperties, nameof(newProperties));
    }

    public string NodeId { get; }

    public IReadOnlyDictionary<string, object?> OldProperties => _oldProperties;

    public IReadOnlyDictionary<string, object?> NewProperties => _newProperties;

    public override void Execute(GraphDocument document) => Apply(document, _newProperties);

    public override void Undo(GraphDocument document) => Apply(document, _oldProperties);

    private void Apply(
        GraphDocument document,
        IReadOnlyDictionary<string, object?> properties)
    {
        ArgumentNullException.ThrowIfNull(document);
        var node = document.FindNode(NodeId) ??
            throw new InvalidOperationException($"The node with ID '{NodeId}' does not exist.");
        node.Properties = CopyProperties(properties, nameof(properties));
    }

    private static Dictionary<string, object?> CopyProperties(
        IReadOnlyDictionary<string, object?> properties,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(properties, parameterName);
        return new Dictionary<string, object?>(properties, StringComparer.Ordinal);
    }
}
