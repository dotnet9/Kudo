using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Kudo.Controls;
using Shouldly;
using Xunit;

namespace Kudo.Controls.Tests;

/// <summary>诊断：KudoRibbon 主题解析与命令区宿主部件。</summary>
[Collection("KudoControls-sequential")]
public sealed class KudoRibbonThemeRegistrationTests
{
    [AvaloniaFact]
    public void Ribbon_resolves_its_own_control_theme()
    {
        var ribbon = new KudoRibbon();
        var window = new Window { Content = ribbon };
        window.Show();

        var theme = ribbon.Theme;
        theme.ShouldNotBeNull("KudoRibbon 默认主题未生效");
        theme.ShouldBeOfType<ControlTheme>();

        var contentHost = ribbon.GetVisualDescendants()
            .OfType<ContentControl>()
            .FirstOrDefault(c => c.Name == "PART_ContentHost");
        contentHost.ShouldNotBeNull("自身 ControlTheme 未生效（PART_ContentHost 缺失）");

        window.Close();
    }
}
