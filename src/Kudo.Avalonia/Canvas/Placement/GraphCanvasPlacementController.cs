using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasPlacementController(GraphCanvasPlacementContext context)
{
    private const string PreviewNodeIdPrefix = "__kudo-placement-preview__";
    private GraphNodePlacementPreview? _preview;

    public GraphNodePlacementPreview? Preview => _preview;

    public GraphNodePlacementPreview Update(
        GraphPoint rawPosition,
        GraphSize size,
        string? title = null,
        bool isValid = true,
        GraphNodeVisualMode visualMode = GraphNodeVisualMode.Card)
    {
        if (!double.IsFinite(rawPosition.X) || !double.IsFinite(rawPosition.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawPosition),
                "The node preview position must use finite coordinates.");
        }

        if (!double.IsFinite(size.Width) || size.Width <= 0 ||
            !double.IsFinite(size.Height) || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                "The node preview size must contain finite positive values.");
        }

        var previewNode = new NodeModel
        {
            Id = CreatePreviewNodeId(),
            Position = rawPosition,
            Size = size
        };
        var initialPositions = new Dictionary<string, GraphPoint>(StringComparer.Ordinal)
        {
            [previewNode.Id] = rawPosition
        };
        var snapResult = GraphNodeSnapper.Snap(
            [previewNode],
            context.Document()?.Nodes.Where(node => context.IsNodeVisible(node.Id)).ToArray() ?? [],
            initialPositions,
            previewNode.Id,
            GraphPoint.Zero,
            context.GridSettings(),
            context.Viewport().Zoom);
        var position = snapResult.NodePositions.TryGetValue(previewNode.Id, out var snappedPosition)
            ? snappedPosition
            : rawPosition;
        context.SetAlignmentGuides(snapResult.AlignmentGuides);
        _preview = new GraphNodePlacementPreview(
            position,
            size,
            title,
            isValid,
            snapResult.IsGuideSnapX,
            snapResult.IsGuideSnapY,
            snapResult.IsGridSnapX,
            snapResult.IsGridSnapY,
            visualMode);
        context.InvalidateVisual();
        return _preview;
    }

    public void Clear()
    {
        if (_preview is null && context.AlignmentGuides().Count == 0)
        {
            return;
        }

        _preview = null;
        context.SetAlignmentGuides([]);
        context.InvalidateVisual();
    }

    private string CreatePreviewNodeId()
    {
        var nodeId = PreviewNodeIdPrefix;
        var sequence = 1;
        while (context.Document()?.FindNode(nodeId) is not null)
        {
            nodeId = $"{PreviewNodeIdPrefix}{sequence++}";
        }

        return nodeId;
    }
}

internal sealed class GraphCanvasPlacementContext
{
    public required Func<GraphDocument?> Document { get; init; }

    public required Func<GraphGridSettings> GridSettings { get; init; }

    public required Func<GraphViewport> Viewport { get; init; }

    public required Func<IReadOnlyList<GraphAlignmentGuide>> AlignmentGuides { get; init; }

    public required Action<IReadOnlyList<GraphAlignmentGuide>> SetAlignmentGuides { get; init; }

    public required Func<string, bool> IsNodeVisible { get; init; }

    public required Action InvalidateVisual { get; init; }
}
