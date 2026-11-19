using Kudo;
using Shouldly;

namespace Kudo.Core.Tests;

public class GraphCanvasSettingsTests
{
    [Fact]
    public void Normalize_restores_invalid_values_without_changing_valid_preferences()
    {
        var settings = new GraphCanvasSettings
        {
            Grid = new GraphGridSettings { Spacing = 32, IsSnapEnabled = true },
            IsMinimapVisible = false,
            LodPortsZoomThreshold = double.NaN,
            LodLabelsZoomThreshold = 8,
            LodSimplifiedZoomThreshold = 0.5,
            PortLabelVisibility = (GraphLabelVisibility)999
        };

        var normalized = settings.Normalize();

        normalized.Grid.Spacing.ShouldBe(32);
        normalized.Grid.IsSnapEnabled.ShouldBeTrue();
        normalized.IsMinimapVisible.ShouldBeFalse();
        normalized.LodPortsZoomThreshold.ShouldBe(GraphCanvasSettings.Default.LodPortsZoomThreshold);
        normalized.LodLabelsZoomThreshold.ShouldBe(GraphCanvasSettings.Default.LodLabelsZoomThreshold);
        normalized.LodSimplifiedZoomThreshold.ShouldBe(0.5);
        normalized.PortLabelVisibility.ShouldBe(GraphLabelVisibility.Auto);
    }
}
