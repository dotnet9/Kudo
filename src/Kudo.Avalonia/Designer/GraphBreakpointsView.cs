using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Kudo.Avalonia;
using Kudo.Core.Runtime;
using CodeWF.Toolkit.Lang;
using BreakpointsLanguage = global::Localization.Kudo_Avalonia.Designer.GraphBreakpointsView;

namespace Kudo.Avalonia.Designer;

public sealed class GraphBreakpointEditorContext
{
    private readonly Action<string> _locateNode;
    private readonly Action<IReadOnlyList<string>> _locateNodes;
    private readonly Action<string> _setMessage;

    public GraphBreakpointEditorContext(
        IGraphRuntimeSession session,
        Action<string>? locateNode = null,
        Action<string>? setMessage = null,
        Action<IReadOnlyList<string>>? locateNodes = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _locateNode = locateNode ?? (_ => { });
        _locateNodes = locateNodes ?? (_ => { });
        _setMessage = setMessage ?? (_ => { });
    }

    public IGraphRuntimeSession Session { get; }

    public void LocateNode(string nodeId) => _locateNode(nodeId);

    public void LocateNodes(IReadOnlyList<string> nodeIds) => _locateNodes(nodeIds);

    public void SetMessage(string message) => _setMessage(message);
}

public sealed class GraphBreakpointsView : GraphDesignerPanel
{
    private readonly TextBlock _contextText;
    private readonly Button _clearButton;
    private readonly Button _selectAllButton;
    private readonly StackPanel _items = new() { Spacing = 5 };
    private Border? _root;
    private GraphBreakpointEditorContext? _editorContext;

    public GraphBreakpointsView()
    {
        _contextText = new TextBlock
        {
            Text = L(BreakpointsLanguage.NoActiveCanvas),
            FontSize = 11,
            Foreground = SecondaryTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        _clearButton = new Button
        {
            Name = "ClearBreakpointsButton",
            Content = L(BreakpointsLanguage.ClearAll),
            Padding = new Thickness(8, 4),
            IsEnabled = false
        };
        _clearButton.Click += ClearBreakpoints;
        _selectAllButton = new Button
        {
            Name = "SelectAllBreakpointsButton",
            Content = L(BreakpointsLanguage.SelectAll),
            Padding = new Thickness(8, 4),
            IsEnabled = false
        };
        _selectAllButton.Click += SelectAllBreakpoints;
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 8,
            Children = { _contextText, _selectAllButton, _clearButton }
        };
        Grid.SetColumn(_selectAllButton, 1);
        Grid.SetColumn(_clearButton, 2);
        _root = new Border
        {
            Background = PanelBrush,
            Child = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Margin = new Thickness(10),
                    Spacing = 8,
                    Children =
                    {
                        header,
                        _items
                    }
                }
            }
        };
        Content = _root;
        Refresh(null);
    }

    public int BreakpointCount { get; private set; }

    public GraphBreakpointEditorContext? EditorContext => _editorContext;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        I18nManager.Instance.CultureChanged += HandleCultureChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        base.OnDetachedFromVisualTree(e);
    }

    public void Refresh(GraphBreakpointEditorContext? editorContext)
    {
        _editorContext = editorContext;
        _items.Children.Clear();
        var breakpointNodeIds = editorContext?.Session.BreakpointNodeIds.ToArray() ?? [];
        BreakpointCount = breakpointNodeIds.Length;
        _clearButton.IsEnabled = BreakpointCount > 0;
        _selectAllButton.IsEnabled = BreakpointCount > 0;
        var documentTitle = editorContext?.Session.Document.Title;
        _contextText.Text = editorContext is null
            ? L(BreakpointsLanguage.NoActiveCanvas)
            : F(
                BreakpointsLanguage.Summary,
                string.IsNullOrWhiteSpace(documentTitle)
                    ? L(BreakpointsLanguage.UntitledCanvas)
                    : documentTitle,
                BreakpointCount);
        if (editorContext is null || breakpointNodeIds.Length == 0)
        {
            _items.Children.Add(new TextBlock
            {
                Text = L(BreakpointsLanguage.EmptyHint),
                TextWrapping = TextWrapping.Wrap,
                Foreground = SecondaryTextBrush
            });
            return;
        }

        foreach (var nodeId in breakpointNodeIds)
        {
            _items.Children.Add(CreateBreakpointRow(editorContext, nodeId));
        }
    }

    protected override void OnPaletteChanged()
    {
        if (_root is not null)
        {
            _root.Background = PanelBrush;
        }

        _contextText.Foreground = SecondaryTextBrush;
        Refresh(_editorContext);
    }

    private Control CreateBreakpointRow(
        GraphBreakpointEditorContext editorContext,
        string nodeId)
    {
        var node = editorContext.Session.Document.FindNode(nodeId);
        var title = node is null || string.IsNullOrWhiteSpace(node.Name) ? nodeId : node.Name;
        var locate = new Button
        {
            Name = "LocateBreakpointButton",
            Tag = nodeId,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 5),
            Content = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = PrimaryTextBrush
                    },
                    new TextBlock
                    {
                        Text = F(BreakpointsLanguage.NodeContext, nodeId),
                        FontSize = 10,
                        Foreground = SecondaryTextBrush
                    }
                }
            }
        };
        locate.Click += (_, _) => editorContext.LocateNode(nodeId);
        var remove = new Button
        {
            Name = "RemoveBreakpointButton",
            Tag = nodeId,
            Content = L(BreakpointsLanguage.Remove),
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        remove.Click += (_, _) =>
        {
            if (editorContext.Session.RemoveBreakpoint(nodeId))
            {
                editorContext.SetMessage(F(BreakpointsLanguage.RemovedMessage, nodeId));
            }
        };
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 6,
            Children = { locate, remove }
        };
        Grid.SetColumn(remove, 1);
        return row;
    }

    private void ClearBreakpoints(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var runtimeSession = _editorContext?.Session;
        var breakpointCount = runtimeSession?.BreakpointNodeIds.Count ?? 0;
        if (_editorContext is null || runtimeSession is null || breakpointCount == 0)
        {
            return;
        }

        runtimeSession.ClearBreakpoints();
        _editorContext.SetMessage(F(BreakpointsLanguage.ClearedMessage, breakpointCount));
    }

    private void SelectAllBreakpoints(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var nodeIds = _editorContext?.Session.BreakpointNodeIds.ToArray() ?? [];
        if (_editorContext is null || nodeIds.Length == 0)
        {
            return;
        }

        _editorContext.LocateNodes(nodeIds);
        _editorContext.SetMessage(F(BreakpointsLanguage.SelectedMessage, nodeIds.Length));
    }

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        _clearButton.Content = L(BreakpointsLanguage.ClearAll);
        _selectAllButton.Content = L(BreakpointsLanguage.SelectAll);
        Refresh(_editorContext);
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
