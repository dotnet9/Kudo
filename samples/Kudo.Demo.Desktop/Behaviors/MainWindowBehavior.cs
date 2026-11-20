using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Threading;
using CodeWF.Toolkit.EventBus;
using CodeWF.Avalonia.Log;
using TKCode.Arc;
using Kudo.Avalonia;
using Kudo.Demo;
using Kudo;
using Kudo.Demo.Desktop.Localization;
using Kudo.Demo.Desktop.Services;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Demo.Desktop.Views;
using Kudo.Avalonia.Designer;
using Kudo.Avalonia.Docking;
using Kudo.Core.Serialization;
using Kudo.Core.Documents;
using CodeWF.Toolkit.Lang;
using System.Diagnostics.CodeAnalysis;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Behaviors;

internal sealed class MainWindowBehavior : IDisposable
{
    private readonly MainWindow _window;
    private readonly DockLayout _workspaceDock;
    private const string ToolboxToolId = "GraphToolbox";
    private const string DocumentsToolId = "GraphDocumentsTool";
    private const string PropertiesToolId = "GraphProperties";
    private const string DiagnosticsToolId = "GraphDiagnostics";
    private const string ValidationToolId = "GraphValidation";
    private const string RuntimeVariablesToolId = "GraphRuntimeVariables";
    private const string RuntimeBreakpointsToolId = "GraphRuntimeBreakpoints";
    private const string RuntimeCallStackToolId = "GraphRuntimeCallStack";
    private const string RuntimeTraceToolId = "GraphRuntimeTrace";

    private readonly GraphDocumentManager _documents = new();
    private readonly IDemoGraphTextProvider _graphTextProvider = new LocalizedDemoGraphTextProvider();
    private readonly GraphExtensionRegistry _extensionRegistry = new();
    private IGraphNodeCatalog _nodeCatalog = new GraphNodeCatalog([]);
    private readonly GraphNodePalette _toolbox;
    private readonly GraphDocumentList _documentExplorer = new()
    {
        Margin = new Thickness(10, 0)
    };
    private readonly GraphPropertiesView _properties = new();
    private readonly LogView _diagnostics = GraphDemoLogging.CreateView();
    private readonly GraphValidationView _validation = new();
    private readonly GraphVariablesView _runtimeVariables = new();
    private readonly GraphBreakpointsView _runtimeBreakpoints = new();
    private readonly GraphCallStackView _runtimeCallStack = new();
    private readonly GraphRuntimeTraceView _runtimeTrace = new();
    private readonly DemoApplicationSettingsStore _settingsStore;
    private readonly MainWindowDockWorkspace _dockWorkspace;
    private readonly MainRibbonView _ribbonView;
    private readonly MainWindowViewModel _viewModel;
    private readonly MainRibbonViewModel _ribbonViewModel;
    private readonly DocumentWorkspaceCoordinator _workspaceCoordinator;
    private readonly MainWindowNodeAssetService _nodeAssetService;
    private readonly MainWindowWorkspaceService _workspaceService;
    private readonly MainWindowShortcutHandler _shortcutHandler;
    private readonly MainWindowDocumentHost _documentHost;
    private readonly MainWindowDocumentService _documentService;
    private readonly MainWindowRuntimeTools _runtimeTools;
    private readonly MainWindowDraftCoordinator _draftCoordinator;
    private readonly CancellationTokenSource _workspaceLoadCancellation = new();
    private long _catalogLoadVersion;
    private volatile bool _isClosed;
    private bool _isDisposed;

    public static void Attach(
        MainWindow window,
        DemoApplicationSettingsStore settingsStore,
        GraphDocumentDraftStore? draftStore = null,
        TimeSpan? draftAutosaveDelay = null)
    {
        new MainWindowBehaviorBootstrapper(
            window,
            settingsStore,
            draftStore,
            draftAutosaveDelay).Start();
    }

