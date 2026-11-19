using Avalonia;
using Avalonia.Media;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private void RenderSelectedEdgeEndpoints(DrawingContext context)
    {
        var document = Document;
        var edge = document?.Edges.FirstOrDefault(candidate => candidate.Id == SelectedEdgeId);
        var route = edge is null ? null : GetEdgeScreenRoute(edge);
        if (edge is null || route is null)
        {
            return;
        }

        var pen = SelectedEdgePen;
        var reconnection = _interactionSession as ReconnectEdgeInteractionSession;
        var hideSource = reconnection is not null &&
            ReferenceEquals(edge, reconnection.Edge) &&
            reconnection.Endpoint == GraphEdgeEndpoint.Source;
        var hideTarget = reconnection is not null &&
            ReferenceEquals(edge, reconnection.Edge) &&
            reconnection.Endpoint == GraphEdgeEndpoint.Target;
        if (!hideSource)
        {
            context.DrawEllipse(CanvasBackgroundBrush, pen, route.Start, EdgeEndpointRadius, EdgeEndpointRadius);
        }

        if (!hideTarget)
        {
            context.DrawEllipse(CanvasBackgroundBrush, pen, route.End, EdgeEndpointRadius, EdgeEndpointRadius);
        }
    }

    private void RenderSelectedEdgeWaypoints(DrawingContext context)
    {
        var edge = Document?.Edges.FirstOrDefault(candidate => candidate.Id == SelectedEdgeId);
        if (edge?.RoutingStyle != EdgeRoutingStyle.Manual || GetEdgeScreenRoute(edge) is null)
        {
            return;
        }

        foreach (var waypoint in edge.Waypoints.Where(point =>
                     double.IsFinite(point.X) && double.IsFinite(point.Y)))
        {
            var position = GraphToScreen(waypoint);
            var rect = new Rect(
                position.X - EdgeWaypointRadius,
                position.Y - EdgeWaypointRadius,
                EdgeWaypointRadius * 2,
                EdgeWaypointRadius * 2);
            context.DrawRectangle(CanvasBackgroundBrush, SelectedEdgePen, rect, 2, 2);
        }
    }

    private void RenderEdgeReconnectionPreview(DrawingContext context)
    {
        if (_interactionSession is not ReconnectEdgeInteractionSession reconnection ||
            reconnection.Edge is not { } edge ||
            GetEdgeGraphPath(edge) is not { } originalPath)
        {
            return;
        }

        var endpoint = reconnection.Endpoint;
        var previewPoint = reconnection.PreviewPoint;
        var start = endpoint == GraphEdgeEndpoint.Source ? ScreenToGraph(previewPoint) : originalPath.Start;
        var end = endpoint == GraphEdgeEndpoint.Target ? ScreenToGraph(previewPoint) : originalPath.End;
        var previewPath = RouteEdge(edge, start, end);
        GraphEdgeRoute.Create(previewPath, GraphToScreen).Draw(context, SelectedEdgePen);
        context.DrawEllipse(
            CanvasBackgroundBrush,
            SelectedEdgePen,
            previewPoint,
            EdgeEndpointRadius,
            EdgeEndpointRadius);
    }
}
