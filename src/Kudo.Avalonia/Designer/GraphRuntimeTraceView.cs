using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kudo.Avalonia;
using Kudo.Core.Runtime;
using CodeWF.Toolkit.Lang;
using RuntimeTraceLanguage = global::Localization.Kudo_Avalonia.Designer.GraphRuntimeTraceView;

namespace Kudo.Avalonia.Designer;

public enum GraphRuntimeTraceFilter
{
    [Description("全部诊断")]
    All,

    [Description("跟踪")]
    Trace,

    [Description("信息")]
    Information,

    [Description("警告")]
    Warning,

    [Description("错误")]
    Error
}

public sealed class GraphRuntimeTraceView : GraphDesignerPanel
{
    private readonly TextBlock _summaryText;
    private readonly Button _clearButton;
    private readonly Button _selectVisibleButton;
    private readonly StackPanel _filters = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6
    };
    private readonly StackPanel _items = new() { Spacing = 5 };
    private readonly Dictionary<GraphRuntimeTraceFilter, ToggleButton> _filterButtons = [];
    private Border? _root;
    private IGraphRuntimeSession? _session;
    private bool _isAttached;

    public GraphRuntimeTraceView()
    {
        _summaryText = new TextBlock
        {
            Text = L(RuntimeTraceLanguage.NoActiveCanvas),
            FontSize = 11,
            Foreground = SecondaryTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        _clearButton = new Button
        {
            Name = "RuntimeTraceClearButton",
            Content = L(RuntimeTraceLanguage.Clear),
            Padding = new Thickness(8, 4),
            IsEnabled = false
        };
        _clearButton.Click += (_, _) => _session?.ClearDiagnostics();
        _selectVisibleButton = new Button
        {
            Name = "RuntimeTraceSelectVisibleDiagnostics",
            Content = L(RuntimeTraceLanguage.SelectNodes),
            Padding = new Thickness(8, 4),
            IsEnabled = false
        };
        _selectVisibleButton.Click += (_, _) => RequestVisibleDiagnostics();
        AddFilterButton(GraphRuntimeTraceFilter.All);
        AddFilterButton(GraphRuntimeTraceFilter.Trace);
        AddFilterButton(GraphRuntimeTraceFilter.Information);
        AddFilterButton(GraphRuntimeTraceFilter.Warning);
        AddFilterButton(GraphRuntimeTraceFilter.Error);
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 8,
            Children = { _summaryText, _selectVisibleButton, _clearButton }
        };
        Grid.SetColumn(_selectVisibleButton, 1);
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
                        _filters,
                        _items
                    }
                }
            }
        };
        Content = _root;
        Rebuild();
    }

    public IGraphRuntimeSession? Session => _session;

    public int TraceCount { get; private set; }

    public int TraceLevelCount { get; private set; }

    public int InformationCount { get; private set; }

    public int WarningCount { get; private set; }

    public int ErrorCount { get; private set; }

    public GraphRuntimeTraceFilter Filter { get; private set; } = GraphRuntimeTraceFilter.All;

    public IReadOnlyList<GraphRuntimeDiagnostic> VisibleDiagnostics =>
        (_session?.RecentDiagnostics ?? []).Where(MatchesFilter).ToArray();

    public event Action<GraphRuntimeDiagnostic>? DiagnosticRequested;

    public event Action<IReadOnlyList<GraphRuntimeDiagnostic>>? VisibleDiagnosticsRequested;

    public void Refresh(IGraphRuntimeSession? session)
    {
        if (!ReferenceEquals(_session, session))
        {
            UnsubscribeSession();
            _session = session;
            SubscribeSession();
        }

        Rebuild();
    }

    public void RequestDiagnostic(GraphRuntimeDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        DiagnosticRequested?.Invoke(diagnostic);
    }

    public void RequestVisibleDiagnostics() =>
        VisibleDiagnosticsRequested?.Invoke(VisibleDiagnostics
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.NodeId))
            .ToArray());

    public void SetFilter(GraphRuntimeTraceFilter filter)
    {
        if (!Enum.IsDefined(filter))
        {
            throw new ArgumentOutOfRangeException(nameof(filter));
        }

        Filter = filter;
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        I18nManager.Instance.CultureChanged += HandleCultureChanged;
        SubscribeSession();
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        UnsubscribeSession();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPaletteChanged()
    {
        if (_root is not null)
        {
            _root.Background = PanelBrush;
        }

        _summaryText.Foreground = SecondaryTextBrush;
        Rebuild();
    }

    private void SubscribeSession()
    {
        if (!_isAttached || _session is null)
        {
            return;
        }

        _session.DiagnosticsChanged -= HandleDiagnosticsChanged;
        _session.DiagnosticsChanged += HandleDiagnosticsChanged;
    }

    private void UnsubscribeSession()
    {
        if (_session is not null)
        {
            _session.DiagnosticsChanged -= HandleDiagnosticsChanged;
        }
    }

    private void HandleDiagnosticsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Rebuild);
            return;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        _items.Children.Clear();
        var diagnostics = _session?.RecentDiagnostics ?? [];
        TraceCount = diagnostics.Count;
        TraceLevelCount = diagnostics.Count(diagnostic =>
            diagnostic.Severity == GraphRuntimeDiagnosticSeverity.Trace);
        InformationCount = diagnostics.Count(diagnostic =>
            diagnostic.Severity == GraphRuntimeDiagnosticSeverity.Information);
        WarningCount = diagnostics.Count(diagnostic =>
            diagnostic.Severity == GraphRuntimeDiagnosticSeverity.Warning);
        ErrorCount = diagnostics.Count(diagnostic =>
            diagnostic.Severity == GraphRuntimeDiagnosticSeverity.Error);
        UpdateFilterButtons();
        _clearButton.IsEnabled = TraceCount > 0;
        _selectVisibleButton.IsEnabled = VisibleDiagnostics.Any(diagnostic =>
            !string.IsNullOrWhiteSpace(diagnostic.NodeId));
        var title = _session?.Document.Title;
        _summaryText.Text = _session is null
            ? L(RuntimeTraceLanguage.NoActiveCanvas)
            : F(
                RuntimeTraceLanguage.Summary,
                string.IsNullOrWhiteSpace(title)
                    ? L(RuntimeTraceLanguage.UntitledCanvas)
                    : title,
                TraceCount,
                ErrorCount);
        if (TraceCount == 0)
        {
            _items.Children.Add(new TextBlock
            {
                Text = L(RuntimeTraceLanguage.EmptyHint),
                TextWrapping = TextWrapping.Wrap,
                Foreground = SecondaryTextBrush
            });
            return;
        }

        var visibleDiagnostics = diagnostics.Where(MatchesFilter).ToArray();
        if (visibleDiagnostics.Length == 0)
        {
            _items.Children.Add(new TextBlock
            {
                Text = L(RuntimeTraceLanguage.NoRecordsAtCurrentLevel),
                TextWrapping = TextWrapping.Wrap,
                Foreground = SecondaryTextBrush
            });
            return;
        }

        foreach (var diagnostic in visibleDiagnostics.AsEnumerable().Reverse())
        {
            _items.Children.Add(CreateTraceRow(diagnostic));
        }
    }

    private void AddFilterButton(GraphRuntimeTraceFilter filter)
    {
        var button = new ToggleButton
        {
            Name = $"RuntimeTraceFilter{filter}",
            Tag = filter,
            Padding = new Thickness(7, 4),
            MinWidth = 52
        };
        button.Click += (_, _) => SetFilter(filter);
        _filterButtons.Add(filter, button);
        _filters.Children.Add(button);
    }

    private void UpdateFilterButtons()
    {
        foreach (var (filter, button) in _filterButtons)
        {
            button.IsChecked = filter == Filter;
            var brush = GetFilterBrush(filter);
            button.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children =
                {
                    new TextBlock
                    {
                        Text = GetFilterLabel(filter),
                        Foreground = PrimaryTextBrush,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new Border
                    {
                        Padding = new Thickness(5, 1),
                        CornerRadius = new CornerRadius(8),
                        Background = ElevatedBrush,
                        BorderBrush = brush,
                        BorderThickness = new Thickness(1),
                        Child = new TextBlock
                        {
                            Text = GetFilterCount(filter).ToString(),
                            FontSize = 10,
                            Foreground = brush
                        }
                    }
                }
            };
        }
    }

    private int GetFilterCount(GraphRuntimeTraceFilter filter) => filter switch
    {
        GraphRuntimeTraceFilter.All => TraceCount,
        GraphRuntimeTraceFilter.Trace => TraceLevelCount,
        GraphRuntimeTraceFilter.Information => InformationCount,
        GraphRuntimeTraceFilter.Warning => WarningCount,
        GraphRuntimeTraceFilter.Error => ErrorCount,
        _ => 0
    };

    private static string GetFilterLabel(GraphRuntimeTraceFilter filter) => filter switch
    {
        GraphRuntimeTraceFilter.All => L(RuntimeTraceLanguage.FilterAll),
        GraphRuntimeTraceFilter.Trace => L(RuntimeTraceLanguage.FilterTrace),
        GraphRuntimeTraceFilter.Information => L(RuntimeTraceLanguage.FilterInformation),
        GraphRuntimeTraceFilter.Warning => L(RuntimeTraceLanguage.FilterWarning),
        GraphRuntimeTraceFilter.Error => L(RuntimeTraceLanguage.FilterError),
        _ => string.Empty
    };

    private bool MatchesFilter(GraphRuntimeDiagnostic diagnostic) => Filter switch
    {
        GraphRuntimeTraceFilter.All => true,
        GraphRuntimeTraceFilter.Trace => diagnostic.Severity == GraphRuntimeDiagnosticSeverity.Trace,
        GraphRuntimeTraceFilter.Information => diagnostic.Severity == GraphRuntimeDiagnosticSeverity.Information,
        GraphRuntimeTraceFilter.Warning => diagnostic.Severity == GraphRuntimeDiagnosticSeverity.Warning,
        GraphRuntimeTraceFilter.Error => diagnostic.Severity == GraphRuntimeDiagnosticSeverity.Error,
        _ => false
    };

    private IBrush GetFilterBrush(GraphRuntimeTraceFilter filter) => filter switch
    {
        GraphRuntimeTraceFilter.Warning => WarningBrush,
        GraphRuntimeTraceFilter.Error => ErrorBrush,
        GraphRuntimeTraceFilter.Trace => SecondaryTextBrush,
        _ => AccentBrush
    };

    private Control CreateTraceRow(GraphRuntimeDiagnostic diagnostic)
    {
        var severityBrush = diagnostic.Severity switch
        {
            GraphRuntimeDiagnosticSeverity.Error => ErrorBrush,
            GraphRuntimeDiagnosticSeverity.Warning => WarningBrush,
            GraphRuntimeDiagnosticSeverity.Information => AccentBrush,
            _ => SecondaryTextBrush
        };
        var marker = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            Background = severityBrush
        };
        var detail = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = diagnostic.Message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = PrimaryTextBrush
                },
                new TextBlock
                {
                    Text = FormatContext(diagnostic),
                    FontSize = 10,
                    Foreground = SecondaryTextBrush
                }
            }
        };
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("4,*"),
            ColumnSpacing = 8,
            Children = { marker, detail }
        };
        Grid.SetColumn(detail, 1);
        var button = new Button
        {
            Tag = diagnostic,
            Padding = new Thickness(8, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            BorderBrush = DividerBrush,
            BorderThickness = new Thickness(1),
            Content = content,
            IsHitTestVisible = !string.IsNullOrWhiteSpace(diagnostic.NodeId)
        };
        if (button.IsHitTestVisible)
        {
            button.Click += (_, _) => RequestDiagnostic(diagnostic);
        }

        return button;
    }

    private static string FormatContext(GraphRuntimeDiagnostic diagnostic)
    {
        var timestamp = diagnostic.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        return string.IsNullOrWhiteSpace(diagnostic.NodeId)
            ? F(RuntimeTraceLanguage.Context, timestamp, diagnostic.TickCount, diagnostic.Code)
            : F(
                RuntimeTraceLanguage.ContextWithNode,
                timestamp,
                diagnostic.TickCount,
                diagnostic.Code,
                diagnostic.NodeId);
    }

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        _clearButton.Content = L(RuntimeTraceLanguage.Clear);
        _selectVisibleButton.Content = L(RuntimeTraceLanguage.SelectNodes);
        Rebuild();
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
