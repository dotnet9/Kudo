using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

internal enum ManualConnectionDirection
{
    Forward,
    Left,
    Right
}

internal sealed class ManualConnectionDraft
{
    public ManualConnectionDraft(
        PortHitResult startPort,
        IReadOnlyList<GraphPoint> routePoints,
        GraphPoint fallbackHeading)
    {
        ArgumentNullException.ThrowIfNull(startPort);
        ArgumentNullException.ThrowIfNull(routePoints);
        if (routePoints.Count == 0)
        {
            throw new ArgumentException(message: null, nameof(routePoints));
        }

        StartPort = startPort;
        var orthogonalPath = ManualConnectionGeometry.CreateOrthogonalPath(
            routePoints,
            fallbackHeading);
        Waypoints = CreateWaypoints(orthogonalPath);
        Heading = ResolveHeading(orthogonalPath, fallbackHeading);
    }

    public PortHitResult StartPort { get; }

    public List<GraphPoint> Waypoints { get; }

    public GraphPoint Endpoint => Waypoints[^1];

    public GraphPoint Heading { get; private set; } =
        new(1, 0);

    public bool AreDirectionHandlesVisible { get; set; }

    public ManualConnectionDirection? HoveredDirection { get; set; }

    public void AddEndpoint(GraphPoint endpoint, GraphPoint heading)
    {
        if (Endpoint != endpoint)
        {
            Waypoints.Add(endpoint);
        }

        Heading = ManualConnectionGeometry.Normalize(heading);
    }

    private static List<GraphPoint> CreateWaypoints(IReadOnlyList<GraphPoint> routePoints)
    {
        var waypoints = new List<GraphPoint>(Math.Max(1, routePoints.Count - 1));
        var previous = routePoints[0];
        for (var index = 1; index < routePoints.Count; index++)
        {
            var point = routePoints[index];
            if (!double.IsFinite(point.X) ||
                !double.IsFinite(point.Y) ||
                point == previous)
            {
                continue;
            }

            waypoints.Add(point);
            previous = point;
        }

        if (waypoints.Count == 0)
        {
            waypoints.Add(previous);
        }

        return waypoints;
    }

    private static GraphPoint ResolveHeading(
        IReadOnlyList<GraphPoint> routePoints,
        GraphPoint fallbackHeading)
    {
        for (var index = routePoints.Count - 1; index > 0; index--)
        {
            var heading = ManualConnectionGeometry.ResolveHeading(
                routePoints[index - 1],
                routePoints[index],
                fallbackHeading);
            if (routePoints[index - 1] != routePoints[index])
            {
                return heading;
            }
        }

        return ManualConnectionGeometry.Normalize(fallbackHeading);
    }
}

internal static class ManualConnectionGeometry
{
    public static IReadOnlyList<ManualConnectionDirection> Directions { get; } =
    [
        ManualConnectionDirection.Forward,
        ManualConnectionDirection.Left,
        ManualConnectionDirection.Right
    ];

    public const double DirectionHandleDistance = 30;
    public const double DirectionHandleRadius = 10;
    public const double DirectionHandleHitRadius = 12;
    public const double DirectionHandleRevealRadius = 44;
    public const double MinimumSegmentScreenLength = 4;

    public static GraphPoint ResolveHeading(
        GraphPoint start,
        GraphPoint end,
        GraphPoint fallback)
    {
        var delta = new GraphPoint(end.X - start.X, end.Y - start.Y);
        return IsUsableDirection(delta)
            ? Normalize(delta)
            : Normalize(fallback);
    }

    public static GraphPoint Normalize(GraphPoint direction)
    {
        if (!IsUsableDirection(direction))
        {
            return new GraphPoint(1, 0);
        }

        return Math.Abs(direction.X) >= Math.Abs(direction.Y)
            ? new GraphPoint(Math.CopySign(1, direction.X), 0)
            : new GraphPoint(0, Math.CopySign(1, direction.Y));
    }

    public static GraphPoint GetDirection(
        GraphPoint heading,
        ManualConnectionDirection direction)
    {
        var normalized = Normalize(heading);
        return direction switch
        {
            ManualConnectionDirection.Left => new GraphPoint(normalized.Y, -normalized.X),
            ManualConnectionDirection.Right => new GraphPoint(-normalized.Y, normalized.X),
            _ => normalized
        };
    }

    public static GraphPoint ProjectOntoRay(
        GraphPoint origin,
        GraphPoint point,
        GraphPoint direction)
    {
        var normalized = Normalize(direction);
        var distance = Math.Max(
            0,
            (point.X - origin.X) * normalized.X +
            (point.Y - origin.Y) * normalized.Y);
        return new GraphPoint(
            origin.X + normalized.X * distance,
            origin.Y + normalized.Y * distance);
    }

