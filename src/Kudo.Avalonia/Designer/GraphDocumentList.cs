using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kudo.Avalonia;
using Kudo.Core.Documents;
using Kudo.Core.Runtime;
using CodeWF.Toolkit.Lang;
using Kudo.Avalonia.Designer;
using System.Windows.Input;
using DocumentListLanguage = global::Localization.Kudo_Avalonia.Designer.GraphDocumentList;

namespace Kudo.Avalonia.Designer;

public sealed class GraphDocumentList : GraphDesignerPanel
{
    public static readonly StyledProperty<GraphDocumentManager?> ManagerProperty =
        AvaloniaProperty.Register<GraphDocumentList, GraphDocumentManager?>(nameof(Manager));

    public static readonly StyledProperty<ICommand?> DocumentRequestedCommandProperty =
        AvaloniaProperty.Register<GraphDocumentList, ICommand?>(nameof(DocumentRequestedCommand));

    public static readonly StyledProperty<ICommand?> RenameRequestedCommandProperty =
        AvaloniaProperty.Register<GraphDocumentList, ICommand?>(nameof(RenameRequestedCommand));

    public static readonly StyledProperty<ICommand?> DuplicateRequestedCommandProperty =
        AvaloniaProperty.Register<GraphDocumentList, ICommand?>(nameof(DuplicateRequestedCommand));

    public static readonly StyledProperty<ICommand?> CloseRequestedCommandProperty =
        AvaloniaProperty.Register<GraphDocumentList, ICommand?>(nameof(CloseRequestedCommand));

    private readonly StackPanel _documents = new() { Spacing = 2 };
    private readonly HashSet<GraphDocumentSession> _subscribedSessions = [];
    private GraphDocumentManager? _subscribedManager;
    private Border? _root;
    private string _emptyText = GraphLocalization.Get(DocumentListLanguage.EmptyText);
    private bool _isEmptyTextCustomized;

