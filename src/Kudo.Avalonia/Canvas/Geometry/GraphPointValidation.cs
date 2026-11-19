namespace Kudo.Avalonia;

internal static class GraphPointValidation
{
    public static bool IsFinite(GraphPoint point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y);
}
