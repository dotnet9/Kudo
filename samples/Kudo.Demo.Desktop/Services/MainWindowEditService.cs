using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowEditService
{
    private readonly Func<GraphDocumentView?> _activeViewProvider;
    private readonly Func<GraphDocumentView?, string, bool> _ensureEditable;
    private readonly Action<string, string?> _appendLog;
    private readonly Action _workspaceChanged;

    public MainWindowEditService(
        Func<GraphDocumentView?> activeViewProvider,
        Func<GraphDocumentView?, string, bool> ensureEditable,
        Action<string, string?> appendLog,
        Action workspaceChanged)
    {
        _activeViewProvider = activeViewProvider ??
            throw new ArgumentNullException(nameof(activeViewProvider));
        _ensureEditable = ensureEditable ??
            throw new ArgumentNullException(nameof(ensureEditable));
        _appendLog = appendLog ??
            throw new ArgumentNullException(nameof(appendLog));
        _workspaceChanged = workspaceChanged ??
            throw new ArgumentNullException(nameof(workspaceChanged));
    }

    public void Execute(MainWindowDocumentEditAction action)
    {
        switch (action)
        {
            case MainWindowDocumentEditAction.Undo:
                Undo();
                break;
            case MainWindowDocumentEditAction.Redo:
                Redo();
                break;
            case MainWindowDocumentEditAction.DuplicateSelection:
                DuplicateSelection();
                break;
            case MainWindowDocumentEditAction.DeleteSelection:
                DeleteSelection();
                break;
            case MainWindowDocumentEditAction.LockSelection:
                SetSelectedNodesLocked(true);
                break;
            case MainWindowDocumentEditAction.UnlockSelection:
                SetSelectedNodesLocked(false);
                break;
            case MainWindowDocumentEditAction.GroupSelection:
                GroupSelectedNodes();
                break;
            case MainWindowDocumentEditAction.UngroupSelection:
                UngroupSelectedNodes();
                break;
            case MainWindowDocumentEditAction.ToggleSelectedGroup:
                ToggleSelectedGroupCollapsed();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    public void SetRoutingStyle(EdgeRoutingStyle style)
    {
        var view = _activeViewProvider();
        if (view is null ||
            !_ensureEditable(view, L(MainWindowLanguage.ChangeRoutingAction)))
        {
            return;
        }

        var edges = view.EditorContext.Connections.SelectedEdges;
        var edge = view.EditorContext.Connections.SelectedEdge;
        if (edge is null || edges.Count == 0)
        {
            _appendLog(
                L(MainWindowLanguage.SelectEdgeFirst),
                L(MainWindowLanguage.NoEdgeSelected));
            return;
        }

        var styleName = FormatRoutingStyle(style);
        if (edges.All(candidate => candidate.RoutingStyle == style))
        {
            view.SetMessage(F(MainWindowLanguage.RoutingAlreadySet, styleName));
            _workspaceChanged();
            return;
        }

        if (!view.EditorContext.Connections.SetRoutingStyle(style))
        {
            return;
        }

        view.SetMessage(F(MainWindowLanguage.RoutingUpdated, styleName));
    }

    public void ClearEdgeWaypoints()
    {
        var view = _activeViewProvider();
        if (view?.EditorContext.Connections.ClearWaypoints() != true)
        {
            return;
        }

        view.SetMessage(L(MainWindowLanguage.WaypointsCleared));
    }

    private void SetSelectedNodesLocked(bool value)
    {
        var view = _activeViewProvider();
        if (view is null ||
            !_ensureEditable(view, value
                ? L(MainWindowLanguage.LockSelectionAction)
                : L(MainWindowLanguage.UnlockSelectionAction)))
        {
            return;
        }

        if (!view.EditorContext.Edit.SetSelectedNodesLocked(value))
        {
            return;
        }

        var message = value
            ? L(MainWindowLanguage.NodesLocked)
            : L(MainWindowLanguage.NodesUnlocked);
        view.SetMessage(message);
    }

    public static string FormatRoutingStyle(EdgeRoutingStyle style) => style switch
    {
        EdgeRoutingStyle.Direct => L(MainWindowLanguage.Direct),
        EdgeRoutingStyle.Bezier => L(MainWindowLanguage.Bezier),
        EdgeRoutingStyle.Orthogonal => L(MainWindowLanguage.Orthogonal),
        EdgeRoutingStyle.Manual => L(MainWindowLanguage.Manual),
        EdgeRoutingStyle.SmartOrthogonal => L(MainWindowLanguage.SmartOrthogonal),
        _ => style.ToString()
    };

    private void Undo()
    {
        var view = _activeViewProvider();
        var commandName = view?.Session.History.UndoName;
        if (view?.EditorContext.Edit.Undo() != true)
        {
            return;
        }

        view.SetMessage(F(MainWindowLanguage.UndoStatus, commandName));
    }

    private void Redo()
    {
        var view = _activeViewProvider();
        var commandName = view?.Session.History.RedoName;
        if (view?.EditorContext.Edit.Redo() != true)
        {
            return;
        }

        view.SetMessage(F(MainWindowLanguage.RedoStatus, commandName));
    }

    private void DuplicateSelection()
    {
        var view = _activeViewProvider();
        if (view is null ||
            !_ensureEditable(
                view,
                L(MainWindowLanguage.DuplicateSelectionAction)))
        {
            return;
        }

        if (!view.EditorContext.Edit.Duplicate(
                commandName: L(MainWindowLanguage.DuplicateCommandName)))
        {
            _appendLog(
                L(MainWindowLanguage.DuplicateUnavailable),
                L(MainWindowLanguage.DuplicateFailedStatus));
            return;
        }

        view.SetMessage(L(MainWindowLanguage.DuplicateCompleted));
    }

    private void DeleteSelection()
    {
        var view = _activeViewProvider();
        if (view is null ||
            !_ensureEditable(view, L(MainWindowLanguage.DeleteSelectionAction)))
        {
            return;
        }

        if (!view.EditorContext.Edit.Delete())
        {
            _appendLog(
                L(MainWindowLanguage.SelectToDelete),
                L(MainWindowLanguage.NoDeleteSelection));
            return;
        }

        view.SetMessage(L(MainWindowLanguage.SelectionDeleted));
    }

    private void GroupSelectedNodes()
    {
        var view = _activeViewProvider();
        if (view is null ||
            !_ensureEditable(view, L(MainWindowLanguage.CreateGroupAction)))
        {
            return;
        }

        if (!view.EditorContext.Edit.Group())
        {
            _appendLog(
                L(MainWindowLanguage.SelectTwoUngroupedNodes),
                L(MainWindowLanguage.CannotCreateGroup));
            return;
        }

        var group = view.Session.Document.Groups.Last();
        view.SetMessage(F(MainWindowLanguage.GroupCreatedStatus, group.Title));
    }

    private void UngroupSelectedNodes()
    {
        var view = _activeViewProvider();
        if (view is null ||
            !_ensureEditable(view, L(MainWindowLanguage.UngroupAction)))
        {
            return;
        }

        var groupCount = view.Session.Document.Groups.Count(
            group => view.Canvas.IsGroupSelected(group.Id));
        if (!view.EditorContext.Edit.Ungroup())
        {
            _appendLog(
                L(MainWindowLanguage.SelectCompleteGroup),
                L(MainWindowLanguage.CannotUngroup));
            return;
        }

        view.SetMessage(F(MainWindowLanguage.GroupsUngroupedStatus, groupCount));
    }

    private void ToggleSelectedGroupCollapsed()
    {
        var view = _activeViewProvider();
        if (view is null ||
            !_ensureEditable(view, L(MainWindowLanguage.ToggleGroupAction)))
        {
            return;
        }

        var group = view.Session.Document.Groups.SingleOrDefault(
            candidate => view.Canvas.IsGroupSelected(candidate.Id));
        if (group is null || !view.EditorContext.Edit.ToggleGroup())
        {
            _appendLog(
                L(MainWindowLanguage.SelectCompleteGroup),
                L(MainWindowLanguage.CannotToggleGroup));
            return;
        }

        var action = group.IsCollapsed
            ? L(MainWindowLanguage.GroupCollapsed)
            : L(MainWindowLanguage.GroupExpanded);
        view.SetMessage(F(
            MainWindowLanguage.GroupStateStatus,
            group.Title,
            action));
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
