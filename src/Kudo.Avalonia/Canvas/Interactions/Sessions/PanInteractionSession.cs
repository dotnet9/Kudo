using Avalonia;
using Avalonia.Input;

namespace Kudo.Avalonia;

internal sealed class PanInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint,
    GraphPoint startViewportOffset,
    bool openContextMenuOnRelease)
    : GraphInteractionSession(canvas, pointer, startScreenPoint)
{
    private bool _openContextMenuOnRelease = openContextMenuOnRelease;

    public override bool AllowContextMenuOnRelease => _openContextMenuOnRelease;

    public override bool SupportsAutoPan => false;

    public override void Update(Point point, KeyModifiers modifiers)
    {
        var delta = point - StartScreenPoint;
        if (_openContextMenuOnRelease && delta.X * delta.X + delta.Y * delta.Y > 16)
        {
            _openContextMenuOnRelease = false;
        }

        Canvas.SetViewportCore(
            new GraphViewport(
                new GraphPoint(
                    startViewportOffset.X + delta.X,
                    startViewportOffset.Y + delta.Y),
                Canvas.Viewport.Zoom),
            GraphViewportChangeReason.Pan);
    }

    public override void Cancel() => Canvas.SetViewportCore(
        new GraphViewport(startViewportOffset, Canvas.Viewport.Zoom),
        GraphViewportChangeReason.Pan);
}
