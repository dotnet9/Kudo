using Avalonia;
using Avalonia.Input;

namespace Kudo.Avalonia;

internal sealed class RectangleSelectionInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint,
    RectangleSelectionMode mode,
    IReadOnlyList<string> baseNodeIds,
    IReadOnlyList<string> startNodeIds,
    string? startPrimaryNodeId,
    IReadOnlyList<string> startEdgeIds,
    string? startEdgeId,
    string? startAnnotationId)
    : GraphInteractionSession(canvas, pointer, startScreenPoint)
{
    public GraphPoint StartGraphPoint { get; } = canvas.ScreenToGraph(startScreenPoint);

    public GraphPoint CurrentGraphPoint { get; private set; } = canvas.ScreenToGraph(startScreenPoint);

    public override void Update(Point point, KeyModifiers modifiers)
    {
        CurrentGraphPoint = Canvas.ScreenToGraph(point);
        var selectionRect = GraphGeometryMath.NormalizeRect(StartGraphPoint, CurrentGraphPoint);
        var intersectingIds = new HashSet<string>(StringComparer.Ordinal);
        if (Canvas.Document is { } document)
        {
            Canvas.ElementSpatialIndex.RebuildIfNeeded(document);
            Canvas.ElementSpatialIndex.AddIntersectingNodeIds(
                GraphGeometryMath.ToGraphRect(selectionRect),
                intersectingIds);
        }

        var selectedSet = mode switch
        {
            RectangleSelectionMode.Add => baseNodeIds
                .Concat(intersectingIds)
                .ToHashSet(StringComparer.Ordinal),
            RectangleSelectionMode.Subtract => baseNodeIds
                .Where(nodeId => !intersectingIds.Contains(nodeId))
                .ToHashSet(StringComparer.Ordinal),
            _ => intersectingIds
        };
        var selectedIds = Canvas.GetDocumentOrderedNodeIds(selectedSet);
        var primaryNodeId = mode == RectangleSelectionMode.Subtract &&
                            startPrimaryNodeId is not null &&
                            selectedSet.Contains(startPrimaryNodeId)
            ? startPrimaryNodeId
            : selectedIds.LastOrDefault();
        Canvas.SetSelectedNodes(selectedIds, primaryNodeId);
        Canvas.InvalidateVisual();
    }

    public override void Cancel()
    {
        Canvas.ApplyNodeSelection(
            startNodeIds,
            startPrimaryNodeId,
            clearEdgeSelection: false);
        Canvas.ApplyEdgeSelection(
            startEdgeIds,
            startEdgeId,
            clearOtherSelection: false);
        Canvas.SelectedAnnotationId = startAnnotationId;
    }
}
