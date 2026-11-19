using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphEdgeSpatialIndex
{
    private readonly List<Segment> _candidates = [];
    private readonly List<Segment> _edgeRepresentatives = [];
    private readonly List<EdgeModel> _edgeResults = [];
    private readonly HashSet<EdgeModel> _seenEdges = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<EdgeModel, List<Segment>> _segmentsByEdge =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<EdgeModel, int> _edgeIndices =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<EdgeModel> _updatedEdges = new(ReferenceEqualityComparer.Instance);
    private readonly List<EdgeIndexUpdate> _edgeUpdates = [];
    private GraphSpatialIndex<Segment>? _segments;
    private bool _isDirty = true;

    public void Invalidate() => _isDirty = true;

    public void RebuildIfNeeded(
        IReadOnlyList<EdgeModel> edges,
        Func<EdgeModel, GraphEdgePath?> pathProvider)
    {
        if (_isDirty)
        {
            Rebuild(edges, pathProvider);
        }
    }

    public void Rebuild(
        IReadOnlyList<EdgeModel> edges,
        Func<EdgeModel, GraphEdgePath?> pathProvider)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(pathProvider);

        var segments = new List<Segment>();
        _segmentsByEdge.Clear();
        _edgeIndices.Clear();
        GraphRect? graphBounds = null;
        for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
        {
            var edge = edges[edgeIndex];
            var edgeSegments = CreateSegments(edge, edgeIndex, pathProvider(edge));
            _segmentsByEdge.Add(edge, edgeSegments);
            _edgeIndices.Add(edge, edgeIndex);
            foreach (var segment in edgeSegments)
            {
                segments.Add(segment);
                graphBounds = graphBounds is { } current
                    ? Union(current, segment.Bounds)
                    : segment.Bounds;
            }
        }

        if (graphBounds is null)
        {
            _segments = null;
            _isDirty = false;
            return;
        }

        var rootBounds = EnsureArea(graphBounds.Value);
        var index = new GraphSpatialIndex<Segment>(rootBounds);
        foreach (var segment in segments)
        {
            index.Insert(segment, segment.Bounds);
        }

        _segments = index;
        _isDirty = false;
    }

    public bool TryUpdateEdges(
        IReadOnlyList<EdgeModel> edges,
        IEnumerable<EdgeModel> updatedEdges,
        Func<EdgeModel, GraphEdgePath?> pathProvider)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(updatedEdges);
        ArgumentNullException.ThrowIfNull(pathProvider);
        if (_isDirty)
        {
            return false;
        }

        _updatedEdges.Clear();
        _updatedEdges.UnionWith(updatedEdges);
        return TryUpdateEdgesCore(edges, pathProvider);
    }

    public bool TryUpdateEdge(
        IReadOnlyList<EdgeModel> edges,
        EdgeModel updatedEdge,
        Func<EdgeModel, GraphEdgePath?> pathProvider)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(updatedEdge);
        ArgumentNullException.ThrowIfNull(pathProvider);
        if (_isDirty)
        {
            return false;
        }

        _updatedEdges.Clear();
        _updatedEdges.Add(updatedEdge);
        return TryUpdateEdgesCore(edges, pathProvider);
    }

    private bool TryUpdateEdgesCore(
        IReadOnlyList<EdgeModel> edges,
        Func<EdgeModel, GraphEdgePath?> pathProvider)
    {
        if (_updatedEdges.Count == 0)
        {
            return true;
        }

        if (_updatedEdges.Count > Math.Max(32, edges.Count / 4))
        {
            Invalidate();
            return false;
        }

        _edgeUpdates.Clear();
        foreach (var edge in _updatedEdges)
        {
            if (!_edgeIndices.TryGetValue(edge, out var edgeIndex) ||
                edgeIndex < 0 ||
                edgeIndex >= edges.Count ||
                !ReferenceEquals(edges[edgeIndex], edge) ||
                !_segmentsByEdge.TryGetValue(edge, out var previous))
            {
                Invalidate();
                return false;
            }

            var current = CreateSegments(edge, edgeIndex, pathProvider(edge));
            if (current.Count > 0 &&
                (_segments is null || current.Any(segment => !_segments.Contains(segment.Bounds))))
            {
                Invalidate();
                return false;
            }

            _edgeUpdates.Add(new EdgeIndexUpdate(edge, previous, current));
        }

        foreach (var update in _edgeUpdates)
        {
            if (_segments is not null)
            {
                foreach (var segment in update.Previous)
                {
                    _segments.Remove(segment, segment.Bounds);
                }

                foreach (var segment in update.Current)
                {
                    _segments.Insert(segment, segment.Bounds);
                }
            }

            _segmentsByEdge[update.Edge] = update.Current;
        }

        return true;
    }

    public EdgeModel? HitTest(GraphPoint point, double tolerance)
    {
        if (_segments is null || !double.IsFinite(tolerance) || tolerance < 0)
        {
            return null;
        }

        _candidates.Clear();
        _segments.Query(
            new GraphRect(
                point.X - tolerance,
                point.Y - tolerance,
                tolerance * 2,
                tolerance * 2),
            _candidates);

        var toleranceSquared = tolerance * tolerance;
        Segment? best = null;
        foreach (var candidate in _candidates)
        {
            if ((best is null || candidate.EdgeIndex >= best.EdgeIndex) &&
                DistanceToSegmentSquared(point, candidate.Start, candidate.End) <= toleranceSquared)
            {
                best = candidate;
            }
        }

        return best?.Edge;
    }

    public IReadOnlyList<EdgeModel> QueryEdges(GraphRect bounds)
    {
        _candidates.Clear();
        _segments?.Query(bounds, _candidates);
        _seenEdges.Clear();
        _edgeRepresentatives.Clear();
        foreach (var candidate in _candidates)
        {
            if (_seenEdges.Add(candidate.Edge))
            {
                _edgeRepresentatives.Add(candidate);
            }
        }

        _edgeRepresentatives.Sort((first, second) => first.EdgeIndex.CompareTo(second.EdgeIndex));
        _edgeResults.Clear();
        foreach (var representative in _edgeRepresentatives)
        {
            _edgeResults.Add(representative.Edge);
        }

        return _edgeResults;
    }

    private static GraphRect GetBounds(GraphPoint start, GraphPoint end) => new(
        Math.Min(start.X, end.X),
        Math.Min(start.Y, end.Y),
        Math.Abs(end.X - start.X),
        Math.Abs(end.Y - start.Y));

    private static GraphRect Union(GraphRect first, GraphRect second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        var right = Math.Max(first.X + first.Width, second.X + second.Width);
        var bottom = Math.Max(first.Y + first.Height, second.Y + second.Height);
        return new GraphRect(left, top, right - left, bottom - top);
    }

    private static GraphRect EnsureArea(GraphRect bounds) => new(
        bounds.Width > 0 ? bounds.X : bounds.X - 0.5,
        bounds.Height > 0 ? bounds.Y : bounds.Y - 0.5,
        bounds.Width > 0 ? bounds.Width : 1,
        bounds.Height > 0 ? bounds.Height : 1);

    private static bool IsFinite(GraphPoint point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y);

    private static List<Segment> CreateSegments(
        EdgeModel edge,
        int edgeIndex,
        GraphEdgePath? path)
    {
        var segments = new List<Segment>();
        if (path is null)
        {
            return segments;
        }

        for (var pointIndex = 1; pointIndex < path.Points.Count; pointIndex++)
        {
            var start = path.Points[pointIndex - 1];
            var end = path.Points[pointIndex];
            if (!IsFinite(start) || !IsFinite(end))
            {
                continue;
            }

            segments.Add(new Segment(edge, edgeIndex, start, end, GetBounds(start, end)));
        }

        return segments;
    }

    private static double DistanceToSegmentSquared(GraphPoint point, GraphPoint start, GraphPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        if (dx == 0 && dy == 0)
        {
            return DistanceSquared(point, start);
        }

        var progress = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (dx * dx + dy * dy);
        progress = Math.Clamp(progress, 0, 1);
        return DistanceSquared(
            point,
            new GraphPoint(start.X + progress * dx, start.Y + progress * dy));
    }

    private static double DistanceSquared(GraphPoint first, GraphPoint second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        return dx * dx + dy * dy;
    }

    private sealed record Segment(
        EdgeModel Edge,
        int EdgeIndex,
        GraphPoint Start,
        GraphPoint End,
        GraphRect Bounds);

    private readonly record struct EdgeIndexUpdate(
        EdgeModel Edge,
        List<Segment> Previous,
        List<Segment> Current);
}
