namespace Kudo.Avalonia;

internal sealed class AnnotationInputProcessor : IGraphDefaultInputProcessor
{
    public static AnnotationInputProcessor Instance { get; } = new();

    public bool TryProcess(in GraphPointerPressedContext context)
    {
        if ((!context.Canvas.IsPrimaryGesture(context) &&
             !context.Canvas.IsDragGesture(context) &&
             !context.Canvas.IsEditGesture(context)) ||
            context.Hits.Annotation is not { } annotation)
        {
            return false;
        }

        var canvas = context.Canvas;
        if (!canvas.SetSelectedAnnotation(annotation.Id))
        {
            return true;
        }

        if (!canvas.IsReadOnly && context.Event.ClickCount >= 2)
        {
            canvas.BeginAnnotationTextEdit(annotation.Id);
        }
        else if (!canvas.IsReadOnly && canvas.IsDragGesture(context))
        {
            canvas.StartAnnotationDrag(context.Point, annotation, context.Event.Pointer);
        }

        return true;
    }
}
