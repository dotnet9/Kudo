using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphEdgePathCache
{
    private readonly Dictionary<EdgeModel, Entry> _entries = new(ReferenceEqualityComparer.Instance);
    private readonly List<EdgeModel> _invalidatedEdges = [];

    public bool TryGet(
        EdgeModel edge,
        GraphPoint start,
        GraphPoint end,
        GraphPoint? startDirection,
        GraphPoint? endDirection,
        long smartObstacleRevision,
        out GraphEdgePath? path)
    {
        ArgumentNullException.ThrowIfNull(edge);

        if (_entries.TryGetValue(edge, out var entry) &&
            entry.Matches(edge, start, end, startDirection, endDirection, smartObstacleRevision))
        {
            path = entry.Path;
            return true;
        }

        path = null;
        return false;
    }

    public void Set(
        EdgeModel edge,
        GraphPoint start,
        GraphPoint end,
        GraphPoint? startDirection,
        GraphPoint? endDirection,
        long smartObstacleRevision,
        GraphEdgePath path)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(path);
        _entries[edge] = new Entry(
            edge,
            start,
            end,
            startDirection,
            endDirection,
            smartObstacleRevision,
            path);
    }

    public bool Remove(EdgeModel edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        return _entries.Remove(edge);
    }

    public void Remove(IEnumerable<EdgeModel> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        foreach (var edge in edges)
        {
            Remove(edge);
        }
    }

    /// <summary>
    /// Removes cached smart routes whose current route envelope intersects any
    /// changed obstacle region. The returned list is reused by this cache and
    /// must only be consumed until the next invalidation call.
    /// </summary>
    public IReadOnlyList<EdgeModel> RemoveSmartOrthogonalIntersecting(
        IReadOnlyList<GraphRect> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        _invalidatedEdges.Clear();
        if (regions.Count == 0 || _entries.Count == 0)
        {
            return _invalidatedEdges;
        }

        foreach (var (edge, entry) in _entries)
        {
            if (entry.RoutingStyle != EdgeRoutingStyle.SmartOrthogonal ||
                !regions.Any(region => IntersectsOrTouches(entry.Bounds, region)))
            {
                continue;
            }

            _invalidatedEdges.Add(edge);
        }

        foreach (var edge in _invalidatedEdges)
        {
            _entries.Remove(edge);
        }

        return _invalidatedEdges;
    }

    public void Clear() => _entries.Clear();

    private static bool IntersectsOrTouches(GraphRect first, GraphRect second) =>
        first.X <= second.X + second.Width &&
        first.X + first.Width >= second.X &&
        first.Y <= second.Y + second.Height &&
        first.Y + first.Height >= second.Y;

    private sealed class Entry
    {
        private readonly EdgeRoutingStyle _routingStyle;
        private readonly GraphPoint _start;
        private readonly GraphPoint _end;
        private readonly GraphPoint? _startDirection;
        private readonly GraphPoint? _endDirection;
        private readonly GraphPoint[] _waypoints;
        private readonly long _smartObstacleRevision;

        public Entry(
            EdgeModel edge,
            GraphPoint start,
            GraphPoint end,
            GraphPoint? startDirection,
            GraphPoint? endDirection,
            long smartObstacleRevision,
            GraphEdgePath path)
        {
            _routingStyle = edge.RoutingStyle;
            _start = start;
            _end = end;
            _startDirection = startDirection;
            _endDirection = endDirection;
            _waypoints = edge.Waypoints?.ToArray() ?? [];
            _smartObstacleRevision = smartObstacleRevision;
            Bounds = GetBounds(path.Points);
            Path = path;
        }

        public GraphEdgePath Path { get; }

        public EdgeRoutingStyle RoutingStyle => _routingStyle;

        public GraphRect Bounds { get; }

        public bool Matches(
            EdgeModel edge,
            GraphPoint start,
            GraphPoint end,
            GraphPoint? startDirection,
            GraphPoint? endDirection,
            long smartObstacleRevision)
        {
            if (_routingStyle != edge.RoutingStyle ||
                _start != start ||
                _end != end ||
                _startDirection != startDirection ||
                _endDirection != endDirection ||
                (_routingStyle == EdgeRoutingStyle.SmartOrthogonal &&
                    _smartObstacleRevision != smartObstacleRevision))
            {
                return false;
            }

            var waypoints = edge.Waypoints;
            if (waypoints is null || waypoints.Count != _waypoints.Length)
            {
                return waypoints is null && _waypoints.Length == 0;
            }

            for (var index = 0; index < _waypoints.Length; index++)
            {
                if (_waypoints[index] != waypoints[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static GraphRect GetBounds(IReadOnlyList<GraphPoint> points)
        {
            var first = points[0];
            var minX = first.X;
            var maxX = first.X;
            var minY = first.Y;
            var maxY = first.Y;
            for (var index = 1; index < points.Count; index++)
            {
                var point = points[index];
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            return new GraphRect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
