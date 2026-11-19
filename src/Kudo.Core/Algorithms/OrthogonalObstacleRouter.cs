using Kudo;

namespace Kudo.Core.Algorithms;

public sealed record OrthogonalRouteOptions
{
    public double ObstaclePadding { get; init; } = 12;

    public double BendPenalty { get; init; } = 24;

    public int MaxSearchStates { get; init; } = 10_000;
}

public sealed record OrthogonalRouteResult(
    IReadOnlyList<GraphPoint> Points,
    bool IsFallback,
    int VisitedStateCount);

public static class OrthogonalObstacleRouter
{
    public static OrthogonalRouteResult Route(
        GraphPoint start,
        GraphPoint end,
        IEnumerable<GraphRect> obstacles,
        OrthogonalRouteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(obstacles);
        options ??= new OrthogonalRouteOptions();
        Validate(start, end, options);
        var obstacleList = obstacles as IReadOnlyList<GraphRect> ?? obstacles.ToArray();
        var expanded = GetRelevantObstacles(
            start,
            end,
            obstacleList,
            options.ObstaclePadding);
        var points = CreateCandidatePoints(start, end, expanded, options.MaxSearchStates);
        if (points is null)
        {
            return new OrthogonalRouteResult(CreateFallback(start, end), true, 0);
        }

        var neighbors = CreateVisibilityNeighbors(points, expanded);
        var startIndex = points.IndexOf(start);
        var endIndex = points.IndexOf(end);
        var queue = new PriorityQueue<RouteState, (double Cost, int Bends, int Point, int Direction)>();
        var startState = new RouteState(startIndex, RouteDirection.None);
        var costs = new Dictionary<RouteState, (double Cost, int Bends)> { [startState] = (0, 0) };
        var previous = new Dictionary<RouteState, RouteState>();
        queue.Enqueue(startState, (0, 0, startIndex, 0));
        var visited = 0;

        while (queue.TryDequeue(out var state, out var priority))
        {
            if (!costs.TryGetValue(state, out var known) ||
                priority.Cost > known.Cost ||
                priority.Cost == known.Cost && priority.Bends > known.Bends)
            {
                continue;
            }

            if (++visited > options.MaxSearchStates)
            {
                break;
            }

            if (state.PointIndex == endIndex)
            {
                return new OrthogonalRouteResult(
                    Simplify(Reconstruct(points, previous, state)),
                    false,
                    visited);
            }

            foreach (var index in neighbors[state.PointIndex])
            {
                if (!TryGetDirection(points[state.PointIndex], points[index], out var direction))
                {
                    continue;
                }

                var bends = known.Bends + (state.Direction is not RouteDirection.None && state.Direction != direction ? 1 : 0);
                var cost = known.Cost + ManhattanDistance(points[state.PointIndex], points[index]) +
                           (bends - known.Bends) * options.BendPenalty;
                var next = new RouteState(index, direction);
                if (costs.TryGetValue(next, out var current) &&
                    (cost > current.Cost || cost == current.Cost && bends >= current.Bends))
                {
                    continue;
                }

                costs[next] = (cost, bends);
                previous[next] = state;
                queue.Enqueue(next, (cost, bends, index, (int)direction));
            }
        }

        return new OrthogonalRouteResult(CreateFallback(start, end), true, visited);
    }

    private static IReadOnlyList<int>[] CreateVisibilityNeighbors(
        IReadOnlyList<GraphPoint> points,
        IReadOnlyList<GraphRect> obstacles)
    {
        var horizontal = new Dictionary<double, List<int>>();
        var vertical = new Dictionary<double, List<int>>();
        for (var index = 0; index < points.Count; index++)
        {
            AddIndex(horizontal, points[index].Y, index);
            AddIndex(vertical, points[index].X, index);
        }

        var neighbors = Enumerable.Range(0, points.Count)
            .Select(_ => new List<int>(4))
            .ToArray();
        foreach (var indices in horizontal.Values)
        {
            indices.Sort((first, second) => points[first].X.CompareTo(points[second].X));
            ConnectAdjacentVisiblePoints(indices, points, obstacles, neighbors);
        }

        foreach (var indices in vertical.Values)
        {
            indices.Sort((first, second) => points[first].Y.CompareTo(points[second].Y));
            ConnectAdjacentVisiblePoints(indices, points, obstacles, neighbors);
        }

        return neighbors;
    }

    private static void AddIndex(
        IDictionary<double, List<int>> indicesByCoordinate,
        double coordinate,
        int index)
    {
        if (!indicesByCoordinate.TryGetValue(coordinate, out var indices))
        {
            indices = [];
            indicesByCoordinate.Add(coordinate, indices);
        }

        indices.Add(index);
    }

    private static void ConnectAdjacentVisiblePoints(
        IReadOnlyList<int> indices,
        IReadOnlyList<GraphPoint> points,
        IReadOnlyList<GraphRect> obstacles,
        IReadOnlyList<List<int>> neighbors)
    {
        for (var index = 1; index < indices.Count; index++)
        {
            var first = indices[index - 1];
            var second = indices[index];
            if (obstacles.Any(rect => SegmentCrossesInterior(points[first], points[second], rect)))
            {
                continue;
            }

            neighbors[first].Add(second);
            neighbors[second].Add(first);
        }
    }

