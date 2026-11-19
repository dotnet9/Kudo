using Avalonia;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private (Point Anchor, Point Handle) GetNodeRotationHandlePositions(NodeModel node)
    {
        var rect = ToScreenRect(node);
        var anchor = new Point(rect.Left + rect.Width / 2, rect.Top);
        var handle = new Point(anchor.X, rect.Top - NodeRotationHandleOffset);
        if (GraphNodeGeometry.NormalizeRotation(node.Rotation) == 0)
        {
            return (anchor, handle);
        }

        return (
            TransformNodeScreenPoint(node, anchor),
            TransformNodeScreenPoint(node, handle));
    }

    internal Point GetNodeRotationHandlePosition(NodeModel node) =>
        GetNodeRotationHandlePositions(node).Handle;

    private IReadOnlyList<NodeModel> GetSelectedNodesForRotation()
    {
        var document = Document;
        if (document is null || _selectedNodeIds.Count == 0)
        {
            return [];
        }

        var nodes = document.Nodes
            .Where(node => _selectedNodeIds.Contains(node.Id))
            .ToArray();
        return nodes.Length == _selectedNodeIds.Count && nodes.All(node => IsNodeVisible(node.Id))
            ? nodes
            : [];
    }

    internal GraphRect? GetSelectedNodeRotationBounds(IReadOnlyList<NodeModel> nodes)
    {
        if (nodes.Count == 0)
        {
            return null;
        }

        var bounds = GraphNodeGeometry.GetCanvasBounds(nodes[0]);
        for (var index = 1; index < nodes.Count; index++)
        {
            var candidate = GraphNodeGeometry.GetCanvasBounds(nodes[index]);
            var left = Math.Min(bounds.X, candidate.X);
            var top = Math.Min(bounds.Y, candidate.Y);
            var right = Math.Max(bounds.X + bounds.Width, candidate.X + candidate.Width);
            var bottom = Math.Max(bounds.Y + bounds.Height, candidate.Y + candidate.Height);
            bounds = new GraphRect(left, top, right - left, bottom - top);
        }

        return bounds;
    }

    private IReadOnlyList<Point> GetSelectionRotationFrameScreenCorners(
        GraphRect bounds,
        double rotation = 0)
    {
        var rect = new Rect(
            GraphToScreen(new GraphPoint(bounds.X, bounds.Y)),
            new Size(bounds.Width * Viewport.Zoom, bounds.Height * Viewport.Zoom));
        Point[] corners =
        [
            new(rect.Left, rect.Top),
            new(rect.Right, rect.Top),
            new(rect.Right, rect.Bottom),
            new(rect.Left, rect.Bottom)
        ];
        if (GraphNodeGeometry.NormalizeRotation(rotation) == 0)
        {
            return corners;
        }

        var center = rect.Center;
        return corners.Select(point => RotateScreenPoint(point, center, rotation)).ToArray();
    }

    internal (Point Anchor, Point Handle) GetSelectionRotationHandlePositions(
        GraphRect bounds,
        double rotation = 0)
    {
        var rect = new Rect(
            GraphToScreen(new GraphPoint(bounds.X, bounds.Y)),
            new Size(bounds.Width * Viewport.Zoom, bounds.Height * Viewport.Zoom));
        var anchor = new Point(rect.Center.X, rect.Top);
        var handle = new Point(anchor.X, rect.Top - NodeRotationHandleOffset);
        if (GraphNodeGeometry.NormalizeRotation(rotation) == 0)
        {
            return (anchor, handle);
        }

        return (
            RotateScreenPoint(anchor, rect.Center, rotation),
            RotateScreenPoint(handle, rect.Center, rotation));
    }
}