    internal MainWindowBehavior(
        MainWindow window,
        MainWindowViewModel viewModel,
        DemoApplicationSettingsStore settingsStore,
        GraphDocumentDraftStore? draftStore,
        TimeSpan? draftAutosaveDelay)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _workspaceDock = window.FindControl<DockLayout>(MainWindowControlNames.WorkspaceDock) ??
            throw new InvalidOperationException("WorkspaceDock control was not found.");
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        var draftAutosave = new GraphDocumentDraftAutosaveService(
            _documents,
            draftStore ?? CreateDraftStore(_settingsStore),
            draftAutosaveDelay);
        _nodeCatalog = _extensionRegistry.CreateCatalog();
        _toolbox = new GraphNodePalette(_nodeCatalog)
        {
            AutoLoadModelDirectoryCatalog = false,
            LoadingTitle = L(MainWindowLanguage.ModelLibrary),
            LoadingMessage = L(MainWindowLanguage.ModelLibraryLoading),
            ReloadText = L(MainWindowLanguage.Retry),
            Margin = new Thickness(10, 0)
        };
        _documentExplorer.Manager = _documents;
        var workspaceSettings = _settingsStore.LoadWorkspace(out var settingsWarning);
        var ribbonView = _window.FindControl<MainRibbonView>(MainWindowControlNames.Ribbon) ??
            throw new InvalidOperationException("MainRibbon control was not found.");
        _ribbonViewModel = ribbonView.DataContext as MainRibbonViewModel ??
            throw new InvalidOperationException("MainRibbonViewModel was not wired to MainRibbonView.");
        _ribbonView = ribbonView;
        _workspaceCoordinator = new DocumentWorkspaceCoordinator(
            _documents,
            _viewModel);
        CodeWF.Toolkit.EventBus.EventBus.Default.Subscribe(this);
        ribbonView.Ribbon.IsExpanded = workspaceSettings.IsRibbonExpanded;
        ribbonView.Ribbon.PropertyChanged += HandleRibbonPropertyChanged;

        _documentHost = new MainWindowDocumentHost(
            _documents,
            _graphTextProvider,
            _extensionRegistry,
            _properties,
            () => UpdateWorkspace(),
            () => UpdateWorkspace(_viewModel.StatusText));
        _documentHost.ApplyInteractionHintVisibility(_settingsStore.LoadCanvasHintVisible());
        _documentHost.InteractionHintDismissed += (_, _) =>
        {
            _settingsStore.TrySaveCanvasHintVisible(false);
            _documentHost.ApplyInteractionHintVisibility(false);
        };
        _runtimeTools = new MainWindowRuntimeTools(
            _runtimeVariables,
            _runtimeBreakpoints,
            _runtimeCallStack,
            _runtimeTrace,
            () => ActiveView);
        _window.Closed += HandleWindowClosed;
        _window.KeyDown += HandleWindowKeyDown;
        _toolbox.NodeRequestedCommand = _viewModel.AddNodeCommand;
        _toolbox.ReloadCommand = new MainWindowCommand(QueueWorkspaceDataLoad);
        _properties.BackgroundImageRequested += SelectNodeBackgroundImage;
        I18nManager.Instance.CultureChanged += HandleCultureChanged;

