using CodeWF.Avalonia.Controls.Controls;
using Kudo.Demo.Desktop.Behaviors;
using Kudo.Demo.Desktop.Services;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Core.Documents;

namespace Kudo.Demo.Desktop.Views;

public partial class MainWindow : CodeWFWindow
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
        MainWindowBehavior.Attach(
            this,
            settingsStore,
            draftStore,
            draftAutosaveDelay);
    }
}
