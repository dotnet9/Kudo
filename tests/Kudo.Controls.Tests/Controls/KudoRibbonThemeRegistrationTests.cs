using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
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
        ribbon.Styles.AddRange(new KudoRibbonStyles());
        var window = new Window { Content = ribbon };
        window.Show();

        var contentHost = ribbon.GetVisualDescendants()
            .OfType<ContentControl>()
            .FirstOrDefault(c => c.Name == "PART_ContentHost");
        contentHost.ShouldNotBeNull("样式模板未生效（PART_ContentHost 缺失）");

        window.Close();
    }
}
