using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Kudo.Controls;
using Semi.Avalonia;
using Shouldly;
using Xunit;

namespace Kudo.Controls.Tests;

/// <summary>二分诊断：Semi TabStrip ControlTheme 参与下，KudoRibbon 模板仍接管。</summary>
[Collection("KudoControls-sequential")]
public sealed class KudoRibbonThemeProbeTests
{
    private static KudoRibbon BuildRibbon(bool withSemiTheme)
    {
        var ribbon = new KudoRibbon();
        if (withSemiTheme)
        {
            ribbon.Styles.AddRange(new Semi.Avalonia.SemiTheme());
        }

        for (var i = 0; i < 5; i++)
        {
            ribbon.Items.Add(new TabStripItem { Content = $"T{i}" });
        }

        return ribbon;
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ribbon_template_wins_over_semi(bool withSemiTheme)
    {
        var ribbon = BuildRibbon(withSemiTheme);
        var window = new Window { Content = ribbon };
        window.Show();
        ribbon.ApplyTemplate();

        var contentHost = ribbon.GetVisualDescendants()
            .OfType<ContentControl>()
            .FirstOrDefault(c => c.Name == "PART_ContentHost");
        contentHost.ShouldNotBeNull("Semi 主题覆盖了 KudoRibbon 模板");

        window.Close();
    }
}
