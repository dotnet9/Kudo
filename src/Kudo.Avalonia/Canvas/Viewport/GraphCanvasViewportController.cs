using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

/// <summary>
/// Keeps viewport math independent from input and rendering. All mutations are sent back as a
/// complete <see cref="GraphViewport"/> so consumers never observe an intermediate zoom/offset pair.
/// </summary>
internal sealed class GraphCanvasViewportController(
    Func<GraphViewport> viewportProvider,
    Func<GraphViewportOptions> optionsProvider,
    Func<Size> viewportSizeProvider,
    Func<GraphViewport, GraphViewportChangeReason, bool> viewportSetter)
{
    public Point GraphToScreen(GraphPoint point)
    {
        var viewport = viewportProvider();
        return new Point(
            point.X * viewport.Zoom + viewport.Offset.X,
            point.Y * viewport.Zoom + viewport.Offset.Y);
    }

    public GraphPoint ScreenToGraph(Point point)
    {
        var viewport = viewportProvider();
        return new GraphPoint(
            (point.X - viewport.Offset.X) / viewport.Zoom,
            (point.Y - viewport.Offset.Y) / viewport.Zoom);
    }

    public bool SetZoom(double targetZoom)
    {
        var size = viewportSizeProvider();
        return ZoomAt(new Point(size.Width / 2, size.Height / 2), targetZoom);
    }

    public bool ZoomAt(Point screenPoint, double targetZoom)
    {
        if (!double.IsFinite(screenPoint.X) || !double.IsFinite(screenPoint.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(screenPoint),
                "The zoom anchor must use finite coordinates.");
        }

        if (!double.IsFinite(targetZoom) || targetZoom <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetZoom),
                "The target zoom must be a finite positive number.");
        }

        var current = viewportProvider();
        var normalizedZoom = ClampZoom(targetZoom);
        if (Math.Abs(normalizedZoom - current.Zoom) <= double.Epsilon)
        {
            return false;
        }

        var graphAnchor = ScreenToGraph(screenPoint);
        return viewportSetter(
            new GraphViewport(
                new GraphPoint(
                    screenPoint.X - graphAnchor.X * normalizedZoom,
                    screenPoint.Y - graphAnchor.Y * normalizedZoom),
                normalizedZoom),
            GraphViewportChangeReason.Zoom);
    }

    public bool ZoomBy(double factor) => ZoomBy(factor, GetViewportCenter());

    public bool ZoomBy(double factor, Point screenPoint)
    {
        if (!double.IsFinite(factor) || factor <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(factor),
                "The zoom factor must be a finite positive number.");
        }

        return ZoomAt(screenPoint, viewportProvider().Zoom * factor);
    }

    public bool Reset() => viewportSetter(GraphViewport.Default, GraphViewportChangeReason.Reset);

    public Rect? GetGraphBounds()
    {
        var viewportSize = viewportSizeProvider();
        if (viewportSize.Width <= 0 || viewportSize.Height <= 0)
        {
            return null;
        }

        var topLeft = ScreenToGraph(default);
        var zoom = viewportProvider().Zoom;
        return new Rect(
            topLeft.X,
            topLeft.Y,
            viewportSize.Width / zoom,
            viewportSize.Height / zoom);
    }

    public bool Center(GraphPoint graphPoint)
    {
        if (!double.IsFinite(graphPoint.X) || !double.IsFinite(graphPoint.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(graphPoint),
                "The graph point must use finite coordinates.");
        }

        var viewportSize = viewportSizeProvider();
        if (viewportSize.Width <= 0 || viewportSize.Height <= 0)
        {
            return false;
        }

        var viewport = viewportProvider();
        return viewportSetter(
            viewport with
            {
                Offset = new GraphPoint(
                    viewportSize.Width / 2 - graphPoint.X * viewport.Zoom,
                    viewportSize.Height / 2 - graphPoint.Y * viewport.Zoom)
            },
            GraphViewportChangeReason.Center);
    }

    public bool Fit(Rect? graphBounds, Size viewportSize, double padding, GraphViewportChangeReason reason)
    {
        if (graphBounds is null || viewportSize.Width <= 0 || viewportSize.Height <= 0)
        {
            return false;
        }

        padding = double.IsFinite(padding) ? Math.Max(0, padding) : 0;
        var bounds = NormalizeFitBounds(graphBounds.Value);
        var availableWidth = Math.Max(1, viewportSize.Width - padding * 2);
        var availableHeight = Math.Max(1, viewportSize.Height - padding * 2);
        var zoom = ClampZoom(Math.Min(
            availableWidth / bounds.Width,
            availableHeight / bounds.Height));
        return viewportSetter(
            new GraphViewport(
                new GraphPoint(
                    (viewportSize.Width - bounds.Width * zoom) / 2 - bounds.X * zoom,
                    (viewportSize.Height - bounds.Height * zoom) / 2 - bounds.Y * zoom),
                zoom),
            reason);
    }

    private Point GetViewportCenter()
    {
        var size = viewportSizeProvider();
        return new Point(size.Width / 2, size.Height / 2);
    }

    private double ClampZoom(double zoom)
    {
        var options = optionsProvider();
        return Math.Clamp(zoom, options.MinimumZoom, options.MaximumZoom);
    }

    private static Rect NormalizeFitBounds(Rect bounds)
    {
        const double minimumExtent = 1;
        var width = Math.Max(minimumExtent, bounds.Width);
        var height = Math.Max(minimumExtent, bounds.Height);
        return new Rect(
            bounds.X - (width - bounds.Width) / 2,
            bounds.Y - (height - bounds.Height) / 2,
            width,
            height);
    }
}
