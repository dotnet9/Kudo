using Avalonia;
using Avalonia.Input;

namespace Kudo.Avalonia;

public abstract class GraphInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint) : IGraphInteractionSession
{
    private readonly GraphPoint _startViewportOffset = canvas.Viewport.Offset;

    protected GraphCanvas Canvas { get; } = canvas;

    protected Point StartScreenPoint { get; } = startScreenPoint;

    public IPointer Pointer { get; } = pointer;

    public Point StartPointerPoint => StartScreenPoint;

    public virtual bool AllowContextMenuOnRelease => false;

    public virtual bool SupportsAutoPan => true;

    protected GraphPoint GetGraphDragDelta(Point point) => new(
        (point.X - StartScreenPoint.X - (Canvas.Viewport.Offset.X - _startViewportOffset.X)) /
        Canvas.Viewport.Zoom,
        (point.Y - StartScreenPoint.Y - (Canvas.Viewport.Offset.Y - _startViewportOffset.Y)) /
        Canvas.Viewport.Zoom);

    public abstract void Update(Point point, KeyModifiers modifiers);

    public virtual void Complete(Point point)
    {
    }

    public virtual void CaptureLost()
    {
    }

    public abstract void Cancel();
}
