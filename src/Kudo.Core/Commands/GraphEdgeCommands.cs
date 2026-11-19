// 连线命令：负责连线结构、端点、路由、外观、属性和途经点变更。
namespace Kudo;

public sealed class AddEdgeCommand(EdgeModel edge, string name = "Add edge") : GraphCommand(name)
{
    public EdgeModel Edge { get; } = edge ?? throw new ArgumentNullException(nameof(edge));

    public override void Execute(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Edges.Any(candidate => candidate.Id == Edge.Id))
        {
            throw new InvalidOperationException($"An edge with ID '{Edge.Id}' already exists.");
        }

        document.AddEdge(Edge);
    }

    public override void Undo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.RemoveEdge(Edge.Id))
        {
            throw new InvalidOperationException($"The edge with ID '{Edge.Id}' no longer exists.");
        }
    }
}
public sealed class RemoveEdgeCommand(string edgeId, string name = "Delete edge") : GraphCommand(name)
{
    private EdgeModel? _edge;
    private int _index;

    public string EdgeId { get; } = string.IsNullOrWhiteSpace(edgeId)
        ? throw new ArgumentException("An edge ID is required.", nameof(edgeId))
        : edgeId;

    public EdgeModel? RemovedEdge => _edge;

    public override void Execute(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var index = document.Edges.FindIndex(edge => edge.Id == EdgeId);
        if (index < 0)
        {
            throw new InvalidOperationException($"The edge with ID '{EdgeId}' does not exist.");
        }

        _index = index;
        _edge = document.Edges[index];
        document.Edges.RemoveAt(index);
    }

    public override void Undo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_edge is null)
        {
            throw new InvalidOperationException("The command has not been executed.");
        }

        document.Edges.Insert(Math.Min(_index, document.Edges.Count), _edge);
    }
}

public sealed class SetEdgeRoutingStyleCommand(
    string edgeId,
    EdgeRoutingStyle oldStyle,
    EdgeRoutingStyle newStyle,
    string name = "Change edge routing") : GraphCommand(name)
{
    public string EdgeId { get; } = string.IsNullOrWhiteSpace(edgeId)
        ? throw new ArgumentException("An edge ID is required.", nameof(edgeId))
        : edgeId;

    public EdgeRoutingStyle OldStyle { get; } = oldStyle;

    public EdgeRoutingStyle NewStyle { get; } = newStyle;

    public override void Execute(GraphDocument document) => SetStyle(document, NewStyle);

    public override void Undo(GraphDocument document) => SetStyle(document, OldStyle);

    private void SetStyle(GraphDocument document, EdgeRoutingStyle style)
    {
        ArgumentNullException.ThrowIfNull(document);
        var edge = document.Edges.FirstOrDefault(candidate => candidate.Id == EdgeId) ??
            throw new InvalidOperationException($"The edge with ID '{EdgeId}' does not exist.");
        edge.RoutingStyle = style;
    }
}

public sealed class SetEdgeStylePresetCommand : GraphCommand
{
    public SetEdgeStylePresetCommand(
        string edgeId,
        GraphEdgeStylePreset oldPreset,
        GraphEdgeAppearance oldAppearance,
        GraphEdgeStylePreset newPreset,
        string name = "Change edge style") : base(name)
    {
        EdgeId = string.IsNullOrWhiteSpace(edgeId)
            ? throw new ArgumentException("An edge ID is required.", nameof(edgeId))
            : edgeId;
        OldPreset = oldPreset;
        OldAppearance = (oldAppearance ?? throw new ArgumentNullException(nameof(oldAppearance))).Normalize();
        NewPreset = newPreset;
        NewAppearance = newPreset == GraphEdgeStylePreset.Custom
            ? OldAppearance
            : GraphEdgeAppearance.FromPreset(newPreset);
    }

    public string EdgeId { get; }

    public GraphEdgeStylePreset OldPreset { get; }

    public GraphEdgeAppearance OldAppearance { get; }

