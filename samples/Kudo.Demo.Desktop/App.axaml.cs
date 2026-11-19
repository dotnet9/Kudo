using Avalonia;
using Avalonia.Markup.Xaml;
using Kudo.Demo.Desktop.Services;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Demo.Desktop.Views;
using Prism.DryIoc;
using Prism.Ioc;
using System.Globalization;

namespace Kudo.Demo.Desktop;

public partial class App : PrismApplication
{
    private DemoApplicationSettingsStore _applicationSettingsStore = null!;
    private DemoLocalizationService _localizationService = null!;

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
        base.Initialize();
    }

    protected override AvaloniaObject CreateShell() =>
        new MainWindow(Container.Resolve<DemoApplicationSettingsStore>());

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterInstance(_applicationSettingsStore);
        containerRegistry.RegisterInstance(_localizationService);
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        containerRegistry.Register<MainRibbonViewModel>();
    }
}
