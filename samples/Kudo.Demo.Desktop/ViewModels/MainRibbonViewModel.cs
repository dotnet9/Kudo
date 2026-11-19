using CodeWF.Toolkit.EventBus;
using Kudo.Avalonia;
using Kudo.Demo.Desktop.Services;
using Kudo.Core.Runtime;
using Kudo.Avalonia.Export;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.ViewModels;

public sealed class DemoSampleTaskMenuItem
{
    public DemoSampleTaskMenuItem(
        DemoSampleTaskDefinition definition,
        ICommand command)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Id = definition.Id;
        Title = definition.Title;
        TaskDirectory = definition.TaskDirectory;
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }

    public string Id { get; }

    public string Title { get; }

    public string TaskDirectory { get; }

    public ICommand Command { get; }
}

public sealed class MainRibbonViewModel : ReactiveObject, IDisposable
{
    private IMainWindowActions? _actions;
    private MainWindowWorkspaceState _workspace = new();
    private bool _isWorkspaceAvailable = true;
    private bool _isDisposed;

    public MainRibbonViewModel()
    {
        var canUseWorkspace = this.WhenAnyValue(viewModel => viewModel.IsWorkspaceAvailable);
        OpenGraphCommand = ReactiveCommand.CreateFromTask(
            () => _actions?.OpenGraphAsync() ?? Task.CompletedTask);
        SaveGraphCommand = ReactiveCommand.CreateFromTask(
            () => _actions?.SaveGraphAsync(false) ?? Task.CompletedTask);
        SaveGraphAsCommand = ReactiveCommand.CreateFromTask(
            () => _actions?.SaveGraphAsync(true) ?? Task.CompletedTask);
        ExportGraphCommand = ReactiveCommand.CreateFromTask<GraphExportFormat>(
            format => _actions?.ExportGraphAsync(format) ?? Task.CompletedTask,
            canUseWorkspace);
        PrintGraphCommand = ReactiveCommand.CreateFromTask(
            () => _actions?.PrintGraphAsync() ?? Task.CompletedTask,
            canUseWorkspace);
        CreateLargeGraphCommand = ReactiveCommand.CreateFromTask<string>(
            nodeCount => int.TryParse(nodeCount, out var parsedNodeCount)
                ? _actions?.CreateLargeGraphAsync(parsedNodeCount) ?? Task.CompletedTask
                : Task.CompletedTask);
        SampleTasks = [];
        LoadSampleTaskCommand = ReactiveCommand.CreateFromTask<DemoSampleTaskMenuItem>(
            task => _actions?.OpenSampleTaskAsync(task.TaskDirectory) ?? Task.CompletedTask);
        StartRuntimeCommand = ReactiveCommand.CreateFromTask(
            () => ExecuteRuntimeCommandAsync(GraphRuntimeCommand.Start, L(MainWindowLanguage.Run)),
            this.WhenAnyValue(viewModel => viewModel.Workspace.CanStartRuntime));
        PauseRuntimeCommand = ReactiveCommand.CreateFromTask(
            () => ExecuteRuntimeCommandAsync(GraphRuntimeCommand.Pause, L(MainWindowLanguage.Pause)),
            this.WhenAnyValue(viewModel => viewModel.Workspace.CanPauseRuntime));
        StepRuntimeCommand = ReactiveCommand.CreateFromTask(
            () => ExecuteRuntimeCommandAsync(GraphRuntimeCommand.Step, L(MainWindowLanguage.Step)),
            this.WhenAnyValue(viewModel => viewModel.Workspace.CanStepRuntime));
        StopRuntimeCommand = ReactiveCommand.CreateFromTask(
            () => ExecuteRuntimeCommandAsync(GraphRuntimeCommand.Stop, L(MainWindowLanguage.Stop)),
            this.WhenAnyValue(viewModel => viewModel.Workspace.CanStopRuntime));
        ResetRuntimeCommand = ReactiveCommand.CreateFromTask(
            () => ExecuteRuntimeCommandAsync(GraphRuntimeCommand.Reset, L(MainWindowLanguage.Reset)),
            this.WhenAnyValue(viewModel => viewModel.Workspace.CanResetRuntime));
        ToggleBreakpointCommand = ReactiveCommand.Create(
            () => _actions?.ToggleBreakpoint(),
            this.WhenAnyValue(viewModel => viewModel.Workspace.CanToggleBreakpoint));
        RefreshRuntimeCommand = ReactiveCommand.CreateFromTask(
            () => _actions?.RefreshRuntimeAsync() ?? Task.CompletedTask,
            this.WhenAnyValue(viewModel => viewModel.Workspace.CanRefreshRuntime));
        ClipboardCommand = ReactiveCommand.CreateFromTask<MainWindowClipboardAction>(
            action => _actions?.ExecuteClipboardActionAsync(action) ?? Task.CompletedTask,
            canUseWorkspace);
        CloseActiveDocumentCommand = ReactiveCommand.CreateFromTask(
            () => _actions?.CloseActiveDocumentAsync() ?? Task.CompletedTask,
            canUseWorkspace);
    }

