using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

internal static class GraphGeometryMath
{
    public static double DistanceToSegmentSquared(Point point, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        if (dx == 0 && dy == 0)
        {
            return DistanceSquared(point, start);
        }

        var progress = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (dx * dx + dy * dy);
        progress = Math.Clamp(progress, 0, 1);
        return DistanceSquared(point, new Point(start.X + progress * dx, start.Y + progress * dy));
    }

    public static double DistanceSquared(Point first, Point second)
    {
        var delta = first - second;
        return delta.X * delta.X + delta.Y * delta.Y;
    }

    public static GraphRect ToGraphRect(Rect rect) =>
        new(rect.X, rect.Y, rect.Width, rect.Height);

    public static Rect NormalizeRect(Point first, Point second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        return new Rect(left, top, Math.Abs(second.X - first.X), Math.Abs(second.Y - first.Y));
    }

    public static Rect NormalizeRect(GraphPoint first, GraphPoint second) =>
        NormalizeRect(new Point(first.X, first.Y), new Point(second.X, second.Y));
}