    public GraphDocumentList()
    {
        _root = new Border
        {
            Background = PanelBrush,
            Child = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Margin = new Thickness(4),
                    Spacing = 2,
                    Children =
                    {
                        _documents
                    }
                }
            }
        };
        _root.Classes.Add("graph-document-list-root");
        Content = _root;
        Refresh();
    }

    public GraphDocumentList(GraphDocumentManager manager) : this()
    {
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public GraphDocumentManager? Manager
    {
        get => GetValue(ManagerProperty);
        set => SetValue(ManagerProperty, value);
    }

    public ICommand? DocumentRequestedCommand
    {
        get => GetValue(DocumentRequestedCommandProperty);
        set => SetValue(DocumentRequestedCommandProperty, value);
    }

    public ICommand? RenameRequestedCommand
    {
        get => GetValue(RenameRequestedCommandProperty);
        set => SetValue(RenameRequestedCommandProperty, value);
    }

    public ICommand? DuplicateRequestedCommand
    {
        get => GetValue(DuplicateRequestedCommandProperty);
        set => SetValue(DuplicateRequestedCommandProperty, value);
    }

    public ICommand? CloseRequestedCommand
    {
        get => GetValue(CloseRequestedCommandProperty);
        set => SetValue(CloseRequestedCommandProperty, value);
    }

    public int TaskCount { get; private set; }

    public GraphDocumentSession? SelectedSession => Manager?.ActiveSession;

    public string EmptyText
    {
        get => _emptyText;
        set
        {
            _isEmptyTextCustomized = true;
            _emptyText = value ?? string.Empty;
            Refresh();
        }
    }

    public event Action<GraphDocumentSession>? DocumentRequested;

    public event Action<GraphDocumentSession>? RenameRequested;

    public event Action<GraphDocumentSession>? DuplicateRequested;

    public event Action<GraphDocumentSession>? CloseRequested;

    public void RequestDocument(GraphDocumentSession session)
    {
        ValidateSession(session);
        DocumentRequested?.Invoke(session);
        ExecuteCommand(DocumentRequestedCommand, session);
    }

    public void RequestRename(GraphDocumentSession session) =>
        RequestAction(session, RenameRequested, RenameRequestedCommand);

    public void RequestDuplicate(GraphDocumentSession session) =>
        RequestAction(session, DuplicateRequested, DuplicateRequestedCommand);

    public void RequestClose(GraphDocumentSession session) =>
        RequestAction(session, CloseRequested, CloseRequestedCommand);

    public void Refresh()
    {
        _documents.Children.Clear();
        var manager = Manager;
        TaskCount = manager?.Sessions.Count ?? 0;
        if (manager is not null)
        {
            foreach (var session in manager.Sessions)
            {
                _documents.Children.Add(CreateDocumentButton(manager, session));
            }
        }

        if (TaskCount == 0)
        {
            _documents.Children.Add(new TextBlock
            {
                Text = EmptyText,
                Foreground = SecondaryTextBrush
            });
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        I18nManager.Instance.CultureChanged += HandleCultureChanged;
        SubscribeManager(Manager);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ManagerProperty)
        {
            return;
        }

        SubscribeManager(change.GetNewValue<GraphDocumentManager?>());
        Refresh();
    }

    protected override void OnPaletteChanged()
    {
        if (_root is not null)
        {
            _root.Background = PanelBrush;
        }

        Refresh();
    }

    private Button CreateDocumentButton(
        GraphDocumentManager manager,
        GraphDocumentSession session)
    {
        var title = string.IsNullOrWhiteSpace(session.Document.Title)
            ? L(DocumentListLanguage.UntitledTask)
            : session.Document.Title;
        var isActive = ReferenceEquals(manager.ActiveSession, session);
        var titleText = new TextBlock
        {
            Text = title,
            FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleText.Classes.Add("graph-document-title");
        var runtimeBadge = CreateRuntimeBadge(session.RuntimeStatus);
        var dirtyIndicator = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            IsVisible = session.IsDirty,
            VerticalAlignment = VerticalAlignment.Center
        };
        dirtyIndicator.Classes.Add("graph-document-dirty-dot");
        var icon = new Border
        {
            Width = 22,
            Height = 22,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = L(DocumentListLanguage.TaskIcon),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        icon.Classes.Add("graph-document-icon");
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            ColumnSpacing = 6,
            Children =
            {
                icon,
                titleText,
                runtimeBadge,
                dirtyIndicator
            }
        };
        Grid.SetColumn(titleText, 1);
        Grid.SetColumn(runtimeBadge, 2);
        Grid.SetColumn(dirtyIndicator, 3);
        var button = new Button
        {
            Tag = session,
            Content = content,
            MinHeight = 34,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        button.Classes.Add("graph-document-item");
        button.Classes.Set("active", isActive);
        GraphToolTipHelper.SetText(
            button,
            F(DocumentListLanguage.Summary, title, session.Document.Nodes.Count, session.Document.Edges.Count));
        button.Click += (_, _) => RequestFromInteraction(session, RequestDocument);
        button.ContextMenu = CreateContextMenu(session);
        return button;
    }

    private ContextMenu CreateContextMenu(GraphDocumentSession session)
    {
        var rename = new MenuItem { Header = L(DocumentListLanguage.Rename) };
        rename.Click += (_, _) => RequestFromInteraction(session, RequestRename);
        var duplicate = new MenuItem { Header = L(DocumentListLanguage.DuplicateTask) };
        duplicate.Click += (_, _) => RequestFromInteraction(session, RequestDuplicate);
        var close = new MenuItem { Header = L(DocumentListLanguage.CloseTask) };
        close.Click += (_, _) => RequestFromInteraction(session, RequestClose);
        return new ContextMenu
        {
            ItemsSource = new object[]
            {
                rename,
                duplicate,
                new Separator(),
                close
            }
        };
    }

    private void RequestAction(
        GraphDocumentSession session,
        Action<GraphDocumentSession>? action,
        ICommand? command)
    {
        ValidateSession(session);
        DocumentRequested?.Invoke(session);
        ExecuteCommand(DocumentRequestedCommand, session);
        action?.Invoke(session);
        ExecuteCommand(command, session);
    }

    private void RequestFromInteraction(
        GraphDocumentSession session,
        Action<GraphDocumentSession> request)
    {
        if (Manager is { } manager && !manager.Sessions.Contains(session))
        {
            Refresh();
            return;
        }

        request(session);
    }

    private static void ExecuteCommand(ICommand? command, GraphDocumentSession session)
    {
        if (command?.CanExecute(session) == true)
        {
            command.Execute(session);
        }
    }

    private void ValidateSession(GraphDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (Manager is not null && !Manager.Sessions.Contains(session))
        {
            throw new InvalidOperationException("The target document session does not belong to the current manager.");
        }
    }

    private Border CreateRuntimeBadge(RuntimeStatus status)
    {
        var (text, brush) = status switch
        {
            RuntimeStatus.Starting => (L(DocumentListLanguage.RuntimeStarting), AccentBrush),
            RuntimeStatus.Running => (L(DocumentListLanguage.RuntimeRunning), SuccessBrush),
            RuntimeStatus.Paused => (L(DocumentListLanguage.RuntimePaused), WarningBrush),
            RuntimeStatus.Stopping => (L(DocumentListLanguage.RuntimeStopping), WarningBrush),
            RuntimeStatus.Faulted => (L(DocumentListLanguage.RuntimeFaulted), ErrorBrush),
            _ => (L(DocumentListLanguage.RuntimeStopped), SecondaryTextBrush)
        };
        return new Border
        {
            Padding = new Thickness(5, 1),
            CornerRadius = new CornerRadius(8),
            BorderBrush = brush,
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = brush
            }
        };
    }

    private void SubscribeManager(GraphDocumentManager? manager)
    {
        if (_subscribedManager is not null)
        {
            _subscribedManager.SessionsChanged -= HandleManagerChanged;
        }

        UnsubscribeSessions();
        _subscribedManager = manager;
        if (manager is null)
        {
            return;
        }

        manager.SessionsChanged -= HandleManagerChanged;
        manager.SessionsChanged += HandleManagerChanged;
        SubscribeSessions(manager.Sessions);
    }

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        if (!_isEmptyTextCustomized)
        {
            _emptyText = L(DocumentListLanguage.EmptyText);
        }

        Refresh();
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) => GraphLocalization.Format(key, arguments);

    private void SubscribeSessions(IEnumerable<GraphDocumentSession> sessions)
    {
        foreach (var session in sessions)
        {
            if (!_subscribedSessions.Add(session))
            {
                continue;
            }

            session.DirtyStateChanged += HandleSessionChanged;
            session.RuntimeStatusChanged += HandleSessionChanged;
        }
    }

    private void UnsubscribeSessions()
    {
        foreach (var session in _subscribedSessions)
        {
            session.DirtyStateChanged -= HandleSessionChanged;
            session.RuntimeStatusChanged -= HandleSessionChanged;
        }

        _subscribedSessions.Clear();
    }

    private void HandleManagerChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => HandleManagerChanged(sender, e));
            return;
        }

        UnsubscribeSessions();
        if (Manager is { } manager)
        {
            SubscribeSessions(manager.Sessions);
        }

        Refresh();
    }

    private void HandleSessionChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => HandleSessionChanged(sender, e));
            return;
        }

        Refresh();
    }

}
