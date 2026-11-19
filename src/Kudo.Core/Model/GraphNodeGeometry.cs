namespace Kudo;

/// <summary>
/// 提供节点局部矩形与画布旋转几何之间的统一转换。Position 和 Size 始终描述未应用整体旋转的局部矩形。
/// </summary>
public static class GraphNodeGeometry
{
    public static double NormalizeRotation(double rotation)
    {
        if (!double.IsFinite(rotation))
        {
            return 0;
        }

        var normalized = Math.IEEERemainder(rotation, 360);
        return Math.Abs(normalized) < 0.0000001 ? 0 : normalized;
    }

    public static GraphPoint GetCenter(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new GraphPoint(
            node.Position.X + node.Size.Width / 2,
            node.Position.Y + node.Size.Height / 2);
    }

    public static GraphRect GetCanvasBounds(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return GetCanvasBounds(node.Position, node.Size, node.Rotation);
    }

    public static GraphRect GetCanvasBounds(
        GraphPoint position,
        GraphSize size,
        double rotation)
    {
        var center = new GraphPoint(
            position.X + size.Width / 2,
            position.Y + size.Height / 2);
        var radians = NormalizeRotation(rotation) * Math.PI / 180;
        var cosine = Math.Abs(Math.Cos(radians));
        var sine = Math.Abs(Math.Sin(radians));
        var width = size.Width * cosine + size.Height * sine;
        var height = size.Width * sine + size.Height * cosine;
        return new GraphRect(
            center.X - width / 2,
            center.Y - height / 2,
            width,
            height);
    }

    public static GraphPoint TransformPoint(NodeModel node, GraphPoint point)
    {
        ArgumentNullException.ThrowIfNull(node);
        return RotatePoint(point, GetCenter(node), node.Rotation);
    }

    public static GraphPoint InverseTransformPoint(NodeModel node, GraphPoint point)
    {
        ArgumentNullException.ThrowIfNull(node);
        return RotatePoint(point, GetCenter(node), -node.Rotation);
    }

    public static GraphPoint RotatePoint(GraphPoint point, GraphPoint center, double rotation)
    {
        var vector = RotateVector(
            new GraphPoint(point.X - center.X, point.Y - center.Y),
            rotation);
        return new GraphPoint(center.X + vector.X, center.Y + vector.Y);
    }

    public static GraphPoint RotateVector(GraphPoint vector, double rotation)
    {
        var radians = NormalizeRotation(rotation) * Math.PI / 180;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return new GraphPoint(
            vector.X * cosine - vector.Y * sine,
            vector.X * sine + vector.Y * cosine);
    }

    public static bool Contains(NodeModel node, GraphPoint point)
    {
        var local = InverseTransformPoint(node, point);
        return local.X >= node.Position.X &&
               local.X <= node.Position.X + node.Size.Width &&
               local.Y >= node.Position.Y &&
               local.Y <= node.Position.Y + node.Size.Height;
    }
}