    public GraphEdgeStylePreset NewPreset { get; }

    public GraphEdgeAppearance NewAppearance { get; }

    public override void Execute(GraphDocument document) => Apply(document, NewPreset, NewAppearance);

    public override void Undo(GraphDocument document) => Apply(document, OldPreset, OldAppearance);

    private void Apply(
        GraphDocument document,
        GraphEdgeStylePreset preset,
        GraphEdgeAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(document);
        var edge = document.Edges.FirstOrDefault(candidate => candidate.Id == EdgeId) ??
            throw new InvalidOperationException($"The edge with ID '{EdgeId}' does not exist.");
        edge.StylePreset = preset;
        edge.Appearance = appearance;
    }
}

public sealed class ReconnectEdgeCommand(
    string edgeId,
    PortReference oldSource,
    PortReference oldTarget,
    PortReference newSource,
    PortReference newTarget,
    string name = "Reconnect edge") : GraphCommand(name)
{
    public string EdgeId { get; } = string.IsNullOrWhiteSpace(edgeId)
        ? throw new ArgumentException("An edge ID is required.", nameof(edgeId))
        : edgeId;

    public PortReference OldSource { get; } = oldSource ?? throw new ArgumentNullException(nameof(oldSource));

    public PortReference OldTarget { get; } = oldTarget ?? throw new ArgumentNullException(nameof(oldTarget));

    public PortReference NewSource { get; } = newSource ?? throw new ArgumentNullException(nameof(newSource));

    public PortReference NewTarget { get; } = newTarget ?? throw new ArgumentNullException(nameof(newTarget));

    public override void Execute(GraphDocument document) => SetEndpoints(document, NewSource, NewTarget);

    public override void Undo(GraphDocument document) => SetEndpoints(document, OldSource, OldTarget);

    private void SetEndpoints(GraphDocument document, PortReference source, PortReference target)
    {
        ArgumentNullException.ThrowIfNull(document);
        var edge = document.Edges.FirstOrDefault(candidate => candidate.Id == EdgeId) ??
            throw new InvalidOperationException($"The edge with ID '{EdgeId}' does not exist.");
        edge.Source = source;
        edge.Target = target;
    }
}

public sealed class SetEdgePropertiesCommand : GraphCommand
{
    private readonly string? _oldLabel;
    private readonly string? _newLabel;
    private readonly EdgeRoutingStyle _oldRoutingStyle;
    private readonly EdgeRoutingStyle _newRoutingStyle;
    private readonly EdgeMarker _oldSourceMarker;
    private readonly EdgeMarker _newSourceMarker;
    private readonly EdgeMarker _oldTargetMarker;
    private readonly EdgeMarker _newTargetMarker;
    private readonly bool _updateMarkers;

    public SetEdgePropertiesCommand(
        string edgeId,
        string? oldLabel,
        string? newLabel,
        EdgeRoutingStyle oldRoutingStyle,
        EdgeRoutingStyle newRoutingStyle,
        string name)
        : this(
            edgeId,
            oldLabel,
            newLabel,
            oldRoutingStyle,
            newRoutingStyle,
            EdgeMarker.None,
            EdgeMarker.None,
            EdgeMarker.None,
            EdgeMarker.None,
            name)
    {
        _updateMarkers = false;
    }

    public SetEdgePropertiesCommand(
        string edgeId,
        string? oldLabel,
        string? newLabel,
        EdgeRoutingStyle oldRoutingStyle,
        EdgeRoutingStyle newRoutingStyle,
        EdgeMarker oldSourceMarker,
        EdgeMarker newSourceMarker,
        EdgeMarker oldTargetMarker,
        EdgeMarker newTargetMarker,
        string name)
        : base(name)
    {
        EdgeId = string.IsNullOrWhiteSpace(edgeId)
            ? throw new ArgumentException("An edge ID is required.", nameof(edgeId))
            : edgeId;
        _oldLabel = oldLabel;
        _newLabel = newLabel;
        _oldRoutingStyle = oldRoutingStyle;
        _newRoutingStyle = newRoutingStyle;
        _oldSourceMarker = oldSourceMarker;
        _newSourceMarker = newSourceMarker;
        _oldTargetMarker = oldTargetMarker;
        _newTargetMarker = newTargetMarker;
        _updateMarkers = true;
    }

