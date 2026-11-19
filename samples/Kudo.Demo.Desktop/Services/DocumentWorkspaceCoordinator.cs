using Kudo;
using Kudo.Avalonia;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Avalonia.Designer;
using Kudo.Core.Documents;
using Kudo.Core.Runtime;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class DocumentWorkspaceCoordinator(
    GraphDocumentManager documents,
    MainWindowViewModel viewModel)
{
    public void Refresh(
        GraphDocumentView? view,
        string? statusMessage = null)
    {
        var session = documents.ActiveSession;
        var workspace = CreateWorkspaceState(view, session);
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new MainWindowWorkspaceChangedCommand(workspace));
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new MainWindowWorkspaceAvailabilityChangedCommand(view is not null));
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new MainWindowPropertyContextChangedCommand(workspace.PropertyEditorContext));
        UpdateStatus(view, session, statusMessage);
    }

    private static MainWindowWorkspaceState CreateWorkspaceState(
        GraphDocumentView? view,
        GraphDocumentSession? session)
    {
        var gridSettings = session?.Document.CanvasSettings.Grid ?? GraphGridSettings.Default;
        var isReadOnly = view?.EditorContext.Document.IsReadOnly == true;
        var selectedNodeCount = view?.Canvas.SelectedNodeIds.Count ?? 0;
        var hasSelection = selectedNodeCount > 0 ||
            view?.Canvas.SelectedEdgeId is not null ||
            view?.Canvas.SelectedAnnotationId is not null;
        var canDeleteSelection = view?.Canvas.CanDeleteSelection() == true;
        var hasDocumentNodes = session?.Document.Nodes.Count > 0;
        var runtimeStatus = view?.RuntimeSession?.Status ?? RuntimeStatus.Stopped;
        var runtimeCommands = GraphRuntimeCommandAvailability.FromStatus(runtimeStatus);
        var selectedNodeId = selectedNodeCount == 1
            ? view?.Canvas.SelectedNodeIds.Single()
            : null;
        var selectedNodeHasBreakpoint = selectedNodeId is not null &&
            view?.RuntimeSession?.BreakpointNodeIds.Contains(selectedNodeId, StringComparer.Ordinal) == true;
        var runtime = view?.EditorContext.Runtime;

        return new MainWindowWorkspaceState
        {
            PropertyEditorContext = view?.PropertyEditorContext,
            CanUndo = view?.CanUndo == true,
            CanRedo = view?.CanRedo == true,
            UndoTip = session?.History.UndoName is { } undoName
                ? F(MainWindowLanguage.UndoAvailableTip, undoName)
                : L(MainWindowLanguage.NoUndoAvailable),
            RedoTip = session?.History.RedoName is { } redoName
                ? F(MainWindowLanguage.RedoAvailableTip, redoName)
                : L(MainWindowLanguage.NoRedoAvailable),
            CanCopy = hasSelection,
            CanCut = canDeleteSelection,
            CanDuplicate = !isReadOnly && hasSelection,
            CanAddContent = !isReadOnly && session is not null,
            CanDeleteSelection = canDeleteSelection,
            CanPaste = !isReadOnly && session is not null,
            CanSelectDocumentNodes = hasDocumentNodes,
            CanClearSelection = hasSelection,
            CanSelectSameType = view?.Canvas.SelectedNodeId is not null,
            CanTraverseSelection = selectedNodeCount > 0,
            CanSelectPath = selectedNodeCount == 2,
            CanAnalyzeDocument = session is not null,
            CanGroup = view?.Canvas.CanGroupSelectedNodes() == true,
            CanUngroup = view?.Canvas.CanUngroupSelectedNodes() == true,
            CanToggleGroup = view?.EditorContext.Edit.CanToggleGroup == true,
            CanChangeRouting = !isReadOnly && view?.Canvas.SelectedEdgeId is not null,
            CanFitSelection = hasSelection,
            CanZoom = view is not null,
            CanAlignSelection = !isReadOnly && selectedNodeCount >= 2,
            CanDistributeSelection = !isReadOnly && selectedNodeCount >= 3,
            CanStartRuntime = runtimeCommands.CanStart,
            StartRuntimeText = runtimeStatus == RuntimeStatus.Paused
                ? L(MainWindowLanguage.Continue)
                : L(MainWindowLanguage.Run),
            CanPauseRuntime = runtimeCommands.CanPause,
            CanStepRuntime = view is not null && runtimeCommands.CanStep,
            CanStopRuntime = runtimeCommands.CanStop,
            CanResetRuntime = view is not null && runtimeCommands.CanReset,
            CanToggleBreakpoint = selectedNodeId is not null,
            CanRefreshRuntime = runtime?.IsAvailable == true,
            CanClearRuntimeBreakpoints = runtime?.BreakpointNodeIds.Count > 0,
            CanClearRuntimeWatches = runtime?.WatchExpressions.Count > 0,
            CanClearRuntimeDiagnostics = runtime?.RecentDiagnostics.Count > 0,
            ToggleBreakpointText = selectedNodeHasBreakpoint
                ? L(MainWindowLanguage.RemoveBreakpoint)
                : L(MainWindowLanguage.Breakpoint),
            GridVisibilityText = gridSettings.IsVisible
                ? L(MainWindowLanguage.HideGrid)
                : L(MainWindowLanguage.ShowGrid),
            GridSnapText = gridSettings.IsSnapEnabled
                ? L(MainWindowLanguage.DisableSnap)
                : L(MainWindowLanguage.EnableSnap),
            AlignmentGuidesText = gridSettings.IsGuidesEnabled
                ? L(MainWindowLanguage.HideGuides)
                : L(MainWindowLanguage.ShowGuides),
            ReadOnlyModeText = isReadOnly
                ? L(MainWindowLanguage.ExitReadOnly)
                : L(MainWindowLanguage.ReadOnlyMode),
            MinimapVisibilityText = view?.EditorContext.Viewport.IsMinimapVisible == true
                ? L(MainWindowLanguage.HideMinimap)
                : L(MainWindowLanguage.ShowMinimap),
            CanSetGridSpacing10 = gridSettings.Spacing != 10,
            CanSetGridSpacing20 = gridSettings.Spacing != 20,
            CanSetGridSpacing40 = gridSettings.Spacing != 40,
            CanSetGridSpacing80 = gridSettings.Spacing != 80
        };
    }

    private void UpdateStatus(
        GraphDocumentView? view,
        GraphDocumentSession? session,
        string? message)
    {
        if (view is null || session is null)
        {
            viewModel.UpdateStatus(
                L(MainWindowLanguage.NoActiveCanvas),
                message ?? L(MainWindowLanguage.Ready),
                F(MainWindowLanguage.OpenCanvasNoActive, documents.Sessions.Count),
                F(MainWindowLanguage.SelectedComponents, 0, 0),
                null,
                null);
            return;
        }

        var document = session.Document;
        var documentTitle = string.IsNullOrWhiteSpace(document.Title)
            ? L(MainWindowLanguage.UntitledCanvas)
            : document.Title;
        var activeDocumentTitle = session.IsDirty ? $"{documentTitle} *" : documentTitle;
        var selectedGroupCount = document.Groups.Count(group => view.Canvas.IsGroupSelected(group.Id));
        var selection = selectedGroupCount > 0
            ? F(MainWindowLanguage.SelectedGroups, view.Canvas.SelectedNodeIds.Count, selectedGroupCount)
            : view.Canvas.SelectedNodeIds.Count > 1
                ? F(MainWindowLanguage.SelectedNodes, view.Canvas.SelectedNodeIds.Count)
            : view.Canvas.SelectedNodeId is { } nodeId
                ? F(MainWindowLanguage.SelectedNode, nodeId)
            : view.Canvas.SelectedEdgeId is { } edgeId
                ? F(MainWindowLanguage.SelectedEdge, edgeId)
            : view.Canvas.SelectedAnnotationId is { } annotationId
                ? F(MainWindowLanguage.SelectedAnnotation, annotationId)
                : string.Empty;
        var prefix = message ?? view.LastMessage;
        viewModel.UpdateStatus(
            activeDocumentTitle,
            string.IsNullOrWhiteSpace(prefix) ? L(MainWindowLanguage.Ready) : prefix,
            F(
                MainWindowLanguage.DocumentSummary,
                documentTitle,
                documents.Sessions.Count,
                document.Nodes.Count,
                document.Edges.Count,
                document.Groups.Count,
                selection),
            F(
                MainWindowLanguage.SelectedComponents,
                view.Canvas.SelectedNodeIds.Count,
                document.Nodes.Count),
            view.Viewport,
            view.ViewportOptions);
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