    public static Point GetDirectionHandleCenter(
        Point endpoint,
        GraphPoint heading,
        ManualConnectionDirection direction)
    {
        var vector = GetDirection(heading, direction);
        return new Point(
            endpoint.X + vector.X * DirectionHandleDistance,
            endpoint.Y + vector.Y * DirectionHandleDistance);
    }

    public static ManualConnectionDirection? HitTestDirectionHandle(
        Point point,
        Point endpoint,
        GraphPoint heading)
    {
        var maximumDistanceSquared = DirectionHandleHitRadius * DirectionHandleHitRadius;
        foreach (var direction in Directions)
        {
            var center = GetDirectionHandleCenter(endpoint, heading, direction);
            if (DistanceSquared(point, center) <= maximumDistanceSquared)
            {
                return direction;
            }
        }

        return null;
    }

    public static bool IsNearEndpoint(Point point, Point endpoint) =>
        DistanceSquared(point, endpoint) <=
        DirectionHandleRevealRadius * DirectionHandleRevealRadius;

    public static IReadOnlyList<GraphPoint> CreateOrthogonalPath(
        IReadOnlyList<GraphPoint> source,
        GraphPoint preferredDirection)
    {
        ArgumentNullException.ThrowIfNull(source);
        var firstIndex = 0;
        while (firstIndex < source.Count && !IsFinite(source[firstIndex]))
        {
            firstIndex++;
        }

        if (firstIndex == source.Count)
        {
            throw new ArgumentException(message: null, nameof(source));
        }

        var points = new List<GraphPoint>((source.Count - firstIndex) * 2)
        {
            source[firstIndex]
        };
        var direction = Normalize(preferredDirection);
        for (var index = firstIndex + 1; index < source.Count; index++)
        {
            var point = source[index];
            if (!IsFinite(point))
            {
                continue;
            }

            AppendOrthogonal(points, point, IsHorizontal(direction));
            if (points.Count >= 2)
            {
                direction = ResolveHeading(points[^2], points[^1], direction);
            }
        }

        return points;
    }

    public static IReadOnlyList<GraphPoint> CreateOrthogonalCompletionPath(
        IReadOnlyList<GraphPoint> prefix,
        GraphPoint target,
        GraphPoint preferredDirection,
        GraphPoint? targetDirection,
        double targetLeadLength)
    {
        var points = CreateOrthogonalPath(prefix, preferredDirection).ToList();
        var approachDirection = targetDirection is { } value && IsUsableDirection(value)
            ? Normalize(value)
            : (GraphPoint?)null;
        var leadLength = double.IsFinite(targetLeadLength)
            ? Math.Max(0, targetLeadLength)
            : 0;
        if (approachDirection is not { } approach || leadLength <= 0)
        {
            AppendOrthogonal(points, target, IsHorizontal(preferredDirection));
            return points;
        }

        var targetLead = new GraphPoint(
            target.X + approach.X * leadLength,
            target.Y + approach.Y * leadLength);
        AppendOrthogonal(points, targetLead, IsHorizontal(approach));
        AppendPoint(points, target);
        return points;
    }

    private static void AppendOrthogonal(
        List<GraphPoint> points,
        GraphPoint target,
        bool horizontalFirst)
    {
        var current = points[^1];
        if (current.X != target.X && current.Y != target.Y)
        {
            AppendPoint(points, horizontalFirst
                ? new GraphPoint(target.X, current.Y)
                : new GraphPoint(current.X, target.Y));
        }

        AppendPoint(points, target);
    }

    private static void AppendPoint(List<GraphPoint> points, GraphPoint point)
    {
        if (points[^1] == point)
        {
            return;
        }

        while (points.Count >= 2 && AreCollinear(points[^2], points[^1], point))
        {
            points.RemoveAt(points.Count - 1);
        }

        points.Add(point);
    }

    private static bool AreCollinear(GraphPoint first, GraphPoint second, GraphPoint third) =>
        first.X == second.X && second.X == third.X ||
        first.Y == second.Y && second.Y == third.Y;

    private static bool IsHorizontal(GraphPoint direction) => direction.Y == 0;

    private static bool IsFinite(GraphPoint point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y);

    private static bool IsUsableDirection(GraphPoint direction) =>
        IsFinite(direction) &&
        (Math.Abs(direction.X) > double.Epsilon || Math.Abs(direction.Y) > double.Epsilon);

    private static double DistanceSquared(Point first, Point second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return dx * dx + dy * dy;
    }
}