    private static List<GraphPoint>? CreateCandidatePoints(
        GraphPoint start,
        GraphPoint end,
        IReadOnlyCollection<GraphRect> obstacles,
        int maxCandidatePoints)
    {
        var xs = obstacles.SelectMany(rect => new[] { rect.X, rect.X + rect.Width })
            .Append(start.X).Append(end.X).Distinct().Order().ToArray();
        var ys = obstacles.SelectMany(rect => new[] { rect.Y, rect.Y + rect.Height })
            .Append(start.Y).Append(end.Y).Distinct().Order().ToArray();
        if ((long)xs.Length * ys.Length > maxCandidatePoints)
        {
            return null;
        }

        return xs.SelectMany(x => ys.Select(y => new GraphPoint(x, y)))
            .Where(point => obstacles.All(rect => !ContainsInterior(rect, point)))
            .OrderBy(point => point.X).ThenBy(point => point.Y).ToList();
    }

    private static IReadOnlyList<GraphPoint> Reconstruct(
        IReadOnlyList<GraphPoint> points,
        IReadOnlyDictionary<RouteState, RouteState> previous,
        RouteState state)
    {
        var result = new List<GraphPoint>();
        while (true)
        {
            result.Add(points[state.PointIndex]);
            if (!previous.TryGetValue(state, out state))
            {
                break;
            }
        }

        result.Reverse();
        return result;
    }

    private static IReadOnlyList<GraphPoint> Simplify(IReadOnlyList<GraphPoint> points)
    {
        var result = new List<GraphPoint>();
        foreach (var point in points)
        {
            if (result.Count >= 2 &&
                (result[^2].X == result[^1].X && result[^1].X == point.X ||
                 result[^2].Y == result[^1].Y && result[^1].Y == point.Y))
            {
                result[^1] = point;
            }
            else if (result.Count == 0 || result[^1] != point)
            {
                result.Add(point);
            }
        }

        return result;
    }

    private static IReadOnlyList<GraphPoint> CreateFallback(GraphPoint start, GraphPoint end) => Simplify([
        start,
        new GraphPoint((start.X + end.X) / 2, start.Y),
        new GraphPoint((start.X + end.X) / 2, end.Y),
        end
    ]);

    private static GraphRect[] GetRelevantObstacles(
        GraphPoint start,
        GraphPoint end,
        IReadOnlyList<GraphRect> obstacles,
        double obstaclePadding)
    {
        if (obstacles.Count == 0)
        {
            return [];
        }

        var searchBounds = new GraphRect(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y));
        var included = new bool[obstacles.Count];
        var relevant = new List<GraphRect>();
        bool changed;
        do
        {
            changed = false;
            for (var index = 0; index < obstacles.Count; index++)
            {
                if (included[index] || !IsFiniteRect(obstacles[index]))
                {
                    continue;
                }

                var expanded = Inflate(obstacles[index], obstaclePadding);
                if (!IsFiniteRect(expanded) ||
                    ContainsInterior(expanded, start) ||
                    ContainsInterior(expanded, end) ||
                    !IntersectsOrTouches(searchBounds, expanded))
                {
                    continue;
                }

                included[index] = true;
                relevant.Add(expanded);
                searchBounds = Union(searchBounds, expanded);
                changed = true;
            }
        }
        while (changed);

        return relevant.ToArray();
    }

    private static bool IntersectsOrTouches(GraphRect first, GraphRect second) =>
        first.X <= second.X + second.Width &&
        first.X + first.Width >= second.X &&
        first.Y <= second.Y + second.Height &&
        first.Y + first.Height >= second.Y;

    private static GraphRect Union(GraphRect first, GraphRect second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        var right = Math.Max(first.X + first.Width, second.X + second.Width);
        var bottom = Math.Max(first.Y + first.Height, second.Y + second.Height);
        return new GraphRect(left, top, right - left, bottom - top);
    }

    private static bool TryGetDirection(GraphPoint first, GraphPoint second, out RouteDirection direction)
    {
        direction = first.Y == second.Y
            ? RouteDirection.Horizontal
            : first.X == second.X
                ? RouteDirection.Vertical
                : RouteDirection.None;
        return direction != RouteDirection.None;
    }

    private static bool SegmentCrossesInterior(GraphPoint first, GraphPoint second, GraphRect rect) =>
        first.Y == second.Y
            ? first.Y > rect.Y && first.Y < rect.Y + rect.Height &&
              Math.Max(first.X, second.X) > rect.X && Math.Min(first.X, second.X) < rect.X + rect.Width
            : first.X > rect.X && first.X < rect.X + rect.Width &&
              Math.Max(first.Y, second.Y) > rect.Y && Math.Min(first.Y, second.Y) < rect.Y + rect.Height;

    private static bool ContainsInterior(GraphRect rect, GraphPoint point) =>
        point.X > rect.X && point.X < rect.X + rect.Width &&
        point.Y > rect.Y && point.Y < rect.Y + rect.Height;

    private static GraphRect Inflate(GraphRect rect, double padding) => new(
        rect.X - padding,
        rect.Y - padding,
        rect.Width + padding * 2,
        rect.Height + padding * 2);

    private static bool IsFiniteRect(GraphRect rect) =>
        double.IsFinite(rect.X) && double.IsFinite(rect.Y) &&
        double.IsFinite(rect.Width) && rect.Width >= 0 &&
        double.IsFinite(rect.Height) && rect.Height >= 0;

    private static double ManhattanDistance(GraphPoint first, GraphPoint second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    private static void Validate(GraphPoint start, GraphPoint end, OrthogonalRouteOptions options)
    {
        if (!double.IsFinite(start.X) || !double.IsFinite(start.Y) ||
            !double.IsFinite(end.X) || !double.IsFinite(end.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Route endpoints must use finite coordinates.");
        }

        if (!double.IsFinite(options.ObstaclePadding) || options.ObstaclePadding < 0 ||
            !double.IsFinite(options.BendPenalty) || options.BendPenalty < 0 ||
            options.MaxSearchStates <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    private readonly record struct RouteState(int PointIndex, RouteDirection Direction);

    private enum RouteDirection
    {
        None,
        Horizontal,
        Vertical
    }
}
