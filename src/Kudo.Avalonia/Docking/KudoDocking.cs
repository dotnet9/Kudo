using Dock.Avalonia.Controls;
using Avalonia;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Dock.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Dock.Settings;
using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using Kudo.Core.Documents;
using CodeWF.Toolkit.Lang;
using DockingLanguage = global::Localization.Kudo_Avalonia.Docking.KudoDocking;

namespace Kudo.Avalonia.Docking;

public sealed record KudoDockingOptions(
    bool UseDocumentTabs = true,
    bool ShowToolbox = true,
    bool ShowDocuments = true,
    bool ShowProperties = true,
    bool ShowDiagnostics = true)
{
    public double LeftRegionProportion { get; init; } = 0.18;

    public double CenterRegionProportion { get; init; } = 0.60;

    public double RightRegionProportion { get; init; } = 0.22;

    public double LeftTopRegionProportion { get; init; } = 0.38;

    public double LeftMiddleRegionProportion { get; init; } = 0.20;

    public double LeftBottomRegionProportion { get; init; } = 0.62;

    public double DocumentRegionProportion { get; init; } = 0.78;

    public double BottomRegionProportion { get; init; } = 0.22;

    public double LeftRegionMinWidth { get; init; } = 200;

    public double RightRegionMinWidth { get; init; }

    public double BottomRegionMinHeight { get; init; }
}

public enum GraphDockToolPlacement
{
    [Description("左侧")]
    Left,

    [Description("左上区域")]
    LeftTop,

    [Description("左下区域")]
    LeftBottom,

    [Description("右侧")]
    Right,

    [Description("底部")]
    Bottom
}

public enum GraphDockToolRole
{
    [Description("自定义工具")]
    Custom,

    [Description("模型工具箱")]
    Toolbox,

    [Description("文档列表")]
    Documents,

    [Description("属性面板")]
    Properties,

    [Description("诊断面板")]
    Diagnostics
}

public static class GraphDockIds
{
    private const string DocumentPrefix = "graph-document-";

    public const string Documents = "GraphDocuments";

    public const string Workspace = "GraphWorkspace";

    public const string WorkspaceRoot = "GraphWorkspaceRoot";

    public const string LeftWorkspace = "GraphLeftWorkspace";

    public static string CreateDocumentId(string documentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        return $"{DocumentPrefix}{documentId}";
    }
}

public static class GraphDockLayout
{
    public static void ConfigureWorkspace(DockControl dockControl)
    {
        ArgumentNullException.ThrowIfNull(dockControl);

        FixedToolGridDockBehavior.SetLayoutAdapter(
            dockControl,
            new GridDockColumnLayoutAdapter(
                GraphDockIds.Workspace,
                5,
                new GridDockRegionDefinition(new[] { 0 }, new[] { 0, 1 }),
                new GridDockRegionDefinition(new[] { 4 }, new[] { 3, 4 })));
        FixedToolGridDockBehavior.SetIsEnabled(dockControl, true);
    }
}

public sealed record GraphDockToolDefinition(
    string Id,
    string Title,
    object Content,
    GraphDockToolPlacement Placement,
    GraphDockToolRole Role = GraphDockToolRole.Custom);

public sealed class GraphDockToolVisibilityChangedEventArgs(string toolId, bool isVisible) : EventArgs
{
    public string ToolId { get; } = toolId;

    public bool IsVisible { get; } = isVisible;
}

public interface IGraphDockLayoutFactory
{
    object CreateLayout(GraphDocumentManager documents, KudoDockingOptions? options = null);
}

public sealed class GraphDockContentTemplate : IRecyclingDataTemplate
{
    public Control? Build(object? param) => Build(param, null);

    public Control? Build(object? param, Control? existing)
    {
        if (param is not IDockable { Context: { } context })
        {
            return null;
        }

        if (context is Control control)
        {
            if (existing is GraphDockControlHost existingHost &&
                ReferenceEquals(existingHost.DockContent, control))
            {
                return existingHost;
            }

            if (ReferenceEquals(existing, control) ||
                control.Parent is null && control.GetVisualParent() is null)
            {
                return control;
            }

            // Dock 的标题栏等辅助 ContentPresenter 在布局迁移时可能短暂回退到
            // 应用级 DataTemplate。Control 只能有一个视觉父级，此时不能让辅助
            // Presenter 抢走已经显示的真实内容；浮动 Host 会通过共享回收器传入
            // existing，并迁移同一个 Control 实例。
            return new GraphDockControlHost(control);
        }

        if (existing is ContentControl contentHost)
        {
            contentHost.Content = context;
            return contentHost;
        }

        return new ContentControl
        {
            Content = context,
            HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch
        };
    }