    public string EdgeId { get; }

    public override void Execute(GraphDocument document) => Apply(
        document,
        _newLabel,
        _newRoutingStyle,
        _newSourceMarker,
        _newTargetMarker);

    public override void Undo(GraphDocument document) => Apply(
        document,
        _oldLabel,
        _oldRoutingStyle,
        _oldSourceMarker,
        _oldTargetMarker);

    private void Apply(
        GraphDocument document,
        string? label,
        EdgeRoutingStyle routingStyle,
        EdgeMarker sourceMarker,
        EdgeMarker targetMarker)
    {
        ArgumentNullException.ThrowIfNull(document);
        var edge = document.Edges.FirstOrDefault(candidate => candidate.Id == EdgeId) ??
            throw new InvalidOperationException($"The edge with ID '{EdgeId}' does not exist.");
        edge.Label = label;
        edge.RoutingStyle = routingStyle;
        if (_updateMarkers)
        {
            edge.SourceMarker = sourceMarker;
            edge.TargetMarker = targetMarker;
        }
    }
}

public sealed class SetEdgeWaypointsCommand : GraphCommand
{
    private readonly GraphPoint[] _oldWaypoints;
    private readonly GraphPoint[] _newWaypoints;

    public SetEdgeWaypointsCommand(
        string edgeId,
        IEnumerable<GraphPoint> oldWaypoints,
        IEnumerable<GraphPoint> newWaypoints,
        string name = "Edit edge path")
        : base(name)
    {
        EdgeId = RequireEdgeId(edgeId);
        _oldWaypoints = CopyValidWaypoints(oldWaypoints, nameof(oldWaypoints));
        _newWaypoints = CopyValidWaypoints(newWaypoints, nameof(newWaypoints));
    }

    public string EdgeId { get; }

    public IReadOnlyList<GraphPoint> OldWaypoints => _oldWaypoints;

    public IReadOnlyList<GraphPoint> NewWaypoints => _newWaypoints;

    public override void Execute(GraphDocument document) => Apply(document, _newWaypoints);

    public override void Undo(GraphDocument document) => Apply(document, _oldWaypoints);

    private void Apply(GraphDocument document, IReadOnlyCollection<GraphPoint> waypoints)
    {
        var edge = FindEdge(document, EdgeId);
        edge.Waypoints = [.. waypoints];
    }

    internal static EdgeModel FindEdge(GraphDocument document, string edgeId)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Edges.FirstOrDefault(candidate => candidate.Id == edgeId) ??
               throw new InvalidOperationException($"The edge with ID '{edgeId}' does not exist.");
    }

    internal static string RequireEdgeId(string edgeId) => string.IsNullOrWhiteSpace(edgeId)
        ? throw new ArgumentException("An edge ID is required.", nameof(edgeId))
        : edgeId;

    internal static void RequireValidWaypoint(GraphPoint waypoint, string paramName)
    {
        if (!double.IsFinite(waypoint.X) || !double.IsFinite(waypoint.Y))
        {
            throw new ArgumentOutOfRangeException(paramName, "A waypoint must use finite coordinates.");
        }
    }

    private static GraphPoint[] CopyValidWaypoints(
        IEnumerable<GraphPoint> waypoints,
        string paramName)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        var result = waypoints.ToArray();
        foreach (var waypoint in result)
        {
            RequireValidWaypoint(waypoint, paramName);
        }

        return result;
    }
}

