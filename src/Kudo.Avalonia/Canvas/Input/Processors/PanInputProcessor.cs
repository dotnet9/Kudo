namespace Kudo.Avalonia;

internal sealed class PanInputProcessor : IGraphDefaultInputProcessor
{
    public static PanInputProcessor Instance { get; } = new();

    public bool TryProcess(in GraphPointerPressedContext context)
    {
        if (!context.Canvas.TryGetPanGesture(context, out var gesture))
        {
            return false;
        }

        var canvas = context.Canvas;
        var openContextMenuOnRelease = gesture.Button == GraphPointerButton.Right;
        if (openContextMenuOnRelease)
        {
            SelectContextTarget(canvas, context.Hits);
        }

        canvas.StartPan(
            context.Point,
            context.Event.Pointer,
            openContextMenuOnRelease);
        return true;
    }

    private static void SelectContextTarget(GraphCanvas canvas, GraphInputHitSnapshot hits)
    {
        if (hits.Node is { } node)
        {
            if (canvas.IsNodeSelected(node.Id))
            {
                canvas.SetSelectedNodes(
                    canvas.GetDocumentOrderedNodeIds(canvas.SelectedNodeIds),
                    node.Id);
            }
            else
            {
                canvas.SetSelectedNodes([node.Id], node.Id);
            }
        }
        else if ((hits.EdgeLabel ?? hits.Edge) is { } edge)
        {
            if (canvas.Document is { } document &&
                canvas.CanPerformEdit(new GraphEditRequest(
                    GraphEditOperation.SelectEdges,
                    document,
                    Edge: edge)))
            {
                if (canvas.IsEdgeSelected(edge.Id))
                {
                    canvas.SetSelectedEdges(canvas.SelectedEdgeIds, edge.Id);
                }
                else
                {
                    canvas.SetSelectedEdges([edge.Id], edge.Id);
                }
            }
        }
        else if (hits.Annotation is { } annotation)
        {
            canvas.SetSelectedAnnotation(annotation.Id);
        }
        else if (hits.Group is { } group && canvas.Document is { } document)
        {
            var nodeIds = (group.NodeIds ?? [])
                .Where(nodeId => document.FindNode(nodeId) is not null)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            canvas.SetSelectedNodes(nodeIds, nodeIds.FirstOrDefault());
        }
    }

}
