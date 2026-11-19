using Kudo;

namespace Kudo.Core.Algorithms;

public sealed record GraphEdgeRoutingOptions
{
    public static GraphEdgeRoutingOptions Default { get; } = new();

    public double MinimumBezierControlOffset { get; init; } = 50;

    public double MaximumBezierControlOffset { get; init; } = 150;

    public OrthogonalRouteOptions SmartOrthogonal { get; init; } = new();

    /// <summary>
    /// 正交路线沿端口方向离开或进入节点时使用的引线长度。
    /// </summary>
    public double PortDirectionLeadLength { get; init; } = 20;
}

public sealed record GraphEdgeRoutingRequest(
    EdgeRoutingStyle RoutingStyle,
    GraphPoint Start,
    GraphPoint End)
{
    public IReadOnlyList<GraphPoint> Waypoints { get; init; } = [];

    public IReadOnlyList<GraphRect> Obstacles { get; init; } = [];

    public GraphEdgeRoutingOptions Options { get; init; } = GraphEdgeRoutingOptions.Default;

    public GraphPoint? StartDirection { get; init; }

    public GraphPoint? EndDirection { get; init; }
}

public sealed class GraphEdgePath
{
    public GraphEdgePath(
        EdgeRoutingStyle routingStyle,
        IEnumerable<GraphPoint> points,
        GraphPoint? controlPoint1 = null,
        GraphPoint? controlPoint2 = null,
        bool isFallback = false)
    {
        ArgumentNullException.ThrowIfNull(points);
        var snapshot = points.ToArray();
        if (snapshot.Length == 0)
        {
            throw new ArgumentException("A route requires at least one point.", nameof(points));
        }

        RoutingStyle = routingStyle;
        Points = Array.AsReadOnly(snapshot);
        ControlPoint1 = controlPoint1;
        ControlPoint2 = controlPoint2;
        IsFallback = isFallback;
    }

    public EdgeRoutingStyle RoutingStyle { get; }

    public IReadOnlyList<GraphPoint> Points { get; }

    public GraphPoint Start => Points[0];

    public GraphPoint End => Points[^1];

    public GraphPoint? ControlPoint1 { get; }

    public GraphPoint? ControlPoint2 { get; }

    public bool IsFallback { get; }
}

public interface IGraphEdgeRouter
{
    EdgeRoutingStyle RoutingStyle { get; }

    GraphEdgePath Route(GraphEdgeRoutingRequest request);
}

public sealed class GraphEdgeRoutingService
{
    private readonly IReadOnlyDictionary<EdgeRoutingStyle, IGraphEdgeRouter> _routers;
    private readonly IReadOnlyDictionary<EdgeRoutingStyle, IGraphEdgePathGenerator> _pathGenerators;
    private readonly IGraphEdgeRouter _fallbackRouter;

    public GraphEdgeRoutingService(IEnumerable<IGraphEdgeRouter> routers)
        : this(routers, pathGenerators: null)
    {
    }

    private GraphEdgeRoutingService(
        IEnumerable<IGraphEdgeRouter> routers,
        IEnumerable<IGraphEdgePathGenerator>? pathGenerators)
    {
        ArgumentNullException.ThrowIfNull(routers);
        var routerArray = routers.ToArray();
        if (routerArray.Length == 0)
        {
            throw new ArgumentException("At least one edge router is required.", nameof(routers));
        }

        var duplicateStyle = routerArray
            .GroupBy(router => router.RoutingStyle)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateStyle is { } style)
        {
            throw new ArgumentException(
                $"Only one edge router can be registered for routing style '{style}'.",
                nameof(routers));
        }

        _routers = routerArray.ToDictionary(router => router.RoutingStyle);
        _fallbackRouter = _routers.GetValueOrDefault(EdgeRoutingStyle.Direct) ?? routerArray[0];
        var generatorArray = (pathGenerators ?? [])
            .Where(generator => generator is not null)
            .ToArray();
        var duplicateGeneratorStyle = generatorArray
            .GroupBy(generator => generator.RoutingStyle)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateGeneratorStyle is { } generatorStyle)
        {
            throw new ArgumentException(
                $"Only one edge path generator can be registered for routing style '{generatorStyle}'.",
                nameof(pathGenerators));
        }

        _pathGenerators = generatorArray.ToDictionary(generator => generator.RoutingStyle);
    }

    public static GraphEdgeRoutingService Default { get; } = new([
        new DirectGraphEdgeRouter(),
        new BezierGraphEdgeRouter(),
        new OrthogonalGraphEdgeRouter(),
        new ManualGraphEdgeRouter(),
        new SmartOrthogonalGraphEdgeRouter()
    ]);

    public GraphEdgePath Route(GraphEdgeRoutingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var router = _routers.GetValueOrDefault(request.RoutingStyle) ?? _fallbackRouter;
        var routedPath = router.Route(request);
        return _pathGenerators.GetValueOrDefault(request.RoutingStyle)?.Generate(request, routedPath)
            ?? routedPath;
    }

    public GraphEdgeRoutingService WithRouter(IGraphEdgeRouter router)
    {
        ArgumentNullException.ThrowIfNull(router);
        return new GraphEdgeRoutingService(
            _routers.Values
                .Where(current => current.RoutingStyle != router.RoutingStyle)
                .Append(router),
            _pathGenerators.Values);
    }

    public GraphEdgeRoutingService WithPathGenerator(
        EdgeRoutingStyle routingStyle,
        IGraphEdgePathGenerator? generator)
    {
        var generators = _pathGenerators.Values
            .Where(current => current.RoutingStyle != routingStyle)
            .ToList();
        if (generator is not null)
        {
            if (generator.RoutingStyle != routingStyle)
            {
                throw new ArgumentException(
                    "The generator routing style must match the requested routing style.",
                    nameof(generator));
            }

            generators.Add(generator);
        }

        return new GraphEdgeRoutingService(_routers.Values, generators);
    }

    public GraphEdgeRoutingService WithPathGenerator(IGraphEdgePathGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        return WithPathGenerator(generator.RoutingStyle, generator);
    }
}

