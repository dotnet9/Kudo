using System.Globalization;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using Kudo.Demo.Desktop.Services;
using Kudo.Demo.Desktop.ViewModels;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests.ViewModels;

/// <summary>主视图模型构造冒烟：不依赖窗口即可完成装配。</summary>
public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_completes_and_disposes_cleanly()
    {
        I18nManager.Instance.Register(new JsonLangPlugin { ScanResourceFolder = false },
            CultureInfo.GetCultureInfo("en-US"));

        var localization = new DemoLocalizationService(
            DemoApplicationSettingsStore.CreateDefault(),
            CultureInfo.GetCultureInfo("en-US"),
            cultureOverride: null);
        var themeService = new DemoThemeService(DemoApplicationSettingsStore.CreateDefault());

        using var viewModel = new MainWindowViewModel(localization, themeService);

        viewModel.ShouldNotBeNull();
        viewModel.Themes.Count.ShouldBeGreaterThan(0);
        viewModel.SelectedTheme.ShouldNotBeNull();
        viewModel.Dispose();
    }
}
