namespace Kudo.Avalonia;

internal sealed class GroupInputProcessor : IGraphDefaultInputProcessor
{
    public static GroupInputProcessor Instance { get; } = new();

    public bool TryProcess(in GraphPointerPressedContext context)
    {
        var canvas = context.Canvas;
        if (!canvas.IsPrimaryGesture(context) &&
            !canvas.IsDragGesture(context) &&
            !canvas.IsEditGesture(context) ||
            context.Hits.Group is not { } group ||
            canvas.Document is not { } document)
        {
            return false;
        }

        if (!canvas.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.SelectGroup,
                document,
                Group: group)))
        {
            return true;
        }

        var nodeIds = (group.NodeIds ?? [])
            .Where(nodeId => document.FindNode(nodeId) is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (nodeIds.Length == 0)
        {
            return false;
        }

        canvas.SetSelectedNodes(nodeIds, nodeIds[0]);
        if (!canvas.IsReadOnly &&
            context.Event.ClickCount >= 2 &&
            canvas.IsEditGesture(context))
        {
            canvas.SetGroupCollapsed(group.Id, !group.IsCollapsed);
        }
        else if (!canvas.IsReadOnly && canvas.IsDragGesture(context))
        {
            canvas.StartNodeDrag(context.Point, nodeIds[0], context.Event.Pointer);
        }

        return true;
    }
}
