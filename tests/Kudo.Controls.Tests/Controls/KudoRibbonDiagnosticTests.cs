using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Kudo.Controls;
using Shouldly;
using Kudo.Controls.Tests.TestInfrastructure;
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
            var window = new Window { Content = ribbon };
            window.Show();

            for (var i = 0; i < 6; i++)
            {
                ribbon.Items.Add(new TabStripItem
                {
                    Classes = { "RibbonMenuTab" },
                    Content = $"页签{i}"
                });
            }

            var containers = ribbon.GetVisualDescendants().OfType<TabStripItem>().ToList();
            containers.Count.ShouldBe(6, $"容器数量不足（{containers.Count}/6）");

            var verticalStacked = containers.Count(c => c.Bounds.Y > 0);
            verticalStacked.ShouldBe(0, "页签被竖排堆叠（Y>0）");

            window.Close();
        });
    }
}
