using Avalonia;
using Kudo;

namespace Kudo.Avalonia.Designer;

/// <summary>
/// 提供当前选中连线的路由与途经点编辑能力。
/// </summary>
public sealed class GraphConnectionOperations
{
    private readonly GraphDocumentView _view;

    internal GraphConnectionOperations(GraphDocumentView view) => _view = view;

    public string? SelectedEdgeId => _view.Canvas.SelectedEdgeId;

    public IReadOnlyList<string> SelectedEdgeIds => _view.Canvas.SelectedEdgeIds;

    public IReadOnlyList<EdgeModel> SelectedEdges
    {
        get
        {
            var selectedIds = SelectedEdgeIds.ToHashSet(StringComparer.Ordinal);
            return _view.Session.Document.Edges
                .Where(edge => selectedIds.Contains(edge.Id))
                .ToArray();
        }
    }

    public EdgeModel? SelectedEdge => SelectedEdgeId is { } edgeId
        ? _view.Session.Document.Edges.FirstOrDefault(edge => edge.Id == edgeId)
        : null;

    public EdgeRoutingStyle? RoutingStyle => SelectedEdge?.RoutingStyle;

    public IReadOnlyList<GraphPoint> Waypoints => SelectedEdge?.Waypoints ?? [];

    public ConnectionValidationResult Validate(
        PortReference source,
        PortReference target,
        GraphRelationKind relationKind = GraphRelationKind.Connection,
        string? ignoredEdgeId = null) =>
        _view.Canvas.ValidateConnection(
            source,
            target,
            relationKind,
            ignoredEdgeId);

    public bool Connect(
        PortReference source,
        PortReference target,
        GraphRelationKind relationKind = GraphRelationKind.Connection,
        string? commandName = null)
    {
        var validation = Validate(source, target, relationKind);
        _view.Canvas.LastConnectionValidation = validation;
        if (!validation.IsValid)
        {
            return false;
        }

        var edge = new EdgeModel
        {
            Source = source,
            Target = target,
            RelationKind = relationKind,
            RoutingStyle = _view.Canvas.NewEdgeRoutingStyle
        };
        return _view.TryExecuteCommand(new AddEdgeCommand(
            edge,
            string.IsNullOrWhiteSpace(commandName)
                ? L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.AddEdgeCommand)
                : commandName));
    }

    public bool Reconnect(string edgeId, PortReference source, PortReference target) =>
        _view.Canvas.ReconnectEdge(edgeId, source, target);

    public bool SetRoutingStyle(EdgeRoutingStyle routingStyle)
    {
        if (!Enum.IsDefined(routingStyle))
        {
            return false;
        }

        var commandName = L(
            global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.EditEdgePropertiesCommand);
        var commands = SelectedEdges
            .Where(edge => edge.RoutingStyle != routingStyle)
            .Select(edge => (IGraphCommand)new SetEdgeRoutingStyleCommand(
                edge.Id,
                edge.RoutingStyle,
                routingStyle,
                commandName))
            .ToArray();
        if (commands.Length == 0)
        {
            return false;
        }

        return _view.TryExecuteCommand(commands.Length == 1
            ? commands[0]
            : new CompositeGraphCommand(commandName, commands));
    }

    public bool SetWaypoints(IEnumerable<GraphPoint> waypoints)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        var edge = SelectedEdge;
        if (edge?.RoutingStyle != EdgeRoutingStyle.Manual)
        {
            return false;
        }

        var updated = waypoints.ToArray();
        if (updated.Any(point => !IsFinite(point)) ||
            edge.Waypoints.SequenceEqual(updated))
        {
            return false;
        }

        return _view.TryExecuteCommand(new SetEdgeWaypointsCommand(
            edge.Id,
            edge.Waypoints,
            updated,
            L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.MoveEdgeSegmentCommand)));
    }

    public bool AddWaypoint(GraphPoint waypoint, int? index = null)
    {
        if (!IsFinite(waypoint))
        {
            return false;
        }

        var edge = SelectedEdge;
        if (edge?.RoutingStyle != EdgeRoutingStyle.Manual)
        {
            return false;
        }

        var insertionIndex = index ?? edge.Waypoints.Count;
        if (insertionIndex < 0 || insertionIndex > edge.Waypoints.Count)
        {
            return false;
        }

        return _view.TryExecuteCommand(new AddEdgeWaypointCommand(
            edge.Id,
            insertionIndex,
            waypoint,
            L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.AddEdgeWaypointCommand)));
    }

    public bool AddWaypoint(Point screenPoint) =>
        _view.Canvas.AddSelectedEdgeWaypoint(screenPoint);

    public bool MoveWaypoint(int index, GraphPoint position)
    {
        if (!IsFinite(position))
        {
            return false;
        }

        var edge = SelectedEdge;
        if (edge?.RoutingStyle != EdgeRoutingStyle.Manual ||
            index < 0 ||
            index >= edge.Waypoints.Count ||
            edge.Waypoints[index] == position)
        {
            return false;
        }

        return _view.TryExecuteCommand(new MoveEdgeWaypointCommand(
            edge.Id,
            index,
            edge.Waypoints[index],
            position,
            L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.MoveEdgeWaypointCommand)));
    }

    public bool RemoveWaypoint(int index)
    {
        var edge = SelectedEdge;
        if (edge?.RoutingStyle != EdgeRoutingStyle.Manual ||
            index < 0 ||
            index >= edge.Waypoints.Count)
        {
            return false;
        }

        return _view.TryExecuteCommand(new RemoveEdgeWaypointCommand(
            edge.Id,
            index,
            edge.Waypoints[index],
            L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.RemoveEdgeWaypointCommand)));
    }

    public bool ClearWaypoints()
    {
        var commandName = L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.MoveEdgeSegmentCommand);
        var commands = SelectedEdges
            .Where(edge => edge.RoutingStyle == EdgeRoutingStyle.Manual && edge.Waypoints.Count > 0)
            .Select(edge => (IGraphCommand)new SetEdgeWaypointsCommand(
                edge.Id,
                edge.Waypoints,
                [],
                commandName))
            .ToArray();
        if (commands.Length == 0)
        {
            return false;
        }

        return _view.TryExecuteCommand(commands.Length == 1
            ? commands[0]
            : new CompositeGraphCommand(commandName, commands));
    }

    public int CutIntersecting(GraphPoint start, GraphPoint end) =>
        _view.Canvas.CutEdgesIntersectingSegment(start, end);

    private static string L(string key) => GraphLocalization.Get(key);

    private static bool IsFinite(GraphPoint point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y);
}
