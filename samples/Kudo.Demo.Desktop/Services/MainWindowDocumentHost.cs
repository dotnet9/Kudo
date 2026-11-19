using Avalonia.Controls;
using Avalonia.Threading;
using Kudo;
using Kudo.Avalonia;
using Kudo.Avalonia.Controls;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Demo.Desktop.Views;
using Kudo.Avalonia.Designer;
using Kudo.Core.Documents;
using Kudo.Core.Runtime;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowDocumentHost : IDisposable
{
    private readonly GraphDocumentManager _documents;
    private readonly IDemoGraphTextProvider _graphTextProvider;
    private readonly GraphExtensionRegistry _extensionRegistry;
    private readonly GraphPropertiesView _propertiesView;
    private Func<IGraphNodeCatalog>? _getNodeCatalog;
    private Func<GraphDocumentView, ContextMenu>? _createContextMenu;
    private Action<GraphDocumentView, GraphNodeDropRequest>? _addNode;
    private IGraphNodeDropHandler? _nodeDropHandler;
    private readonly Action _updateWorkspace;
    private readonly Action _updateViewport;
    private readonly Dictionary<GraphDocumentSession, GraphDocumentView> _views = [];
    private readonly Dictionary<GraphDocumentSession, GraphRuntimeSession> _runtimeSessions = [];
    private readonly Dictionary<GraphDocumentSession, IDisposable> _addons = [];
    private readonly Dictionary<GraphDocumentSession, GraphExtensionApplication> _extensionApplications = [];
    private readonly Dictionary<GraphDocumentSession, EventHandler<GraphHistoryOperationChangedEventArgs>>
        _historyOperationHandlers = [];
    private readonly GraphHistoryDescriptionRegistry _historyDescriptions;
    private readonly GraphHistoryLogFormatter _historyLogFormatter;
    private readonly Dictionary<GraphDocumentSession, long> _loadVersions = [];
    private long _documentLoadVersion;
    private bool _isDisposed;

    public MainWindowDocumentHost(
        GraphDocumentManager documents,
        IDemoGraphTextProvider graphTextProvider,
        GraphExtensionRegistry extensionRegistry,
        GraphPropertiesView propertiesView,
        Action updateWorkspace,
        Action updateViewport)
    {
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _graphTextProvider = graphTextProvider ??
            throw new ArgumentNullException(nameof(graphTextProvider));
        _extensionRegistry = extensionRegistry ?? throw new ArgumentNullException(nameof(extensionRegistry));
        _propertiesView = propertiesView ?? throw new ArgumentNullException(nameof(propertiesView));
        _updateWorkspace = updateWorkspace ??
            throw new ArgumentNullException(nameof(updateWorkspace));
        _updateViewport = updateViewport ??
            throw new ArgumentNullException(nameof(updateViewport));
        _historyDescriptions = _extensionRegistry.CreateHistoryDescriptionRegistry([
            new GraphBuiltInHistoryDescriptionProvider(),
            new DemoGraphHistoryDescriptionProvider()
        ]);
        _historyLogFormatter = new(
            _historyDescriptions,
            () => _getNodeCatalog?.Invoke());
        _documents.SessionsChanged += HandleSessionsChanged;
    }

    public IEnumerable<GraphDocumentView> Views => _views.Values;

    public void Configure(
        Func<IGraphNodeCatalog> getNodeCatalog,
        Func<GraphDocumentView, ContextMenu> createContextMenu,
        IGraphNodeDropHandler? nodeDropHandler,
        Action<GraphDocumentView, GraphNodeDropRequest> addNode)
    {
        if (_getNodeCatalog is not null)
        {
            throw new InvalidOperationException("The document host is already configured.");
        }

        _getNodeCatalog = getNodeCatalog ??
            throw new ArgumentNullException(nameof(getNodeCatalog));
        _createContextMenu = createContextMenu ??
            throw new ArgumentNullException(nameof(createContextMenu));
        _nodeDropHandler = nodeDropHandler;
        _addNode = addNode ?? throw new ArgumentNullException(nameof(addNode));
    }

    public object CreateContent(GraphDocumentSession session)
    {
        var host = new GraphLoadingHost
        {
            LoadingTitle = CreateLoadingTitle(session),
            LoadingMessage = L(MainWindowLanguage.CanvasLoading),
            RetryText = L(MainWindowLanguage.Retry)
        };
        host.RetryCommand = new MainWindowCommand(() => QueueContentLoad(session, host));
        QueueContentLoad(session, host);
        return host;
    }

    private void QueueContentLoad(GraphDocumentSession session, GraphLoadingHost host)
    {
        if (_isDisposed || !_documents.Sessions.Contains(session))
        {
            return;
        }

        var version = ++_documentLoadVersion;
        _loadVersions[session] = version;
        host.LoadingTitle = CreateLoadingTitle(session);
        host.LoadingMessage = L(MainWindowLanguage.CanvasLoading);
        host.RetryText = L(MainWindowLanguage.Retry);
        host.ErrorMessage = null;
        host.IsLoading = true;
        Dispatcher.UIThread.Post(
            () => LoadContent(session, host, version),
            DispatcherPriority.Background);
    }

    private void LoadContent(
        GraphDocumentSession session,
        GraphLoadingHost host,
        long version)
    {
        if (!IsCurrentLoad(session, version))
        {
            return;
        }

        try
        {
            DisposeLoadedSessionResources(session);
            var content = CreateLoadedContent(session);
            if (!IsCurrentLoad(session, version))
            {
                DisposeLoadedSessionResources(session);
                return;
            }

            host.LoadedContent = content;
            host.ErrorMessage = null;
            host.IsLoading = false;
            _loadVersions.Remove(session);
            _updateWorkspace();
            _updateViewport();
        }
        catch (Exception exception)
        {
            DisposeLoadedSessionResources(session);
            if (!IsCurrentLoad(session, version))
            {
                return;
            }

            host.IsLoading = false;
            host.ErrorMessage = F(MainWindowLanguage.CanvasLoadFailed, exception.Message);
        }
    }

    private object CreateLoadedContent(GraphDocumentSession session)
    {
        var getNodeCatalog = _getNodeCatalog ??
            throw new InvalidOperationException("The document host is not configured.");
        var createContextMenu = _createContextMenu ??
            throw new InvalidOperationException("The document host is not configured.");
        var addNode = _addNode ??
            throw new InvalidOperationException("The document host is not configured.");
        var runtimeSession = new GraphRuntimeSession(
            session.Document,
            new DemoGraphRuntimeStateProvider(_graphTextProvider));
        _runtimeSessions[session] = runtimeSession;
        var view = new GraphDocumentView(session, runtimeSession);
        view.NodeCatalog = getNodeCatalog();
        view.NodeFactory = GraphNodeDefinitionFactory.Instance;
        view.NodeDropHandler = _nodeDropHandler;
        view.CreateDroppedNodesAutomatically = true;
        view.ImportDroppedNodeAssets = true;
        view.NodeDefinitionResolver = nodeType => getNodeCatalog().FindByNodeType(nodeType);
        view.Canvas.NewEdgeRoutingStyle = EdgeRoutingStyle.Orthogonal;
        if (HasDocumentFlag(session.Document, SampleGraphs.RendererShowcaseProperty))
        {
            DemoGraphRendererShowcase.Apply(view.Canvas);
        }

        _views[session] = view;
        _extensionApplications[session] = _extensionRegistry.ApplyTo(view.Canvas, _propertiesView);
        view.Canvas.ContextMenu = createContextMenu(view);
        view.StateChanged += (_, _) => _updateWorkspace();
        view.ViewportChanged += (_, _) => _updateViewport();
        view.NodeDropRequested += request => addNode(view, request);
        view.RuntimeDiagnosticReceived += (_, e) =>
            GraphDemoLogging.Runtime(session, e.Diagnostic);
        EventHandler<GraphHistoryOperationChangedEventArgs> historyOperationHandler = (_, operation) =>
        {
            try
            {
                var message = _historyLogFormatter.Format(session.Document, operation);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    GraphDemoLogging.Information(message);
                }
            }
            catch
            {
                // History logging is supplemental and must not affect undo/redo.
            }
        };
        session.History.OperationChanged += historyOperationHandler;
        _historyOperationHandlers[session] = historyOperationHandler;

        if (HasDocumentFlag(session.Document, SampleGraphs.EdgeStylesProperty))
        {
            var viewModel = new EdgeStylesDemoDocumentViewModel(view);
            _addons[session] = viewModel;
            return new EdgeStylesDemoDocumentView { DataContext = viewModel };
        }

        if (!HasDocumentFlag(session.Document, SampleGraphs.ThreeDShowcaseProperty))
        {
            return view;
        }

        var threeDViewModel = new ThreeDDemoDocumentViewModel(view);
        _addons[session] = threeDViewModel;
        return new ThreeDDemoDocumentView { DataContext = threeDViewModel };
    }

    public bool TryGetView(GraphDocumentSession session, out GraphDocumentView? view) =>
        _views.TryGetValue(session, out view);

    public GraphDocumentView? GetView(GraphDocumentSession session) =>
        _views.GetValueOrDefault(session);

    public void RefreshContextMenus()
    {
        var createContextMenu = _createContextMenu ??
            throw new InvalidOperationException("The document host is not configured.");
        foreach (var view in _views.Values)
        {
            view.Canvas.ContextMenu = createContextMenu(view);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _documents.SessionsChanged -= HandleSessionsChanged;
        foreach (var pair in _historyOperationHandlers)
        {
            pair.Key.History.OperationChanged -= pair.Value;
        }

        foreach (var addon in _addons.Values)
        {
            addon.Dispose();
        }

        foreach (var application in _extensionApplications.Values)
        {
            application.Dispose();
        }

        foreach (var view in _views.Values)
        {
            view.Dispose();
        }

        foreach (var runtimeSession in _runtimeSessions.Values)
        {
            runtimeSession.Dispose();
        }

        _addons.Clear();
        _extensionApplications.Clear();
        _views.Clear();
        _runtimeSessions.Clear();
        _historyOperationHandlers.Clear();
        _loadVersions.Clear();
    }

    private void HandleSessionsChanged(object? sender, EventArgs e)
    {
        foreach (var session in _views.Keys
                     .Concat(_runtimeSessions.Keys)
                     .Concat(_loadVersions.Keys)
                     .Distinct()
                     .Where(session => !_documents.Sessions.Contains(session))
                     .ToArray())
        {
            CancelSessionLoad(session);
            DisposeLoadedSessionResources(session);
        }

        _updateWorkspace();
    }

    private void CancelSessionLoad(GraphDocumentSession session)
    {
        _loadVersions.Remove(session);
    }

    private void DisposeLoadedSessionResources(GraphDocumentSession session)
    {
        if (_historyOperationHandlers.Remove(session, out var historyOperationHandler))
        {
            session.History.OperationChanged -= historyOperationHandler;
        }

        if (_addons.Remove(session, out var addon))
        {
            addon.Dispose();
        }

        if (_extensionApplications.Remove(session, out var application))
        {
            application.Dispose();
        }

        if (_views.Remove(session, out var view))
        {
            view.Dispose();
        }

        if (_runtimeSessions.Remove(session, out var runtimeSession))
        {
            runtimeSession.Dispose();
        }
    }

    private bool IsCurrentLoad(GraphDocumentSession session, long version) =>
        !_isDisposed &&
        _documents.Sessions.Contains(session) &&
        _loadVersions.TryGetValue(session, out var currentVersion) &&
        currentVersion == version;

    private static bool HasDocumentFlag(GraphDocument document, string propertyName)
    {
        if (!document.Properties.TryGetValue(propertyName, out var value))
        {
            return false;
        }

        return value is bool boolean && boolean;
    }

    private static string CreateLoadingTitle(GraphDocumentSession session) =>
        string.IsNullOrWhiteSpace(session.Document.Title)
            ? L(MainWindowLanguage.UntitledCanvas)
            : session.Document.Title;

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
