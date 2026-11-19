using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Kudo.Controls;
using Shouldly;
using Kudo.Controls.Tests.TestInfrastructure;
using Xunit;

namespace Kudo.Controls.Tests;

/// <summary>回归：KudoRibbon 默认主题生效（页签横排），Semi TabStrip 主题在场也不回落。</summary>
public sealed class KudoRibbonThemeProbeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tabs_realize_horizontally(bool withSemiTheme)
    {
        HeadlessTestRuntime.Run(() =>
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

            var window = new Window { Content = ribbon };
            window.Show();

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