    public bool Match(object? data) => data is IDockable { Context: not null };

    private sealed class GraphDockControlHost : ContentControl
    {
        private const string ToolTitleBarContentPartName = "PART_TitleBarContent";

        private bool _isAttached;

        public GraphDockControlHost(Control dockContent)
        {
            DockContent = dockContent;
            HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;
            DockContent.DetachedFromVisualTree += HandleDockContentDetached;
        }

        public Control DockContent { get; }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _isAttached = true;
            TryAttachDockContent();
            QueueContentTransfer();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isAttached = false;
            if (!ReferenceEquals(Content, DockContent))
            {
                DockContent.DetachedFromVisualTree -= HandleDockContentDetached;
            }

            base.OnDetachedFromVisualTree(e);
        }

        private void HandleDockContentDetached(object? sender, VisualTreeAttachmentEventArgs e) =>
            QueueContentTransfer();

        private void QueueContentTransfer() =>
            Dispatcher.UIThread.Post(TryAttachDockContent, DispatcherPriority.Loaded);

        private void TryAttachDockContent()
        {
            if (!_isAttached || IsAuxiliaryDockHost() || ReferenceEquals(Content, DockContent))
            {
                return;
            }

            if (!TryDetachDockContent())
            {
                return;
            }

            SetCurrentValue(ContentProperty, DockContent);
            DockContent.DetachedFromVisualTree -= HandleDockContentDetached;
        }

        private bool TryDetachDockContent()
        {
            var logicalParent = DockContent.Parent;
            var visualParent = DockContent.GetVisualParent();
            if (logicalParent is null && visualParent is null)
            {
                return true;
            }

            if (logicalParent is not null && TryDetachFrom(logicalParent))
            {
                return true;
            }

            return visualParent is not null &&
                   !ReferenceEquals(visualParent, logicalParent) &&
                   TryDetachFrom(visualParent);
        }

        private bool TryDetachFrom(object parent)
        {
            switch (parent)
            {
                case Panel panel:
                    return panel.Children.Remove(DockContent);
                case ContentPresenter presenter when ReferenceEquals(presenter.Child, DockContent):
                    presenter.SetCurrentValue(ContentPresenter.ContentProperty, null);
                    presenter.UpdateChild();
                    return DockContent.Parent is null && DockContent.GetVisualParent() is null;
                case ContentControl contentControl when ReferenceEquals(contentControl.Content, DockContent):
                    contentControl.SetCurrentValue(ContentControl.ContentProperty, null);
                    return DockContent.Parent is null && DockContent.GetVisualParent() is null;
                case Decorator decorator when ReferenceEquals(decorator.Child, DockContent):
                    decorator.Child = null;
                    return DockContent.Parent is null && DockContent.GetVisualParent() is null;
                default:
                    return false;
            }
        }

        private bool IsAuxiliaryDockHost()
        {
            for (var ancestor = this.GetVisualParent(); ancestor is not null; ancestor = ancestor.GetVisualParent())
            {
                if (ancestor is DragPreviewControl or ContentControl { Name: ToolTitleBarContentPartName })
                {
                    return true;
                }
            }

            return false;
        }
    }
}

public sealed class GraphDockDocument : Document, IDisposable
{
    public GraphDockDocument(GraphDocumentSession session, object content)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Id = GraphDockIds.CreateDocumentId(session.Document.Id);
        Context = content ?? throw new ArgumentNullException(nameof(content));
        CanClose = true;
        CanFloat = true;
        Session.DirtyStateChanged += OnDirtyStateChanged;
        Session.History.Changed += OnHistoryChanged;
        I18nManager.Instance.CultureChanged += OnCultureChanged;
        RefreshTitle();
    }

    public GraphDocumentSession Session { get; }

    public void RefreshTitle()
    {
        var title = string.IsNullOrWhiteSpace(Session.Document.Title)
            ? GraphLocalization.Get(DockingLanguage.UntitledCanvas)
            : Session.Document.Title;
        Title = Session.IsDirty ? $"{title} *" : title;
        IsModified = Session.IsDirty;
    }

    public void Dispose()
    {
        Session.DirtyStateChanged -= OnDirtyStateChanged;
        Session.History.Changed -= OnHistoryChanged;
        I18nManager.Instance.CultureChanged -= OnCultureChanged;
        GC.SuppressFinalize(this);
    }

    private void OnDirtyStateChanged(object? sender, EventArgs e) => RefreshTitle();

    private void OnHistoryChanged(object? sender, EventArgs e) => RefreshTitle();

    private void OnCultureChanged(object? sender, EventArgs e) => RefreshTitle();
}

