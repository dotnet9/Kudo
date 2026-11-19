using Avalonia;
using Avalonia.Data;
using Avalonia.Media;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public static readonly StyledProperty<string?> SelectedAnnotationIdProperty =
        AvaloniaProperty.Register<GraphCanvas, string?>(
            nameof(SelectedAnnotationId),
            defaultBindingMode: BindingMode.TwoWay);

    public string? SelectedAnnotationId
    {
        get => GetValue(SelectedAnnotationIdProperty);
        set => _annotationController.SetSelected(value);
    }

    public GraphSize MinimumAnnotationSize
    {
        get => _annotationController.MinimumSize;
        set => _annotationController.MinimumSize = value;
    }

    public AnnotationModel? HitTestAnnotation(Point screenPoint) =>
        _annotationController.HitTest(screenPoint);

    public AnnotationResizeHandleHitResult? HitTestSelectedAnnotationResizeHandle(Point screenPoint) =>
        _annotationController.HitTestSelectedResizeHandle(screenPoint);

    public bool SetSelectedAnnotation(string? annotationId) =>
        _annotationController.SetSelected(annotationId);

    public bool DeleteSelectedAnnotation() =>
        _annotationController.DeleteSelected();

    private void RenderAnnotations(
        DrawingContext context,
        IEnumerable<AnnotationModel> annotations,
        GraphRect? cullingBounds) =>
        _annotationRenderer.Render(context, annotations, cullingBounds);

    private void RenderSelectedAnnotationResizeHandles(DrawingContext context) =>
        _annotationRenderer.RenderSelectedResizeHandles(context);

    private Rect GetAnnotationScreenRect(AnnotationModel annotation) =>
        GraphCanvasAnnotationGeometry.GetScreenRect(annotation, Viewport);

    private static Rect GetAnnotationGraphRect(AnnotationModel annotation) =>
        GraphCanvasAnnotationGeometry.GetGraphRect(annotation);

    private static bool IsValidAnnotationBounds(AnnotationModel annotation) =>
        GraphCanvasAnnotationGeometry.IsValidBounds(annotation);
}

public sealed record AnnotationResizeHandleHitResult(
    AnnotationModel Annotation,
    GraphNodeResizeHandle Handle,
    Point ScreenPosition);
