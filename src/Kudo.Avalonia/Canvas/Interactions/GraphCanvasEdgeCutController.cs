using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasEdgeCutController(GraphCanvasEdgeCutContext context)
{
    public int CutEdgesIntersectingSegment(GraphPoint start, GraphPoint end)
    {
        if (!context.GetIsEnabled() ||
            context.GetIsReadOnly() ||
            context.GetDocument() is not { } document ||
            !GraphPointValidation.IsFinite(start) ||
            !GraphPointValidation.IsFinite(end) ||
            start == end)
        {
            return 0;
        }

        var edges = FindEdgesIntersectingSegment(start, end);
        if (edges.Count == 0 || edges.Any(edge =>
                !context.CanPerformEdit(new GraphEditRequest(
                    GraphEditOperation.DeleteEdges,
                    document,
                    Edge: edge))))
        {
            return 0;
        }

        var commandName = context.GetCommandName();
        var commands = edges
            .Select(edge => (IGraphCommand)new RemoveEdgeCommand(edge.Id, commandName))
            .ToArray();
        var command = commands.Length == 1
            ? commands[0]
            : new CompositeGraphCommand(commandName, commands);
        command.Execute(document);
        if (!context.CommitGraphEdit(command))
        {
            return 0;
        }

        var removedIds = edges.Select(edge => edge.Id).ToHashSet(StringComparer.Ordinal);
        var remainingSelectedIds = context.GetSelectedEdgeIds()
            .Where(edgeId => !removedIds.Contains(edgeId))
            .ToArray();
        if (remainingSelectedIds.Length != context.GetSelectedEdgeIds().Count)
        {
            var primaryEdgeId = context.GetSelectedEdgeId() is { } selectedEdgeId &&
                                remainingSelectedIds.Contains(selectedEdgeId, StringComparer.Ordinal)
                ? selectedEdgeId
                : remainingSelectedIds.LastOrDefault();
            context.SetSelectedEdges(remainingSelectedIds, primaryEdgeId);
        }

        return edges.Count;
    }

    public IReadOnlyList<EdgeModel> FindEdgesIntersectingSegment(
        GraphPoint start,
        GraphPoint end)
    {
        if (context.GetDocument() is not { } document || start == end)
        {
            return [];
        }

        IReadOnlyList<EdgeModel> candidates = document.Edges;
        if (context.GetIsEdgeRoutingCacheEnabled())
        {
            context.EdgeSpatialIndex.RebuildIfNeeded(document.Edges, context.GetEdgeGraphPath);
            candidates = context.EdgeSpatialIndex.QueryEdges(new GraphRect(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Abs(end.X - start.X),
                Math.Abs(end.Y - start.Y))).ToArray();
        }

        return candidates
            .Where(edge => context.GetEdgeGraphPath(edge) is { } path &&
                PathIntersectsSegment(path.Points, start, end))
            .ToArray();
    }

    private static bool PathIntersectsSegment(
        IReadOnlyList<GraphPoint> points,
        GraphPoint start,
        GraphPoint end)
    {
        for (var index = 1; index < points.Count; index++)
        {
            if (SegmentsIntersect(points[index - 1], points[index], start, end))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(
        GraphPoint firstStart,
        GraphPoint firstEnd,
        GraphPoint secondStart,
        GraphPoint secondEnd)
    {
        const double epsilon = 1e-9;
        var firstSide1 = Cross(firstStart, firstEnd, secondStart);
        var firstSide2 = Cross(firstStart, firstEnd, secondEnd);
        var secondSide1 = Cross(secondStart, secondEnd, firstStart);
        var secondSide2 = Cross(secondStart, secondEnd, firstEnd);
        if (Math.Abs(firstSide1) <= epsilon && IsPointOnSegment(secondStart, firstStart, firstEnd, epsilon) ||
            Math.Abs(firstSide2) <= epsilon && IsPointOnSegment(secondEnd, firstStart, firstEnd, epsilon) ||
            Math.Abs(secondSide1) <= epsilon && IsPointOnSegment(firstStart, secondStart, secondEnd, epsilon) ||
            Math.Abs(secondSide2) <= epsilon && IsPointOnSegment(firstEnd, secondStart, secondEnd, epsilon))
        {
            return true;
        }

        return (firstSide1 > 0 && firstSide2 < 0 || firstSide1 < 0 && firstSide2 > 0) &&
               (secondSide1 > 0 && secondSide2 < 0 || secondSide1 < 0 && secondSide2 > 0);
    }

    private static double Cross(GraphPoint start, GraphPoint end, GraphPoint point) =>
        (end.X - start.X) * (point.Y - start.Y) -
        (end.Y - start.Y) * (point.X - start.X);

    private static bool IsPointOnSegment(
        GraphPoint point,
        GraphPoint start,
        GraphPoint end,
        double epsilon) =>
        point.X >= Math.Min(start.X, end.X) - epsilon &&
        point.X <= Math.Max(start.X, end.X) + epsilon &&
        point.Y >= Math.Min(start.Y, end.Y) - epsilon &&
        point.Y <= Math.Max(start.Y, end.Y) + epsilon;
}

internal sealed class GraphCanvasEdgeCutContext
{
    public required Func<bool> GetIsEnabled { get; init; }

    public required Func<bool> GetIsReadOnly { get; init; }

    public required Func<GraphDocument?> GetDocument { get; init; }

    public required Func<bool> GetIsEdgeRoutingCacheEnabled { get; init; }

    public required GraphEdgeSpatialIndex EdgeSpatialIndex { get; init; }

    public required Func<EdgeModel, GraphEdgePath?> GetEdgeGraphPath { get; init; }

    public required Func<GraphEditRequest, bool> CanPerformEdit { get; init; }

    public required Func<string> GetCommandName { get; init; }

    public required Func<string?> GetSelectedEdgeId { get; init; }

    public required Func<IReadOnlyList<string>> GetSelectedEdgeIds { get; init; }

    public required Action<IEnumerable<string>, string?> SetSelectedEdges { get; init; }

    public required Func<IGraphCommand, bool> CommitGraphEdit { get; init; }
}
