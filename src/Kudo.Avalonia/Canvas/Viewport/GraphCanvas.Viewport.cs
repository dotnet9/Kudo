using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public Point GraphToScreen(GraphPoint point) => _viewportController.GraphToScreen(point);

    public GraphPoint ScreenToGraph(Point point) => _viewportController.ScreenToGraph(point);

    public bool SetViewport(GraphViewport viewport) =>
        SetViewportCore(viewport, GraphViewportChangeReason.SetViewport);

    public bool SetZoom(double zoom) => _viewportController.SetZoom(zoom);

    public bool ZoomAt(Point screenPoint, double targetZoom) =>
        _viewportController.ZoomAt(screenPoint, targetZoom);

    public bool ZoomBy(double factor) => _viewportController.ZoomBy(factor);

    public bool ZoomBy(double factor, Point screenPoint) =>
        _viewportController.ZoomBy(factor, screenPoint);

    public bool ResetViewport() => _viewportController.Reset();

    public Rect? GetViewportGraphBounds() => _viewportController.GetGraphBounds();

    public bool CenterViewport(GraphPoint graphPoint) => _viewportController.Center(graphPoint);

    public bool PanBy(Vector screenDelta)
    {
        if (!double.IsFinite(screenDelta.X) || !double.IsFinite(screenDelta.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(screenDelta),
                "The screen delta must use finite coordinates.");
        }

        var viewport = Viewport;
        return SetViewportCore(
            viewport with
            {
                Offset = new GraphPoint(
                    viewport.Offset.X + screenDelta.X,
                    viewport.Offset.Y + screenDelta.Y)
            },
            GraphViewportChangeReason.Pan);
    }

    internal bool SetViewportCore(GraphViewport viewport, GraphViewportChangeReason reason)
    {
        var normalized = NormalizeViewport(viewport, ViewportOptions);
        var previous = GetValue(ViewportProperty);
        if (previous == normalized)
        {
            return false;
        }

        _pendingViewportChangeReason = reason;
        try
        {
            SetValue(ViewportProperty, normalized);
            return true;
        }
        finally
        {
            _pendingViewportChangeReason = null;
        }
    }

    private void ApplyViewportOptionsChange(
        GraphViewportOptions? previousOptions,
        GraphViewportOptions? options)
    {
        var previousNormalized = (previousOptions ?? GraphViewportOptions.Default).Normalize();
        var normalized = (options ?? GraphViewportOptions.Default).Normalize();
        if (previousNormalized == normalized)
        {
            return;
        }

        var previousViewport = Viewport;
        var visibleCenter = GetViewportGraphBounds() is { } visibleBounds
            ? new GraphPoint(visibleBounds.Center.X, visibleBounds.Center.Y)
            : default(GraphPoint?);
        var normalizedViewport = NormalizeViewport(previousViewport, normalized);
        if (visibleCenter is { } center && normalizedViewport.Zoom != previousViewport.Zoom)
        {
            var viewportSize = Bounds.Size;
            normalizedViewport = normalizedViewport with
            {
                Offset = new GraphPoint(
                    viewportSize.Width / 2 - center.X * normalizedViewport.Zoom,
                    viewportSize.Height / 2 - center.Y * normalizedViewport.Zoom)
            };
        }

        _pendingViewportChangeReason = GraphViewportChangeReason.ViewportOptionsChanged;
        _suppressViewportChangedEvent = true;
        try
        {
            if (previousViewport != normalizedViewport)
            {
                SetValue(ViewportProperty, normalizedViewport);
            }

            RaiseViewportChanged(
                previousViewport,
                normalizedViewport,
                previousNormalized,
                normalized,
                GraphViewportChangeReason.ViewportOptionsChanged);
        }
        finally
        {
            _suppressViewportChangedEvent = false;
            _pendingViewportChangeReason = null;
        }
    }

    private static GraphViewport NormalizeViewport(
        GraphViewport? viewport,
        GraphViewportOptions options)
    {
        var value = viewport ?? GraphViewport.Default;
        var zoom = double.IsFinite(value.Zoom) && value.Zoom > 0
            ? Math.Clamp(value.Zoom, options.MinimumZoom, options.MaximumZoom)
            : 1.0;
        zoom = Math.Clamp(zoom, options.MinimumZoom, options.MaximumZoom);
        var offset = double.IsFinite(value.Offset.X) && double.IsFinite(value.Offset.Y)
            ? value.Offset
            : GraphPoint.Zero;
        return new GraphViewport(offset, zoom);
    }

    private void RaiseViewportChanged(
        GraphViewport previousViewport,
        GraphViewport viewport,
        GraphViewportOptions previousOptions,
        GraphViewportOptions viewportOptions,
        GraphViewportChangeReason reason) =>
        ViewportChanged?.Invoke(
            this,
            new GraphViewportChangedEventArgs(
                previousViewport,
                viewport,
                previousOptions,
                viewportOptions,
                GetViewportGraphBounds(),
                reason));
}