public sealed class AddEdgeWaypointCommand : GraphCommand
{
    public AddEdgeWaypointCommand(
        string edgeId,
        int index,
        GraphPoint waypoint,
        string name = "Add edge waypoint")
        : base(name)
    {
        EdgeId = SetEdgeWaypointsCommand.RequireEdgeId(edgeId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        SetEdgeWaypointsCommand.RequireValidWaypoint(waypoint, nameof(waypoint));
        Index = index;
        Waypoint = waypoint;
    }

    public string EdgeId { get; }

    public int Index { get; }

    public GraphPoint Waypoint { get; }

    public override void Execute(GraphDocument document)
    {
        var edge = SetEdgeWaypointsCommand.FindEdge(document, EdgeId);
        if (Index > edge.Waypoints.Count)
        {
            throw new InvalidOperationException($"Waypoint index {Index} is outside the edge waypoint collection.");
        }

        edge.Waypoints.Insert(Index, Waypoint);
    }

    public override void Undo(GraphDocument document)
    {
        var edge = SetEdgeWaypointsCommand.FindEdge(document, EdgeId);
        RequireWaypointAtIndex(edge, Index, Waypoint);
        edge.Waypoints.RemoveAt(Index);
    }

    internal static void RequireWaypointAtIndex(EdgeModel edge, int index, GraphPoint waypoint)
    {
        if (index < 0 || index >= edge.Waypoints.Count || edge.Waypoints[index] != waypoint)
        {
            throw new InvalidOperationException(
                $"The expected waypoint at index {index} no longer exists on edge '{edge.Id}'.");
        }
    }
}

public sealed class MoveEdgeWaypointCommand : GraphCommand
{
    public MoveEdgeWaypointCommand(
        string edgeId,
        int index,
        GraphPoint oldPosition,
        GraphPoint newPosition,
        string name = "Move edge waypoint")
        : base(name)
    {
        EdgeId = SetEdgeWaypointsCommand.RequireEdgeId(edgeId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        SetEdgeWaypointsCommand.RequireValidWaypoint(oldPosition, nameof(oldPosition));
        SetEdgeWaypointsCommand.RequireValidWaypoint(newPosition, nameof(newPosition));
        Index = index;
        OldPosition = oldPosition;
        NewPosition = newPosition;
    }

    public string EdgeId { get; }

    public int Index { get; }

    public GraphPoint OldPosition { get; }

    public GraphPoint NewPosition { get; }

    public override void Execute(GraphDocument document) => Move(document, OldPosition, NewPosition);

    public override void Undo(GraphDocument document) => Move(document, NewPosition, OldPosition);

    private void Move(GraphDocument document, GraphPoint expected, GraphPoint position)
    {
        var edge = SetEdgeWaypointsCommand.FindEdge(document, EdgeId);
        AddEdgeWaypointCommand.RequireWaypointAtIndex(edge, Index, expected);
        edge.Waypoints[Index] = position;
    }
}

public sealed class RemoveEdgeWaypointCommand : GraphCommand
{
    public RemoveEdgeWaypointCommand(
        string edgeId,
        int index,
        GraphPoint waypoint,
        string name = "Remove edge waypoint")
        : base(name)
    {
        EdgeId = SetEdgeWaypointsCommand.RequireEdgeId(edgeId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        SetEdgeWaypointsCommand.RequireValidWaypoint(waypoint, nameof(waypoint));
        Index = index;
        Waypoint = waypoint;
    }

    public string EdgeId { get; }

    public int Index { get; }

    public GraphPoint Waypoint { get; }

    public override void Execute(GraphDocument document)
    {
        var edge = SetEdgeWaypointsCommand.FindEdge(document, EdgeId);
        AddEdgeWaypointCommand.RequireWaypointAtIndex(edge, Index, Waypoint);
        edge.Waypoints.RemoveAt(Index);
    }

    public override void Undo(GraphDocument document)
    {
        var edge = SetEdgeWaypointsCommand.FindEdge(document, EdgeId);
        if (Index > edge.Waypoints.Count)
        {
            throw new InvalidOperationException($"Waypoint index {Index} is outside the edge waypoint collection.");
        }

        edge.Waypoints.Insert(Index, Waypoint);
    }
}
