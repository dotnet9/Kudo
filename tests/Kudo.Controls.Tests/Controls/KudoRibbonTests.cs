using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Kudo.Controls;
using Shouldly;
using Xunit;

namespace Kudo.Controls.Tests;

[Collection("KudoControls-sequential")]
public sealed class KudoRibbonTests
{
    [AvaloniaFact]
    public void Defaults_match_collapsed_ribbon()
    {
        var ribbon = new KudoRibbon();

        // 折叠是产品化默认态：命令区隐藏，选中页签或双击可展开。
        ribbon.IsExpanded.ShouldBeFalse();
        ribbon.ContentHeight.ShouldBe(74);
        ribbon.QuickAccessContent.ShouldBeNull();
        ribbon.ShouldBeAssignableTo<TabStrip>();
    }

    [AvaloniaFact]
    public void Selecting_a_tab_expands_the_content_area()
    {
        var ribbon = new KudoRibbon { IsExpanded = false };
        var window = new Window { Content = ribbon };
        window.Show();

        var tab = new TabStripItem { Tag = new TextBlock { Text = "文件命令区" } };
        ribbon.Items.Add(tab);
        ribbon.SelectedItem = tab;

        ribbon.IsExpanded.ShouldBeTrue();
        ribbon.SelectedItem.ShouldBeSameAs(tab);

        window.Close();
    }

    [AvaloniaFact]
    public void Collapse_and_quick_access_roundtrip()
    {
        var quickAccess = new TextBlock { Text = "quick" };
        var ribbon = new KudoRibbon
        {
            IsExpanded = false,
            QuickAccessContent = quickAccess,
            ContentHeight = 60
        };

        ribbon.IsExpanded.ShouldBeFalse();
        ribbon.QuickAccessContent.ShouldBeSameAs(quickAccess);
        ribbon.ContentHeight.ShouldBe(60);

        ribbon.IsExpanded = true;
        ribbon.IsExpanded.ShouldBeTrue();
    }
}
