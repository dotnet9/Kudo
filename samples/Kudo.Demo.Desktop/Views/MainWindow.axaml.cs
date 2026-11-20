using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Interactivity;
using TKCode.Arc;
using Kudo.Demo.Desktop.Behaviors;
using Kudo.Demo.Desktop.Services;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Core.Documents;

namespace Kudo.Demo.Desktop.Views;

public partial class MainWindow : ArcWindow
{
    public MainWindow() : this(DemoApplicationSettingsStore.CreateDefault())
    {
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        DemoApplicationSettingsStore settingsStore) : this(
        settingsStore,
        null,
        null,
        viewModel)
    {
    }

    public MainWindow(
        DemoApplicationSettingsStore settingsStore,
        GraphDocumentDraftStore? draftStore = null,
        TimeSpan? draftAutosaveDelay = null) : this(
            settingsStore,
            draftStore,
            draftAutosaveDelay,
            null)
    {
    }

    private MainWindow(
        DemoApplicationSettingsStore settingsStore,
        GraphDocumentDraftStore? draftStore,
        TimeSpan? draftAutosaveDelay,
        MainWindowViewModel? viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? DataContext;
        if (this.FindControl<global::Avalonia.Controls.Button>("TitleBarSettingsButton") is { } settingsButton)
        {
            settingsButton.Click += HandleSettingsClicked;
        }

        MainWindowBehavior.Attach(
            this,
            settingsStore,
            draftStore,
            draftAutosaveDelay);
        // 临时验证钩子：KUDO_OPEN_SETTINGS=1 时启动即打开设置窗口（验证后移除）。
        if (Environment.GetEnvironmentVariable("KUDO_OPEN_SETTINGS") == "1")
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(OpenSettings);
        }
    }

    /// <summary>打开设置窗口（主题 / 语言 / 关于），以主窗口为属主。</summary>
    private void OpenSettings()
    {
        var settings = new SettingsWindow
        {
            DataContext = DataContext
        };
        settings.Show(this);
    }

    private void HandleSettingsClicked(object? sender, RoutedEventArgs args) => OpenSettings();
}
