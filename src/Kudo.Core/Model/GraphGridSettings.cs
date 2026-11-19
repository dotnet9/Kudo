using System.ComponentModel;

namespace Kudo;

public sealed record GraphGridSettings
{
    private const double MinimumSpacing = 0.01;
    private const double MaximumSpacing = 1_000_000;
    private const double MaximumTolerance = 256;

    public static GraphGridSettings Default { get; } = new();

    public bool IsVisible { get; init; } = true;

    public bool IsSnapEnabled { get; init; }

    public bool IsGuidesEnabled { get; init; } = true;

    public double Spacing { get; init; } = 20;

    public int MajorLineInterval { get; init; } = 4;

    public double SnapTolerance { get; init; } = 8;

    public double GuideTolerance { get; init; } = 8;

    public double MajorSpacing => Spacing * MajorLineInterval;

    public GraphGridSettings Normalize()
    {
        var spacing = IsFiniteInRange(Spacing, MinimumSpacing, MaximumSpacing)
            ? Spacing
            : Default.Spacing;
        var majorLineInterval = MajorLineInterval is >= 2 and <= 16
            ? MajorLineInterval
            : Default.MajorLineInterval;
        var snapTolerance = IsFiniteInRange(SnapTolerance, 0, MaximumTolerance)
            ? SnapTolerance
            : Default.SnapTolerance;
        var guideTolerance = IsFiniteInRange(GuideTolerance, 0, MaximumTolerance)
            ? GuideTolerance
            : Default.GuideTolerance;

        return this with
        {
            Spacing = spacing,
            MajorLineInterval = majorLineInterval,
            SnapTolerance = snapTolerance,
            GuideTolerance = guideTolerance
        };
    }

    private static bool IsFiniteInRange(double value, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum;
}

public enum GraphGuideOrientation
{
    [Description("水平参考线")]
    Horizontal,

    [Description("垂直参考线")]
    Vertical
}

public sealed record GraphAlignmentGuide(GraphGuideOrientation Orientation, double Position);
