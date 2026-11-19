using Kudo.Core.Algorithms;
using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowSelectionService
{
    private readonly Func<GraphDocumentView?> _activeViewProvider;
    private readonly Action _workspaceChanged;

    public MainWindowSelectionService(
        Func<GraphDocumentView?> activeViewProvider,
        Action workspaceChanged)
    {
        _activeViewProvider = activeViewProvider ??
            throw new ArgumentNullException(nameof(activeViewProvider));
        _workspaceChanged = workspaceChanged ??
            throw new ArgumentNullException(nameof(workspaceChanged));
    }

    public void Execute(MainWindowSelectionAction action)
    {
        switch (action)
        {
            case MainWindowSelectionAction.SelectAll:
                SelectAllNodes();
                break;
            case MainWindowSelectionAction.Clear:
                ClearSelection();
                break;
            case MainWindowSelectionAction.Invert:
                InvertNodeSelection();
                break;
            case MainWindowSelectionAction.SameType:
                SelectNodesOfSameType();
                break;
            case MainWindowSelectionAction.Adjacent:
                SelectAdjacentNodes();
                break;
            case MainWindowSelectionAction.Upstream:
                SelectReachableNodes(
                    GraphTraversalDirection.Upstream,
                    L(MainWindowLanguage.UpstreamSelected));
                break;
            case MainWindowSelectionAction.Downstream:
                SelectReachableNodes(
                    GraphTraversalDirection.Downstream,
                    L(MainWindowLanguage.DownstreamSelected));
                break;
            case MainWindowSelectionAction.Connected:
                SelectReachableNodes(
                    GraphTraversalDirection.Connected,
                    L(MainWindowLanguage.ConnectedBranchSelected));
                break;
            case MainWindowSelectionAction.ShortestPath:
                SelectShortestPath();
                break;
            case MainWindowSelectionAction.FirstCycle:
                SelectFirstCycle();
                break;
            case MainWindowSelectionAction.Sources:
                SelectNodesByRole(
                    GraphNodeRole.Source,
                    L(MainWindowLanguage.SourceNodesSelected),
                    L(MainWindowLanguage.NoSourceNodes));
                break;
            case MainWindowSelectionAction.Sinks:
                SelectNodesByRole(
                    GraphNodeRole.Sink,
                    L(MainWindowLanguage.SinkNodesSelected),
                    L(MainWindowLanguage.NoSinkNodes));
                break;
            case MainWindowSelectionAction.Isolated:
                SelectNodesByRole(
                    GraphNodeRole.Isolated,
                    L(MainWindowLanguage.IsolatedNodesSelected),
                    L(MainWindowLanguage.NoIsolatedNodes));
                break;
            case MainWindowSelectionAction.CriticalPath:
                SelectCriticalPath();
                break;
            case MainWindowSelectionAction.Viewport:
                SelectViewportNodes();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private void SelectAllNodes()
    {
        var view = _activeViewProvider();
        if (view is null)
        {
            return;
        }

        var selectedCount = view.EditorContext.Selection.SelectAllNodes();
        view.SetMessage(selectedCount == 0
            ? L(MainWindowLanguage.CanvasHasNoNodes)
            : F(MainWindowLanguage.AllNodesSelected, selectedCount));
        _workspaceChanged();
    }

    private void ClearSelection()
    {
        var view = _activeViewProvider();
        if (view is null)
        {
            return;
        }

        view.EditorContext.Selection.Clear();
        view.SetMessage(L(MainWindowLanguage.SelectionCleared));
        _workspaceChanged();
    }

    private void InvertNodeSelection()
    {
        var view = _activeViewProvider();
        if (view is null)
        {
            return;
        }

        var selectedCount = view.EditorContext.Selection.InvertNodes();
        view.SetMessage(selectedCount == 0
            ? L(MainWindowLanguage.InvertSelectionEmpty)
            : F(MainWindowLanguage.InvertSelectionCount, selectedCount));
        _workspaceChanged();
    }

    private void SelectNodesOfSameType()
    {
        var view = _activeViewProvider();
        if (view is null)
        {
            return;
        }

        var selectedCount = view.EditorContext.Selection.SelectNodesOfSameType();
        view.SetMessage(selectedCount == 0
            ? L(MainWindowLanguage.SelectNodeFirst)
            : F(MainWindowLanguage.SameTypeNodesSelected, selectedCount));
        _workspaceChanged();
    }

    private void SelectAdjacentNodes()
    {
        var view = _activeViewProvider();
        if (view is null)
        {
            return;
        }

        view.SetMessage(view.EditorContext.Selection.SelectAdjacent()
            ? F(
                MainWindowLanguage.AdjacentNodesSelected,
                view.Canvas.SelectedNodeIds.Count)
            : L(MainWindowLanguage.SelectAtLeastOneNode));
        _workspaceChanged();
    }

    private void SelectShortestPath()
    {
        var view = _activeViewProvider();
        if (view?.EditorContext.Selection.SelectShortestPath() != true)
        {
            return;
        }

        view.SetMessage(L(MainWindowLanguage.ShortestPathSelected));
        _workspaceChanged();
    }

    private void SelectFirstCycle()
    {
        var view = _activeViewProvider();
        if (view is null)
        {
            return;
        }

        view.SetMessage(view.EditorContext.Selection.SelectFirstCycle()
            ? L(MainWindowLanguage.FirstCycleSelected)
            : L(MainWindowLanguage.NoDirectedCycles));
        _workspaceChanged();
    }

    private void SelectNodesByRole(
        GraphNodeRole role,
        string successMessage,
        string emptyMessage)
    {
        var view = _activeViewProvider();
        if (view is null)
        {
            return;
        }

        view.SetMessage(view.EditorContext.Selection.SelectNodesByRole(role)
            ? successMessage
            : emptyMessage);
        _workspaceChanged();
    }

    private void SelectCriticalPath()
    {
        var view = _activeViewProvider();
        if (view is null)
        {
            return;
        }

        view.SetMessage(view.EditorContext.Selection.SelectCriticalPath()
            ? L(MainWindowLanguage.CriticalPathSelected)
            : L(MainWindowLanguage.CriticalPathUnavailable));
        _workspaceChanged();
    }

    private void SelectViewportNodes()
    {
        var view = _activeViewProvider();
        if (view is null)
        {
            return;
        }

        var count = view.EditorContext.Selection.SelectNodesInViewport();
        view.SetMessage(count > 0
            ? F(MainWindowLanguage.ViewportNodesSelected, count)
            : L(MainWindowLanguage.NoViewportNodes));
        _workspaceChanged();
    }

    private void SelectReachableNodes(
        GraphTraversalDirection direction,
        string message)
    {
        var view = _activeViewProvider();
        if (view?.EditorContext.Selection.SelectReachable(direction) != true)
        {
            return;
        }

        view.SetMessage(message);
        _workspaceChanged();
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
