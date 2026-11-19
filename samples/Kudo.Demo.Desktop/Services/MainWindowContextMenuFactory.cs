using Avalonia.Controls;
using Avalonia.Data;
using Kudo.Avalonia;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Avalonia.Designer;
using Kudo.Core.Documents;
using System.Windows.Input;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowContextMenuFactory(
    MainRibbonViewModel ribbonViewModel,
    Func<GraphDocumentView?> activeViewProvider,
    Func<GraphDocumentSession?> activeSessionProvider,
    Action<GraphDocumentSession> activateDocument)
{
    public ContextMenu Create(GraphDocumentView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var copy = CreateCommandItem(
            L(MainWindowLanguage.CopyShortcut),
            ribbonViewModel.ClipboardCommand,
            MainWindowClipboardAction.Copy);
        var cut = CreateCommandItem(
            L(MainWindowLanguage.CutShortcut),
            ribbonViewModel.ClipboardCommand,
            MainWindowClipboardAction.Cut);
        var paste = CreateCommandItem(
            L(MainWindowLanguage.PasteShortcut),
            ribbonViewModel.ClipboardCommand,
            MainWindowClipboardAction.Paste);
        var duplicate = CreateMethodItem(
            L(MainWindowLanguage.DuplicateShortcut),
            nameof(MainRibbonViewModel.ExecuteDocumentEditAction),
            MainWindowDocumentEditAction.DuplicateSelection);
        var delete = CreateMethodItem(
            L(MainWindowLanguage.DeleteSelection),
            nameof(MainRibbonViewModel.ExecuteDocumentEditAction),
            MainWindowDocumentEditAction.DeleteSelection);
        var lockSelection = CreateMethodItem(
            L(MainWindowLanguage.LockSelection),
            nameof(MainRibbonViewModel.ExecuteDocumentEditAction),
            MainWindowDocumentEditAction.LockSelection);
        var unlockSelection = CreateMethodItem(
            L(MainWindowLanguage.UnlockSelection),
            nameof(MainRibbonViewModel.ExecuteDocumentEditAction),
            MainWindowDocumentEditAction.UnlockSelection);
        var breakpoint = CreateCommandItem(
            L(MainWindowLanguage.BreakpointTip),
            ribbonViewModel.ToggleBreakpointCommand);
        var group = CreateMethodItem(
            L(MainWindowLanguage.CreateGroupShortcut),
            nameof(MainRibbonViewModel.ExecuteDocumentEditAction),
            MainWindowDocumentEditAction.GroupSelection);
        var ungroup = CreateMethodItem(
            L(MainWindowLanguage.UngroupShortcut),
            nameof(MainRibbonViewModel.ExecuteDocumentEditAction),
            MainWindowDocumentEditAction.UngroupSelection);
        var toggleGroup = CreateMethodItem(
            L(MainWindowLanguage.CollapseOrExpandGroup),
            nameof(MainRibbonViewModel.ExecuteDocumentEditAction),
            MainWindowDocumentEditAction.ToggleSelectedGroup);
        var routingItems = CreateRoutingItems();
        var routing = new MenuItem
        {
            Header = L(MainWindowLanguage.EdgeRouting),
            ItemsSource = routingItems
        };
        var selectAll = CreateMethodItem(
            L(MainWindowLanguage.SelectAllShortcut),
            nameof(MainRibbonViewModel.ExecuteSelectionAction),
            MainWindowSelectionAction.SelectAll);
        var clear = CreateMethodItem(
            L(MainWindowLanguage.ClearSelectionShortcut),
            nameof(MainRibbonViewModel.ExecuteSelectionAction),
            MainWindowSelectionAction.Clear);
        var alignItems = CreateAlignmentItems();
        var distributeItems = CreateDistributionItems();
        var align = new MenuItem { Header = L(MainWindowLanguage.Align), ItemsSource = alignItems };
        var distribute = new MenuItem
        {
            Header = L(MainWindowLanguage.Distribute),
            ItemsSource = distributeItems
        };
        var menu = new ContextMenu
        {
            ItemsSource = new object[]
            {
                copy,
                cut,
                paste,
                duplicate,
                new Separator(),
                selectAll,
                clear,
                delete,
                lockSelection,
                unlockSelection,
                breakpoint,
                new Separator(),
                group,
                ungroup,
                toggleGroup,
                routing,
                new Separator(),
                align,
                distribute,
                new Separator(),
                CreateMethodItem(
                    L(MainWindowLanguage.ToggleGrid),
                    nameof(MainRibbonViewModel.ToggleGrid)),
                CreateMethodItem(
                    L(MainWindowLanguage.ToggleGridSnap),
                    nameof(MainRibbonViewModel.ToggleGridSnap)),
                CreateMethodItem(
                    L(MainWindowLanguage.ToggleGuides),
                    nameof(MainRibbonViewModel.ToggleAlignmentGuides))
            }
        };
        menu.Opening += (_, _) => UpdateMenuState(
            view,
            copy,
            cut,
            paste,
            duplicate,
            delete,
            lockSelection,
            unlockSelection,
            breakpoint,
            group,
            ungroup,
            toggleGroup,
            routing,
            routingItems,
            selectAll,
            clear,
            align,
            alignItems,
            distribute,
            distributeItems);
        return menu;
    }

    private MenuItem[] CreateRoutingItems() =>
    [
        CreateMethodItem(
            L(MainWindowLanguage.Direct),
            nameof(MainRibbonViewModel.SetRoutingStyle),
            EdgeRoutingStyle.Direct),
        CreateMethodItem(
            L(MainWindowLanguage.Bezier),
            nameof(MainRibbonViewModel.SetRoutingStyle),
            EdgeRoutingStyle.Bezier),
        CreateMethodItem(
            L(MainWindowLanguage.Orthogonal),
            nameof(MainRibbonViewModel.SetRoutingStyle),
            EdgeRoutingStyle.Orthogonal),
        CreateMethodItem(
            L(MainWindowLanguage.SmartOrthogonal),
            nameof(MainRibbonViewModel.SetRoutingStyle),
            EdgeRoutingStyle.SmartOrthogonal)
    ];

    private MenuItem[] CreateAlignmentItems() =>
    [
        CreateMethodItem(
            L(MainWindowLanguage.AlignLeft),
            nameof(MainRibbonViewModel.ExecuteArrangementAction),
            MainWindowArrangementAction.AlignLeft),
        CreateMethodItem(
            L(MainWindowLanguage.AlignHorizontalCenter),
            nameof(MainRibbonViewModel.ExecuteArrangementAction),
            MainWindowArrangementAction.AlignHorizontalCenter),
        CreateMethodItem(
            L(MainWindowLanguage.AlignRight),
            nameof(MainRibbonViewModel.ExecuteArrangementAction),
            MainWindowArrangementAction.AlignRight),
        CreateMethodItem(
            L(MainWindowLanguage.AlignTop),
            nameof(MainRibbonViewModel.ExecuteArrangementAction),
            MainWindowArrangementAction.AlignTop),
        CreateMethodItem(
            L(MainWindowLanguage.AlignVerticalCenter),
            nameof(MainRibbonViewModel.ExecuteArrangementAction),
            MainWindowArrangementAction.AlignVerticalCenter),
        CreateMethodItem(
            L(MainWindowLanguage.AlignBottom),
            nameof(MainRibbonViewModel.ExecuteArrangementAction),
            MainWindowArrangementAction.AlignBottom)
    ];

    private MenuItem[] CreateDistributionItems() =>
    [
        CreateMethodItem(
            L(MainWindowLanguage.DistributeHorizontally),
            nameof(MainRibbonViewModel.ExecuteArrangementAction),
            MainWindowArrangementAction.DistributeHorizontally),
        CreateMethodItem(
            L(MainWindowLanguage.DistributeVertically),
            nameof(MainRibbonViewModel.ExecuteArrangementAction),
            MainWindowArrangementAction.DistributeVertically)
    ];

    private void UpdateMenuState(
        GraphDocumentView view,
        MenuItem copy,
        MenuItem cut,
        MenuItem paste,
        MenuItem duplicate,
        MenuItem delete,
        MenuItem lockSelection,
        MenuItem unlockSelection,
        MenuItem breakpoint,
        MenuItem group,
        MenuItem ungroup,
        MenuItem toggleGroup,
        MenuItem routing,
        IReadOnlyList<MenuItem> routingItems,
        MenuItem selectAll,
        MenuItem clear,
        MenuItem align,
        IReadOnlyList<MenuItem> alignItems,
        MenuItem distribute,
        IReadOnlyList<MenuItem> distributeItems)
    {
        if (!ReferenceEquals(view, activeViewProvider()))
        {
            activateDocument(view.Session);
        }

        var activeView = activeViewProvider();
        var activeSession = activeSessionProvider();
        var selectedNodeCount = activeView?.Canvas.SelectedNodeIds.Count ?? 0;
        var hasSelection = selectedNodeCount > 0 ||
            activeView?.Canvas.SelectedEdgeId is not null ||
            activeView?.Canvas.SelectedAnnotationId is not null;
        var selectedEdge = activeView?.Canvas.SelectedEdgeId is { } selectedEdgeId
            ? activeView.Session.Document.Edges.FirstOrDefault(edge => edge.Id == selectedEdgeId)
            : null;
        var canChangeRouting = activeView?.IsReadOnly != true && selectedEdge is not null;
        copy.IsEnabled = hasSelection;
        cut.IsEnabled = activeView?.Canvas.CanDeleteSelection() == true;
        duplicate.IsEnabled = hasSelection;
        paste.IsEnabled = activeSession is not null;
        selectAll.IsEnabled = activeSession?.Document.Nodes.Count > 0;
        clear.IsEnabled = hasSelection;
        delete.IsEnabled = activeView?.Canvas.CanDeleteSelection() == true;
        var selectedNodes = activeView?.Session.Document.Nodes
            .Where(node => activeView.Canvas.SelectedNodeIds.Contains(node.Id))
            .ToArray() ?? [];
        lockSelection.IsEnabled = selectedNodes.Length > 0 &&
            activeView?.IsReadOnly != true &&
            selectedNodes.Any(node => !node.IsLocked);
        unlockSelection.IsEnabled = selectedNodes.Length > 0 &&
            activeView?.IsReadOnly != true &&
            selectedNodes.Any(node => node.IsLocked);
        breakpoint.IsEnabled = selectedNodeCount == 1;
        group.IsEnabled = activeView?.Canvas.CanGroupSelectedNodes() == true;
        ungroup.IsEnabled = activeView?.Canvas.CanUngroupSelectedNodes() == true;
        toggleGroup.IsEnabled = activeView?.Canvas.CanToggleSelectedGroupCollapsed() == true;
        var selectedGroup = activeView?.Session.Document.Groups
            .SingleOrDefault(candidate => activeView.Canvas.IsGroupSelected(candidate.Id));
        toggleGroup.Header = selectedGroup?.IsCollapsed == true
            ? L(MainWindowLanguage.ExpandGroup)
            : L(MainWindowLanguage.CollapseGroup);
        routing.Header = selectedEdge is null
            ? L(MainWindowLanguage.EdgeRouting)
            : $"{L(MainWindowLanguage.EdgeRouting)} · " +
              MainWindowEditService.FormatRoutingStyle(selectedEdge.RoutingStyle);
        SetEnabled(routingItems, canChangeRouting);
        routing.IsEnabled = canChangeRouting;
        align.IsEnabled = selectedNodeCount >= 2;
        distribute.IsEnabled = selectedNodeCount >= 3;
        SetEnabled(alignItems, align.IsEnabled);
        SetEnabled(distributeItems, distribute.IsEnabled);
    }

    private static void SetEnabled(IEnumerable<MenuItem> items, bool value)
    {
        foreach (var item in items)
        {
            item.IsEnabled = value;
        }
    }

    private static MenuItem CreateCommandItem(
        string header,
        ICommand command,
        object? commandParameter = null) =>
        new()
        {
            Header = header,
            Command = command,
            CommandParameter = commandParameter
        };

    private MenuItem CreateMethodItem(
        string header,
        string methodName,
        object? commandParameter = null)
    {
        var item = new MenuItem
        {
            Header = header,
            CommandParameter = commandParameter
        };
        item.Bind(
            MenuItem.CommandProperty,
            new Binding(methodName)
            {
                Source = ribbonViewModel
            });
        return item;
    }

    private static string L(string key) => GraphLocalization.Get(key);
}
