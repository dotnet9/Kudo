using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;

using Avalonia.Styling;
using Avalonia.VisualTree;
using Kudo.Controls;
using Semi.Avalonia;
using Kudo.Controls.Tests;
using Shouldly;
using Xunit;

namespace Kudo.Controls.Tests;

/// <summary>二分诊断：Semi TabStrip ControlTheme 参与下，KudoRibbon 页签仍横排。</summary>
public sealed class KudoRibbonThemeProbeTests
{
    private static KudoRibbon BuildRibbon(bool withSemiTheme)
    {
        var ribbon = new KudoRibbon();
        var myStyles = (Styles)AvaloniaXamlLoader.Load(
            new Uri("avares://Kudo.Controls/Styles/KudoRibbon.axaml"));
        ribbon.Styles.AddRange(myStyles);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tabs_realize_horizontally(bool withSemiTheme)
    {
        HeadlessTestRuntime.Run(() =>
        {
            var ribbon = BuildRibbon(withSemiTheme);
            var window = new Window { Content = ribbon };
            window.Show();

            ribbon.Measure(new Size(1200, 40));
            ribbon.Arrange(new Rect(0, 0, 1200, 40));

            var containers = ribbon.GetVisualDescendants().OfType<TabStripItem>().ToList();
            containers.Count.ShouldBe(5, $"容器数量 {containers.Count}/5");
            foreach (var container in containers)
            {
                container.Bounds.Y.ShouldBe(0, $"页签 '{container.Content}' 竖排堆叠（Y={container.Bounds.Y}）");
            }

            window.Close();
        });
    }
}
