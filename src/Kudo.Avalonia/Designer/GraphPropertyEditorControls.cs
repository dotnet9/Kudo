// 属性编辑控件工厂：集中创建字段布局并绑定提交事件，避免各内置编辑器重复搭建 UI。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Kudo.Avalonia.Designer;
using UrsaIconButton = Ursa.Controls.IconButton;

namespace Kudo.Avalonia.Designer;

internal static class GraphPropertyEditorControls
{
    public static Border CreateInfo(string text)
    {
        var info = new Border
        {
            Margin = new Thickness(8, 6),
            Padding = new Thickness(8, 6),
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            }
        };
        info.Classes.Add("PropertyGroupHintPanel");
        return info;
    }

    public static Border CreateSectionTitle(string text)
    {
        var title = new Border
        {
            Child = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        title.Classes.Add("PropertyGroupHeader");
        title.Classes.Add("graph-property-section-title");
        return title;
    }

    public static Grid CreateReadOnlyField(string label, string value)
    {
        var valueText = new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        return CreatePropertyRow(label, valueText);
    }

    public static TextBox CreateTextEditor(string name, string watermark, string? text) => new()
    {
        Name = name,
        PlaceholderText = watermark,
        Text = text ?? string.Empty,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    public static NumericUpDown CreateNumericEditor(
        string name,
        decimal value,
        decimal minimum,
        decimal maximum,
        decimal increment,
        string formatString) =>
        new()
        {
            Name = name,
            Value = value,
            Minimum = minimum,
            Maximum = maximum,
            Increment = increment,
            FormatString = formatString,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ShowButtonSpinner = true
        };

    public static GraphPercentageEditor CreatePercentageSliderEditor(
        string name,
        double value,
        double minimum,
        double maximum,
        double tickFrequency = 1) =>
        new(name, value, minimum, maximum, tickFrequency);

    public static GraphColorEditor CreateColorEditor(
        string name,
        string? value,
        string clearTooltip) =>
        new(name, value, clearTooltip);

    public static void SetEditorReadOnly(TextBox editor, bool isReadOnly)
    {
        editor.IsReadOnly = isReadOnly;
        editor.IsTabStop = !isReadOnly;
    }

    public static ComboBox CreateEnumEditor<T>(
        string name,
        T value,
        Func<T, string> format) where T : struct, Enum
    {
        var items = Enum.GetValues<T>()
            .Select(enumValue => new ComboBoxItem
            {
                Content = format(enumValue),
                Tag = enumValue
            })
            .ToArray();
        return new ComboBox
        {
            Name = name,
            ItemsSource = items,
            SelectedItem = items.Single(item => Equals(item.Tag, value)),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    public static T GetEnumValue<T>(ComboBox editor, T fallback) where T : struct, Enum =>
        editor.SelectedItem is ComboBoxItem { Tag: T value } ? value : fallback;

    /// <summary>
    /// 文本框失焦时提交；单行编辑器还可使用 Enter 提交。
    /// </summary>
    public static void AttachTextCommit(
        TextBox editor,
        Action commit,
        bool commitOnEnter = true)
    {
        editor.LostFocus += (_, _) => commit();
        if (!commitOnEnter)
        {
            return;
        }

        editor.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            commit();
            e.Handled = true;
        };
    }

    public static void AttachSelectionCommit(ComboBox editor, Action commit) =>
        editor.SelectionChanged += (_, _) => commit();

    public static void AttachNumericCommit(NumericUpDown editor, Action commit) =>
        editor.ValueChanged += (_, _) => commit();

    public static void AttachColorCommit(GraphColorEditor editor, Action commit) =>
        editor.ValueChanged += (_, _) => commit();

    public static Grid CreateField(string label, Control editor)
    {
        editor.Classes.Add("graph-property-editor");
        return CreatePropertyRow(label, editor);
    }

    public static Grid CreatePropertyRow(string label, Control value)
    {
        var labelHost = new Border
        {
            Child = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        labelHost.Classes.Add("graph-property-key");
        var valueHost = new Border
        {
            Child = value
        };
        valueHost.Classes.Add("graph-property-value");
        var row = new Grid
        {
            MinHeight = 32,
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Children = { labelHost, valueHost }
        };
        row.Classes.Add("graph-property-row");
        Grid.SetColumn(valueHost, 1);
        return row;
    }

    public static Button CreateActionButton(string name, string content) => new()
    {
        Name = name,
        Content = content,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Margin = new Thickness(8, 8)
    };

    public static UrsaIconButton CreateIconActionButton(string name, string tooltip, string iconResourceKey)
    {
        var button = new UrsaIconButton
        {
            Name = name,
            Width = 26,
            Height = 26,
            MinWidth = 26,
            MinHeight = 26,
            Padding = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("graph-property-icon-button");
        GraphToolTipHelper.SetText(button, tooltip);
        button[!UrsaIconButton.IconProperty] = new DynamicResourceExtension(iconResourceKey);
        button[!StyledElement.ThemeProperty] = new DynamicResourceExtension("BorderlessIconButton");
        return button;
    }
}

internal sealed class GraphColorEditor : Grid
{
    private bool _hasExplicitColor;

    public GraphColorEditor(string name, string? value, string clearTooltip)
    {
        _hasExplicitColor = TryParseColor(value, out var color);
        Picker = new ColorPicker
        {
            Name = name,
            Color = _hasExplicitColor ? color : Colors.Black,
            IsAlphaEnabled = true,
            IsAlphaVisible = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        Picker[!StyledElement.ThemeProperty] = new DynamicResourceExtension("HexSimpleColorPicker");
        ClearButton = GraphPropertyEditorControls.CreateIconActionButton(
            $"{name}ClearButton",
            clearTooltip,
            "SemiIconClose");
        ClearButton.IsVisible = _hasExplicitColor;
        ClearButton.Click += (_, _) =>
        {
            if (!_hasExplicitColor)
            {
                return;
            }

            _hasExplicitColor = false;
            ClearButton.IsVisible = false;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };
        Picker.ColorChanged += (_, _) =>
        {
            _hasExplicitColor = true;
            ClearButton.IsVisible = true;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };

        ColumnDefinitions = new ColumnDefinitions("*,26");
        ColumnSpacing = 4;
        Children.Add(Picker);
        Children.Add(ClearButton);
        Grid.SetColumn(ClearButton, 1);
    }

    public event EventHandler? ValueChanged;

    public ColorPicker Picker { get; }

    public UrsaIconButton ClearButton { get; }

    public string? Value => _hasExplicitColor ? FormatColor(Picker.Color) : null;

    private static bool TryParseColor(string? value, out Color color)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            try
            {
                color = Color.Parse(value.Trim());
                return true;
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
            }
        }

        color = Colors.Black;
        return false;
    }

    private static string FormatColor(Color color) => color.A == byte.MaxValue
        ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
}

internal sealed class GraphPercentageEditor : Grid
{
    private static readonly TimeSpan PreviewInterval = TimeSpan.FromMilliseconds(16);
    private readonly DispatcherTimer _previewTimer;
    private bool _isEditing;
    private bool _isPointerEditing;
    private bool _isKeyboardEditing;
    private bool _hasPendingPreview;
    private bool _hasValueChange;

    public GraphPercentageEditor(
        string name,
        double value,
        double minimum,
        double maximum,
        double tickFrequency)
    {
        Slider = new Slider
        {
            Name = name,
            Minimum = minimum,
            Maximum = maximum,
            TickFrequency = double.IsFinite(tickFrequency) && tickFrequency > 0 ? tickFrequency : 1,
            IsSnapToTickEnabled = false,
            Value = Math.Clamp(value, minimum, maximum),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ValueText = new TextBlock
        {
            Name = $"{name}ValueText",
            MinWidth = 44,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        ColumnDefinitions = new ColumnDefinitions("*,48");
        ColumnSpacing = 4;
        Children.Add(Slider);
        Children.Add(ValueText);
        Grid.SetColumn(ValueText, 1);
        UpdateValueText();

        _previewTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = PreviewInterval
        };
        _previewTimer.Tick += HandlePreviewTick;
        Slider.PropertyChanged += HandleSliderPropertyChanged;
        Slider.AddHandler(
            InputElement.PointerPressedEvent,
            HandlePointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        Slider.AddHandler(
            InputElement.PointerReleasedEvent,
            HandlePointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        Slider.AddHandler(
            InputElement.PointerCaptureLostEvent,
            HandlePointerCaptureLost,
            RoutingStrategies.Direct,
            handledEventsToo: true);
        Slider.AddHandler(
            InputElement.KeyDownEvent,
            HandleKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        Slider.AddHandler(
            InputElement.KeyUpEvent,
            HandleKeyUp,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        Slider.LostFocus += (_, _) => CompleteEdit();
        DetachedFromVisualTree += (_, _) => _previewTimer.Stop();
    }

    public Slider Slider { get; }

    public TextBlock ValueText { get; }

    public double Value => Slider.Value;

    public void AttachEdit(Action begin, Action preview, Action commit)
    {
        EditStarted = begin ?? throw new ArgumentNullException(nameof(begin));
        PreviewRequested = preview ?? throw new ArgumentNullException(nameof(preview));
        EditCommitted = commit ?? throw new ArgumentNullException(nameof(commit));
    }

    private Action? EditStarted { get; set; }

    private Action? PreviewRequested { get; set; }

    private Action? EditCommitted { get; set; }

    private void HandleSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != RangeBase.ValueProperty)
        {
            return;
        }

        UpdateValueText();
        BeginEdit();
        _hasValueChange = true;
        _hasPendingPreview = true;
        if (_isPointerEditing || _isKeyboardEditing)
        {
            QueuePreview();
            return;
        }

        FlushPreview();
        CompleteEdit();
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isPointerEditing = true;
        BeginEdit();
    }

    private void HandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPointerEditing = false;
        CompleteEdit();
    }

    private void HandlePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isPointerEditing = false;
        CompleteEdit();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsAdjustmentKey(e.Key))
        {
            return;
        }

        _isKeyboardEditing = true;
        BeginEdit();
    }

    private void HandleKeyUp(object? sender, KeyEventArgs e)
    {
        if (!IsAdjustmentKey(e.Key))
        {
            return;
        }

        _isKeyboardEditing = false;
        CompleteEdit();
    }

    private void HandlePreviewTick(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
        FlushPreview();
    }

    private void BeginEdit()
    {
        if (_isEditing)
        {
            return;
        }

        _isEditing = true;
        EditStarted?.Invoke();
    }

    private void QueuePreview()
    {
        if (!_previewTimer.IsEnabled)
        {
            _previewTimer.Start();
        }
    }

    private void FlushPreview()
    {
        _previewTimer.Stop();
        if (!_hasPendingPreview)
        {
            return;
        }

        _hasPendingPreview = false;
        PreviewRequested?.Invoke();
    }

    private void CompleteEdit()
    {
        if (!_isEditing || _isPointerEditing || _isKeyboardEditing)
        {
            return;
        }

        FlushPreview();
        var shouldCommit = _hasValueChange;
        _isEditing = false;
        _hasValueChange = false;
        if (shouldCommit)
        {
            EditCommitted?.Invoke();
        }
    }

    private void UpdateValueText() =>
        ValueText.Text = string.Create(
            System.Globalization.CultureInfo.CurrentCulture,
            $"{Slider.Value:0.##}%");

    private static bool IsAdjustmentKey(Key key) => key is
        Key.Left or Key.Right or Key.Up or Key.Down or
        Key.PageUp or Key.PageDown or Key.Home or Key.End;
}