public sealed class GraphDockLayoutFactory : Factory, IGraphDockLayoutFactory, IDisposable
{
    private readonly Func<GraphDocumentSession, object> _documentContentFactory;
    private readonly IReadOnlyList<GraphDockToolDefinition> _toolDefinitions;
    private readonly Dictionary<GraphDocumentSession, GraphDockDocument> _documents = [];
    private readonly Dictionary<IDockable, DockLocation> _dockLocations =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IDock> _toolHosts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IDock> _toolOwners = new(StringComparer.Ordinal);
    private GraphDocumentManager? _documentManager;
    private IRootDock? _rootDock;
    private ProportionalDock? _leftRegion;
    private ProportionalDock? _rightRegion;
    private double _leftRegionWidth = 200;
    private double _rightRegionWidth = 280;
    private bool _isSynchronizing;
    private bool _isDisposed;

    public GraphDockLayoutFactory(
        Func<GraphDocumentSession, object>? documentContentFactory = null,
        IEnumerable<GraphDockToolDefinition>? tools = null)
    {
        _documentContentFactory = documentContentFactory ?? CreateDefaultDocumentContent;
        _toolDefinitions = tools?.ToArray() ?? [];
        ActiveDockableChanged += HandleActiveDockableChanged;
        DockableClosed += HandleDockableClosed;
        I18nManager.Instance.CultureChanged += HandleCultureChanged;
    }

    public IDocumentDock? DocumentDock { get; private set; }

    public GraphDockDocument? ActiveDocument => DocumentDock?.ActiveDockable as GraphDockDocument;

    public event EventHandler? ActiveGraphDocumentChanged;

    public event EventHandler<GraphDockToolVisibilityChangedEventArgs>? ToolVisibilityChanged;

    object IGraphDockLayoutFactory.CreateLayout(
        GraphDocumentManager documents,
        KudoDockingOptions? options) => CreateDockLayout(documents, options);

