using Avalonia;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private IReadOnlyList<(GraphNodeResizeHandle Handle, Point ScreenPosition)> GetNodeResizeHandlePositions(
        NodeModel node)
    {
        var rect = ToScreenRect(node);
        var centerX = rect.Left + rect.Width / 2;
        var centerY = rect.Top + rect.Height / 2;
        (GraphNodeResizeHandle Handle, Point ScreenPosition)[] handles =
        [
            (GraphNodeResizeHandle.TopLeft,
                new Point(rect.Left - NodeResizeHandleOffset, rect.Top - NodeResizeHandleOffset)),
            (GraphNodeResizeHandle.Top,
                new Point(centerX, rect.Top - NodeResizeHandleOffset)),
            (GraphNodeResizeHandle.TopRight,
                new Point(rect.Right + NodeResizeHandleOffset, rect.Top - NodeResizeHandleOffset)),
            (GraphNodeResizeHandle.Right,
                new Point(rect.Right + NodeResizeHandleOffset, centerY)),
            (GraphNodeResizeHandle.BottomRight,
                new Point(rect.Right + NodeResizeHandleOffset, rect.Bottom + NodeResizeHandleOffset)),
            (GraphNodeResizeHandle.Bottom,
                new Point(centerX, rect.Bottom + NodeResizeHandleOffset)),
            (GraphNodeResizeHandle.BottomLeft,
                new Point(rect.Left - NodeResizeHandleOffset, rect.Bottom + NodeResizeHandleOffset)),
            (GraphNodeResizeHandle.Left,
                new Point(rect.Left - NodeResizeHandleOffset, centerY))
        ];
        if (GraphNodeGeometry.NormalizeRotation(node.Rotation) == 0)
        {
            return handles;
        }

        return handles
            .Select(handle => (
                handle.Handle,
                TransformNodeScreenPoint(node, handle.ScreenPosition)))
            .ToArray();
    }

    private GraphSize ClampNodeSize(NodeModel node, GraphSize size)
    {
        var minimumSize = ResolveMinimumNodeSize(node);
        return new GraphSize(
            Math.Max(minimumSize.Width, size.Width),
            Math.Max(minimumSize.Height, size.Height));
    }

    internal GraphSize ResolveMinimumNodeSize(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return MinimumNodeSizeResolver?.Invoke(node) is { } minimumSize
            ? GraphNodeResizeCalculator.NormalizeMinimumSize(minimumSize)
            : MinimumNodeSize;
    }
}
