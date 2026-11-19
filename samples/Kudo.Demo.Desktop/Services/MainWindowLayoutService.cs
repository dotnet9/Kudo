using Kudo.Core.Algorithms;
using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowLayoutService
{
    private readonly Func<GraphDocumentView?> _activeViewProvider;
    private readonly Func<GraphDocumentView?, string, bool> _ensureEditable;
    private readonly Action<string, string?> _appendLog;
    private readonly Action _workspaceChanged;
    private readonly Action _zoomToActualSize;

    public MainWindowLayoutService(
        Func<GraphDocumentView?> activeViewProvider,
        Func<GraphDocumentView?, string, bool> ensureEditable,
        Action<string, string?> appendLog,
        Action workspaceChanged,
        Action zoomToActualSize)
    {
        _activeViewProvider = activeViewProvider ??
            throw new ArgumentNullException(nameof(activeViewProvider));
        _ensureEditable = ensureEditable ??
            throw new ArgumentNullException(nameof(ensureEditable));
        _appendLog = appendLog ??
            throw new ArgumentNullException(nameof(appendLog));
        _workspaceChanged = workspaceChanged ??
            throw new ArgumentNullException(nameof(workspaceChanged));
        _zoomToActualSize = zoomToActualSize ??
            throw new ArgumentNullException(nameof(zoomToActualSize));
    }

    public void ExecuteCanvasViewAction(MainWindowCanvasViewAction action)
    {
        switch (action)
        {
            case MainWindowCanvasViewAction.ApplyLayeredLayout:
                ApplyLayeredLayout();
                break;
            case MainWindowCanvasViewAction.FitDocument:
                FitDocument();
                break;
            case MainWindowCanvasViewAction.FitSelection:
                FitSelection();
                break;
            case MainWindowCanvasViewAction.ActualSize:
                _zoomToActualSize();
                break;
            case MainWindowCanvasViewAction.Reset:
                ResetView();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    public void ExecuteArrangementAction(MainWindowArrangementAction action)
    {
        switch (action)
        {
            case MainWindowArrangementAction.AlignLeft:
                AlignSelectedNodes(
                    NodeAlignment.Left,
                    L(MainWindowLanguage.AlignLeftCommand),
                    L(MainWindowLanguage.AlignLeftCompleted));
                break;
            case MainWindowArrangementAction.AlignHorizontalCenter:
                AlignSelectedNodes(
                    NodeAlignment.HorizontalCenter,
                    L(MainWindowLanguage.AlignHorizontalCenterCommand),
                    L(MainWindowLanguage.AlignHorizontalCenterCompleted));
                break;
            case MainWindowArrangementAction.AlignRight:
                AlignSelectedNodes(
                    NodeAlignment.Right,
                    L(MainWindowLanguage.AlignRightCommand),
                    L(MainWindowLanguage.AlignRightCompleted));
                break;
            case MainWindowArrangementAction.AlignTop:
                AlignSelectedNodes(
                    NodeAlignment.Top,
                    L(MainWindowLanguage.AlignTopCommand),
                    L(MainWindowLanguage.AlignTopCompleted));
                break;
            case MainWindowArrangementAction.AlignVerticalCenter:
                AlignSelectedNodes(
                    NodeAlignment.VerticalCenter,
                    L(MainWindowLanguage.AlignVerticalCenterCommand),
                    L(MainWindowLanguage.AlignVerticalCenterCompleted));
                break;
            case MainWindowArrangementAction.AlignBottom:
                AlignSelectedNodes(
                    NodeAlignment.Bottom,
                    L(MainWindowLanguage.AlignBottomCommand),
                    L(MainWindowLanguage.AlignBottomCompleted));
                break;
            case MainWindowArrangementAction.DistributeHorizontally:
                DistributeSelectedNodes(
                    NodeDistribution.Horizontal,
                    L(MainWindowLanguage.DistributeHorizontalCommand),
                    L(MainWindowLanguage.DistributeHorizontalCompleted));
                break;
            case MainWindowArrangementAction.DistributeVertically:
                DistributeSelectedNodes(
                    NodeDistribution.Vertical,
                    L(MainWindowLanguage.DistributeVerticalCommand),
                    L(MainWindowLanguage.DistributeVerticalCompleted));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private void ApplyLayeredLayout()
    {
        var view = _activeViewProvider();
        if (view is null ||
            !_ensureEditable(view, L(MainWindowLanguage.ApplyLayoutAction)))
        {
            return;
        }

        if (!view.EditorContext.Arrangement.ApplyLayered(
                new GraphLayoutRequest(view.Session.Document)
                {
                    Origin = new GraphPoint(80, 90)
                },
                L(MainWindowLanguage.ApplyLayoutAction)))
        {
            view.SetMessage(string.IsNullOrWhiteSpace(
                view.Canvas.LastEditRejectionReason)
                    ? L(MainWindowLanguage.LayeredLayoutCurrent)
                    : view.Canvas.LastEditRejectionReason);
            _workspaceChanged();
            return;
        }

        view.SetMessage(L(MainWindowLanguage.LayeredLayoutApplied));
    }

    private void AlignSelectedNodes(
        NodeAlignment alignment,
        string commandName,
        string message)
    {
        var view = _activeViewProvider();
        if (view is null ||
            !_ensureEditable(view, L(MainWindowLanguage.AlignNodesAction)))
        {
            return;
        }

        if (GetSelectedNodes(view).Count < 2)
        {
            _appendLog(
                L(MainWindowLanguage.SelectTwoNodes),
                L(MainWindowLanguage.CannotAlignNodes));
            return;
        }

        ApplySelectedNodeArrangement(
            view,
            () => view.EditorContext.Arrangement.Align(alignment, commandName),
            message);
    }

    private void DistributeSelectedNodes(
        NodeDistribution distribution,
        string commandName,
        string message)
    {
        var view = _activeViewProvider();
        if (view is null ||
            !_ensureEditable(view, L(MainWindowLanguage.DistributeNodesAction)))
        {
            return;
        }

        if (GetSelectedNodes(view).Count < 3)
        {
            _appendLog(
                L(MainWindowLanguage.SelectThreeNodes),
                L(MainWindowLanguage.CannotDistributeNodes));
            return;
        }

        ApplySelectedNodeArrangement(
            view,
            () => view.EditorContext.Arrangement.Distribute(distribution, commandName),
            message);
    }

    private void ApplySelectedNodeArrangement(
        GraphDocumentView view,
        Func<bool> arrange,
        string message)
    {
        if (!arrange())
        {
            view.SetMessage(string.IsNullOrWhiteSpace(
                view.Canvas.LastEditRejectionReason)
                    ? F(MainWindowLanguage.PositionsUnchanged, message)
                    : view.Canvas.LastEditRejectionReason);
            _workspaceChanged();
            return;
        }

        view.SetMessage(message);
    }

    private void FitDocument()
    {
        var view = _activeViewProvider();
        if (view?.EditorContext.Viewport.FitDocument() != true)
        {
            return;
        }

        view.SetMessage(L(MainWindowLanguage.CanvasFit));
        _workspaceChanged();
    }

    private void FitSelection()
    {
        var view = _activeViewProvider();
        if (view?.EditorContext.Viewport.FitSelection() != true)
        {
            return;
        }

        view.SetMessage(L(MainWindowLanguage.SelectionFit));
        _workspaceChanged();
    }

    private void ResetView()
    {
        var view = _activeViewProvider();
        if (view is null)
        {
            return;
        }

        view.EditorContext.Viewport.Reset();
        view.SetMessage(L(MainWindowLanguage.ViewReset));
        _workspaceChanged();
    }

    private static IReadOnlyList<NodeModel> GetSelectedNodes(
        GraphDocumentView view) =>
        view.Session.Document.Nodes
            .Where(node => view.Canvas.IsNodeSelected(node.Id))
            .ToArray();

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
