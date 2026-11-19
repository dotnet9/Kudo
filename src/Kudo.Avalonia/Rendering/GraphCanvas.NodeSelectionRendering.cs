using Avalonia;
using Avalonia.Media;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private void RenderSelectedNodeResizeHandles(DrawingContext context)
    {
        var nodes = GetSelectedNodesForRotation();
        if (nodes.Count == 0)
        {
            return;
        }

        if (nodes.Count > 1)
        {
            if (nodes.Any(node => node.IsLocked))
            {
                foreach (var node in nodes)
                {
                    RenderSelectedNodeOutline(context, node);
                }

                return;
            }

            var rotationSession = _interactionSession as NodeRotationInteractionSession;
            var bounds = rotationSession?.SelectionBounds ?? GetSelectedNodeRotationBounds(nodes)!.Value;
            var frameRotation = rotationSession?.FrameRotation ?? 0;
            RenderSelectedNodeRotationFrame(context, bounds, frameRotation);
            RenderSelectedNodeRotationHandle(context, bounds, frameRotation);
            if (rotationSession is not null)
            {
                RenderNodeRotationReadout(context, rotationSession);
            }

            return;
        }

        var selectedNode = nodes[0];
        RenderSelectedNodeOutline(context, selectedNode);
        if (selectedNode.IsLocked)
        {
            return;
        }

        RenderSelectedNodeRotationHandle(context, selectedNode);
        foreach (var (_, screenPosition) in GetNodeResizeHandlePositions(selectedNode))
        {
            var rect = new Rect(
                screenPosition.X - NodeResizeHandleRadius,
                screenPosition.Y - NodeResizeHandleRadius,
                NodeResizeHandleRadius * 2,
                NodeResizeHandleRadius * 2);
            context.DrawRectangle(SelectedNodeStrokeBrush, PortStrokePen, rect, 1, 1);
        }

        if (_interactionSession is NodeRotationInteractionSession rotation &&
            string.Equals(rotation.NodeId, selectedNode.Id, StringComparison.Ordinal))
        {
            RenderNodeRotationReadout(context, rotation);
        }
    }

    private void RenderSelectedNodeRotationFrame(
        DrawingContext context,
        GraphRect bounds,
        double rotation)
    {
        var corners = GetSelectionRotationFrameScreenCorners(bounds, rotation);
        for (var index = 0; index < corners.Count; index++)
        {
            context.DrawLine(
                SelectedNodeStrokePen,
                corners[index],
                corners[(index + 1) % corners.Count]);
        }
    }

    private void RenderSelectedNodeOutline(DrawingContext context, NodeModel node)
    {
        var corners = GetNodeScreenCorners(node);
        for (var index = 0; index < corners.Count; index++)
        {
            context.DrawLine(
                SelectedNodeStrokePen,
                corners[index],
                corners[(index + 1) % corners.Count]);
        }
    }

    private void RenderSelectedNodeRotationHandle(DrawingContext context, NodeModel node)
    {
        var (anchor, handle) = GetNodeRotationHandlePositions(node);
        context.DrawLine(SelectedNodeStrokePen, anchor, handle);
        context.DrawEllipse(
            CanvasBackgroundBrush,
            SelectedNodeStrokePen,
            handle,
            NodeRotationHandleRadius,
            NodeRotationHandleRadius);
    }

    private void RenderSelectedNodeRotationHandle(
        DrawingContext context,
        GraphRect bounds,
        double rotation)
    {
        var (anchor, handle) = GetSelectionRotationHandlePositions(bounds, rotation);
        context.DrawLine(SelectedNodeStrokePen, anchor, handle);
        context.DrawEllipse(
            CanvasBackgroundBrush,
            SelectedNodeStrokePen,
            handle,
            NodeRotationHandleRadius,
            NodeRotationHandleRadius);
    }

    private void RenderNodeRotationReadout(
        DrawingContext context,
        NodeRotationInteractionSession rotation)
    {
        var text = GraphCanvasVisualFactory.CreateText($"{rotation.CurrentRotation:0.#} deg", 12, CanvasBackgroundBrush);
        var handle = rotation.RotationHandlePosition;
        var padding = 5;
        var rect = new Rect(
            handle.X + 10,
            handle.Y - text.Height / 2 - padding,
            text.Width + padding * 2,
            text.Height + padding * 2);
        context.DrawRectangle(SelectedNodeStrokeBrush, null, rect, 4, 4);
        context.DrawText(text, new Point(rect.X + padding, rect.Y + padding));
    }
}
