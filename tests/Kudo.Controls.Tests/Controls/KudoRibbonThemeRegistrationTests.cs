using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Kudo.Controls;
using Shouldly;
using Xunit;

namespace Kudo.Controls.Tests;

/// <summary>诊断：KudoRibbon 主题解析与命令区宿主部件。</summary>
public sealed class KudoRibbonThemeRegistrationTests
{
    [Fact]
    public void Ribbon_resolves_its_own_control_theme()
    {
        HeadlessTestRuntime.Run(() =>
        {
            var ribbon = new KudoRibbon();
            var window = new Window { Content = ribbon };
            window.Show();

            var theme = ribbon.GetValue(StyledElement.ThemeProperty);
            var contentHost = ribbon.GetVisualDescendants()
                .OfType<ContentControl>()
                .FirstOrDefault(c => c.Name == "PART_ContentHost");
            var itemsPresenter = ribbon.GetVisualDescendants()
                .FirstOrDefault(c => c.Name == "PART_ItemsPresenter");

            File.WriteAllLines(@"E:\github\temp_files\theme-reg.txt", new List<string>
            {
                $"Theme={theme?.GetType().FullName ?? "null"}",
                $"ContentHost={(contentHost is null ? "null" : "ok")}",
                $"ItemsPresenter={(itemsPresenter is null ? "null" : itemsPresenter.GetType().FullName)}",
            });

            theme.ShouldNotBeNull();
            contentHost.ShouldNotBeNull("自身 ControlTheme 未生效（PART_ContentHost 缺失）");

            window.Close();
        });
    }
}
