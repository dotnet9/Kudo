using Avalonia;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasHitTester(
    GraphCanvasHitTesterContext context,
    GraphCanvasHitTestMetrics metrics)
{
    public GraphGridRenderMetrics GetGridRenderMetrics() =>
        GraphGridCalculator.GetRenderMetrics(context.GridSettings(), context.Viewport().Zoom);

    public NodeModel? HitTestNode(Point screenPoint)
    {
        var document = context.Document();
        if (document is null)
        {
            return null;
        }

        context.ElementSpatialIndex.RebuildIfNeeded(document);
        return context.ElementSpatialIndex.HitTestNode(context.ScreenToGraph(screenPoint));
    }

    public PortHitResult? HitTestPort(Point screenPoint)
    {
        var document = context.Document();
        if (document is null || !context.ShowPorts())
        {
            return null;
        }

        var graphPoint = context.ScreenToGraph(screenPoint);
        var graphTolerance = context.PortHitRadius() / context.Viewport().Zoom;
        context.ElementSpatialIndex.RebuildIfNeeded(document);
        foreach (var node in context.ElementSpatialIndex.QueryNodes(new GraphRect(
                     graphPoint.X - graphTolerance,
                     graphPoint.Y - graphTolerance,
                     graphTolerance * 2,
                     graphTolerance * 2)).Reverse())
        {
            foreach (var port in node.Ports)
            {
                if (!port.IsVisible)
                {
                    continue;
                }

                var portPoint = context.GetPortScreenPosition(node, port);
                var delta = screenPoint - portPoint;
                if (delta.X * delta.X + delta.Y * delta.Y <= context.PortHitRadius() * context.PortHitRadius())
                {
                    return new PortHitResult(node, port, new PortReference(node.Id, port.Id), portPoint);
                }
            }
        }

        return null;
    }

    public GroupModel? HitTestGroup(Point screenPoint)
    {
        var document = context.Document();
        if (document is null)
        {
            return null;
        }

        context.ElementSpatialIndex.RebuildIfNeeded(document);
        return context.ElementSpatialIndex.HitTestGroup(context.ScreenToGraph(screenPoint));
    }

    public EdgeModel? HitTestEdge(Point screenPoint)
    {
        var document = context.Document();
        if (document is null)
        {
            return null;
        }

        if (!context.IsEdgeRoutingCacheEnabled())
        {
            return document.Edges
                .AsEnumerable()
                .Reverse()
                .FirstOrDefault(edge => context.GetEdgeScreenRoute(edge)?.IsNear(screenPoint, metrics.EdgeHitTolerance) == true);
        }

        context.EdgeSpatialIndex.RebuildIfNeeded(document.Edges, context.EdgeGraphPathProvider);
        return context.EdgeSpatialIndex.HitTest(
            context.ScreenToGraph(screenPoint),
            metrics.EdgeHitTolerance / context.Viewport().Zoom);
    }

    public EdgeSegmentHitResult? HitTestOrthogonalEdgeSegment(
        EdgeModel edge,
        Point screenPoint)
    {
        if (edge.RoutingStyle is not (EdgeRoutingStyle.Orthogonal or EdgeRoutingStyle.Manual) ||
            context.GetEdgeGraphPath(edge) is not { } path)
        {
            return null;
        }

        EdgeSegmentHitResult? bestHit = null;
        var bestDistance = double.PositiveInfinity;
        var maximumDistance = metrics.EdgeHitTolerance * metrics.EdgeHitTolerance;
        for (var index = 1; index < path.Points.Count; index++)
        {
            var graphStart = path.Points[index - 1];
            var graphEnd = path.Points[index];
            if (graphStart == graphEnd)
            {
                continue;
            }

            var orientation = graphStart.Y == graphEnd.Y
                ? OrthogonalEdgeSegmentOrientation.Horizontal
                : graphStart.X == graphEnd.X
                    ? OrthogonalEdgeSegmentOrientation.Vertical
                    : (OrthogonalEdgeSegmentOrientation?)null;
            if (orientation is null)
            {
                continue;
            }

            var distance = DistanceToSegmentSquared(
                screenPoint,
                context.GraphToScreen(graphStart),
                context.GraphToScreen(graphEnd));
            if (distance > maximumDistance || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestHit = new EdgeSegmentHitResult(
                edge,
                index - 1,
                orientation.Value,
                path.Points.ToArray());
        }

        return bestHit;
    }

    public EdgeEndpointHitResult? HitTestSelectedEdgeEndpoint(Point screenPoint)
    {
        if (context.IsReadOnly())
        {
            return null;
        }

        var document = context.Document();
        var edge = document?.Edges.FirstOrDefault(candidate => candidate.Id == context.SelectedEdgeId());
        var route = edge is null ? null : context.GetEdgeScreenRoute(edge);
        if (edge is null || route is null)
        {
            return null;
        }

        var sourceDistance = DistanceSquared(screenPoint, route.Start);
        var targetDistance = DistanceSquared(screenPoint, route.End);
        var maxDistance = metrics.EdgeEndpointHitRadius * metrics.EdgeEndpointHitRadius;
        if (sourceDistance > maxDistance && targetDistance > maxDistance)
        {
            return null;
        }

        return sourceDistance <= targetDistance
            ? new EdgeEndpointHitResult(edge, GraphEdgeEndpoint.Source, edge.Source, route.Start)
            : new EdgeEndpointHitResult(edge, GraphEdgeEndpoint.Target, edge.Target, route.End);
    }

    public EdgeWaypointHitResult? HitTestSelectedEdgeWaypoint(Point screenPoint)
    {
        if (context.IsReadOnly())
        {
            return null;
        }

        var edge = context.Document()?.Edges.FirstOrDefault(candidate => candidate.Id == context.SelectedEdgeId());
        if (edge?.RoutingStyle != EdgeRoutingStyle.Manual || context.GetEdgeScreenRoute(edge) is null)
        {
            return null;
        }

        var maxDistance = metrics.EdgeWaypointHitRadius * metrics.EdgeWaypointHitRadius;
        return edge.Waypoints
            .Select((waypoint, index) => new
            {
                Index = index,
                Waypoint = waypoint,
                ScreenPosition = context.GraphToScreen(waypoint)
            })
            .Where(item => double.IsFinite(item.Waypoint.X) && double.IsFinite(item.Waypoint.Y))
            .Select(item => new
            {
                item.Index,
                item.Waypoint,
                item.ScreenPosition,
                Distance = DistanceSquared(screenPoint, item.ScreenPosition)
            })
            .Where(item => item.Distance <= maxDistance)
            .OrderBy(item => item.Distance)
            .Select(item => new EdgeWaypointHitResult(
                edge,
                item.Index,
                item.Waypoint,
                item.ScreenPosition))
            .FirstOrDefault();
    }

    public NodeResizeHandleHitResult? HitTestSelectedNodeResizeHandle(Point screenPoint)
    {
        if (context.IsReadOnly())
        {
            return null;
        }

        var selectedNodeIds = context.SelectedNodeIds();
        var selectedNodeId = context.SelectedNodeId();
        if (selectedNodeIds.Count != 1 ||
            context.Document()?.FindNode(selectedNodeId ?? string.Empty) is not { } node ||
            node.IsLocked ||
            !context.IsNodeVisible(node.Id))
        {
            return null;
        }

        var maxDistance = metrics.NodeResizeHandleHitRadius * metrics.NodeResizeHandleHitRadius;
        return context.GetNodeResizeHandlePositions(node)
            .Select(item => new
            {
                item.Handle,
                item.ScreenPosition,
                Distance = DistanceSquared(screenPoint, item.ScreenPosition)
            })
            .Where(item => item.Distance <= maxDistance)
            .OrderBy(item => item.Distance)
            .Select(item => new NodeResizeHandleHitResult(node, item.Handle, item.ScreenPosition))
            .FirstOrDefault();
    }

    public NodeRotationHandleHitResult? HitTestSelectedNodeRotationHandle(Point screenPoint)
    {
        if (context.IsReadOnly())
        {
            return null;
        }

        var nodes = context.GetSelectedNodesForRotation();
        if (nodes.Count == 0 || nodes.Any(node => node.IsLocked))
        {
            return null;
        }

        var bounds = context.GetSelectedNodeRotationBounds(nodes);
        if (bounds is null)
        {
            return null;
        }

        var position = nodes.Count == 1
            ? context.GetNodeRotationHandlePosition(nodes[0])
            : context.GetSelectionRotationHandlePositions(bounds.Value).Handle;
        return DistanceSquared(screenPoint, position) <=
               metrics.NodeRotationHandleHitRadius * metrics.NodeRotationHandleHitRadius
            ? new NodeRotationHandleHitResult(
                context.Document()!.FindNode(context.SelectedNodeId() ?? string.Empty) ?? nodes[0],
                position,
                nodes)
            : null;
    }

    private static double DistanceToSegmentSquared(Point point, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        if (dx == 0 && dy == 0)
        {
            return DistanceSquared(point, start);
        }

        var progress = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (dx * dx + dy * dy);
        progress = Math.Clamp(progress, 0, 1);
        return DistanceSquared(point, new Point(start.X + progress * dx, start.Y + progress * dy));
    }

    private static double DistanceSquared(Point first, Point second)
    {
        var delta = first - second;
        return delta.X * delta.X + delta.Y * delta.Y;
    }
}

internal sealed record GraphCanvasHitTestMetrics(
    double EdgeHitTolerance,
    double EdgeEndpointHitRadius,
    double EdgeWaypointHitRadius,
    double NodeResizeHandleHitRadius,
    double NodeRotationHandleHitRadius);

internal sealed class GraphCanvasHitTesterContext
{
    public required Func<GraphDocument?> Document { get; init; }

    public required Func<GraphViewport> Viewport { get; init; }

    public required Func<GraphGridSettings> GridSettings { get; init; }

    public required Func<bool> IsReadOnly { get; init; }

    public required Func<string?> SelectedNodeId { get; init; }

    public required Func<string?> SelectedEdgeId { get; init; }

    public required Func<IReadOnlyCollection<string>> SelectedNodeIds { get; init; }

    public required Func<double> PortHitRadius { get; init; }

    public required Func<bool> ShowPorts { get; init; }

    public required Func<bool> IsEdgeRoutingCacheEnabled { get; init; }

    public required GraphElementSpatialIndex ElementSpatialIndex { get; init; }

    public required GraphEdgeSpatialIndex EdgeSpatialIndex { get; init; }

    public required Func<EdgeModel, GraphEdgePath?> EdgeGraphPathProvider { get; init; }

    public required Func<Point, GraphPoint> ScreenToGraph { get; init; }

    public required Func<GraphPoint, Point> GraphToScreen { get; init; }

    public required Func<NodeModel, PortModel, Point> GetPortScreenPosition { get; init; }

    public required Func<EdgeModel, GraphEdgePath?> GetEdgeGraphPath { get; init; }

    public required Func<EdgeModel, GraphEdgeRoute?> GetEdgeScreenRoute { get; init; }

    public required Func<string, bool> IsNodeVisible { get; init; }

    public required Func<NodeModel, IReadOnlyList<(GraphNodeResizeHandle Handle, Point ScreenPosition)>>
        GetNodeResizeHandlePositions { get; init; }

    public required Func<IReadOnlyList<NodeModel>> GetSelectedNodesForRotation { get; init; }

    public required Func<IReadOnlyList<NodeModel>, GraphRect?> GetSelectedNodeRotationBounds { get; init; }

    public required Func<NodeModel, Point> GetNodeRotationHandlePosition { get; init; }

    public required Func<GraphRect, (Point Anchor, Point Handle)> GetSelectionRotationHandlePositions { get; init; }
}