    public ICommand OpenGraphCommand { get; }

    public ICommand SaveGraphCommand { get; }

    public ICommand SaveGraphAsCommand { get; }

    public ICommand ExportGraphCommand { get; }

    public ICommand PrintGraphCommand { get; }

    public ICommand CreateLargeGraphCommand { get; }

    public ICommand LoadSampleTaskCommand { get; }

    public ObservableCollection<DemoSampleTaskMenuItem> SampleTasks { get; }

    public ICommand StartRuntimeCommand { get; }

    public ICommand PauseRuntimeCommand { get; }

    public ICommand StepRuntimeCommand { get; }

    public ICommand StopRuntimeCommand { get; }

    public ICommand ResetRuntimeCommand { get; }

    public ICommand ToggleBreakpointCommand { get; }

    public ICommand RefreshRuntimeCommand { get; }

    public ICommand ClipboardCommand { get; }

    public ICommand CloseActiveDocumentCommand { get; }

    public MainWindowWorkspaceState Workspace
    {
        get => _workspace;
        private set => this.RaiseAndSetIfChanged(ref _workspace, value);
    }

    public bool IsWorkspaceAvailable
    {
        get => _isWorkspaceAvailable;
        private set => this.RaiseAndSetIfChanged(ref _isWorkspaceAvailable, value);
    }

    public void ExecuteSelectionAction(MainWindowSelectionAction action) =>
        _actions?.ExecuteSelectionAction(action);

    public void ExecuteCanvasViewAction(MainWindowCanvasViewAction action) =>
        _actions?.ExecuteCanvasViewAction(action);

    public void ExecuteArrangementAction(MainWindowArrangementAction action) =>
        _actions?.ExecuteArrangementAction(action);

    public void ExecuteDocumentEditAction(MainWindowDocumentEditAction action) =>
        _actions?.ExecuteDocumentEditAction(action);

    public void ExecuteModelingAction(MainWindowModelingAction action) =>
        _actions?.ExecuteModelingAction(action);

    public void SetRoutingStyle(EdgeRoutingStyle style) =>
        _actions?.SetRoutingStyle(style);

    public void ClearEdgeWaypoints() => _actions?.ClearEdgeWaypoints();

    public void ZoomOut() => _actions?.ZoomBy(1 / 1.2);

    public void ZoomIn() => _actions?.ZoomBy(1.2);

    public void CreateGraph() => _actions?.CreateGraph();

    public void ToggleToolbox() => _actions?.ToggleToolbox();

    public void ToggleDocumentExplorer() => _actions?.ToggleDocumentExplorer();

    public void ToggleProperties() => _actions?.ToggleProperties();

    public void OpenCanvasDisplaySettings() => _actions?.OpenCanvasDisplaySettings();

    public void ToggleDiagnostics() => _actions?.ToggleDiagnostics();

