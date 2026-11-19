using Avalonia.Input;

namespace Kudo.Avalonia;

internal sealed class CanvasSelectionInputProcessor : IGraphDefaultInputProcessor
{
    public static CanvasSelectionInputProcessor Instance { get; } = new();

    public bool TryProcess(in GraphPointerPressedContext context)
    {
        if (!context.Canvas.IsReadOnly &&
            context.Canvas.IsEdgeCuttingEnabled &&
            context.Canvas.IsEdgeCuttingGesture(context))
        {
            context.Canvas.StartEdgeCutting(context.Point, context.Event.Pointer);
            return true;
        }

        if (!context.Canvas.IsReadOnly &&
            context.Canvas.IsNodePushingEnabled &&
            context.Canvas.IsNodePushingGesture(context))
        {
            context.Canvas.StartNodePushing(context.Point, context.Event.Pointer);
            return true;
        }

        var mode = context.Canvas.IsSubtractSelectionGesture(context)
            ? RectangleSelectionMode.Subtract
            : context.Canvas.IsExtendSelectionGesture(context)
                ? RectangleSelectionMode.Add
                : context.Canvas.IsPrimaryGesture(context)
                    ? RectangleSelectionMode.Replace
                    : (RectangleSelectionMode?)null;
        if (mode is null)
        {
            return false;
        }

        context.Canvas.StartRectangleSelection(context.Point, context.Event.Pointer, mode.Value);
        return true;
    }
}
