using Kudo;
using Shouldly;

namespace Kudo.Core.Tests;

public class GraphGridSettingsTests
{
    [Fact]
    public void Normalize_restores_safe_defaults_for_out_of_range_values()
    {
        var settings = new GraphGridSettings
        {
            Spacing = double.Epsilon,
            MajorLineInterval = int.MaxValue,
            SnapTolerance = double.MaxValue,
            GuideTolerance = double.NaN
        };

        var normalized = settings.Normalize();

        normalized.Spacing.ShouldBe(GraphGridSettings.Default.Spacing);
        normalized.MajorLineInterval.ShouldBe(GraphGridSettings.Default.MajorLineInterval);
        normalized.SnapTolerance.ShouldBe(GraphGridSettings.Default.SnapTolerance);
        normalized.GuideTolerance.ShouldBe(GraphGridSettings.Default.GuideTolerance);
    }
}
