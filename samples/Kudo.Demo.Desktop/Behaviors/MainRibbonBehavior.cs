using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Kudo.Demo.Desktop.Views;

namespace Kudo.Demo.Desktop.Behaviors;

internal sealed class MainRibbonBehavior : IDisposable
{
    private const double ExpandedContentHeight = 74;

    private readonly MainRibbonView _view;
    private readonly TabStrip _tabs;
    private readonly ContentControl _content;
    private readonly Action _persistSettings;
    private bool _isDisposed;

    public MainRibbonBehavior(
        MainRibbonView view,
        bool isExpanded,
        Action persistSettings)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _persistSettings = persistSettings ?? throw new ArgumentNullException(nameof(persistSettings));
        _tabs = view.FindControl<TabStrip>(MainRibbonControlNames.Tabs) ??
            throw new InvalidOperationException("RibbonTabs control was not found.");
        _content = view.FindControl<ContentControl>(MainRibbonControlNames.Content) ??
            throw new InvalidOperationException("RibbonContent control was not found.");

        SynchronizeContentDataContexts();
        _view.AttachedToVisualTree += HandleAttachedToVisualTree;
        _tabs.SelectionChanged += HandleSelectionChanged;
        _tabs.AddHandler(
            InputElement.DoubleTappedEvent,
            HandleDoubleTapped,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        SetExpanded(isExpanded, persist: false);
        UpdateContent(expand: false);
    }

    public bool IsExpanded { get; private set; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _view.AttachedToVisualTree -= HandleAttachedToVisualTree;
        _tabs.SelectionChanged -= HandleSelectionChanged;
        _tabs.RemoveHandler(InputElement.DoubleTappedEvent, HandleDoubleTapped);
    }

    private void HandleSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        UpdateContent(expand: true);

    private void HandleAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        SynchronizeContentDataContexts();
        UpdateContent(expand: false);
    }

    private void HandleDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source || FindTabHeader(source) is null)
        {
            return;
        }

        SetExpanded(!IsExpanded);
        e.Handled = true;
    }

    private void SetExpanded(bool value, bool persist = true)
    {
        var changed = IsExpanded != value;
        IsExpanded = value;
        _content.IsHitTestVisible = value;
        _content.Height = value ? ExpandedContentHeight : 0;
        _content.Opacity = value ? 1 : 0;
        if (persist && changed)
        {
            _persistSettings();
        }
    }

    private void UpdateContent(bool expand)
    {
        if (_tabs.SelectedItem is not TabStripItem item)
        {
            return;
        }

        _content.Content = item.Tag;
        if (expand)
        {
            SetExpanded(true);
        }
    }

    private void SynchronizeContentDataContexts()
    {
        foreach (var item in _tabs.Items.OfType<TabStripItem>())
        {
            if (item.Tag is StyledElement content)
            {
                content.DataContext = _view.DataContext;
            }
        }
    }

    private TabStripItem? FindTabHeader(Visual source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TabStripItem item && _tabs.IndexFromContainer(item) >= 0)
            {
                return item;
            }
        }

        return null;
    }
}
