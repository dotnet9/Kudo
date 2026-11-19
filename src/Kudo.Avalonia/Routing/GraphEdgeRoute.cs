using Avalonia;
using Avalonia.Media;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public sealed class GraphEdgeRoute
{
    private GraphEdgeRoute(
        EdgeRoutingStyle routingStyle,
        IReadOnlyList<Point> points,
        Point? controlPoint1 = null,
        Point? controlPoint2 = null,
        bool isFallback = false)
    {
        RoutingStyle = routingStyle;
        Points = points;
        ControlPoint1 = controlPoint1;
        ControlPoint2 = controlPoint2;
        IsFallback = isFallback;
    }

    public EdgeRoutingStyle RoutingStyle { get; }

    public IReadOnlyList<Point> Points { get; }

    public Point Start => Points[0];

    public Point End => Points[^1];

    public Point? ControlPoint1 { get; }

    public Point? ControlPoint2 { get; }

    public bool IsFallback { get; }

    public Vector? GetStartTangent()
    {
        for (var index = 1; index < Points.Count; index++)
        {
            if (TryCreateUnitVector(Start, Points[index], out var tangent))
            {
                return tangent;
            }
        }

        return null;
    }

    public Vector? GetEndTangent()
    {
        for (var index = Points.Count - 2; index >= 0; index--)
        {
            if (TryCreateUnitVector(Points[index], End, out var tangent))
            {
                return tangent;
            }
        }

        return null;
    }

    public Point GetPointAtProgress(double progress)
    {
        if (double.IsNaN(progress))
        {
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress cannot be NaN.");
        }

        var normalizedProgress = Math.Clamp(progress, 0, 1);
        if (normalizedProgress <= 0 || Points.Count == 1)
        {
            return Start;
        }

        if (normalizedProgress >= 1)
        {
            return End;
        }

        var totalLength = 0.0;
        for (var index = 1; index < Points.Count; index++)
        {
            totalLength += Math.Sqrt(DistanceSquared(Points[index - 1], Points[index]));
        }

        if (totalLength <= double.Epsilon)
        {
            return Start;
        }

        var targetLength = totalLength * normalizedProgress;
        var traversedLength = 0.0;
        for (var index = 1; index < Points.Count; index++)
        {
            var segmentLength = Math.Sqrt(DistanceSquared(Points[index - 1], Points[index]));
            if (segmentLength <= double.Epsilon)
            {
                continue;
            }

            if (traversedLength + segmentLength >= targetLength)
            {
                var segmentProgress = (targetLength - traversedLength) / segmentLength;
                var start = Points[index - 1];
                var end = Points[index];
                return new Point(
                    start.X + (end.X - start.X) * segmentProgress,
                    start.Y + (end.Y - start.Y) * segmentProgress);
            }

            traversedLength += segmentLength;
        }

        return End;
    }

    internal static GraphEdgeRoute CreateManual(
        GraphPoint start,
        GraphPoint end,
        IReadOnlyList<GraphPoint> waypoints,
        Func<GraphPoint, Point> transform,
        GraphPoint? trailingWaypoint = null)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        ArgumentNullException.ThrowIfNull(transform);

        var routePoints = new List<GraphPoint>(waypoints.Count + 3)
        {
            start
        };
        for (var waypointIndex = 0; waypointIndex < waypoints.Count; waypointIndex++)
        {
            var waypoint = waypoints[waypointIndex];
            if (ShouldAppendManualPoint(routePoints[^1], waypoint))
            {
                routePoints.Add(waypoint);
            }
        }

        if (trailingWaypoint is { } trailing &&
            ShouldAppendManualPoint(routePoints[^1], trailing))
        {
            routePoints.Add(trailing);
        }

        if (ShouldAppendManualPoint(routePoints[^1], end))
        {
            routePoints.Add(end);
        }

        var preferredDirection = routePoints.Count > 1
            ? new GraphPoint(
                routePoints[1].X - routePoints[0].X,
                routePoints[1].Y - routePoints[0].Y)
            : new GraphPoint(1, 0);
        var orthogonalPoints = ManualConnectionGeometry.CreateOrthogonalPath(
            routePoints,
            preferredDirection);
        return new GraphEdgeRoute(
            EdgeRoutingStyle.Manual,
            orthogonalPoints.Select(transform).ToArray());
    }

    public static GraphEdgeRoute Create(EdgeRoutingStyle routingStyle, Point start, Point end) =>
        Create(routingStyle, start, end, waypoints: null, 1);

    public static GraphEdgeRoute Create(
        EdgeRoutingStyle routingStyle,
        Point start,
        Point end,
        double zoom) =>
        Create(routingStyle, start, end, waypoints: null, zoom);

    public static GraphEdgeRoute Create(
        EdgeRoutingStyle routingStyle,
        Point start,
        Point end,
        IReadOnlyList<Point>? waypoints) =>
        Create(routingStyle, start, end, waypoints, 1);

    public static GraphEdgeRoute Create(
        EdgeRoutingStyle routingStyle,
        Point start,
        Point end,
        IReadOnlyList<Point>? waypoints,
        double zoom)
    {
        var normalizedZoom = Math.Max(0.01, Math.Abs(zoom));
        var path = GraphEdgeRoutingService.Default.Route(new GraphEdgeRoutingRequest(
            routingStyle,
            ToGraphPoint(start),
            ToGraphPoint(end))
        {
            Waypoints = waypoints?.Select(ToGraphPoint).ToArray() ?? [],
            Options = new GraphEdgeRoutingOptions
            {
                MinimumBezierControlOffset = 50 * normalizedZoom,
                MaximumBezierControlOffset = 150 * normalizedZoom
            }
        });
        return Create(path, ToAvaloniaPoint);
    }

    public static GraphEdgeRoute Create(
        GraphEdgePath path,
        Func<GraphPoint, Point> transform)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(transform);
        return new GraphEdgeRoute(
            path.RoutingStyle,
            path.Points.Select(transform).ToArray(),
            path.ControlPoint1 is { } controlPoint1 ? transform(controlPoint1) : null,
            path.ControlPoint2 is { } controlPoint2 ? transform(controlPoint2) : null,
            path.IsFallback);
    }

    public bool IsNear(Point point, double tolerance)
    {
        if (tolerance < 0)
        {
            return false;
        }

        var toleranceSquared = tolerance * tolerance;
        for (var index = 1; index < Points.Count; index++)
        {
            if (DistanceToSegmentSquared(point, Points[index - 1], Points[index]) <= toleranceSquared)
            {
                return true;
            }
        }

        return false;
    }

    public void Draw(DrawingContext context, IPen pen)
        => Draw(context, pen, EdgeMarker.None, EdgeMarker.None);

    public void Draw(
        DrawingContext context,
        IPen pen,
        EdgeMarker sourceMarker,
        EdgeMarker targetMarker)
    {
        const double markerSize = 8;
        var startTangent = GetStartTangent();
        var endTangent = GetEndTangent();
        var startInset = sourceMarker == EdgeMarker.None || startTangent is null ? 0 : markerSize;
        var endInset = targetMarker == EdgeMarker.None || endTangent is null ? 0 : markerSize;

        if (startInset == 0 &&
            endInset == 0 &&
            RoutingStyle == EdgeRoutingStyle.Bezier &&
            ControlPoint1 is { } controlPoint1 &&
            ControlPoint2 is { } controlPoint2)
        {
            var geometry = new StreamGeometry();
            using (var geometryContext = geometry.Open())
            {
                geometryContext.BeginFigure(Start, false);
                geometryContext.CubicBezierTo(controlPoint1, controlPoint2, End);
                geometryContext.EndFigure(false);
            }

            context.DrawGeometry(null, pen, geometry);
        }
        else
        {
            var visiblePoints = GetTrimmedPoints(startInset, endInset);
            for (var index = 1; index < visiblePoints.Count; index++)
            {
                context.DrawLine(pen, visiblePoints[index - 1], visiblePoints[index]);
            }
        }

        if (startTangent is { } sourceTangent)
        {
            DrawMarker(
                context,
                pen,
                sourceMarker,
                Start,
                new Vector(-sourceTangent.X, -sourceTangent.Y),
                markerSize);
        }

        if (endTangent is { } targetTangent)
        {
            DrawMarker(context, pen, targetMarker, End, targetTangent, markerSize);
        }
    }

    private IReadOnlyList<Point> GetTrimmedPoints(double startInset, double endInset)
    {
        if (startInset <= 0 && endInset <= 0)
        {
            return Points;
        }

        var cumulativeLengths = new double[Points.Count];
        for (var index = 1; index < Points.Count; index++)
        {
            cumulativeLengths[index] = cumulativeLengths[index - 1] +
                                       Math.Sqrt(DistanceSquared(Points[index - 1], Points[index]));
        }

        var totalLength = cumulativeLengths[^1];
        if (totalLength <= double.Epsilon)
        {
            return [Start];
        }

        var startDistance = Math.Clamp(startInset, 0, totalLength);
        var endDistance = Math.Clamp(totalLength - endInset, startDistance, totalLength);
        var result = new List<Point> { GetPointAtDistance(startDistance, cumulativeLengths) };
        for (var index = 1; index < Points.Count - 1; index++)
        {
            if (cumulativeLengths[index] > startDistance && cumulativeLengths[index] < endDistance)
            {
                result.Add(Points[index]);
            }
        }

        var endPoint = GetPointAtDistance(endDistance, cumulativeLengths);
        if (result[^1] != endPoint)
        {
            result.Add(endPoint);
        }

        return result;
    }

    private Point GetPointAtDistance(double distance, IReadOnlyList<double> cumulativeLengths)
    {
        if (distance <= 0)
        {
            return Start;
        }

        for (var index = 1; index < Points.Count; index++)
        {
            if (cumulativeLengths[index] < distance)
            {
                continue;
            }

            var segmentLength = cumulativeLengths[index] - cumulativeLengths[index - 1];
            if (segmentLength <= double.Epsilon)
            {
                continue;
            }

            var progress = (distance - cumulativeLengths[index - 1]) / segmentLength;
            return Interpolate(Points[index - 1], Points[index], progress);
        }

        return End;
    }

    private static void DrawMarker(
        DrawingContext context,
        IPen pen,
        EdgeMarker marker,
        Point anchor,
        Vector direction,
        double size)
    {
        if (marker == EdgeMarker.None || pen.Brush is not { } brush)
        {
            return;
        }

        var normal = new Vector(-direction.Y, direction.X);
        switch (marker)
        {
            case EdgeMarker.Arrow:
                {
                    var backCenter = Offset(anchor, direction, -size);
                    DrawFilledPolygon(
                        context,
                        brush,
                        anchor,
                        Offset(backCenter, normal, size * 0.5),
                        Offset(backCenter, normal, -size * 0.5));
                    break;
                }
            case EdgeMarker.Circle:
                {
                    var radius = size * 0.5;
                    var center = Offset(anchor, direction, -radius);
                    context.DrawEllipse(brush, null, center, radius, radius);
                    break;
                }
            case EdgeMarker.Diamond:
                {
                    var center = Offset(anchor, direction, -size * 0.5);
                    var rear = Offset(anchor, direction, -size);
                    DrawFilledPolygon(
                        context,
                        brush,
                        anchor,
                        Offset(center, normal, size * 0.45),
                        rear,
                        Offset(center, normal, -size * 0.45));
                    break;
                }
        }
    }

    private static void DrawFilledPolygon(DrawingContext context, IBrush brush, params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(points[0], true);
            for (var index = 1; index < points.Length; index++)
            {
                geometryContext.LineTo(points[index]);
            }

            geometryContext.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geometry);
    }

    private static bool TryCreateUnitVector(Point start, Point end, out Vector vector)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= double.Epsilon)
        {
            vector = default;
            return false;
        }

        vector = new Vector(dx / length, dy / length);
        return true;
    }

    private static Point Offset(Point point, Vector vector, double distance) => new(
        point.X + vector.X * distance,
        point.Y + vector.Y * distance);

    private static Point Interpolate(Point start, Point end, double progress) => new(
        start.X + (end.X - start.X) * progress,
        start.Y + (end.Y - start.Y) * progress);

    private static GraphPoint ToGraphPoint(Point point) => new(point.X, point.Y);

    private static Point ToAvaloniaPoint(GraphPoint point) => new(point.X, point.Y);

    private static bool ShouldAppendManualPoint(GraphPoint previous, GraphPoint point) =>
        double.IsFinite(point.X) &&
        double.IsFinite(point.Y) &&
        previous != point;

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
        var projection = new Point(start.X + progress * dx, start.Y + progress * dy);
        return DistanceSquared(point, projection);
    }

    private static double DistanceSquared(Point first, Point second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return dx * dx + dy * dy;
    }
}
