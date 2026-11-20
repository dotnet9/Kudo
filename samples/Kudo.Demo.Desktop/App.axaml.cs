using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Kudo.Demo.Desktop.Behaviors;
using Kudo.Demo.Desktop.Services;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Demo.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace Kudo.Demo.Desktop;

public partial class App : Application
{
    /// <summary>应用容器。视图可通过它自取 ViewModel，验证工程可通过它换入测试替身。</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    private DemoApplicationSettingsStore _applicationSettingsStore = null!;
    private DemoLocalizationService _localizationService = null!;
    private DemoThemeService _themeService = null!;

    public override void Initialize()
    {
        _applicationSettingsStore = DemoApplicationSettingsStore.CreateDefault();
        _localizationService = new DemoLocalizationService(
            _applicationSettingsStore,
            CultureInfo.CurrentUICulture,
            Environment.GetEnvironmentVariable(DemoLocalizationService.CultureOverrideEnvironmentVariable));
        _localizationService.Initialize(Path.Combine(AppContext.BaseDirectory, "I18n"));
        AvaloniaXamlLoader.Load(this);
        _localizationService.ApplyControlCultures(this);
        _themeService = new DemoThemeService(_applicationSettingsStore);

        var services = new ServiceCollection();
        services.AddSingleton(_applicationSettingsStore);
        services.AddSingleton(_localizationService);
        services.AddSingleton(_themeService);
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainRibbonViewModel>();
        Services = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 先恢复持久化的品牌主题，再创建主窗口，避免首帧闪错主题。
            _themeService.ApplySaved();

            var window = new MainWindow(Services.GetRequiredService<DemoApplicationSettingsStore>());
            window.DataContext = Services.GetRequiredService<MainWindowViewModel>();
            if (window.FindControl<MainRibbonView>(MainWindowControlNames.Ribbon) is { } ribbon)
            {
                ribbon.DataContext = Services.GetRequiredService<MainRibbonViewModel>();
            }

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