    public void ToggleValidation() => _actions?.ToggleValidation();

    public void OpenRuntimeVariables() => _actions?.OpenRuntimeVariables();

    public void OpenRuntimeBreakpoints() => _actions?.OpenRuntimeBreakpoints();

    public void OpenRuntimeCallStack() => _actions?.OpenRuntimeCallStack();

    public void OpenRuntimeTrace() => _actions?.OpenRuntimeTrace();

    public void ClearRuntimeBreakpoints() => _actions?.ClearRuntimeBreakpoints();

    public void ClearRuntimeWatches() => _actions?.ClearRuntimeWatches();

    public void ClearRuntimeDiagnostics() => _actions?.ClearRuntimeDiagnostics();

    public void ValidateGraph() => _actions?.ValidateGraph();

    public void ToggleGrid() => _actions?.ToggleGrid();

    public void ToggleGridSnap() => _actions?.ToggleGridSnap();

    public void ToggleAlignmentGuides() => _actions?.ToggleAlignmentGuides();

    public void ToggleReadOnlyMode() => _actions?.ToggleReadOnlyMode();

    public void ToggleMinimap() => _actions?.ToggleMinimap();

    public void SetGridSpacing(string spacing)
    {
        if (int.TryParse(
                spacing,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value))
        {
            _actions?.SetGridSpacing(value);
        }
    }

    internal void Attach(IMainWindowActions actions)
    {
        CodeWF.Toolkit.EventBus.EventBus.Default.Unsubscribe(this);
        CodeWF.Toolkit.EventBus.EventBus.Default.Subscribe(this);
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    internal void UpdateSampleTasks(IEnumerable<DemoSampleTaskDefinition> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        SampleTasks.Clear();
        foreach (var task in tasks)
        {
            SampleTasks.Add(new DemoSampleTaskMenuItem(task, LoadSampleTaskCommand));
        }
    }

    internal void UpdateWorkspace(MainWindowWorkspaceState workspace) =>
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    internal void UpdateWorkspaceAvailability(bool value) =>
        IsWorkspaceAvailable = value;

    [EventHandler]
    private void HandleWorkspaceChanged(MainWindowWorkspaceChangedCommand command) =>
        UpdateWorkspace(command.Workspace);

    [EventHandler]
    private void HandleWorkspaceAvailabilityChanged(
        MainWindowWorkspaceAvailabilityChangedCommand command) =>
        UpdateWorkspaceAvailability(command.IsAvailable);

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        (OpenGraphCommand as IDisposable)?.Dispose();
        (SaveGraphCommand as IDisposable)?.Dispose();
        (SaveGraphAsCommand as IDisposable)?.Dispose();
        (ExportGraphCommand as IDisposable)?.Dispose();
        (PrintGraphCommand as IDisposable)?.Dispose();
        (CreateLargeGraphCommand as IDisposable)?.Dispose();
        (LoadSampleTaskCommand as IDisposable)?.Dispose();
        (StartRuntimeCommand as IDisposable)?.Dispose();
        (PauseRuntimeCommand as IDisposable)?.Dispose();
        (StepRuntimeCommand as IDisposable)?.Dispose();
        (StopRuntimeCommand as IDisposable)?.Dispose();
        (ResetRuntimeCommand as IDisposable)?.Dispose();
        (ToggleBreakpointCommand as IDisposable)?.Dispose();
        (RefreshRuntimeCommand as IDisposable)?.Dispose();
        (ClipboardCommand as IDisposable)?.Dispose();
        (CloseActiveDocumentCommand as IDisposable)?.Dispose();
        CodeWF.Toolkit.EventBus.EventBus.Default.Unsubscribe(this);
        _actions = null;
    }

    private Task ExecuteRuntimeCommandAsync(GraphRuntimeCommand command, string displayName) =>
        _actions?.ExecuteRuntimeCommandAsync(command, displayName) ?? Task.CompletedTask;

    private static string L(string key) => GraphLocalization.Get(key);
}
