namespace Kudo.Avalonia;

internal static class GraphCanvasLodSettings
{
    private const double MinimumZoom = 0.01;
    private const double MaximumZoom = 3.0;

    public static double NormalizeThreshold(double value, double fallback) =>
        double.IsFinite(value) && value >= MinimumZoom && value <= MaximumZoom
            ? value
            : fallback;
}