    public IRootDock CreateDockLayout(
        GraphDocumentManager documents,
        KudoDockingOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(documents);

        DetachDocumentManager();
        foreach (var document in _documents.Values)
        {
            document.Dispose();
        }

        _documents.Clear();
        _dockLocations.Clear();
        _tools.Clear();
        _toolHosts.Clear();
        _toolOwners.Clear();
        _rootDock = null;
        _leftRegion = null;
        _rightRegion = null;
        _documentManager = documents;
        _documentManager.SessionsChanged += HandleSessionsChanged;
        options ??= new KudoDockingOptions();
        _leftRegionWidth = options.LeftRegionMinWidth > 0 ? options.LeftRegionMinWidth : 200;
        _rightRegionWidth = options.RightRegionMinWidth > 0 ? options.RightRegionMinWidth : 280;

        var documentDockables = documents.Sessions.Select(CreateDocument).Cast<IDockable>().ToArray();
        var activeDocument = documents.ActiveSession is { } activeSession &&
                             _documents.TryGetValue(activeSession, out var selected)
            ? selected
            : documentDockables.LastOrDefault();
        var documentDock = new DocumentDock
        {
            Id = GraphDockIds.Documents,
            Title = L(DockingLanguage.Canvas),
            IsCollapsable = false,
            CanCreateDocument = false,
            CanCloseLastDockable = true,
            CloseButtonShowMode = DocumentCloseButtonShowMode.Active,
            EnableWindowDrag = options.UseDocumentTabs,
            EmptyContent = CreateEmptyDocumentContent(),
            ActiveDockable = activeDocument,
            VisibleDockables = CreateList(documentDockables)
        };
        DocumentDock = documentDock;

        var enabledTools = _toolDefinitions.Where(tool => IsToolEnabled(tool, options)).ToArray();
        var leftTools = CreateLeftRegion(enabledTools, options);
        var rightTools = CreateToolRegion(
            enabledTools,
            GraphDockToolPlacement.Right,
            Alignment.Right,
            options.RightRegionProportion,
            minWidth: options.RightRegionMinWidth);
        var bottomTools = CreateToolRegion(
            enabledTools,
            GraphDockToolPlacement.Bottom,
            Alignment.Bottom,
            options.BottomRegionProportion,
            minHeight: options.BottomRegionMinHeight);

        IDockable center = documentDock;
        if (bottomTools is not null)
        {
            documentDock.Proportion = options.DocumentRegionProportion;
            center = new ProportionalDock
            {
                Proportion = options.CenterRegionProportion,
                Orientation = Orientation.Vertical,
                VisibleDockables = CreateList<IDockable>(
                    documentDock,
                    new ProportionalDockSplitter(),
                    bottomTools)
            };
        }
        else
        {
            documentDock.Proportion = options.CenterRegionProportion;
        }

        var leftSplitter = new GridDockSplitter { Column = 1 };
        var rightSplitter = new GridDockSplitter { Column = 3 };
        if (leftTools is IDockable leftDockable)
        {
            leftDockable.Column = 0;
        }

        if (center is IDockable centerDockable)
        {
            centerDockable.Column = 2;
        }

        if (rightTools is IDockable rightDockable)
        {
            rightDockable.Column = 4;
        }

        var workspaceItems = CreateList<IDockable>();
        if (leftTools is { } leftRegion)
        {
            workspaceItems.Add(leftRegion);
            workspaceItems.Add(leftSplitter);
        }

        workspaceItems.Add(center);
        if (rightTools is { } rightRegion)
        {
            workspaceItems.Add(rightSplitter);
            workspaceItems.Add(rightRegion);
        }

        var workspace = new GridDock
        {
            Id = GraphDockIds.Workspace,
            IsCollapsable = false,
            ColumnDefinitions = FormattableString.Invariant($"{_leftRegionWidth},4,*,4,{_rightRegionWidth}"),
            VisibleDockables = workspaceItems
        };
        _leftRegion = leftTools as ProportionalDock;
        _rightRegion = rightTools as ProportionalDock;
        var root = CreateRootDock();
        root.Id = GraphDockIds.WorkspaceRoot;
        root.Title = L(DockingLanguage.WorkspaceTitle);
        root.IsCollapsable = false;
        root.ActiveDockable = workspace;
        root.DefaultDockable = workspace;
        root.VisibleDockables = CreateList<IDockable>(workspace);
        _rootDock = root;
        UpdateFixedToolColumns();
        return root;
    }

    public override void InitLayout(IDockable layout)
    {
        var root = layout as IRootDock ?? FindRoot(layout, _ => true);
        var hostWindowLocator = DefaultHostWindowLocator ??= () => CreateHostWindow(root);
        HostWindowLocator ??= new Dictionary<string, Func<IHostWindow?>>();
        HostWindowLocator.TryAdd(nameof(IDockWindow), hostWindowLocator);
        base.InitLayout(layout);
        CaptureToolDockLocations();
    }

    public override IDockWindow? CreateWindowFrom(IDockable dockable)
    {
        var window = base.CreateWindowFrom(dockable);
        if (window is not null)
        {
            window.Title = string.IsNullOrWhiteSpace(dockable.Title)
                ? L(DockingLanguage.WorkspaceTitle)
                : dockable.Title;
        }

        return window;
    }

    public override void HideDockable(IDockable dockable)
    {
        var isTrackedTool = IsTrackedTool(dockable);
        var wasVisible = isTrackedTool && IsToolInLiveWorkspace((ITool)dockable);
        var owner = dockable.Owner as IDock;
        var wasActive = owner?.ActiveDockable == dockable;
        base.HideDockable(dockable);

        if (wasActive && owner is not null && isTrackedTool)
        {
            var next = owner.VisibleDockables?.FirstOrDefault(candidate => candidate is not ISplitter);
            owner.ActiveDockable = next;
            if (next is not null)
            {
                SetFocusedDockable(owner, next);
            }
        }

        if (wasVisible && !IsToolInLiveWorkspace((ITool)dockable))
        {
            ToolVisibilityChanged?.Invoke(
                this,
                new GraphDockToolVisibilityChangedEventArgs(dockable.Id!, false));
        }

        UpdateFixedToolColumns();
    }

    public override void RestoreDockable(IDockable dockable)
    {
        var isTrackedTool = IsTrackedTool(dockable);
        var wasVisible = isTrackedTool && IsToolInLiveWorkspace((ITool)dockable);
        base.RestoreDockable(dockable);
        if (isTrackedTool)
        {
            RestoreToolToLiveWorkspace((ITool)dockable);
        }

        if (!wasVisible && isTrackedTool && IsToolInLiveWorkspace((ITool)dockable))
        {
            ToolVisibilityChanged?.Invoke(
                this,
                new GraphDockToolVisibilityChangedEventArgs(dockable.Id!, true));
        }

        UpdateFixedToolColumns();
    }