internal sealed class DirectGraphEdgeRouter : IGraphEdgeRouter
{
    public EdgeRoutingStyle RoutingStyle => EdgeRoutingStyle.Direct;

    public GraphEdgePath Route(GraphEdgeRoutingRequest request) => new(
        request.RoutingStyle,
        [request.Start, request.End]);
}

internal sealed class BezierGraphEdgeRouter : IGraphEdgeRouter
{
    private const int SegmentCount = 32;

    public EdgeRoutingStyle RoutingStyle => EdgeRoutingStyle.Bezier;

    public GraphEdgePath Route(GraphEdgeRoutingRequest request)
    {
        var options = request.Options ?? GraphEdgeRoutingOptions.Default;
        var minimumOffset = NormalizeNonNegative(options.MinimumBezierControlOffset, 50);
        var maximumOffset = Math.Max(
            minimumOffset,
            NormalizeNonNegative(options.MaximumBezierControlOffset, 150));
        var controlOffset = Math.Clamp(
            Math.Abs(request.End.X - request.Start.X) * 0.5,
            minimumOffset,
            maximumOffset);
        var controlPoint1 = new GraphPoint(request.Start.X + controlOffset, request.Start.Y);
        var controlPoint2 = new GraphPoint(request.End.X - controlOffset, request.End.Y);
        var points = new GraphPoint[SegmentCount + 1];
        for (var index = 0; index <= SegmentCount; index++)
        {
            points[index] = CubicBezier(
                request.Start,
                controlPoint1,
                controlPoint2,
                request.End,
                index / (double)SegmentCount);
        }

        return new GraphEdgePath(
            request.RoutingStyle,
            points,
            controlPoint1,
            controlPoint2);
    }

    private static GraphPoint CubicBezier(
        GraphPoint start,
        GraphPoint controlPoint1,
        GraphPoint controlPoint2,
        GraphPoint end,
        double progress)
    {
        var remaining = 1 - progress;
        var startWeight = remaining * remaining * remaining;
        var firstControlWeight = 3 * remaining * remaining * progress;
        var secondControlWeight = 3 * remaining * progress * progress;
        var endWeight = progress * progress * progress;
        return new GraphPoint(
            startWeight * start.X + firstControlWeight * controlPoint1.X + secondControlWeight * controlPoint2.X + endWeight * end.X,
            startWeight * start.Y + firstControlWeight * controlPoint1.Y + secondControlWeight * controlPoint2.Y + endWeight * end.Y);
    }

    private static double NormalizeNonNegative(double value, double fallback) =>
        double.IsFinite(value) && value >= 0 ? value : fallback;
}

internal sealed class OrthogonalGraphEdgeRouter : IGraphEdgeRouter
{
    public EdgeRoutingStyle RoutingStyle => EdgeRoutingStyle.Orthogonal;

    public GraphEdgePath Route(GraphEdgeRoutingRequest request)
    {
        var routed = CreateCoreRoute(request);
        return ApplyPortDirections(request, routed);
    }

    private static GraphEdgePath CreateCoreRoute(GraphEdgeRoutingRequest request)
    {
        var waypoints = (request.Waypoints ?? [])
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .ToArray();
        if (waypoints.Length > 0)
        {
            var points = new List<GraphPoint>(waypoints.Length * 2 + 2)
            {
                request.Start
            };
            foreach (var waypoint in waypoints)
            {
                AppendOrthogonal(points, waypoint, horizontalFirst: true);
            }

            AppendOrthogonal(points, request.End, horizontalFirst: false);
            return new GraphEdgePath(request.RoutingStyle, RemoveRedundantPoints(points));
        }

        var middleX = (request.Start.X + request.End.X) / 2;
        return new GraphEdgePath(request.RoutingStyle, RemoveRedundantPoints([
            request.Start,
            new GraphPoint(middleX, request.Start.Y),
            new GraphPoint(middleX, request.End.Y),
            request.End
        ]));
    }

