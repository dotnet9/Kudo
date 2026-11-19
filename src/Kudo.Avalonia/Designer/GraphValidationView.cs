using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Kudo.Avalonia;
using CodeWF.Toolkit.Lang;
using ValidationLanguage = global::Localization.Kudo_Avalonia.Designer.GraphValidationView;

namespace Kudo.Avalonia.Designer;

public enum GraphValidationFilter
{
    [Description("全部问题")]
    All,

    [Description("错误")]
    Error,

    [Description("警告")]
    Warning,

    [Description("提示")]
    Info
}

public sealed class GraphValidationView : GraphDesignerPanel
{
    private readonly TextBlock _summaryText;
    private readonly StackPanel _filters = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6
    };
    private readonly StackPanel _issues = new() { Spacing = 5 };
    private readonly Dictionary<GraphValidationFilter, ToggleButton> _filterButtons = [];
    private readonly Button _selectVisibleButton = new()
    {
        Name = "ValidationSelectVisibleIssues",
        Content = GraphLocalization.Get(ValidationLanguage.SelectIssues),
        Padding = new Thickness(7, 4)
    };
    private IGraphValidationRule[] _rules =
    [
        new GraphStructureValidationRule(new LocalizedGraphValidationTextProvider())
    ];
    private GraphDocument? _document;
    private IReadOnlyList<GraphValidationIssue> _currentIssues = [];
    private Border? _root;

    public GraphValidationView()
    {
        _summaryText = new TextBlock
        {
            Text = L(ValidationLanguage.NoActiveCanvas),
            FontSize = 11,
            Foreground = SecondaryTextBrush
        };
        AddFilterButton(GraphValidationFilter.All);
        AddFilterButton(GraphValidationFilter.Error);
        AddFilterButton(GraphValidationFilter.Warning);
        AddFilterButton(GraphValidationFilter.Info);
        _selectVisibleButton.Click += (_, _) => RequestVisibleIssues();
        _filters.Children.Add(_selectVisibleButton);
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
                        _summaryText,
                        _filters,
                        _issues
                    }
                }
            }
        };
        Content = _root;
        Refresh(null);
    }

    public GraphValidationView(IEnumerable<IGraphValidationRule> rules) : this()
    {
        Rules = rules.ToArray();
    }

    public IReadOnlyList<IGraphValidationRule> Rules
    {
        get => _rules;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var rules = value.ToArray();
            if (rules.Any(rule => rule is null))
            {
                throw new ArgumentException(L(ValidationLanguage.RulesContainNull), nameof(value));
            }

            _rules = rules;
            Refresh(_document);
        }
    }

    public GraphDocument? Document => _document;

    public IReadOnlyList<GraphValidationIssue> CurrentIssues => _currentIssues;

    public IReadOnlyList<GraphValidationIssue> VisibleIssues =>
        _currentIssues.Where(MatchesFilter).ToArray();

    public GraphValidationFilter Filter { get; private set; } = GraphValidationFilter.All;

    public int IssueCount => _currentIssues.Count;

    public int ErrorCount { get; private set; }

    public int WarningCount { get; private set; }

    public int InfoCount { get; private set; }

    public event Action<GraphValidationIssue>? IssueRequested;

    public event Action<IReadOnlyList<GraphValidationIssue>>? VisibleIssuesRequested;

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

    protected override void OnPaletteChanged()
    {
        if (_root is not null)
        {
            _root.Background = PanelBrush;
        }

        _summaryText.Foreground = SecondaryTextBrush;
        RenderIssues();
    }

    public void Refresh(GraphDocument? document)
    {
        _document = document;
        if (document is null)
        {
            SetIssues([]);
            RenderIssues();
            return;
        }

        try
        {
            SetIssues(new GraphValidator(_rules).Validate(document));
        }
        catch (Exception exception)
        {
            SetIssues([
                new GraphValidationIssue(
                    ValidationSeverity.Error,
                    F(ValidationLanguage.ValidationFailed, exception.Message),
                    Code: "GK0000")
            ]);
        }

        RenderIssues();
    }

    public void SetFilter(GraphValidationFilter filter)
    {
        if (!Enum.IsDefined(filter))
        {
            throw new ArgumentOutOfRangeException(nameof(filter));
        }

        Filter = filter;
        RenderIssues();
    }

    public void RequestIssue(GraphValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        IssueRequested?.Invoke(issue);
    }

    public void RequestVisibleIssues() =>
        VisibleIssuesRequested?.Invoke(VisibleIssues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.ElementId))
            .ToArray());

    private void RenderIssues()
    {
        _issues.Children.Clear();
        UpdateFilterButtons();
        _selectVisibleButton.IsEnabled = false;
        if (_document is null)
        {
            _summaryText.Text = L(ValidationLanguage.NoActiveCanvas);
            AddEmptyState(L(ValidationLanguage.SelectTaskHint), SecondaryTextBrush);
            return;
        }

        var title = string.IsNullOrWhiteSpace(_document.Title)
            ? L(ValidationLanguage.UntitledCanvas)
            : _document.Title;
        _summaryText.Text = F(ValidationLanguage.Summary, title, IssueCount);
        var visibleIssues = VisibleIssues;
        _selectVisibleButton.IsEnabled = visibleIssues.Any(issue => !string.IsNullOrWhiteSpace(issue.ElementId));
        if (visibleIssues.Count == 0)
        {
            var message = IssueCount == 0
                ? L(ValidationLanguage.NoIssues)
                : L(ValidationLanguage.NoIssuesAtCurrentLevel);
            AddEmptyState(message, IssueCount == 0 ? SuccessBrush : SecondaryTextBrush);
            return;
        }

        foreach (var issue in visibleIssues)
        {
            _issues.Children.Add(CreateIssueRow(issue));
        }
    }

    private void SetIssues(IReadOnlyList<GraphValidationIssue> issues)
    {
        _currentIssues = issues;
        ErrorCount = issues.Count(issue => issue.Severity == ValidationSeverity.Error);
        WarningCount = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        InfoCount = issues.Count(issue => issue.Severity == ValidationSeverity.Info);
    }

    private void AddFilterButton(GraphValidationFilter filter)
    {
        var button = new ToggleButton
        {
            Name = $"ValidationFilter{filter}",
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

    private int GetFilterCount(GraphValidationFilter filter) => filter switch
    {
        GraphValidationFilter.All => IssueCount,
        GraphValidationFilter.Error => ErrorCount,
        GraphValidationFilter.Warning => WarningCount,
        GraphValidationFilter.Info => InfoCount,
        _ => 0
    };

    private static string GetFilterLabel(GraphValidationFilter filter) => filter switch
    {
        GraphValidationFilter.All => L(ValidationLanguage.FilterAll),
        GraphValidationFilter.Error => L(ValidationLanguage.FilterError),
        GraphValidationFilter.Warning => L(ValidationLanguage.FilterWarning),
        GraphValidationFilter.Info => L(ValidationLanguage.FilterInfo),
        _ => string.Empty
    };

    private bool MatchesFilter(GraphValidationIssue issue) => Filter switch
    {
        GraphValidationFilter.All => true,
        GraphValidationFilter.Error => issue.Severity == ValidationSeverity.Error,
        GraphValidationFilter.Warning => issue.Severity == ValidationSeverity.Warning,
        GraphValidationFilter.Info => issue.Severity == ValidationSeverity.Info,
        _ => false
    };

    private IBrush GetFilterBrush(GraphValidationFilter filter) => filter switch
    {
        GraphValidationFilter.Error => ErrorBrush,
        GraphValidationFilter.Warning => WarningBrush,
        _ => AccentBrush
    };

    private Control CreateIssueRow(GraphValidationIssue issue)
    {
        var severityBrush = GetSeverityBrush(issue.Severity);
        var detail = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = issue.Message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = PrimaryTextBrush
                },
                new TextBlock
                {
                    Text = FormatContext(issue),
                    FontSize = 10,
                    Foreground = SecondaryTextBrush
                }
            }
        };
        var badge = new Border
        {
            Padding = new Thickness(6, 2),
            BorderBrush = severityBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = FormatSeverity(issue.Severity),
                FontSize = 10,
                Foreground = severityBrush
            }
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
            Children = { badge, detail }
        };
        Grid.SetColumn(detail, 1);
        var button = new Button
        {
            Tag = issue,
            Padding = new Thickness(8, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            BorderBrush = DividerBrush,
            BorderThickness = new Thickness(1),
            Content = grid
        };
        button.Click += (_, _) => RequestIssue(issue);
        return button;
    }

    private void AddEmptyState(string text, IBrush foreground) =>
        _issues.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = foreground
        });

    private static string FormatContext(GraphValidationIssue issue)
    {
        var code = string.IsNullOrWhiteSpace(issue.Code)
            ? L(ValidationLanguage.Uncategorized)
            : issue.Code;
        return string.IsNullOrWhiteSpace(issue.ElementId)
            ? code
            : F(ValidationLanguage.ElementContext, code, issue.ElementId);
    }

    private static string FormatSeverity(ValidationSeverity severity) => severity switch
    {
        ValidationSeverity.Error => L(ValidationLanguage.SeverityError),
        ValidationSeverity.Warning => L(ValidationLanguage.SeverityWarning),
        _ => L(ValidationLanguage.SeverityInfo)
    };

    private IBrush GetSeverityBrush(ValidationSeverity severity) => severity switch
    {
        ValidationSeverity.Error => ErrorBrush,
        ValidationSeverity.Warning => WarningBrush,
        _ => AccentBrush
    };

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        _selectVisibleButton.Content = L(ValidationLanguage.SelectIssues);
        Refresh(_document);
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