        _dockWorkspace = new MainWindowDockWorkspace(
            _workspaceDock,
            _documents,
            session => (Control)_documentHost.CreateContent(session));
        AssignToolContent(window);
        _workspaceService = new MainWindowWorkspaceService(
            _dockWorkspace,
            _settingsStore,
            () => ribbonView.Ribbon.IsExpanded,
            () => ActiveView,
            AppendLog,
            () => UpdateWorkspace());
        var selectionService = new MainWindowSelectionService(
            () => ActiveView,
            () => UpdateWorkspace());
        var layoutService = new MainWindowLayoutService(
            () => ActiveView,
            EnsureEditable,
            AppendLog,
            () => UpdateWorkspace(),
            _workspaceService.SetActualSize);
        var clipboardCoordinator = new MainWindowClipboardCoordinator(
            _window,
            () => ActiveView,
            EnsureEditable,
            AppendLog);
        var editService = new MainWindowEditService(
            () => ActiveView,
            EnsureEditable,
            AppendLog,
            () => UpdateWorkspace());
        var modelingService = new MainWindowModelingService(
            _documents,
            () => _nodeCatalog,
            () => ActiveView,
            EnsureEditable,
            AppendLog);
        var runtimeService = new MainWindowRuntimeService(
            () => ActiveView,
            AppendLog,
            _runtimeTools.Refresh);
        var sampleService = new MainWindowSampleService(
            _documents,
            _graphTextProvider,
            session => _dockWorkspace.ActivateDocument(session),
            AppendLog);
        var fileService = new MainWindowFileService(
            _window,
            _documents,
            () => ActiveSession,
            session => _dockWorkspace.ActivateDocument(session),
            AppendLog,
            () => UpdateWorkspace());
        var outputService = new MainWindowOutputService(
            _window,
            () => ActiveView,
            () => _extensionRegistry.CreateValidator([
                new GraphStructureValidationRule()
            ]),
            _extensionRegistry.SpecialSymbols,
            () =>
            {
                _validation.Rules = _extensionRegistry.CreateValidator([
                    new GraphStructureValidationRule()
                ]).Rules;
                _validation.Refresh(ActiveSession?.Document);
                _dockWorkspace.SetToolVisible(ValidationToolId, true);
            },
            AppendLog);
        _nodeAssetService = new MainWindowNodeAssetService(
            _window,
            _documents,
            _documentHost.GetView,
            EnsureEditable,
            AppendLog);
        _shortcutHandler = new MainWindowShortcutHandler(
            _ribbonViewModel,
            () => ActiveView?.RuntimeSession?.Status);
        var contextMenuFactory = new MainWindowContextMenuFactory(
            _ribbonViewModel,
            () => ActiveView,
            () => ActiveSession,
            session => _dockWorkspace.ActivateDocument(session));
        var dialogService = new MainWindowDialogService(_window);
        _documentHost.Configure(
            () => _nodeCatalog,
            contextMenuFactory.Create,
            nodeDropHandler: null,
            modelingService.AddNode);
        _documentService = new MainWindowDocumentService(
            _documents,
            _documentHost,
            _dockWorkspace,
            dialogService,
            draftAutosave,
            AppendLog);
        _dockWorkspace.DocumentCloseRequested += (_, session) =>
            _ = _documentService.CloseAsync(session);
        var actions = new MainWindowActions(
            _workspaceService,
            sampleService,
            fileService,
            outputService,
            modelingService,
            _documentService,
            selectionService,
            layoutService,
            editService,
            clipboardCoordinator,
            runtimeService,
            () => _workspaceService.ToggleTool(ToolboxToolId, L(MainWindowLanguage.ModelLibrary)),
            () => _workspaceService.ToggleTool(DocumentsToolId, L(MainWindowLanguage.TaskManager)),
            () => _workspaceService.ToggleTool(PropertiesToolId, L(MainWindowLanguage.Properties)),
            () => _workspaceService.ToggleTool(DiagnosticsToolId, L(MainWindowLanguage.Output)),
            () => _workspaceService.ToggleTool(ValidationToolId, L(MainWindowLanguage.ValidationPanel)),
            () => OpenRuntimeTool(RuntimeVariablesToolId),
            () => OpenRuntimeTool(RuntimeBreakpointsToolId),
            () => OpenRuntimeTool(RuntimeCallStackToolId),
            () => OpenRuntimeTool(RuntimeTraceToolId),
            OpenCanvasDisplaySettings);
        _viewModel.Attach(actions);
        _ribbonViewModel.Attach(actions);
        _documentExplorer.DocumentRequestedCommand = _viewModel.DocumentRequestedCommand;
        _documentExplorer.RenameRequestedCommand = _viewModel.RenameRequestedCommand;
        _documentExplorer.DuplicateRequestedCommand = _viewModel.DuplicateRequestedCommand;
        _documentExplorer.CloseRequestedCommand = _viewModel.CloseRequestedCommand;
        _dockWorkspace.ActiveDocumentChanged += HandleActiveGraphDocumentChanged;