    public override void CloseDockable(IDockable? dockable)
    {
        if (dockable is null || !IsTrackedTool(dockable))
        {
            base.CloseDockable(dockable);
            return;
        }

        var previousHideToolsOnClose = HideToolsOnClose;
        HideToolsOnClose = true;
        try
        {
            base.CloseDockable(dockable);
        }
        finally
        {
            HideToolsOnClose = previousHideToolsOnClose;
        }
    }

    public bool ActivateDocument(GraphDocumentSession session)
    {
        if (DocumentDock is null ||
            _documentManager is null ||
            !_documentManager.Sessions.Contains(session))
        {
            return false;
        }

        if (!_documents.TryGetValue(session, out var document))
        {
            document = CreateDocument(session);
        }

        _isSynchronizing = true;
        try
        {
            if (document.Owner is not IDock owner || owner.VisibleDockables?.Contains(document) != true)
            {
                AddDockable(DocumentDock, document);
            }

            if (!ReferenceEquals(_documentManager.ActiveSession, session))
            {
                _documentManager.SetActive(session);
            }

            SetActiveDockable(document);
            SetFocusedDockable(DocumentDock, document);
        }
        finally
        {
            _isSynchronizing = false;
        }

        ActiveGraphDocumentChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool TryGetDocument(GraphDocumentSession session, out GraphDockDocument? document) =>
        _documents.TryGetValue(session, out document);

    public bool TryGetTool(string id, out ITool? tool) => _tools.TryGetValue(id, out tool);

    public bool IsToolVisible(string id) =>
        _tools.TryGetValue(id, out var tool) && IsToolInLiveWorkspace(tool);

    public IReadOnlyDictionary<string, bool> CaptureToolVisibility() =>
        _tools
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => IsToolInLiveWorkspace(pair.Value), StringComparer.Ordinal);

    public void ApplyToolVisibility(IReadOnlyDictionary<string, bool> visibility)
    {
        ArgumentNullException.ThrowIfNull(visibility);
        foreach (var (id, isVisible) in visibility)
        {
            SetToolVisible(id, isVisible);
        }
    }

