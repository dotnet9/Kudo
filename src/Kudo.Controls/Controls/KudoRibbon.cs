using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Kudo.Controls;

/// <summary>
///     Office 式 Ribbon 外壳：页签条 + 快速访问槽 + 分组命令区宿主。
///     继承 TabStrip——页签（TabStripItem）的 Tag 即对应命令区内容；
///     双击页签折叠/展开命令区，切换页签自动展开并切换内容。
/// </summary>
public class KudoRibbon : TabStrip
{
    private ContentControl? _contentHost;

    public static readonly StyledProperty<object?> QuickAccessContentProperty =
        AvaloniaProperty.Register<KudoRibbon, object?>(nameof(QuickAccessContent));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<KudoRibbon, bool>(nameof(IsExpanded), true);

    public static readonly StyledProperty<double> ContentHeightProperty =
        AvaloniaProperty.Register<KudoRibbon, double>(nameof(ContentHeight), 74);

    /// <summary>快速访问区内容（页签条左上角插槽）。</summary>
    public object? QuickAccessContent
    {
        get => GetValue(QuickAccessContentProperty);
        set => SetValue(QuickAccessContentProperty, value);
    }

    /// <summary>命令区是否展开（折叠时高度收起，仅保留页签条）。</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>命令区展开高度。</summary>
    public double ContentHeight
    {
        get => GetValue(ContentHeightProperty);
        set => SetValue(ContentHeightProperty, value);
    }

    private static ControlTheme? _theme;

    public KudoRibbon()
    {
        // 直接以本地值指定 ControlTheme：绕开隐式主题查找——否则会沿基类链命中 Semi 的 TabStrip 主题
        //（其模板无快速访问槽与命令区宿主，页签竖排），且样式/主题优先级均低于本地值。
        try
        {
            _theme ??= (ControlTheme)AvaloniaXamlLoader.Load(
                new Uri("avares://Kudo.Controls/Themes/KudoRibbonTheme.axaml"));
        }
        catch (Exception themeLoadEx)
        {
            File.WriteAllLines(
                @"E:\github\temp_files\theme-load-error.txt",
                new[] { themeLoadEx.ToString() });
            throw;
        }
        SetCurrentValue(ThemeProperty, _theme);
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Horizontal });
        SelectionChanged += HandleSelectionChanged;
        AddHandler(InputElement.DoubleTappedEvent, HandleDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private bool _themeReapplied;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs args)
    {
        base.OnApplyTemplate(args);
        // 隐式主题解析发生在 Initialized（晚于构造器），会用沿基类链命中的 Semi TabStrip 主题
        // 覆盖构造器里的本地值；此处在其之后回设本控件主题，触发模板按正确主题重新应用。
        if (!_themeReapplied &&
            _theme is not null &&
            !ReferenceEquals(GetValue(ThemeProperty), _theme))
        {
            _themeReapplied = true;
            SetValue(ThemeProperty, _theme);
            return;
        }

        _contentHost = args.NameScope.Find<ContentControl>("PART_ContentHost");
        ApplyExpanded(false);
        UpdateContent(expand: false);
        SynchronizeContentDataContexts();
    }

    /// <summary>命令区宿主把选中页签的 Tag 作为内容，并继承 Ribbon 的 DataContext。</summary>
    private void UpdateContent(bool expand)
    {
        if (SelectedItem is not TabStripItem item)
        {
            return;
        }

        if (_contentHost is not null)
        {
            _contentHost.Content = item.Tag;
            if (item.Tag is StyledElement content)
            {
                content.DataContext = DataContext;
            }
        }

        if (expand)
        {
            SetCurrentValue(IsExpandedProperty, true);
        }
    }

    private void ApplyExpanded(bool animate)
    {
        if (_contentHost is null)
        {
            return;
        }

        _contentHost.IsHitTestVisible = IsExpanded;
        _contentHost.Height = IsExpanded ? ContentHeight : 0;
        _contentHost.Opacity = IsExpanded ? 1 : 0;
        _ = animate;
    }

    private void HandleSelectionChanged(object? sender, SelectionChangedEventArgs args) => UpdateContent(expand: true);

    private void HandleDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (args.Source is not Visual source || FindTabHeader(source) is null)
        {
            return;
        }

        SetCurrentValue(IsExpandedProperty, !IsExpanded);
        args.Handled = true;
    }

    private TabStripItem? FindTabHeader(Visual source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TabStripItem item && IndexFromContainer(item) >= 0)
            {
                return item;
            }
        }

        return null;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty ||
            change.Property == ContentHeightProperty)
        {
            ApplyExpanded(animate: false);
        }
        else if (change.Property == DataContextProperty)
        {
            SynchronizeContentDataContexts();
        }
    }

    private void SynchronizeContentDataContexts()
    {
        foreach (var item in Items.OfType<TabStripItem>())
        {
            if (item.Tag is StyledElement content)
            {
                content.DataContext = DataContext;
            }
        }
    }
}