        _dockWorkspace.ApplyToolVisibility(workspaceSettings.ToolVisibility);
        _dockWorkspace.SetToolVisible(DiagnosticsToolId, true);
        _dockWorkspace.ToolVisibilityChanged += HandleToolVisibilityChanged;
        _draftCoordinator = new MainWindowDraftCoordinator(
            _window,
            _documents,
            draftAutosave,
            _dockWorkspace,
            _viewModel,
            dialogService,
            AppendLog);
        AppendLog(L(MainWindowLanguage.WorkbenchStarted), L(MainWindowLanguage.Ready));
        if (!string.IsNullOrWhiteSpace(settingsWarning))
        {
            AppendLog(settingsWarning, L(MainWindowLanguage.WorkspaceLayoutRestored));
        }

        QueueWorkspaceDataLoad();
    }

    /// <summary>把工具窗控件装入 MainWindow.axaml 声明的停靠占位符。</summary>
    private void AssignToolContent(MainWindow window)
    {
        SetToolHost(window, "ToolboxHost", _toolbox);
        SetToolHost(window, "DocumentsHost", _documentExplorer);
        SetToolHost(window, "PropertiesHost", _properties);
        SetToolHost(window, "DiagnosticsHost", _diagnostics);
        SetToolHost(window, "ValidationHost", _validation);
        SetToolHost(window, "RuntimeVariablesHost", _runtimeVariables);
        SetToolHost(window, "RuntimeBreakpointsHost", _runtimeBreakpoints);
        SetToolHost(window, "RuntimeCallStackHost", _runtimeCallStack);
        SetToolHost(window, "RuntimeTraceHost", _runtimeTrace);
    }

    private static void SetToolHost(MainWindow window, string hostName, Control content)
    {
        window.FindControl<ContentControl>(hostName)!.Content = content;
    }

    private GraphDocumentSession? ActiveSession => _documents.ActiveSession;

    private GraphDocumentView? ActiveView =>
        _dockWorkspace.ActiveSession is { } session
            ? _documentHost.GetView(session)
            : null;

    private bool EnsureEditable([NotNullWhen(true)] GraphDocumentView? view, string action)
    {
        if (view is null)
        {
            return false;
        }

        if (!view.IsReadOnly)
        {
            return true;
        }

        AppendLog(
            F(MainWindowLanguage.ReadOnlyBlocked, view.Session.Document.Title, action),
            L(MainWindowLanguage.ReadOnlyStatus));
        return false;
    }

    private async void HandleDocumentCloseRequested(object? sender, GraphDocumentSession session)
    {
        try
        {
            await _documentService.CloseAsync(session);
        }
        catch (Exception exception)
        {
            AppendLog(
                F(MainWindowLanguage.OpenFailed, exception.Message),
                L(MainWindowLanguage.CloseCanvasDialogTitle));
        }
    }

    private void HandleRibbonPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property.Name == nameof(Kudo.Controls.KudoRibbon.IsExpanded))
        {
            PersistWorkspaceSettings();
        }
    }

    private void HandleWindowClosed(object? sender, EventArgs e)
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _isClosed = true;
        _window.Closed -= HandleWindowClosed;
        _window.KeyDown -= HandleWindowKeyDown;
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        _ribbonView.Ribbon.PropertyChanged -= HandleRibbonPropertyChanged;
        _workspaceLoadCancellation.Cancel();
        _dockWorkspace.ToolVisibilityChanged -= HandleToolVisibilityChanged;
        _dockWorkspace.ActiveDocumentChanged -= HandleActiveGraphDocumentChanged;
        _draftCoordinator.Dispose();
        _documentHost.Dispose();
        _runtimeTools.Dispose();
        _dockWorkspace.DocumentCloseRequested -= HandleDocumentCloseRequested;
        _dockWorkspace.Dispose();
        CodeWF.Toolkit.EventBus.EventBus.Default.Unsubscribe(this);
        _ribbonViewModel.Dispose();
        _viewModel.Dispose();
        _workspaceLoadCancellation.Dispose();
    }

    private void HandleWindowKeyDown(object? sender, KeyEventArgs e)
        => _shortcutHandler.Handle(
            e,
            IsTextInputSource(e.Source));

    private static bool IsTextInputSource(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TextBox)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleToolVisibilityChanged(object? sender, GraphDockToolVisibilityChangedEventArgs e) =>
        PersistWorkspaceSettings();

    private void PersistWorkspaceSettings() => _workspaceService.PersistSettings();

    [EventHandler]
    private void HandlePropertyContextChanged(MainWindowPropertyContextChangedCommand command)
    {
        _properties.EditorContext = command.Context;
        UpdatePropertiesToolTitle(_properties.Context.Node);
    }

    private async void SelectNodeBackgroundImage(GraphNodeBackgroundImageRequest request)
        => await _nodeAssetService.SelectBackgroundImageAsync(request);

    private void UpdateWorkspace(string? statusMessage = null)
    {
        _documentExplorer.Refresh();
        _workspaceCoordinator.Refresh(ActiveView, statusMessage);
        _runtimeTools.Refresh();
        UpdatePropertiesToolTitle(_properties.Context.Node);
    }

    private void HandleActiveGraphDocumentChanged(object? sender, EventArgs e) => UpdateWorkspace();

    private void OpenRuntimeTool(string toolId)
    {
        _runtimeTools.Refresh();
        _dockWorkspace.SetToolVisible(toolId, true);
    }

    private void OpenCanvasDisplaySettings()
    {
        var view = ActiveView;
        if (view is null)
        {
            return;
        }

        view.EditorContext.Selection.Clear();
        _properties.EditorContext = view.PropertyEditorContext;
        _dockWorkspace.SetToolVisible(PropertiesToolId, true);
    }

    private void AppendLog(string logMessage, string? statusMessage = null)
    {
        GraphDemoLogging.Information(logMessage);
        UpdateWorkspace(statusMessage);
    }

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        if (_isClosed)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ApplyCultureChanged, DispatcherPriority.Normal);
            return;
        }

        ApplyCultureChanged();
    }

    private void ApplyCultureChanged()
    {
        if (_isClosed)
        {
            return;
        }

        QueueCatalogReload();
        UpdateDockTitles();
        _documentHost.RefreshContextMenus();
        _properties.Refresh(_properties.EditorContext);

        UpdateWorkspace();
    }

    private void UpdateDockTitles()
    {
        SetToolTitle(ToolboxToolId, L(MainWindowLanguage.ModelLibrary));
        SetToolTitle(DocumentsToolId, L(MainWindowLanguage.TaskManager));
        UpdatePropertiesToolTitle(_properties.Context.Node);
        SetToolTitle(DiagnosticsToolId, L(MainWindowLanguage.Output));
        SetToolTitle(ValidationToolId, L(MainWindowLanguage.ValidationPanel));
        SetToolTitle(RuntimeVariablesToolId, L(MainWindowLanguage.RuntimeVariables));
        SetToolTitle(RuntimeBreakpointsToolId, L(MainWindowLanguage.RuntimeBreakpoints));
        SetToolTitle(RuntimeCallStackToolId, L(MainWindowLanguage.RuntimeCallStack));
        SetToolTitle(RuntimeTraceToolId, L(MainWindowLanguage.RuntimeTrace));
    }

    private void SetToolTitle(string toolId, string title)
    {
        _dockWorkspace.SetToolTitle(toolId, title);
    }

    private void UpdatePropertiesToolTitle(NodeModel? node)
    {
        var title = L(MainWindowLanguage.Properties);
        if (node is not null)
        {
            title += $"[{node.Number}-{node.Name ?? string.Empty}-{node.DefinitionId}]";
        }

        SetToolTitle(PropertiesToolId, title);
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) => GraphLocalization.Format(key, arguments);

    private static GraphDocumentDraftStore CreateDraftStore(DemoApplicationSettingsStore settingsStore)
    {
        var settingsPath = Path.GetFullPath(settingsStore.FilePath);
        var directory = Path.GetDirectoryName(settingsPath) ?? AppContext.BaseDirectory;
        var name = $"{Path.GetFileNameWithoutExtension(settingsPath)}.drafts";
        return new GraphDocumentDraftStore(Path.Combine(directory, name));
    }

    private void QueueWorkspaceDataLoad()
    {
        var version = Interlocked.Increment(ref _catalogLoadVersion);
        BeginToolboxLoading();
        Dispatcher.UIThread.Post(
            () => _ = LoadWorkspaceDataAsync(version, _workspaceLoadCancellation.Token),
            DispatcherPriority.Background);
    }

    private async Task LoadWorkspaceDataAsync(
        long catalogVersion,
        CancellationToken cancellationToken)
    {
        if (_isClosed || _isDisposed)
        {
            return;
        }

        try
        {
            var catalogTask = DemoGraphNodeCatalog.CreateAsync(_extensionRegistry, cancellationToken);
            var sampleTasksTask = Task.Run(
                static () => new DemoSampleTaskLibrary().Tasks,
                cancellationToken);
            var catalog = await catalogTask.ConfigureAwait(false);
            var sampleTasks = await sampleTasksTask.ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    if (_isClosed || _isDisposed || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (catalogVersion != Interlocked.Read(ref _catalogLoadVersion))
                    {
                        return;
                    }

                    ApplyNodeCatalog(catalog);
                    _ribbonViewModel.UpdateSampleTasks(sampleTasks);
                    CompleteToolboxLoading();
                },
                DispatcherPriority.Background);

            await OpenStartupSessionAsync(sampleTasks, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (_isClosed || _isDisposed)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    FailToolboxLoading(exception);
                    AppendLog(
                        F(MainWindowLanguage.OpenFailed, exception.Message),
                        L(MainWindowLanguage.OpenFailedStatus));
                },
                DispatcherPriority.Background);
        }
    }

    private void QueueCatalogReload()
    {
        var version = Interlocked.Increment(ref _catalogLoadVersion);
        BeginToolboxLoading();
        _ = ReloadCatalogAsync(version, _workspaceLoadCancellation.Token);
    }

    private async Task ReloadCatalogAsync(long version, CancellationToken cancellationToken)
    {
        try
        {
            var catalog = await DemoGraphNodeCatalog
                .CreateAsync(_extensionRegistry, cancellationToken)
                .ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    if (_isClosed ||
                        _isDisposed ||
                        cancellationToken.IsCancellationRequested ||
                        version != Interlocked.Read(ref _catalogLoadVersion))
                    {
                        return;
                    }

                    ApplyNodeCatalog(catalog);
                    CompleteToolboxLoading();
                    _documentHost.RefreshContextMenus();
                    _properties.Refresh(_properties.EditorContext);
                    UpdateWorkspace();
                },
                DispatcherPriority.Background);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (_isClosed || _isDisposed)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    FailToolboxLoading(exception);
                    AppendLog(
                        F(MainWindowLanguage.OpenFailed, exception.Message),
                        L(MainWindowLanguage.OpenFailedStatus));
                },
                DispatcherPriority.Background);
        }
    }

    private void BeginToolboxLoading()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(BeginToolboxLoading, DispatcherPriority.Background);
            return;
        }

        _toolbox.LoadingTitle = L(MainWindowLanguage.ModelLibrary);
        _toolbox.LoadingMessage = L(MainWindowLanguage.ModelLibraryLoading);
        _toolbox.ReloadText = L(MainWindowLanguage.Retry);
        _toolbox.LoadErrorMessage = null;
        _toolbox.IsLoading = true;
    }

    private void CompleteToolboxLoading()
    {
        _toolbox.LoadErrorMessage = null;
        _toolbox.IsLoading = false;
    }

    private void FailToolboxLoading(Exception exception)
    {
        _toolbox.IsLoading = false;
        _toolbox.LoadErrorMessage = F(MainWindowLanguage.ModelLibraryLoadFailed, exception.Message);
    }

    private void ApplyNodeCatalog(IGraphNodeCatalog catalog)
    {
        _nodeCatalog = catalog;
        _toolbox.Catalog = catalog;
        foreach (var view in _documentHost.Views)
        {
            view.NodeCatalog = catalog;
        }
    }

    private async Task OpenStartupSessionAsync(
        IReadOnlyList<DemoSampleTaskDefinition> sampleTasks,
        CancellationToken cancellationToken)
    {
        if (_isClosed || _isDisposed)
        {
            return;
        }

        try
        {
            var startupSession = await CreateStartupSessionAsync(sampleTasks, cancellationToken)
                .ConfigureAwait(false);
            if (startupSession is null ||
                _isClosed ||
                _isDisposed ||
                cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    if (_isClosed || _isDisposed)
                    {
                        return;
                    }

                    _dockWorkspace.ActivateDocument(startupSession);
                },
                DispatcherPriority.Background);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (_isClosed || _isDisposed)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(
                () => AppendLog(
                    F(MainWindowLanguage.OpenFailed, exception.Message),
                    L(MainWindowLanguage.OpenFailedStatus)),
                DispatcherPriority.Background);
        }
    }

    private async Task<GraphDocumentSession?> CreateStartupSessionAsync(
        IReadOnlyList<DemoSampleTaskDefinition> sampleTasks,
        CancellationToken cancellationToken)
    {
        var startupTask = sampleTasks.FirstOrDefault(task =>
                string.Equals(
                    task.Id,
                    DemoSampleTaskLibrary.DefaultStartupTaskId,
                    StringComparison.Ordinal)) ??
            sampleTasks.FirstOrDefault();
        if (startupTask is not null)
        {
            var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(startupTask.TaskDirectory));
            var document = await LoadDocumentAsync(directory, cancellationToken).ConfigureAwait(false);
            return await Dispatcher.UIThread.InvokeAsync(
                () => _documents.Sessions.Count == 0
                    ? _documents.Add(document, directory)
                    : null,
                DispatcherPriority.Background);
        }

        return await Dispatcher.UIThread.InvokeAsync(
            () => _documents.Sessions.Count == 0
                ? _documents.Create(F(MainWindowLanguage.UntitledCanvasSequence, 1))
                : null,
            DispatcherPriority.Background);
    }

    private static async Task<GraphDocument> LoadDocumentAsync(
        string taskDirectory,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(taskDirectory, GraphDocumentManager.ManifestFileName);
        await using (var manifestStream = new FileStream(
                         manifestPath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read,
                         4096,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await GraphTaskPackage.ValidateManifestAsync(manifestStream, cancellationToken)
                .ConfigureAwait(false);
        }

        var taskPath = Path.Combine(taskDirectory, GraphDocumentManager.TaskFileName);
        var serializer = new GraphTaskXmlSerializer();
        await using var stream = new FileStream(
            taskPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var document = await serializer.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
        GraphTaskAssetStore.ValidateReferencedAssets(document, taskDirectory);
        return document;
    }
}
