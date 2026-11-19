using Avalonia;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private Rect ToScreenRect(NodeModel node)
    {
        var origin = GraphToScreen(node.Position);
        var zoom = Viewport.Zoom;
        return new Rect(origin.X, origin.Y, node.Size.Width * zoom, node.Size.Height * zoom);
    }

    private Rect GetNodeVisualScreenRect(NodeModel node)
    {
        var bounds = NodeGeometryProvider.GetBounds(node);
        var topLeft = GraphToScreen(new GraphPoint(bounds.X, bounds.Y));
        var bottomRight = GraphToScreen(new GraphPoint(
            bounds.X + bounds.Width,
            bounds.Y + bounds.Height));
        return new Rect(topLeft, bottomRight);
    }

    private IReadOnlyList<Point> GetNodeScreenCorners(NodeModel node)
    {
        var rect = ToScreenRect(node);
        Point[] corners =
        [
            new(rect.Left, rect.Top),
            new(rect.Right, rect.Top),
            new(rect.Right, rect.Bottom),
            new(rect.Left, rect.Bottom)
        ];
        if (GraphNodeGeometry.NormalizeRotation(node.Rotation) == 0)
        {
            return corners;
        }

        return corners
            .Select(point => TransformNodeScreenPoint(node, point))
            .ToArray();
    }

    private static Point RotateScreenPoint(Point point, Point center, double rotation)
    {
        var transformed = GraphNodeGeometry.RotatePoint(
            new GraphPoint(point.X, point.Y),
            new GraphPoint(center.X, center.Y),
            rotation);
        return new Point(transformed.X, transformed.Y);
    }

    private Point TransformNodeScreenPoint(NodeModel node, Point point)
    {
        var center = GraphToScreen(GraphNodeGeometry.GetCenter(node));
        var transformed = GraphNodeGeometry.RotatePoint(
            new GraphPoint(point.X, point.Y),
            new GraphPoint(center.X, center.Y),
            node.Rotation);
        return new Point(transformed.X, transformed.Y);
    }

    private Point InverseTransformNodeScreenPoint(NodeModel node, Point point)
    {
        var center = GraphToScreen(GraphNodeGeometry.GetCenter(node));
        var transformed = GraphNodeGeometry.RotatePoint(
            new GraphPoint(point.X, point.Y),
            new GraphPoint(center.X, center.Y),
            -node.Rotation);
        return new Point(transformed.X, transformed.Y);
    }
}
