using Avalonia;
using Avalonia.Input;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class NodeResizeInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint,
    NodeResizeHandleHitResult hit)
    : GraphInteractionSession(canvas, pointer, startScreenPoint)
{
    private readonly GraphPoint _startPosition = hit.Node.Position;
    private readonly GraphSize _startSize = hit.Node.Size;

    public override void Update(Point point, KeyModifiers modifiers)
    {
        var graphDelta = GetGraphDragDelta(point);
        var localDelta = GraphNodeGeometry.RotateVector(graphDelta, -hit.Node.Rotation);
        var result = GraphNodeResizeCalculator.Calculate(
            GraphPoint.Zero,
            _startSize,
            hit.Handle,
            localDelta,
            Canvas.ResolveMinimumNodeSize(hit.Node),
            resizeFromCenter: modifiers.HasFlag(KeyModifiers.Alt),
            preserveAspectRatio: modifiers.HasFlag(KeyModifiers.Shift));
        var localPosition = result.Position;
        var actualSize = result.Size;
        hit.Node.Position = ResolveRotatedResizePosition(
            _startPosition,
            _startSize,
            localPosition,
            actualSize,
            hit.Node.Rotation);
        hit.Node.Size = actualSize;
        Canvas.InvalidateNodeGeometry(hit.Node.Id);
        Canvas.UpdateNodeControlHostLayout(hit.Node.Id);
        Canvas.InvalidateVisual();
    }

    public override void Complete(Point point)
    {
        if (hit.Node.Position == _startPosition &&
            hit.Node.Size == _startSize)
        {
            return;
        }

        Canvas.CommitGraphEdit(new ResizeNodeCommand(
            hit.Node.Id,
            _startPosition,
            _startSize,
            hit.Node.Position,
            hit.Node.Size,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.ResizeNodeCommand)));
    }

    public override void CaptureLost() => Cancel();

    public override void Cancel()
    {
        hit.Node.Position = _startPosition;
        hit.Node.Size = _startSize;
        Canvas.InvalidateNodeGeometry(hit.Node.Id);
        Canvas.UpdateNodeControlHostLayout(hit.Node.Id);
    }

    private static GraphPoint ResolveRotatedResizePosition(
        GraphPoint startPosition,
        GraphSize startSize,
        GraphPoint localPosition,
        GraphSize newSize,
        double rotation)
    {
        var localCenterOffset = new GraphPoint(
            localPosition.X + newSize.Width / 2 - startSize.Width / 2,
            localPosition.Y + newSize.Height / 2 - startSize.Height / 2);
        var canvasCenterOffset = GraphNodeGeometry.RotateVector(localCenterOffset, rotation);
        var startCenter = new GraphPoint(
            startPosition.X + startSize.Width / 2,
            startPosition.Y + startSize.Height / 2);
        var newCenter = new GraphPoint(
            startCenter.X + canvasCenterOffset.X,
            startCenter.Y + canvasCenterOffset.Y);
        return new GraphPoint(
            newCenter.X - newSize.Width / 2,
            newCenter.Y - newSize.Height / 2);
    }
}
