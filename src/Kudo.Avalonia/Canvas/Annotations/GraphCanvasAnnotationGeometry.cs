using Avalonia;

namespace Kudo.Avalonia;

internal static class GraphCanvasAnnotationGeometry
{
    public const double Padding = 12;
    public const double ResizeHandleRadius = 4;
    public const double ResizeHandleHitRadius = 7;
    public const double ResizeHandleOffset = 8;

    public static GraphSize DefaultMinimumSize { get; } = new(120, 72);

    public static GraphSize NormalizeMinimumSize(GraphSize value) =>
        new(
            double.IsFinite(value.Width) && value.Width > 0
                ? value.Width
                : DefaultMinimumSize.Width,
            double.IsFinite(value.Height) && value.Height > 0
                ? value.Height
                : DefaultMinimumSize.Height);

    public static Rect GetScreenRect(AnnotationModel annotation, GraphViewport viewport)
    {
        var origin = new Point(
            annotation.Position.X * viewport.Zoom + viewport.Offset.X,
            annotation.Position.Y * viewport.Zoom + viewport.Offset.Y);
        return new Rect(
            origin.X,
            origin.Y,
            annotation.Size.Width * viewport.Zoom,
            annotation.Size.Height * viewport.Zoom);
    }

    public static Rect GetGraphRect(AnnotationModel annotation) =>
        new(
            annotation.Position.X,
            annotation.Position.Y,
            annotation.Size.Width,
            annotation.Size.Height);

    public static bool Contains(AnnotationModel annotation, GraphPoint point) =>
        IsValidBounds(annotation) &&
        point.X >= annotation.Position.X &&
        point.X <= annotation.Position.X + annotation.Size.Width &&
        point.Y >= annotation.Position.Y &&
        point.Y <= annotation.Position.Y + annotation.Size.Height;

    public static bool IsValidBounds(AnnotationModel annotation) =>
        double.IsFinite(annotation.Position.X) &&
        double.IsFinite(annotation.Position.Y) &&
        double.IsFinite(annotation.Size.Width) &&
        double.IsFinite(annotation.Size.Height) &&
        annotation.Size.Width > 0 &&
        annotation.Size.Height > 0;

    public static bool Intersects(GraphRect first, Rect second) =>
        first.X < second.Right &&
        first.X + first.Width > second.Left &&
        first.Y < second.Bottom &&
        first.Y + first.Height > second.Top;

    public static IReadOnlyList<(GraphNodeResizeHandle Handle, Point ScreenPosition)>
        GetResizeHandlePositions(AnnotationModel annotation, GraphViewport viewport)
    {
        var rect = GetScreenRect(annotation, viewport);
        var centerX = rect.Left + rect.Width / 2;
        var centerY = rect.Top + rect.Height / 2;
        return
        [
            (GraphNodeResizeHandle.TopLeft,
                new Point(rect.Left - ResizeHandleOffset, rect.Top - ResizeHandleOffset)),
            (GraphNodeResizeHandle.Top,
                new Point(centerX, rect.Top - ResizeHandleOffset)),
            (GraphNodeResizeHandle.TopRight,
                new Point(rect.Right + ResizeHandleOffset, rect.Top - ResizeHandleOffset)),
            (GraphNodeResizeHandle.Right,
                new Point(rect.Right + ResizeHandleOffset, centerY)),
            (GraphNodeResizeHandle.BottomRight,
                new Point(rect.Right + ResizeHandleOffset, rect.Bottom + ResizeHandleOffset)),
            (GraphNodeResizeHandle.Bottom,
                new Point(centerX, rect.Bottom + ResizeHandleOffset)),
            (GraphNodeResizeHandle.BottomLeft,
                new Point(rect.Left - ResizeHandleOffset, rect.Bottom + ResizeHandleOffset)),
            (GraphNodeResizeHandle.Left,
                new Point(rect.Left - ResizeHandleOffset, centerY))
        ];
    }
}
