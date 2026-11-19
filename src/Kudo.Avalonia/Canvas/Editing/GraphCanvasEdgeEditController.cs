using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasEdgeEditController(
    GraphCanvasEditContext context,
    GraphCanvasEditMetrics metrics)
{
    public bool ReconnectEdge(string edgeId, PortReference source, PortReference target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edgeId);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (context.IsReadOnly())
        {
            return false;
        }

        var document = context.Document();
        var edge = document?.Edges.FirstOrDefault(candidate => candidate.Id == edgeId);
        if (document is null || edge is null || (edge.Source == source && edge.Target == target))
        {
            return false;
        }

        if (!context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.ReconnectEdge,
                document,
                Edge: edge,
                Source: source,
                Target: target)))
        {
            return false;
        }

        var validation = context.ValidateConnection(
            source,
            target,
            edge.RelationKind,
            edge.Id);
        if (!validation.IsValid)
        {
            context.SetLastConnectionValidation(validation);
            return false;
        }

        var command = new ReconnectEdgeCommand(
            edge.Id,
            edge.Source,
            edge.Target,
            source,
            target,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.ReconnectEdgeCommand));
        command.Execute(document);
        if (!context.CommitGraphEdit(command))
        {
            return false;
        }

        context.ClearSelection();
        context.SetSelectedEdgeId(edge.Id);
        context.SetLastConnectionValidation(validation with
        {
            Message = GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.ConnectionReconnected)
        });
        return true;
    }

    public Point? GetEdgeLabelScreenPosition(EdgeModel edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        return string.IsNullOrWhiteSpace(edge.Label)
            ? null
            : context.GetEdgeScreenRoute(edge)?.GetPointAtProgress(metrics.EdgeLabelProgress);
    }

    public bool AddSelectedEdgeWaypoint(Point screenPoint)
    {
        if (context.IsReadOnly())
        {
            return false;
        }

        var document = context.Document();
        var edge = document?.Edges.FirstOrDefault(candidate => candidate.Id == context.SelectedEdgeId());
        var route = edge is null ? null : context.GetEdgeScreenRoute(edge);
        if (document is null ||
            edge?.RoutingStyle != EdgeRoutingStyle.Manual ||
            route is null ||
            !route.IsNear(screenPoint, metrics.EdgeHitTolerance))
        {
            return false;
        }

        if (!context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.AddEdgeWaypoint,
                document,
                Edge: edge)))
        {
            return false;
        }

        var waypoint = context.ScreenToGraph(screenPoint);
        var index = context.FindWaypointInsertionIndex(edge, route, screenPoint);
        var command = new AddEdgeWaypointCommand(
            edge.Id,
            index,
            waypoint,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.AddEdgeWaypointCommand));
        command.Execute(document);
        return context.CommitGraphEdit(command);
    }

    public bool RemoveSelectedEdgeWaypoint(int index)
    {
        if (context.IsReadOnly())
        {
            return false;
        }

        var document = context.Document();
        var edge = document?.Edges.FirstOrDefault(candidate => candidate.Id == context.SelectedEdgeId());
        if (document is null ||
            edge?.RoutingStyle != EdgeRoutingStyle.Manual ||
            index < 0 ||
            index >= edge.Waypoints.Count)
        {
            return false;
        }

        if (!context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.RemoveEdgeWaypoint,
                document,
                Edge: edge)))
        {
            return false;
        }

        var command = new RemoveEdgeWaypointCommand(
            edge.Id,
            index,
            edge.Waypoints[index],
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.RemoveEdgeWaypointCommand));
        command.Execute(document);
        return context.CommitGraphEdit(command);
    }

    public bool DeleteSelectedEdges()
    {
        var document = context.Document();
        if (context.IsReadOnly() || document is null || context.SelectedEdgeIds().Count == 0)
        {
            return false;
        }

        var edges = document.Edges
            .Where(edge => context.SelectedEdgeIds().Contains(edge.Id))
            .ToArray();
        if (edges.Length == 0 ||
            !context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.DeleteEdges,
                document,
                Edge: edges.FirstOrDefault(edge => edge.Id == context.SelectedEdgeId()) ?? edges[0],
                Edges: edges)))
        {
            return false;
        }

        var commandName = GraphLocalization.Get(
            global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.RemoveEdgeCommand);
        var commands = edges
            .Select(edge => (IGraphCommand)new RemoveEdgeCommand(edge.Id, commandName))
            .ToArray();
        var command = commands.Length == 1
            ? commands[0]
            : new CompositeGraphCommand(commandName, commands);
        command.Execute(document);
        if (!context.CommitGraphEdit(command))
        {
            return false;
        }

        context.SetSelectedEdges([], null);
        return true;
    }
}