    internal static GraphEdgePath ApplyPortDirections(
        GraphEdgeRoutingRequest request,
        GraphEdgePath path)
    {
        var leadLength = request.Options?.PortDirectionLeadLength ??
                         GraphEdgeRoutingOptions.Default.PortDirectionLeadLength;
        if (!double.IsFinite(leadLength) || leadLength <= 0)
        {
            return path;
        }

        var startDirection = PortGeometry.NormalizeDirection(request.StartDirection);
        var endDirection = PortGeometry.NormalizeDirection(request.EndDirection);
        if (startDirection is null && endDirection is null)
        {
            return path;
        }

        var points = new List<GraphPoint>(path.Points.Count + 4) { request.Start };
        if (startDirection is { } sourceDirection)
        {
            points.Add(Offset(request.Start, sourceDirection, leadLength));
        }

        foreach (var point in path.Points.Skip(1).SkipLast(1))
        {
            AppendOrthogonal(points, point, horizontalFirst: true);
        }

        var endLead = endDirection is { } targetDirection
            ? Offset(request.End, targetDirection, leadLength)
            : request.End;
        AppendOrthogonal(points, endLead, horizontalFirst: false);
        if (endLead != request.End)
        {
            points.Add(request.End);
        }

        return new GraphEdgePath(
            path.RoutingStyle,
            RemoveRedundantPoints(points),
            isFallback: path.IsFallback);
    }

    private static GraphPoint Offset(GraphPoint point, GraphPoint direction, double distance) =>
        new(point.X + direction.X * distance, point.Y + direction.Y * distance);

    internal static void AppendOrthogonal(
        List<GraphPoint> points,
        GraphPoint target,
        bool horizontalFirst)
    {
        var current = points[^1];
        if (current.X != target.X && current.Y != target.Y)
        {
            points.Add(horizontalFirst
                ? new GraphPoint(target.X, current.Y)
                : new GraphPoint(current.X, target.Y));
        }

        points.Add(target);
    }

    internal static IReadOnlyList<GraphPoint> RemoveRedundantPoints(IEnumerable<GraphPoint> points)
    {
        var result = new List<GraphPoint>();
        foreach (var point in points)
        {
            if (result.Count > 0 && result[^1] == point)
            {
                continue;
            }

            while (result.Count >= 2 && AreCollinear(result[^2], result[^1], point))
            {
                result.RemoveAt(result.Count - 1);
            }

            result.Add(point);
        }

        return result;
    }

    private static bool AreCollinear(GraphPoint first, GraphPoint second, GraphPoint third) =>
        (first.X == second.X && second.X == third.X) ||
        (first.Y == second.Y && second.Y == third.Y);
}

internal sealed class ManualGraphEdgeRouter : IGraphEdgeRouter
{
    public EdgeRoutingStyle RoutingStyle => EdgeRoutingStyle.Manual;

    public GraphEdgePath Route(GraphEdgeRoutingRequest request)
    {
        var points = new List<GraphPoint>((request.Waypoints?.Count ?? 0) + 2)
        {
            request.Start
        };
        foreach (var waypoint in request.Waypoints ?? [])
        {
            if (double.IsFinite(waypoint.X) && double.IsFinite(waypoint.Y))
            {
                OrthogonalGraphEdgeRouter.AppendOrthogonal(
                    points,
                    waypoint,
                    horizontalFirst: true);
            }
        }

        OrthogonalGraphEdgeRouter.AppendOrthogonal(
            points,
            request.End,
            horizontalFirst: false);

        return new GraphEdgePath(
            request.RoutingStyle,
            RemoveDuplicatePoints(points));
    }

    private static IReadOnlyList<GraphPoint> RemoveDuplicatePoints(
        IEnumerable<GraphPoint> points)
    {
        var result = new List<GraphPoint>();
        foreach (var point in points)
        {
            if (result.Count == 0 || result[^1] != point)
            {
                result.Add(point);
            }
        }

        return result;
    }
}

internal sealed class SmartOrthogonalGraphEdgeRouter : IGraphEdgeRouter
{
    public EdgeRoutingStyle RoutingStyle => EdgeRoutingStyle.SmartOrthogonal;

    public GraphEdgePath Route(GraphEdgeRoutingRequest request)
    {
        var result = OrthogonalObstacleRouter.Route(
            request.Start,
            request.End,
            request.Obstacles ?? [],
            request.Options?.SmartOrthogonal);
        var path = new GraphEdgePath(
            request.RoutingStyle,
            result.Points,
            isFallback: result.IsFallback);
        return OrthogonalGraphEdgeRouter.ApplyPortDirections(request, path);
    }
}
