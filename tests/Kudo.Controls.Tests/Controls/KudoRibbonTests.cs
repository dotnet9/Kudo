using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Kudo.Controls;
using Kudo.Controls.Tests;
using Shouldly;
using Xunit;

namespace Kudo.Controls.Tests;

[Collection("KudoControls-headless")]
public sealed class KudoRibbonTests
{
    [Fact]
    public void Defaults_match_expanded_ribbon()
    {
        HeadlessTestRuntime.Run(() =>
        {
            var ribbon = new KudoRibbon();

            ribbon.IsExpanded.ShouldBeTrue();
            ribbon.ContentHeight.ShouldBe(74);
            ribbon.QuickAccessContent.ShouldBeNull();
            ribbon.ShouldBeAssignableTo<TabStrip>();
        });
    }

    [Fact]
    public void Selecting_a_tab_expands_the_content_area()
    {
        HeadlessTestRuntime.Run(() =>
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
        });
    }

    [Fact]
    public void Collapse_and_quick_access_roundtrip()
    {
        HeadlessTestRuntime.Run(() =>
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
        });
    }
}
