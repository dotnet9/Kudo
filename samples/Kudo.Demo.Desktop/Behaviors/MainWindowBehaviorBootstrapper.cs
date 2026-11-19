using Avalonia.Controls;
using Dock.Avalonia.Controls;
using Kudo.Avalonia.Docking;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Demo.Desktop.Views;
using Kudo.Core.Documents;
using Kudo.Demo.Desktop.Services;

namespace Kudo.Demo.Desktop.Behaviors;

internal sealed class MainWindowBehaviorBootstrapper
{
    private readonly MainWindow _window;
    private readonly MainRibbonView _ribbon;
    private readonly DockControl _workspaceDock;
    private readonly GraphWorkspaceSkeleton _workspaceSkeleton;
    private readonly DemoApplicationSettingsStore _settingsStore;
    private readonly GraphDocumentDraftStore? _draftStore;
    private readonly TimeSpan? _draftAutosaveDelay;
    private bool _isCompleted;

    public MainWindowBehaviorBootstrapper(
        MainWindow window,
        DemoApplicationSettingsStore settingsStore,
        GraphDocumentDraftStore? draftStore,
        TimeSpan? draftAutosaveDelay)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _ribbon = window.FindControl<MainRibbonView>(MainWindowControlNames.Ribbon) ??
            throw new InvalidOperationException("MainRibbon control was not found.");
        _workspaceDock = window.FindControl<DockControl>(MainWindowControlNames.WorkspaceDock) ??
            throw new InvalidOperationException("WorkspaceDock control was not found.");
        _workspaceSkeleton = window.FindControl<GraphWorkspaceSkeleton>(MainWindowControlNames.WorkspaceSkeleton) ??
            throw new InvalidOperationException("WorkspaceSkeleton control was not found.");
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _draftStore = draftStore;
        _draftAutosaveDelay = draftAutosaveDelay;
    }

    public void Start()
    {
        _window.DataContextChanged += HandleDataContextChanged;
        _ribbon.DataContextChanged += HandleDataContextChanged;
        _window.Closed += HandleWindowClosed;
        TryAttach();
    }

    private void HandleDataContextChanged(object? sender, EventArgs e) => TryAttach();

    private void HandleWindowClosed(object? sender, EventArgs e) => Detach();

    private void TryAttach()
    {
        if (_isCompleted ||
            _window.DataContext is not MainWindowViewModel viewModel ||
            _ribbon.DataContext is not MainRibbonViewModel)
        {
            return;
        }

        _isCompleted = true;
        Detach();
        _ = new MainWindowBehavior(
            _window,
            viewModel,
            _settingsStore,
            _draftStore,
            _draftAutosaveDelay);
        _workspaceDock.IsVisible = true;
        _workspaceSkeleton.IsVisible = false;
    }

    private void Detach()
    {
        _window.DataContextChanged -= HandleDataContextChanged;
        _ribbon.DataContextChanged -= HandleDataContextChanged;
        _window.Closed -= HandleWindowClosed;
    }
}
