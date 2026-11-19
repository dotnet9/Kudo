using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasNodePushController(GraphCanvasNodePushContext context)
{
    public int Push(GraphPoint boundary, GraphNodePushAxis axis, double amount)
    {
        if (!context.GetIsEnabled() ||
            context.GetIsReadOnly() ||
            context.GetIsPointerInteracting() ||
            context.GetDocument() is not { } document ||
            !GraphPointValidation.IsFinite(boundary) ||
            !double.IsFinite(amount) ||
            amount == 0 ||
            axis is not GraphNodePushAxis.Horizontal and not GraphNodePushAxis.Vertical)
        {
            return 0;
        }

        var nodes = GetCandidates(boundary, axis);
        if (nodes.Count == 0 ||
            !context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.MoveNodes,
                document,
                Nodes: nodes)))
        {
            return 0;
        }

        var oldPositions = nodes.ToDictionary(
            node => node.Id,
            node => node.Position,
            StringComparer.Ordinal);
        var newPositions = CreatePushedNodePositions(oldPositions, axis, amount);
        if (newPositions.Values.Any(position => !GraphPointValidation.IsFinite(position)))
        {
            return 0;
        }

        var command = new SetNodePositionsCommand(
            oldPositions,
            newPositions,
            context.GetCommandName());
        command.Execute(document);
        return context.CommitGraphEdit(command) ? nodes.Count : 0;
    }

    public IReadOnlyList<NodeModel> GetCandidates(
        GraphPoint boundary,
        GraphNodePushAxis axis)
    {
        if (context.GetDocument() is not { } document)
        {
            return [];
        }

        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in document.Nodes)
        {
            if (node.IsLocked)
            {
                continue;
            }

            var bounds = GraphNodeGeometry.GetCanvasBounds(node);
            if (context.IsNodeVisible(node.Id) && IsBeyondBoundary(
                    new GraphPoint(bounds.X, bounds.Y),
                    boundary,
                    axis))
            {
                candidateIds.Add(node.Id);
            }
        }

        foreach (var group in document.Groups.Where(group => group.IsCollapsed))
        {
            if (GraphGroupBoundsCalculator.Calculate(document, group) is not { } bounds ||
                !IsBeyondBoundary(
                    new GraphPoint(bounds.X, bounds.Y),
                    boundary,
                    axis))
            {
                continue;
            }

            candidateIds.UnionWith(group.NodeIds ?? []);
        }

        return document.Nodes
            .Where(node => candidateIds.Contains(node.Id) && !node.IsLocked)
            .ToArray();
    }

    public static Dictionary<string, GraphPoint> CreatePushedNodePositions(
        IReadOnlyDictionary<string, GraphPoint> startPositions,
        GraphNodePushAxis axis,
        double amount) =>
        startPositions.ToDictionary(
            pair => pair.Key,
            pair => axis == GraphNodePushAxis.Horizontal
                ? new GraphPoint(pair.Value.X + amount, pair.Value.Y)
                : new GraphPoint(pair.Value.X, pair.Value.Y + amount),
            StringComparer.Ordinal);

    private static bool IsBeyondBoundary(
        GraphPoint position,
        GraphPoint boundary,
        GraphNodePushAxis axis) =>
        axis == GraphNodePushAxis.Horizontal
            ? position.X >= boundary.X
            : position.Y >= boundary.Y;
}

internal sealed class GraphCanvasNodePushContext
{
    public required Func<bool> GetIsEnabled { get; init; }

    public required Func<bool> GetIsReadOnly { get; init; }

    public required Func<bool> GetIsPointerInteracting { get; init; }

    public required Func<GraphDocument?> GetDocument { get; init; }

    public required Func<string, bool> IsNodeVisible { get; init; }

    public required Func<GraphEditRequest, bool> CanPerformEdit { get; init; }

    public required Func<string> GetCommandName { get; init; }

    public required Func<IGraphCommand, bool> CommitGraphEdit { get; init; }
}
