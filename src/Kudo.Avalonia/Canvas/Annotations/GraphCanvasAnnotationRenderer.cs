using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasAnnotationRenderer(GraphCanvasAnnotationRendererContext context)
{
    public void Render(
        DrawingContext drawingContext,
        IEnumerable<AnnotationModel> annotations,
        GraphRect? cullingBounds)
    {
        var fill = context.AnnotationFillBrush();
        var textBrush = context.AnnotationTextBrush();
        var viewport = context.Viewport();
        foreach (var annotation in annotations)
        {
            var graphRect = GraphCanvasAnnotationGeometry.GetGraphRect(annotation);
            if (!GraphCanvasAnnotationGeometry.IsValidBounds(annotation) ||
                cullingBounds is { } visibleBounds &&
                !GraphCanvasAnnotationGeometry.Intersects(visibleBounds, graphRect))
            {
                continue;
            }

            var rect = GraphCanvasAnnotationGeometry.GetScreenRect(annotation, viewport);
            var isSelected = annotation.Id == context.SelectedAnnotationId();
            var pen = new Pen(
                isSelected ? context.SelectedAnnotationStrokeBrush() : context.AnnotationStrokeBrush(),
                isSelected ? 2.0 : 1.0);
            var radius = Math.Max(2, 6 * viewport.Zoom);
            drawingContext.DrawRectangle(fill, pen, rect, radius, radius);

            if (context.IsEditingAnnotation(annotation) ||
                string.IsNullOrWhiteSpace(annotation.Text) ||
                rect.Width <= 4 ||
                rect.Height <= 4)
            {
                continue;
            }

            var padding = Math.Min(
                GraphCanvasAnnotationGeometry.Padding * viewport.Zoom,
                Math.Min(rect.Width, rect.Height) / 4);
            var text = new FormattedText(
                annotation.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                Math.Max(8, 13 * viewport.Zoom),
                textBrush)
            {
                MaxTextWidth = Math.Max(1, rect.Width - padding * 2),
                MaxTextHeight = Math.Max(1, rect.Height - padding * 2),
                Trimming = TextTrimming.CharacterEllipsis
            };
            drawingContext.DrawText(text, new Point(rect.Left + padding, rect.Top + padding));
        }
    }

    public void RenderSelectedResizeHandles(DrawingContext drawingContext)
    {
        if (context.Document()?.FindAnnotation(context.SelectedAnnotationId() ?? string.Empty) is not { } annotation)
        {
            return;
        }

        foreach (var (_, position) in GraphCanvasAnnotationGeometry.GetResizeHandlePositions(
                     annotation,
                     context.Viewport()))
        {
            drawingContext.DrawEllipse(
                context.CanvasBackgroundBrush(),
                new Pen(context.SelectedAnnotationStrokeBrush(), 1.5),
                position,
                GraphCanvasAnnotationGeometry.ResizeHandleRadius,
                GraphCanvasAnnotationGeometry.ResizeHandleRadius);
        }
    }
}

internal sealed class GraphCanvasAnnotationRendererContext
{
    public required Func<GraphDocument?> Document { get; init; }

    public required Func<GraphViewport> Viewport { get; init; }

    public required Func<string?> SelectedAnnotationId { get; init; }

    public required Func<IBrush> CanvasBackgroundBrush { get; init; }

    public required Func<IBrush> AnnotationFillBrush { get; init; }

    public required Func<IBrush> AnnotationStrokeBrush { get; init; }

    public required Func<IBrush> SelectedAnnotationStrokeBrush { get; init; }

    public required Func<IBrush> AnnotationTextBrush { get; init; }

    public required Func<AnnotationModel, bool> IsEditingAnnotation { get; init; }
}