    public bool SetToolVisible(string id, bool isVisible)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!_tools.TryGetValue(id, out var tool))
        {
            return false;
        }

        if (!isVisible)
        {
            if (IsToolInLiveWorkspace(tool))
            {
                HideDockable(tool);
            }

            return true;
        }

        if (!IsToolInLiveWorkspace(tool))
        {
            if (IsToolHidden(tool))
            {
                RestoreDockable(tool);
            }
            else
            {
                var wasVisible = IsToolInLiveWorkspace(tool);
                RestoreToolToLiveWorkspace(tool);
                if (!wasVisible && IsToolInLiveWorkspace(tool))
                {
                    ToolVisibilityChanged?.Invoke(
                        this,
                        new GraphDockToolVisibilityChangedEventArgs(tool.Id!, true));
                }
            }
        }

        if (tool.Owner is IDock owner)
        {
            SetActiveDockable(tool);
            SetFocusedDockable(owner, tool);
        }

        return true;
    }

    public bool ToggleTool(string id) =>
        _tools.ContainsKey(id) && SetToolVisible(id, !IsToolVisible(id));

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        ActiveDockableChanged -= HandleActiveDockableChanged;
        DockableClosed -= HandleDockableClosed;
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        DetachDocumentManager();
        foreach (var document in _documents.Values)
        {
            document.Dispose();
        }

        _documents.Clear();
        _dockLocations.Clear();
        _tools.Clear();
        _toolHosts.Clear();
        _toolOwners.Clear();
        _rootDock = null;
        _leftRegion = null;
        _rightRegion = null;
        GC.SuppressFinalize(this);
    }

    private static object CreateDefaultDocumentContent(GraphDocumentSession session) =>
        new GraphDesignerShell { Document = session.Document };

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        if (DocumentDock is not null)
        {
            DocumentDock.Title = L(DockingLanguage.Canvas);
            DocumentDock.EmptyContent = CreateEmptyDocumentContent();
        }

        if (_rootDock is not null)
        {
            _rootDock.Title = L(DockingLanguage.WorkspaceTitle);
        }

        foreach (var document in _documents.Values)
        {
            document.RefreshTitle();
        }
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string[] CreateEmptyDocumentContent() =>
    [
        L(DockingLanguage.NoOpenCanvas),
        L(DockingLanguage.OpenCanvasHint)
    ];

    private GraphDockDocument CreateDocument(GraphDocumentSession session)
    {
        var document = new GraphDockDocument(session, _documentContentFactory(session));
        _documents.Add(session, document);
        return document;
    }

    private ToolDock? CreateToolDock(
        IEnumerable<GraphDockToolDefinition> definitions,
        GraphDockToolPlacement placement,
        Alignment alignment)
    {
        var tools = definitions
            .Where(definition => definition.Placement == placement)
            .Select(CreateTool)
            .Cast<IDockable>()
            .ToArray();
        if (tools.Length == 0)
        {
            return null;
        }

        var toolDock = new ToolDock
        {
            Alignment = alignment,
            GripMode = GripMode.Visible,
            CanCloseLastDockable = true,
            Proportion = 1.0,
            CollapsedProportion = 1.0,
            ActiveDockable = tools[0],
            VisibleDockables = CreateList(tools)
        };

        foreach (var tool in tools.OfType<ITool>())
        {
            _toolOwners[tool.Id!] = toolDock;
        }

        return toolDock;
    }

    private IDockable? CreateToolRegion(
        IEnumerable<GraphDockToolDefinition> definitions,
        GraphDockToolPlacement placement,
        Alignment alignment,
        double proportion,
        double minWidth = 0,
        double minHeight = 0)
    {
        var toolDock = CreateToolDock(definitions, placement, alignment);
        if (toolDock is null)
        {
            return null;
        }

        var host = new ProportionalDock
        {
            IsCollapsable = true,
            Proportion = proportion,
            CollapsedProportion = proportion,
            MinWidth = minWidth,
            MinHeight = minHeight,
            VisibleDockables = CreateList<IDockable>(toolDock)
        };
        foreach (var tool in toolDock.VisibleDockables!.OfType<ITool>())
        {
            _toolHosts[tool.Id!] = host;
        }

        return host;
    }

    private IDockable? CreateLeftRegion(
        IEnumerable<GraphDockToolDefinition> definitions,
        KudoDockingOptions options)
    {
        var top = CreateToolRegion(
            definitions,
            GraphDockToolPlacement.LeftTop,
            Alignment.Left,
            options.LeftTopRegionProportion);
        var bottom = CreateToolRegion(
            definitions,
            GraphDockToolPlacement.LeftBottom,
            Alignment.Left,
            options.LeftBottomRegionProportion);
        if (top is null && bottom is null)
        {
            return CreateToolRegion(
                definitions,
                GraphDockToolPlacement.Left,
                Alignment.Left,
                options.LeftRegionProportion,
                minWidth: options.LeftRegionMinWidth);
        }

        var regions = CreateList<IDockable>();
        AddRegion(regions, top);
        AddRegion(
            regions,
            CreateToolRegion(
                definitions,
                GraphDockToolPlacement.Left,
                Alignment.Left,
                options.LeftMiddleRegionProportion));
        AddRegion(regions, bottom);
        return new ProportionalDock
        {
            Id = GraphDockIds.LeftWorkspace,
            Orientation = Orientation.Vertical,
            Proportion = options.LeftRegionProportion,
            MinWidth = options.LeftRegionMinWidth,
            VisibleDockables = regions
        };
    }

    private void UpdateFixedToolColumns()
    {
        if (_leftRegion is not null)
        {
            _leftRegion.MinWidth = HasVisibleDockContent(_leftRegion) ? _leftRegionWidth : 0;
        }

        if (_rightRegion is not null)
        {
            _rightRegion.MinWidth = HasVisibleDockContent(_rightRegion) ? _rightRegionWidth : 0;
        }
    }

    private static bool HasVisibleDockContent(IDock dock)
    {
        if (dock.VisibleDockables is not { Count: > 0 } visibleDockables)
        {
            return false;
        }

        return visibleDockables.Any(item => item switch
        {
            ISplitter => false,
            IDock childDock => HasVisibleDockContent(childDock),
            _ => !item.IsEmpty
        });
    }

    private ITool CreateTool(GraphDockToolDefinition definition)
    {
        var tool = new Tool
        {
            Id = definition.Id,
            Title = definition.Title,
            Context = definition.Content,
            CanClose = true,
            CanFloat = true,
            CanPin = true
        };
        if (!_tools.TryAdd(definition.Id, tool))
        {
            throw new InvalidOperationException($"A Dock Tool with ID '{definition.Id}' is already registered.");
        }

        return tool;
    }

    private bool IsTrackedTool(IDockable dockable) =>
        !string.IsNullOrWhiteSpace(dockable.Id) &&
        _tools.TryGetValue(dockable.Id, out var tool) &&
        ReferenceEquals(tool, dockable);

    private bool IsToolHidden(ITool tool) =>
        FindRoot(tool, _ => true)?.HiddenDockables?.Contains(tool) == true;

    private bool IsToolInLiveWorkspace(ITool tool)
    {
        if (_rootDock is null || IsToolHidden(tool))
        {
            return false;
        }

        var root = FindRoot(tool, _ => true);
        if (ReferenceEquals(root, _rootDock))
        {
            return IsAttachedToRoot(tool, _rootDock);
        }

        return root?.Window is { Host: not null } window &&
               _rootDock.Windows?.Contains(window) == true &&
               IsAttachedToRoot(tool, root);
    }

    private void RestoreToolToLiveWorkspace(ITool tool)
    {
        if (IsToolInLiveWorkspace(tool) ||
            string.IsNullOrWhiteSpace(tool.Id) ||
            !_toolOwners.TryGetValue(tool.Id, out var owner))
        {
            return;
        }

        EnsureRegisteredOwnerAttached(tool, owner);

        if (tool.Owner is IDock oldOwner && oldOwner.VisibleDockables?.Contains(tool) == true)
        {
            RemoveDockable(tool, false);
        }

        if (owner.VisibleDockables?.Contains(tool) != true)
        {
            AddDockable(owner, tool);
        }

        InitDockable(owner, owner.Owner);
        owner.ActiveDockable = tool;
        ActivateOwnerChain(owner);
    }

    private void EnsureRegisteredOwnerAttached(ITool tool, IDock owner)
    {
        if (_rootDock is null || IsAttachedToRoot(owner, _rootDock) ||
            string.IsNullOrWhiteSpace(tool.Id) ||
            !_toolHosts.TryGetValue(tool.Id, out var host))
        {
            return;
        }

        EnsureDockAttached(host);

        if (owner.Owner is IDock previousOwner && previousOwner.VisibleDockables?.Contains(owner) == true)
        {
            RemoveDockable(owner, false);
        }

        if (host.VisibleDockables?.Contains(owner) != true)
        {
            AddDockable(host, owner);
        }
    }

    private void CaptureToolDockLocations()
    {
        _dockLocations.Clear();
        foreach (var host in _toolHosts.Values.Distinct())
        {
            IDockable current = host;
            while (current.Owner is IDock parent)
            {
                if (parent.VisibleDockables is not { } visibleDockables)
                {
                    break;
                }

                var index = visibleDockables.IndexOf(current);
                if (index >= 0 && !_dockLocations.ContainsKey(current))
                {
                    var previous = visibleDockables
                        .Take(index)
                        .LastOrDefault(candidate => candidate is not ISplitter);
                    var next = visibleDockables
                        .Skip(index + 1)
                        .FirstOrDefault(candidate => candidate is not ISplitter);
                    _dockLocations[current] = new DockLocation(parent, previous, next);
                }

                current = parent;
            }
        }
    }

    private bool EnsureDockAttached(IDockable dockable)
    {
        if (_rootDock is null)
        {
            return false;
        }

        if (IsAttachedToRoot(dockable, _rootDock))
        {
            return true;
        }

        if (!_dockLocations.TryGetValue(dockable, out var location))
        {
            return false;
        }

        if (!ReferenceEquals(location.Parent, _rootDock) &&
            !EnsureDockAttached(location.Parent))
        {
            return false;
        }

        if (location.Parent.VisibleDockables?.Contains(dockable) != true)
        {
            InsertAtRegisteredLocation(location, dockable);
        }

        return IsAttachedToRoot(dockable, _rootDock);
    }

    private void InsertAtRegisteredLocation(DockLocation location, IDockable dockable)
    {
        var parent = location.Parent;
        parent.VisibleDockables ??= CreateList<IDockable>();
        var dockables = parent.VisibleDockables;
        if (dockables is null)
        {
            return;
        }

        if (dockables.Count == 0 || parent is not IProportionalDock)
        {
            AddDockable(parent, dockable);
            return;
        }

        if (location.Next is not null && dockables.IndexOf(location.Next) is var nextIndex && nextIndex >= 0)
        {
            InsertDockable(parent, dockable, nextIndex);
            InsertDockable(parent, new ProportionalDockSplitter(), nextIndex + 1);
            return;
        }

        if (location.Previous is not null && dockables.IndexOf(location.Previous) is var previousIndex &&
            previousIndex >= 0)
        {
            var insertionIndex = previousIndex + 1;
            InsertDockable(parent, new ProportionalDockSplitter(), insertionIndex);
            InsertDockable(parent, dockable, insertionIndex + 1);
            return;
        }

        InsertDockable(parent, dockable, 0);
        InsertDockable(parent, new ProportionalDockSplitter(), 1);
    }

    private static bool IsAttachedToRoot(IDockable dockable, IRootDock root)
    {
        var current = dockable;
        while (current.Owner is IDock parent)
        {
            if (parent.VisibleDockables?.Contains(current) != true)
            {
                return false;
            }

            current = parent;
        }

        return ReferenceEquals(current, root);
    }

    private static void ActivateOwnerChain(IDockable dockable)
    {
        var current = dockable;
        while (current.Owner is IDock parent)
        {
            parent.ActiveDockable = current;
            current = parent;
        }
    }

    private sealed record DockLocation(IDock Parent, IDockable? Previous, IDockable? Next);

    private static bool IsToolEnabled(GraphDockToolDefinition tool, KudoDockingOptions options) =>
        tool.Role switch
        {
            GraphDockToolRole.Toolbox => options.ShowToolbox,
            GraphDockToolRole.Documents => options.ShowDocuments,
            GraphDockToolRole.Properties => options.ShowProperties,
            GraphDockToolRole.Diagnostics => options.ShowDiagnostics,
            _ => true
        };

    private static IHostWindow CreateHostWindow(IRootDock? root) =>
        DockSettings.ResolveFloatingWindowHostMode(root) == DockFloatingWindowHostMode.Managed
            ? new ManagedHostWindow()
            : new HostWindow();

    private static void AddRegion(IList<IDockable> regions, IDockable? region)
    {
        if (region is null)
        {
            return;
        }

        if (regions.Count > 0)
        {
            regions.Add(new ProportionalDockSplitter());
        }

        regions.Add(region);
    }

    private void HandleSessionsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            if (!_isDisposed)
            {
                Dispatcher.UIThread.Post(
                    () => HandleSessionsChanged(sender, e));
            }

            return;
        }

        if (_isSynchronizing || _documentManager is null || DocumentDock is null)
        {
            return;
        }

        _isSynchronizing = true;
        try
        {
            foreach (var session in _documentManager.Sessions.Where(session => !_documents.ContainsKey(session)))
            {
                var document = CreateDocument(session);
                AddDockable(DocumentDock, document);
            }

            foreach (var pair in _documents.Where(pair => !_documentManager.Sessions.Contains(pair.Key)).ToArray())
            {
                RemoveDockable(pair.Value, false);
                pair.Value.Dispose();
                _documents.Remove(pair.Key);
            }

            if (_documentManager.ActiveSession is { } activeSession &&
                _documents.TryGetValue(activeSession, out var activeDocument) &&
                !ReferenceEquals(DocumentDock.ActiveDockable, activeDocument))
            {
                SetActiveDockable(activeDocument);
                SetFocusedDockable(DocumentDock, activeDocument);
            }
        }
        finally
        {
            _isSynchronizing = false;
        }

        ActiveGraphDocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleActiveDockableChanged(object? sender, Dock.Model.Core.Events.ActiveDockableChangedEventArgs e)
    {
        if (_isSynchronizing || e.Dockable is not GraphDockDocument document || _documentManager is null)
        {
            return;
        }

        _isSynchronizing = true;
        try
        {
            if (!ReferenceEquals(_documentManager.ActiveSession, document.Session))
            {
                _documentManager.SetActive(document.Session);
            }
        }
        finally
        {
            _isSynchronizing = false;
        }

        ActiveGraphDocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleDockableClosed(object? sender, Dock.Model.Core.Events.DockableClosedEventArgs e)
    {
        if (_isSynchronizing || e.Dockable is not GraphDockDocument)
        {
            return;
        }

        ActiveGraphDocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DetachDocumentManager()
    {
        if (_documentManager is not null)
        {
            _documentManager.SessionsChanged -= HandleSessionsChanged;
            _documentManager = null;
        }
    }
}
