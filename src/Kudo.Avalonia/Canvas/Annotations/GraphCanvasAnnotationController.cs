using Avalonia;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasAnnotationController(GraphCanvasAnnotationContext context)
{
    private GraphSize _minimumSize = GraphCanvasAnnotationGeometry.DefaultMinimumSize;

    public GraphSize MinimumSize
    {
        get => _minimumSize;
        set => _minimumSize = GraphCanvasAnnotationGeometry.NormalizeMinimumSize(value);
    }

    public bool CanApplySelectedPropertyValue(string? annotationId)
    {
        if (!string.IsNullOrWhiteSpace(annotationId) &&
            context.Document() is { } document &&
            document.FindAnnotation(annotationId) is { } annotation &&
            !context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.SelectAnnotation,
                document,
                Annotation: annotation)))
        {
            return false;
        }

        return true;
    }

    public AnnotationModel? HitTest(Point screenPoint)
    {
        var graphPoint = context.ScreenToGraph(screenPoint);
        return context.Document()?.Annotations
            .AsEnumerable()
            .Reverse()
            .FirstOrDefault(annotation => GraphCanvasAnnotationGeometry.Contains(annotation, graphPoint));
    }

    public AnnotationResizeHandleHitResult? HitTestSelectedResizeHandle(Point screenPoint)
    {
        if (context.IsReadOnly() ||
            context.Document()?.FindAnnotation(context.SelectedAnnotationId() ?? string.Empty) is not { } annotation)
        {
            return null;
        }

        var maxDistance = GraphCanvasAnnotationGeometry.ResizeHandleHitRadius *
            GraphCanvasAnnotationGeometry.ResizeHandleHitRadius;
        return GraphCanvasAnnotationGeometry.GetResizeHandlePositions(annotation, context.Viewport())
            .Select(item => new
            {
                item.Handle,
                item.ScreenPosition,
                Distance = DistanceSquared(screenPoint, item.ScreenPosition)
            })
            .Where(item => item.Distance <= maxDistance)
            .OrderBy(item => item.Distance)
            .Select(item => new AnnotationResizeHandleHitResult(
                annotation,
                item.Handle,
                item.ScreenPosition))
            .FirstOrDefault();
    }

    public bool SetSelected(string? annotationId)
    {
        var document = context.Document();
        var annotation = string.IsNullOrWhiteSpace(annotationId)
            ? null
            : document?.FindAnnotation(annotationId);
        if (annotationId is not null && annotation is null)
        {
            return false;
        }

        if (annotation is not null &&
            !context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.SelectAnnotation,
                document!,
                Annotation: annotation)))
        {
            return false;
        }

        ApplySelection(annotation?.Id);
        return true;
    }

    public bool DeleteSelected()
    {
        if (context.IsReadOnly() ||
            context.Document() is not { } document ||
            document.FindAnnotation(context.SelectedAnnotationId() ?? string.Empty) is not { } annotation ||
            !context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.DeleteAnnotation,
                document,
                Annotation: annotation)))
        {
            return false;
        }

        var command = new RemoveAnnotationCommand(
            annotation.Id,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.RemoveAnnotationCommand));
        command.Execute(document);
        if (!context.CommitGraphEdit(command))
        {
            return false;
        }

        context.SetSelectedAnnotationId(null);
        return true;
    }

    private void ApplySelection(string? annotationId)
    {
        var selectionChanged = context.SelectedNodeIds().Count > 0 ||
            context.SelectedNodeId() is not null ||
            context.SelectedEdgeIds().Count > 0 ||
            context.SelectedAnnotationId() != annotationId;
        if (!selectionChanged)
        {
            return;
        }

        context.SetSuppressSelectionChanged(true);
        context.SetIsUpdatingNodeSelection(true);
        context.SetIsUpdatingEdgeSelection(true);
        try
        {
            context.SelectedNodeIds().Clear();
            context.SetSelectedNodeId(null);
            context.SelectedEdgeIds().Clear();
            context.SetSelectedEdgeId(null);
            context.SetSelectedAnnotationId(annotationId);
        }
        finally
        {
            context.SetIsUpdatingEdgeSelection(false);
            context.SetIsUpdatingNodeSelection(false);
            context.SetSuppressSelectionChanged(false);
        }

        context.InvalidateVisual();
        context.RaiseSelectionChanged();
        context.RefreshNodeControlStates();
    }

    private static double DistanceSquared(Point first, Point second)
    {
        var delta = first - second;
        return delta.X * delta.X + delta.Y * delta.Y;
    }
}

internal sealed class GraphCanvasAnnotationContext
{
    public required Func<GraphDocument?> Document { get; init; }

    public required Func<bool> IsReadOnly { get; init; }

    public required Func<GraphViewport> Viewport { get; init; }

    public required Func<HashSet<string>> SelectedNodeIds { get; init; }

    public required Func<string?> SelectedNodeId { get; init; }

    public required Action<string?> SetSelectedNodeId { get; init; }

    public required Func<string?> SelectedEdgeId { get; init; }

    public required Func<HashSet<string>> SelectedEdgeIds { get; init; }

    public required Action<string?> SetSelectedEdgeId { get; init; }

    public required Func<string?> SelectedAnnotationId { get; init; }

    public required Action<string?> SetSelectedAnnotationId { get; init; }

    public required Action<bool> SetIsUpdatingNodeSelection { get; init; }

    public required Action<bool> SetIsUpdatingEdgeSelection { get; init; }

    public required Action<bool> SetSuppressSelectionChanged { get; init; }

    public required Func<Point, GraphPoint> ScreenToGraph { get; init; }

    public required Func<GraphEditRequest, bool> CanPerformEdit { get; init; }

    public required Func<IGraphCommand, bool> CommitGraphEdit { get; init; }

    public required Action InvalidateVisual { get; init; }

    public required Action RaiseSelectionChanged { get; init; }

    public required Action RefreshNodeControlStates { get; init; }
}
