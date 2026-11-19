using Avalonia.Input;

namespace Kudo.Avalonia;

internal sealed class EdgeInputProcessor : IGraphDefaultInputProcessor
{
    public static EdgeInputProcessor Instance { get; } = new();

    public bool TryProcess(in GraphPointerPressedContext context)
    {
        if ((context.Hits.EdgeLabel ?? context.Hits.Edge) is not { } edge)
        {
            return false;
        }

        var canvas = context.Canvas;
        if (canvas.Document is { } document &&
            !canvas.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.SelectEdges,
                document,
                Edge: edge)))
        {
            return true;
        }

        if (canvas.IsExtendSelectionGesture(context))
        {
            canvas.ToggleEdgeSelection(edge.Id);
        }
        else if (canvas.IsPrimaryGesture(context) || canvas.IsEditGesture(context))
        {
            canvas.SetSelectedEdges([edge.Id], edge.Id);
        }
        else
        {
            return false;
        }

        if (!canvas.IsEdgeSelected(edge.Id))
        {
            return true;
        }
        if (!canvas.IsReadOnly &&
            context.Event.ClickCount >= 2 &&
            canvas.IsEditGesture(context) &&
            context.Hits.EdgeLabel is not null &&
            canvas.TryBeginEdgeLabelEdit(edge, context.Point))
        {
            return true;
        }

        if (!canvas.IsReadOnly &&
            context.Event.ClickCount >= 2 &&
            canvas.IsEditGesture(context) &&
            edge.RoutingStyle == EdgeRoutingStyle.Manual)
        {
            canvas.AddSelectedEdgeWaypoint(context.Point);
        }

        if (!canvas.IsReadOnly &&
            context.Event.ClickCount == 1 &&
            context.Hits.EdgeLabel is null &&
            ReferenceEquals(context.Hits.Edge, edge) &&
            canvas.IsEditGesture(context) &&
            canvas.HitTestOrthogonalEdgeSegment(edge, context.Point) is { } segment)
        {
            canvas.StartEdgeSegmentMove(context.Point, segment, context.Event.Pointer);
        }

        return true;
    }
}
