using Avalonia;
using Avalonia.Input;

namespace Kudo.Avalonia;

internal sealed class AnnotationDragInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint,
    AnnotationModel annotation)
    : GraphInteractionSession(canvas, pointer, startScreenPoint)
{
    private readonly GraphPoint _startPosition = annotation.Position;

    public override void Update(Point point, KeyModifiers modifiers)
    {
        var delta = GetGraphDragDelta(point);
        annotation.Position = new GraphPoint(
            _startPosition.X + delta.X,
            _startPosition.Y + delta.Y);
        Canvas.InvalidateDocumentGraphBounds();
        Canvas.InvalidateVisual();
    }

    public override void Complete(Point point)
    {
        if (annotation.Position == _startPosition)
        {
            return;
        }

        Canvas.CommitGraphEdit(new MoveAnnotationCommand(
            annotation.Id,
            _startPosition,
            annotation.Position,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.MoveAnnotationCommand)));
    }

    public override void CaptureLost() => Cancel();

    public override void Cancel()
    {
        annotation.Position = _startPosition;
        Canvas.InvalidateDocumentGraphBounds();
    }
}
internal sealed class AnnotationResizeInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint,
    AnnotationResizeHandleHitResult hit)
    : GraphInteractionSession(canvas, pointer, startScreenPoint)
{
    private readonly GraphPoint _startPosition = hit.Annotation.Position;
    private readonly GraphSize _startSize = hit.Annotation.Size;

    public override void Update(Point point, KeyModifiers modifiers)
    {
        var delta = GetGraphDragDelta(point);
        var result = GraphNodeResizeCalculator.Calculate(
            _startPosition,
            _startSize,
            hit.Handle,
            delta,
            Canvas.MinimumAnnotationSize,
            resizeFromCenter: modifiers.HasFlag(KeyModifiers.Alt),
            preserveAspectRatio: modifiers.HasFlag(KeyModifiers.Shift));
        hit.Annotation.Position = result.Position;
        hit.Annotation.Size = result.Size;
        Canvas.InvalidateDocumentGraphBounds();
        Canvas.UpdateAnnotationTextEditorLayout();
        Canvas.InvalidateVisual();
    }

    public override void Complete(Point point)
    {
        if (hit.Annotation.Position == _startPosition && hit.Annotation.Size == _startSize)
        {
            return;
        }

        Canvas.CommitGraphEdit(new ResizeAnnotationCommand(
            hit.Annotation.Id,
            _startPosition,
            _startSize,
            hit.Annotation.Position,
            hit.Annotation.Size,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.ResizeAnnotationCommand)));
    }

    public override void CaptureLost() => Cancel();

    public override void Cancel()
    {
        hit.Annotation.Position = _startPosition;
        hit.Annotation.Size = _startSize;
        Canvas.InvalidateDocumentGraphBounds();
        Canvas.UpdateAnnotationTextEditorLayout();
    }
}
