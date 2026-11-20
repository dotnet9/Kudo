using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kudo.Controls;
using Shouldly;
using Xunit;

namespace Kudo.Controls.Tests;

/// <summary>回归探针：Semi 移除后 KudoRibbon 模板独立生效（PART_ContentHost 存在）。</summary>
[Collection("KudoControls-sequential")]
public sealed class KudoRibbonThemeProbeTests
{
    [AvaloniaFact]
    public void Ribbon_template_wins_over_semi()
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
