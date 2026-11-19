using Kudo;

namespace Kudo.Avalonia;

internal static class GraphNodeResizeCalculator
{
    public static readonly GraphSize DefaultMinimumSize = new(96, 56);

    public static (GraphPoint Position, GraphSize Size) Calculate(
        GraphPoint startPosition,
        GraphSize startSize,
        GraphNodeResizeHandle handle,
        GraphPoint delta,
        GraphSize minimumSize,
        bool resizeFromCenter = false,
        bool preserveAspectRatio = false)
    {
        var left = startPosition.X;
        var top = startPosition.Y;
        var right = startPosition.X + startSize.Width;
        var bottom = startPosition.Y + startSize.Height;
        var centerX = left + startSize.Width / 2;
        var centerY = top + startSize.Height / 2;
        var movesLeft = handle is GraphNodeResizeHandle.TopLeft or
            GraphNodeResizeHandle.Left or GraphNodeResizeHandle.BottomLeft;
        var movesRight = handle is GraphNodeResizeHandle.TopRight or
            GraphNodeResizeHandle.Right or GraphNodeResizeHandle.BottomRight;
        var movesTop = handle is GraphNodeResizeHandle.TopLeft or
            GraphNodeResizeHandle.Top or GraphNodeResizeHandle.TopRight;
        var movesBottom = handle is GraphNodeResizeHandle.BottomLeft or
            GraphNodeResizeHandle.Bottom or GraphNodeResizeHandle.BottomRight;
        var resizeFactor = resizeFromCenter ? 2.0 : 1.0;
        var requestedWidth = movesLeft
            ? startSize.Width - delta.X * resizeFactor
            : movesRight
                ? startSize.Width + delta.X * resizeFactor
                : startSize.Width;
        var requestedHeight = movesTop
            ? startSize.Height - delta.Y * resizeFactor
            : movesBottom
                ? startSize.Height + delta.Y * resizeFactor
                : startSize.Height;

        double width;
        double height;
        if (preserveAspectRatio && startSize.Width > 0 && startSize.Height > 0)
        {
            var minimumScale = Math.Max(
                minimumSize.Width / startSize.Width,
                minimumSize.Height / startSize.Height);
            var widthScale = requestedWidth / startSize.Width;
            var heightScale = requestedHeight / startSize.Height;
            var scale = movesLeft || movesRight
                ? movesTop || movesBottom
                    ? Math.Abs(widthScale - 1) >= Math.Abs(heightScale - 1)
                        ? widthScale
                        : heightScale
                    : widthScale
                : heightScale;
            scale = Math.Max(minimumScale, scale);
            width = startSize.Width * scale;
            height = startSize.Height * scale;
        }
        else
        {
            width = Math.Max(minimumSize.Width, requestedWidth);
            height = Math.Max(minimumSize.Height, requestedHeight);
        }

        if (resizeFromCenter)
        {
            left = centerX - width / 2;
            top = centerY - height / 2;
        }
        else
        {
            if (movesLeft)
            {
                left = right - width;
            }
            else if (!movesRight && preserveAspectRatio)
            {
                left = centerX - width / 2;
            }

            if (movesTop)
            {
                top = bottom - height;
            }
            else if (!movesBottom && preserveAspectRatio)
            {
                top = centerY - height / 2;
            }
        }

        return (
            new GraphPoint(left, top),
            new GraphSize(width, height));
    }

    public static GraphSize NormalizeMinimumSize(GraphSize size) =>
        new(
            double.IsFinite(size.Width) && size.Width > 0
                ? size.Width
                : DefaultMinimumSize.Width,
            double.IsFinite(size.Height) && size.Height > 0
                ? size.Height
                : DefaultMinimumSize.Height);
}
