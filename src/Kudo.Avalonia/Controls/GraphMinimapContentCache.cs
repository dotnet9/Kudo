using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Kudo;

namespace Kudo.Avalonia;

internal sealed class GraphMinimapContentCache
{
    private RenderTargetBitmap? _bitmap;
    private GraphDocument? _document;
    private GraphPoint _projectionOrigin;
    private double _projectionScale;
    private PixelSize _pixelSize;
    private bool _isDirty = true;

    public bool TryRender(
        DrawingContext context,
        Size minimapSize,
        GraphCanvas canvas,
        GraphDocument document,
        GraphMinimapProjection projection,
        GraphMinimapRenderPalette palette,
        GraphMinimapContentRenderer renderer)
    {
        var pixelSize = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(minimapSize.Width)),
            Math.Max(1, (int)Math.Ceiling(minimapSize.Height)));
        if (_isDirty ||
            !ReferenceEquals(_document, document) ||
            _projectionOrigin != projection.Origin ||
            _projectionScale != projection.Scale ||
            _pixelSize != pixelSize ||
            _bitmap is null)
        {
            var bitmap = new RenderTargetBitmap(pixelSize);
            using (var bitmapContext = bitmap.CreateDrawingContext())
            {
                renderer.Render(bitmapContext, canvas, document, projection, palette);
            }

            _bitmap = bitmap;
            _document = document;
            _projectionOrigin = projection.Origin;
            _projectionScale = projection.Scale;
            _pixelSize = pixelSize;
            _isDirty = false;
        }

        try
        {
            context.DrawImage(
                _bitmap,
                new Rect(_bitmap.Size),
                new Rect(minimapSize));
            return true;
        }
        catch (NotImplementedException)
        {
            return false;
        }
    }

    public void Invalidate()
    {
        // Retained frames may still reference the bitmap, so release it instead of disposing immediately.
        _bitmap = null;
        _document = null;
        _projectionOrigin = default;
        _projectionScale = 0;
        _pixelSize = default;
        _isDirty = true;
    }
}
