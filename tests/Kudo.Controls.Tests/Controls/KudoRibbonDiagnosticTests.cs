using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kudo.Controls;
using Shouldly;
using Xunit;

namespace Kudo.Controls.Tests;

/// <summary>回归：KudoRibbon 模板接管（Semi 的 TabStrip 主题不得覆盖）。</summary>
[Collection("KudoControls-sequential")]
public sealed class KudoRibbonDiagnosticTests
{
    [AvaloniaFact]
    public void Ribbon_template_host_is_ours_not_semis()
    {
        var ribbon = new KudoRibbon();
        var window = new Window { Content = ribbon };
        window.Show();

        var quickAccessHost = ribbon.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("QuickToolBarRoot") && b.Classes.Contains("RibbonHeader"));
        quickAccessHost.ShouldNotBeNull("Ribbon 模板未接管——快速访问槽缺失");

        var contentHost = ribbon.GetVisualDescendants()
            .OfType<ContentControl>()
            .FirstOrDefault(c => c.Name == "PART_ContentHost");
        contentHost.ShouldNotBeNull("命令区宿主缺失");

        window.Close();
    }
}
