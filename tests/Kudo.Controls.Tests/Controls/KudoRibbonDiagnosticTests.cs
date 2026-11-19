using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Avalonia.Styling;
using Kudo.Controls;
using Kudo.Controls.Tests;
using Shouldly;
using Xunit;

namespace Kudo.Controls.Tests;

/// <summary>回归：KudoRibbon 页签容器数量与排布方向（横排）。</summary>
public sealed class KudoRibbonDiagnosticTests
{
    [Fact]
    public void All_tab_containers_realize_horizontally()
    {
        HeadlessTestRuntime.Run(() =>
        {
            var ribbon = new KudoRibbon();
            var styles = (Styles)AvaloniaXamlLoader.Load(
                new Uri("avares://Kudo.Controls/Styles/KudoRibbon.axaml"));
            ribbon.Styles.AddRange(styles);
            var window = new Window { Content = ribbon };
            window.Show();

            ribbon.Measure(new Size(1200, 40));
            ribbon.Arrange(new Rect(0, 0, 1200, 40));

            var containers = ribbon.GetVisualDescendants().OfType<TabStripItem>().ToList();
            containers.Count.ShouldBe(6, $"容器数量不足（{containers.Count}/6）");

            var verticalStacked = containers.Count(c => c.Bounds.Y > 0);
            verticalStacked.ShouldBe(0, "页签被竖排堆叠（Y>0）");

            window.Close();
        });
    }
}
