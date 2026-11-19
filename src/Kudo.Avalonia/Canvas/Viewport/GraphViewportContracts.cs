using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

/// <summary>
/// Defines the zoom range owned by a graph canvas host.
/// This is runtime UI policy and is intentionally not persisted in a graph document.
/// </summary>
public sealed record GraphViewportOptions
{
    public static GraphViewportOptions Default { get; } = new();

    /// <summary>
    /// Gets the lowest allowed zoom factor. The default is 10%.
    /// </summary>
    public double MinimumZoom { get; init; } = 0.1;

    /// <summary>
    /// Gets the highest allowed zoom factor. The default is 500%.
    /// </summary>
    public double MaximumZoom { get; init; } = 5.0;

    public bool Contains(double zoom) =>
        double.IsFinite(zoom) && zoom >= MinimumZoom && zoom <= MaximumZoom;

    internal GraphViewportOptions Normalize()
    {
        var minimum = double.IsFinite(MinimumZoom) && MinimumZoom > 0
            ? MinimumZoom
            : Default.MinimumZoom;
        var maximum = double.IsFinite(MaximumZoom) && MaximumZoom > 0
            ? MaximumZoom
            : Default.MaximumZoom;
        if (maximum < minimum)
        {
            maximum = minimum;
        }

        return minimum == MinimumZoom && maximum == MaximumZoom
            ? this
            : new GraphViewportOptions
            {
                MinimumZoom = minimum,
                MaximumZoom = maximum
            };
    }
}

/// <summary>
/// Identifies the operation that produced a viewport update.
/// </summary>
public enum GraphViewportChangeReason
{
    External,
    SetViewport,
    Zoom,
    Pan,
    Center,
    Reset,
    FitDocument,
    FitSelection,
    ViewportOptionsChanged
}

/// <summary>
/// Describes one atomic viewport update.
/// </summary>
public sealed class GraphViewportChangedEventArgs(
    GraphViewport previousViewport,
    GraphViewport viewport,
    GraphViewportOptions previousOptions,
    GraphViewportOptions viewportOptions,
    Rect? visibleGraphBounds,
    GraphViewportChangeReason reason) : EventArgs
{
    public GraphViewport PreviousViewport { get; } = previousViewport;

    public GraphViewport Viewport { get; } = viewport;

    public GraphViewportOptions PreviousOptions { get; } = previousOptions;

    public GraphViewportOptions ViewportOptions { get; } = viewportOptions;

    public Rect? VisibleGraphBounds { get; } = visibleGraphBounds;

    public GraphViewportChangeReason Reason { get; } = reason;
}

/// <summary>
/// Provides the stable viewport contract consumed by host UI, assistive input and external devices.
/// Implementations own range validation and viewport notifications.
/// </summary>
public interface IGraphViewportHost
{
    GraphViewport Viewport { get; set; }

    GraphViewportOptions ViewportOptions { get; set; }

    Rect? VisibleGraphBounds { get; }

    event EventHandler<GraphViewportChangedEventArgs>? ViewportChanged;

    Point GraphToScreen(GraphPoint point);

    GraphPoint ScreenToGraph(Point point);

    bool SetViewport(GraphViewport viewport);

    bool SetZoom(double zoom);

    bool ZoomAt(Point screenPoint, double targetZoom);

    bool ZoomBy(double factor);

    bool ZoomBy(double factor, Point screenPoint);

    bool PanBy(Vector screenDelta);

    bool CenterViewport(GraphPoint graphPoint);

    bool ResetViewport();

    bool FitToDocument(Size? viewportSize = null, double padding = 48);

    bool FitToSelection(Size? viewportSize = null, double padding = 64);
}
