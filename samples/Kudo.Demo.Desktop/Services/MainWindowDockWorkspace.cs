using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Kudo.Avalonia.Docking;
using Kudo.Core.Documents;
using TKCode.Arc;

namespace Kudo.Demo.Desktop.Services;

/// <summary>停靠工具窗条目：Arc DockPane 的数据源适配。</summary>
public sealed class DockToolItem : INotifyPropertyChanged, IDockItem
{
    private string _title;

    public DockToolItem(string id, string title, Control content, DockSide side)
    {
        DockId = id;
        _title = title;
        Content = content;
        Side = side;
    }

    public string DockId { get; }

    public string DockTitle
    {
        get => _title;
        set
        {
            if (_title == value)
            {
                return;
            }

            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DockTitle)));
        }
    }

    public DockSide Side { get; }

    public Control Content { get; }

    /// <summary>重命名工具窗标题。</summary>
    public void Rename(string title) => DockTitle = title;

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>停靠文档条目：一个画板会话对应一个文档页签。</summary>
public sealed class DockDocumentItem : INotifyPropertyChanged, IDockItem
{
    public DockDocumentItem(GraphDocumentSession session, Control content)
    {
        Session = session;
        Content = content;
        DockId = GraphDockIds.CreateDocumentId(session.SessionId);
        DockTitle = session.Document.Title ?? GraphDockIds.Documents;
    }

    public string DockId { get; }

    public string DockTitle { get; private set; }

    public GraphDocumentSession Session { get; }

    public Control Content { get; }

    /// <summary>同步画板页签标题（重命名后调用）。</summary>
    public void Rename(string title) => DockTitle = title;

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => PropertyChanged += value;
        remove => PropertyChanged -= value;
    }

    private event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
///     基于 Arc DockLayout 的主窗口停靠工作台：工具窗走 ToolsSource、画板文档走 DocumentsSource，
///     页签关闭经 PaneClosing 取消后转确认流程。
/// </summary>
public sealed class MainWindowDockWorkspace : IDisposable
{
    private readonly DockLayout _layout;
    private readonly GraphDocumentManager _documents;
    private readonly Func<GraphDocumentSession, Control> _contentFactory;
    private readonly List<DockToolItem> _toolItems = [];
    private readonly ObservableCollection<DockToolItem> _toolsSource = [];
    private readonly ObservableCollection<DockDocumentItem> _documentsSource = [];
    private bool _isDisposed;

    public MainWindowDockWorkspace(
        DockLayout layout,
        GraphDocumentManager documents,
        Func<GraphDocumentSession, Control> contentFactory,
        IEnumerable<GraphDockToolDefinition> toolDefinitions)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _contentFactory = contentFactory ?? throw new ArgumentNullException(nameof(contentFactory));

        foreach (var definition in toolDefinitions)
        {
            _toolItems.Add(new DockToolItem(
                definition.Id,
                definition.Title,
                (Control)definition.Content,
                MapSide(definition.Placement)));
        }

