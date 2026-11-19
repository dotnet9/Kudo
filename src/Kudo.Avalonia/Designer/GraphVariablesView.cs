using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Kudo.Avalonia;
using Kudo.Core.Runtime;
using CodeWF.Toolkit.Lang;
using VariablesLanguage = global::Localization.Kudo_Avalonia.Designer.GraphVariablesView;

namespace Kudo.Avalonia.Designer;

public sealed class GraphVariablesView : GraphDesignerPanel
{
    private readonly TextBlock _contextText;
    private readonly TextBox _watchExpression;
    private readonly Button _addWatchButton;
    private readonly TextBlock _watchSectionLabel;
    private readonly TextBlock _snapshotVariablesLabel;
    private readonly TextBlock _nameHeader;
    private readonly TextBlock _valueHeader;
    private readonly StackPanel _watches = new() { Spacing = 5 };
    private readonly StackPanel _variables = new() { Spacing = 5 };
    private Border? _root;
    private IGraphRuntimeSession? _session;

    public GraphVariablesView()
    {
        _contextText = new TextBlock
        {
            Text = L(VariablesLanguage.NoActiveCanvas),
            FontSize = 11,
            Foreground = SecondaryTextBrush
        };
        _watchExpression = new TextBox
        {
            Name = "WatchExpressionTextBox",
            PlaceholderText = L(VariablesLanguage.WatchPlaceholder),
            MinWidth = 220
        };
        _addWatchButton = new Button
        {
            Name = "AddWatchButton",
            Content = L(VariablesLanguage.Add),
            Padding = new Thickness(12, 5),
            IsEnabled = false
        };
        _addWatchButton.Click += AddWatch;
        var watchEditor = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 6,
            Children = { _watchExpression, _addWatchButton }
        };
        Grid.SetColumn(_addWatchButton, 1);
        _watchSectionLabel = CreateSectionLabel(L(VariablesLanguage.WatchSection));
        _snapshotVariablesLabel = CreateSectionLabel(L(VariablesLanguage.SnapshotVariables));
        _nameHeader = CreateHeaderText(L(VariablesLanguage.Name));
        _valueHeader = CreateHeaderText(L(VariablesLanguage.Value));
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
                        _contextText,
                        watchEditor,
                        _watchSectionLabel,
                        _watches,
                        _snapshotVariablesLabel,
                        CreateHeader(),
                        _variables
                    }
                }
            }
        };
        Content = _root;
        Refresh(null);
    }

    public IGraphRuntimeSession? Session => _session;

    public int VariableCount { get; private set; }

    public int WatchCount { get; private set; }

    public event Action<GraphRuntimeVariable>? VariableRequested;

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

    public async ValueTask<bool> AddWatchAsync(
        string expression,
        CancellationToken cancellationToken = default)
    {
        var session = _session;
        return session is not null &&
            await session.AddWatchAsync(expression, cancellationToken);
    }

    public bool RemoveWatch(string expression) =>
        _session?.RemoveWatch(expression) == true;

    public void Refresh(IGraphRuntimeSession? session)
    {
        _session = session;
        _watches.Children.Clear();
        _variables.Children.Clear();
        var snapshot = session?.CurrentSnapshot;
        VariableCount = snapshot?.Variables.Count ?? 0;
        WatchCount = session?.WatchExpressions.Count ?? 0;
        _addWatchButton.IsEnabled = session is not null;
        var documentTitle = session?.Document.Title;
        _contextText.Text = session is null
            ? L(VariablesLanguage.NoActiveCanvas)
            : F(
                VariablesLanguage.Summary,
                string.IsNullOrWhiteSpace(documentTitle)
                    ? L(VariablesLanguage.UntitledCanvas)
                    : documentTitle,
                WatchCount,
                VariableCount);
        if (session is null || WatchCount == 0)
        {
            _watches.Children.Add(CreateEmptyText(L(VariablesLanguage.WatchHint)));
        }
        else
        {
            foreach (var expression in session.WatchExpressions)
            {
                var result = snapshot?.WatchResults.FirstOrDefault(candidate =>
                    string.Equals(candidate.Expression, expression, StringComparison.Ordinal));
                _watches.Children.Add(CreateWatchRow(expression, result));
            }
        }

        if (snapshot?.Variables.Count is not > 0)
        {
            _variables.Children.Add(CreateEmptyText(
                L(VariablesLanguage.VariablesEmpty)));
            return;
        }

        foreach (var variable in snapshot.Variables)
        {
            _variables.Children.Add(CreateVariableRow(variable));
        }
    }

    public void RequestVariable(GraphRuntimeVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        VariableRequested?.Invoke(variable);
    }

    protected override void OnPaletteChanged()
    {
        if (_root is not null)
        {
            _root.Background = PanelBrush;
        }

        _contextText.Foreground = SecondaryTextBrush;
        Refresh(_session);
    }

    private async void AddWatch(object? sender, RoutedEventArgs e)
    {
        var expression = _watchExpression.Text;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return;
        }

        _addWatchButton.IsEnabled = false;
        try
        {
            if (await AddWatchAsync(expression))
            {
                _watchExpression.Clear();
            }
        }
        catch (Exception exception)
        {
            _contextText.Text = F(VariablesLanguage.AddWatchFailed, exception.Message);
        }
        finally
        {
            _addWatchButton.IsEnabled = _session is not null;
        }
    }

    private Control CreateWatchRow(string expression, GraphRuntimeWatchResult? result)
    {
        var valueText = result is null
            ? L(VariablesLanguage.WaitingForSnapshot)
            : result.IsSuccess
                ? FormatValue(result.Value)
                : FormatWatchError(result);
        var value = new TextBlock
        {
            Text = valueText,
            TextWrapping = TextWrapping.Wrap,
            Foreground = result?.IsSuccess == false ? ErrorBrush : AccentBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        var removeButton = new Button
        {
            Name = "RemoveWatchButton",
            Tag = expression,
            Content = L(VariablesLanguage.Delete),
            Padding = new Thickness(8, 3),
            VerticalAlignment = VerticalAlignment.Center
        };
        removeButton.Click += (_, _) => RemoveWatch(expression);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("4*,4*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = expression,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = PrimaryTextBrush,
                    VerticalAlignment = VerticalAlignment.Center
                },
                value,
                removeButton
            }
        };
        Grid.SetColumn(value, 1);
        Grid.SetColumn(removeButton, 2);
        return new Border
        {
            Padding = new Thickness(8, 5),
            BorderBrush = DividerBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = grid
        };
    }

    private TextBlock CreateSectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Foreground = SecondaryTextBrush
    };

    private TextBlock CreateEmptyText(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = SecondaryTextBrush
    };

    private Grid CreateHeader()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("5*,4*"),
            Children = { _nameHeader, _valueHeader }
        };
        Grid.SetColumn(grid.Children[1], 1);
        return grid;
    }

    private TextBlock CreateHeaderText(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Foreground = SecondaryTextBrush
    };

    private Control CreateVariableRow(GraphRuntimeVariable variable)
    {
        var context = new StackPanel
        {
            Spacing = 1,
            Children =
            {
                new TextBlock
                {
                    Text = variable.Name,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = PrimaryTextBrush
                },
                new TextBlock
                {
                    Text = FormatContext(variable),
                    FontSize = 10,
                    Foreground = SecondaryTextBrush
                }
            }
        };
        var value = new TextBlock
        {
            Text = FormatValue(variable.Value),
            FontFamily = FontFamily.Default,
            TextWrapping = TextWrapping.Wrap,
            Foreground = AccentBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("5*,4*"),
            ColumnSpacing = 8,
            Children = { context, value }
        };
        Grid.SetColumn(value, 1);
        var button = new Button
        {
            Name = "VariableButton",
            Tag = variable,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 5),
            Content = grid
        };
        if (variable.NodeId is { } nodeId && _session?.Document.FindNode(nodeId) is not null)
        {
            button.Click += (_, _) => RequestVariable(variable);
        }
        else
        {
            button.IsHitTestVisible = false;
        }

        return button;
    }

    private static string FormatContext(GraphRuntimeVariable variable)
    {
        var scope = string.IsNullOrWhiteSpace(variable.Scope) ? "Runtime" : variable.Scope;
        var typeName = string.IsNullOrWhiteSpace(variable.TypeName) ? "object" : variable.TypeName;
        return variable.NodeId is null
            ? $"{scope} · {typeName}"
            : F(VariablesLanguage.NodeContext, scope, typeName, variable.NodeId);
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string FormatWatchError(GraphRuntimeWatchResult result) =>
        result.ErrorCode == GraphRuntimeWatchErrorCodes.VariableNotFound
            ? L(VariablesLanguage.VariableNotFound)
            : result.Error ?? L(VariablesLanguage.EvaluationFailed);

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        _watchExpression.PlaceholderText = L(VariablesLanguage.WatchPlaceholder);
        _addWatchButton.Content = L(VariablesLanguage.Add);
        _watchSectionLabel.Text = L(VariablesLanguage.WatchSection);
        _snapshotVariablesLabel.Text = L(VariablesLanguage.SnapshotVariables);
        _nameHeader.Text = L(VariablesLanguage.Name);
        _valueHeader.Text = L(VariablesLanguage.Value);
        Refresh(_session);
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
