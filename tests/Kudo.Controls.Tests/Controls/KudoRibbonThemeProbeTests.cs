using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kudo.Controls;
using Shouldly;
using Xunit;

namespace Kudo.Controls.Tests;

/// <summary>二分诊断：KudoRibbon 模板在无 Semi 环境下接管（PART_ContentHost 存在）。</summary>
[Collection("KudoControls-sequential")]
public sealed class KudoRibbonThemeProbeTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ribbon_template_wins_over_semi(bool withLegacyStyles)
    {
        var ribbon = new KudoRibbon();
        ribbon.Styles.AddRange(new KudoRibbonStyles());
        var window = new Window { Content = ribbon };
        window.Show();

        var contentHost = ribbon.GetVisualDescendants()
            .OfType<ContentControl>()
            .FirstOrDefault(c => c.Name == "PART_ContentHost");
        contentHost.ShouldNotBeNull("模板未生效（PART_ContentHost 缺失）");

        window.Close();
    }
}
