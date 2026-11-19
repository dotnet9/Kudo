using System.ComponentModel;

namespace Kudo;

public enum GraphLabelVisibility
{
    [Description("自动")]
    Auto,

    [Description("始终显示")]
    Always,

    [Description("选中或悬浮时显示")]
    SelectedOrHovered,

    [Description("悬浮时显示")]
    Hovered,

    [Description("从不显示")]
    Never
}
public sealed record GraphCanvasSettings
{
    private const double MinimumLodThreshold = 0.01;
    private const double MaximumLodThreshold = 3;

    public static GraphCanvasSettings Default { get; } = new();

    public GraphGridSettings Grid { get; init; } = GraphGridSettings.Default;

    public bool IsMinimapVisible { get; init; } = true;

    public bool IsLevelOfDetailEnabled { get; init; } = true;

    public bool ShowPorts { get; init; } = true;

    public double LodPortsZoomThreshold { get; init; } = 0.4;

    public double LodLabelsZoomThreshold { get; init; } = 0.3;

    public double LodSimplifiedZoomThreshold { get; init; } = 0.4;

    public GraphLabelVisibility PortLabelVisibility { get; init; } = GraphLabelVisibility.Auto;

    public GraphLabelVisibility SymbolNameVisibility { get; init; } = GraphLabelVisibility.Never;

    public GraphLabelVisibility SymbolDescriptionVisibility { get; init; } = GraphLabelVisibility.Never;

    public GraphCanvasSettings Normalize() => this with
    {
        Grid = (Grid ?? GraphGridSettings.Default).Normalize(),
        LodPortsZoomThreshold = NormalizeThreshold(
            LodPortsZoomThreshold,
            Default.LodPortsZoomThreshold),
        LodLabelsZoomThreshold = NormalizeThreshold(
            LodLabelsZoomThreshold,
            Default.LodLabelsZoomThreshold),
        LodSimplifiedZoomThreshold = NormalizeThreshold(
            LodSimplifiedZoomThreshold,
            Default.LodSimplifiedZoomThreshold),
        PortLabelVisibility = NormalizeVisibility(
            PortLabelVisibility,
            Default.PortLabelVisibility),
        SymbolNameVisibility = NormalizeVisibility(
            SymbolNameVisibility,
            Default.SymbolNameVisibility),
        SymbolDescriptionVisibility = NormalizeVisibility(
            SymbolDescriptionVisibility,
            Default.SymbolDescriptionVisibility)
    };

    private static double NormalizeThreshold(double value, double fallback) =>
        double.IsFinite(value) && value is >= MinimumLodThreshold and <= MaximumLodThreshold
            ? value
            : fallback;

    private static GraphLabelVisibility NormalizeVisibility(
        GraphLabelVisibility value,
        GraphLabelVisibility fallback) =>
        Enum.IsDefined(value) ? value : fallback;
}