        _layout.ToolTemplate = new ToolPaneTemplate();
        _layout.DocumentTemplate = new DocumentPaneTemplate();
        _layout.ToolsSource = _toolsSource;
        _layout.DocumentsSource = _documentsSource;
        _toolsSource.CollectionChanged += HandleToolsChanged;
        _layout.ActivePaneChanged += HandleActivePaneChanged;
        _layout.PaneClosing += HandlePaneClosing;
        _documents.SessionsChanged += HandleSessionsChanged;
    }

    /// <summary>活动画板会话（由活动文档页签解析）。</summary>
    public GraphDocumentSession? ActiveSession =>
        ResolveSession(_layout.ActivePane);

    /// <summary>活动文档页签变化（切换画板时触发）。</summary>
    public event EventHandler? ActiveDocumentChanged;

    /// <summary>工具窗可见性变化（用于持久化）。</summary>
    public event EventHandler<GraphDockToolVisibilityChangedEventArgs>? ToolVisibilityChanged;

    /// <summary>用户点页签关闭按钮：默认关闭已被取消，宿主应转确认流程后调用 CloseDocument。</summary>
    public event EventHandler<GraphDocumentSession>? DocumentCloseRequested;

    /// <summary>激活画板：不存在则创建页签并置为活动。</summary>
    public void ActivateDocument(GraphDocumentSession session)
    {
        var item = EnsureDocumentItem(session);
        if (_layout.Workspace.FindPane(item.DockId) is { } pane)
        {
            _layout.Activate(pane);
        }

        _documents.SetActive(session);
        ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>会话关闭后移除对应页签。</summary>
    public void CloseDocument(GraphDocumentSession session)
    {
        var item = FindDocumentItem(session);
        if (item is null)
        {
            return;
        }

        _documentsSource.Remove(item);
    }

    /// <summary>同步画板页签标题（重命名后调用）。</summary>
    public void SetDocumentTitle(GraphDocumentSession session, string title)
    {
        FindDocumentItem(session)?.Rename(title);
    }

    public bool IsToolVisible(string id) =>
        _toolItems.Any(item => item.DockId == id && _toolsSource.Contains(item));

    public bool SetToolVisible(string id, bool isVisible)
    {
        var item = _toolItems.FirstOrDefault(candidate => candidate.DockId == id);
        if (item is null)
        {
            return false;
        }

        var contains = _toolsSource.Contains(item);
        if (isVisible && !contains)
        {
            _toolsSource.Add(item);
            return true;
        }

        if (!isVisible && contains)
        {
            _toolsSource.Remove(item);
            return false;
        }

        return contains;
    }

    public bool ToggleTool(string id) => SetToolVisible(id, !IsToolVisible(id));

    public IReadOnlyDictionary<string, bool> CaptureToolVisibility() =>
        _toolItems
            .OrderBy(item => item.DockId, StringComparer.Ordinal)
            .ToDictionary(item => item.DockId, item => _toolsSource.Contains(item));

    public void ApplyToolVisibility(IReadOnlyDictionary<string, bool> visibility)
    {
        foreach (var item in _toolItems)
        {
            var isVisible = visibility.TryGetValue(item.DockId, out var value) ? value : true;
            SetToolVisible(item.DockId, isVisible);
        }
    }

    /// <summary>更新工具窗标题（语言切换、属性上下文变化时）。</summary>
    public void SetToolTitle(string id, string title)
    {
        _toolItems.FirstOrDefault(item => item.DockId == id)?.Rename(title);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _layout.ActivePaneChanged -= HandleActivePaneChanged;
        _layout.PaneClosing -= HandlePaneClosing;
        _documents.SessionsChanged -= HandleSessionsChanged;
        _toolsSource.Clear();
        _documentsSource.Clear();
    }

    private DockDocumentItem? FindDocumentItem(GraphDocumentSession session) =>
        _documentsSource.FirstOrDefault(item => ReferenceEquals(item.Session, session));

    private DockDocumentItem? FindDocumentItem(string paneId) =>
        _documentsSource.FirstOrDefault(item => item.DockId == paneId);

    private GraphDocumentSession? ResolveSession(DockPane? pane) =>
        pane is null ? null : FindDocumentItem(pane.Id)?.Session;

    private DockDocumentItem EnsureDocumentItem(GraphDocumentSession session)
    {
        if (FindDocumentItem(session) is { } existing)
        {
            return existing;
        }

        var item = new DockDocumentItem(session, _contentFactory(session));
        _documentsSource.Add(item);
        return item;
    }

    private void HandleActivePaneChanged(object? sender, DockPaneEventArgs args)
    {
        if (ResolveSession(args.Pane) is { } session)
        {
            _documents.SetActive(session);
            ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandlePaneClosing(object? sender, DockPaneClosingEventArgs args)
    {
        if (ResolveSession(args.Pane) is { } session)
        {
            // 同步取消默认关闭，交由确认流程（确认后经 SessionChanged 移除条目完成关闭）。
            args.Cancel = true;
            DocumentCloseRequested?.Invoke(this, session);
        }
    }

    private void HandleSessionsChanged(object? sender, EventArgs args)
    {
        foreach (var item in _documentsSource.Where(item =>
                     !_documents.Sessions.Contains(item.Session)).ToList())
        {
            _documentsSource.Remove(item);
        }
    }

    private static DockSide MapSide(GraphDockToolPlacement placement) => placement switch
    {
        GraphDockToolPlacement.Right => DockSide.Right,
        GraphDockToolPlacement.Bottom => DockSide.Bottom,
        _ => DockSide.Left
    };

    private void HandleToolsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action is NotifyCollectionChangedAction.Add)
        {
            foreach (DockToolItem item in args.NewItems!.OfType<DockToolItem>())
            {
                ToolVisibilityChanged?.Invoke(
                    this, new GraphDockToolVisibilityChangedEventArgs(item.DockId, true));
            }
        }
        else if (args.Action is NotifyCollectionChangedAction.Remove)
        {
            foreach (DockToolItem item in args.OldItems!.OfType<DockToolItem>())
            {
                ToolVisibilityChanged?.Invoke(
                    this, new GraphDockToolVisibilityChangedEventArgs(item.DockId, false));
            }
        }
    }

    /// <summary>工具窗模板：按条目生成配置好的 DockPane（Id/标题由 IDockItem 契约同步）。</summary>
    private sealed class ToolPaneTemplate : IDataTemplate
    {
        public Control Build(object? item)
        {
            var tool = (DockToolItem)item!;
            return new DockPane
            {
                Id = tool.DockId,
                PreferredSide = tool.Side,
                CanClose = true,
                CanFloat = true,
                CanAutoHide = true,
                Content = tool.Content
            };
        }

        public bool Match(object? data) => data is DockToolItem;
    }

    /// <summary>文档页签模板：承载画板视图，可关闭。</summary>
    private sealed class DocumentPaneTemplate : IDataTemplate
    {
        public Control Build(object? item)
        {
            var document = (DockDocumentItem)item!;
            return new DockPane
            {
                Id = document.DockId,
                CanClose = true,
                CanFloat = true,
                Content = document.Content
            };
        }

        public bool Match(object? data) => data is DockDocumentItem;
    }
}
