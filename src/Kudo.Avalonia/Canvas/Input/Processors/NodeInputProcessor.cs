using Avalonia.Input;

namespace Kudo.Avalonia;

internal sealed class NodeInputProcessor : IGraphDefaultInputProcessor
{
    public static NodeInputProcessor Instance { get; } = new();

    public bool TryProcess(in GraphPointerPressedContext context)
    {
        if (context.Hits.Node is not { } node)
        {
            return false;
        }

        var canvas = context.Canvas;
        if (canvas.IsExtendSelectionGesture(context))
        {
            canvas.ToggleNodeSelection(node.Id);
        }
        else if (!canvas.IsPrimaryGesture(context) &&
                 !canvas.IsDragGesture(context) &&
                 !canvas.IsEditGesture(context))
        {
            return false;
        }
        else if (!canvas.IsNodeSelected(node.Id))
        {
            canvas.SetSelectedNodes([node.Id], node.Id);
        }
        else
        {
            canvas.SetSelectedNodes(canvas.GetDocumentOrderedNodeIds(canvas.SelectedNodeIds), node.Id);
        }

        if (!canvas.IsNodeSelected(node.Id))
        {
            return true;
        }

        if (!canvas.IsReadOnly &&
            context.Event.ClickCount >= 2 &&
            canvas.IsEditGesture(context) &&
            canvas.TryBeginNodeTitleEdit(node, context.Point))
        {
            return true;
        }

        if (!canvas.IsReadOnly && canvas.IsDragGesture(context))
        {
            canvas.StartNodeDrag(context.Point, node.Id, context.Event.Pointer);
        }

        return true;
    }
}
