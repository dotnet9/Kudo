using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using Kudo.Core.Runtime;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowRuntimeService(
    Func<GraphDocumentView?> activeViewProvider,
    Action<string, string?> appendLog,
    Action refreshTools)
{
    public void ToggleBreakpoint()
    {
        var view = activeViewProvider();
        var runtime = view?.EditorContext.Runtime;
        if (view is null || runtime?.IsAvailable != true || view.EditorContext.Selection.NodeIds.Count != 1)
        {
            return;
        }

        var nodeId = view.EditorContext.Selection.NodeIds.Single();
        var isBreakpoint = runtime.BreakpointNodeIds.Contains(nodeId, StringComparer.Ordinal);
        var changed = runtime.ToggleBreakpoint(nodeId);
        if (!changed)
        {
            return;
        }

        view.Canvas.BreakpointNodeIds = runtime.BreakpointNodeIds.ToArray();
        var statusKey = isBreakpoint
            ? MainWindowLanguage.BreakpointRemovedStatus
            : MainWindowLanguage.BreakpointAddedStatus;
        var detailsKey = isBreakpoint
            ? MainWindowLanguage.BreakpointRemovedDetails
            : MainWindowLanguage.BreakpointAddedDetails;
        var summaryKey = isBreakpoint
            ? MainWindowLanguage.BreakpointRemoved
            : MainWindowLanguage.BreakpointAdded;
        view.SetMessage(F(statusKey, nodeId));
        appendLog(
            F(detailsKey, view.Session.Document.Title, nodeId),
            L(summaryKey));
        refreshTools();
    }

    public async Task RefreshAsync()
    {
        var view = activeViewProvider();
        if (view?.EditorContext.Runtime.IsAvailable != true ||
            !await view.EditorContext.Runtime.RefreshAsync())
        {
            return;
        }

        view.SetMessage(L(MainWindowLanguage.RuntimeRefreshed));
        appendLog(
            F(MainWindowLanguage.RuntimeRefreshedDetails, view.Session.Document.Title),
            view.LastMessage);
        refreshTools();
    }

    public void ClearBreakpoints() => ClearRuntimeState(
        runtime => runtime.ClearBreakpoints(),
        MainWindowLanguage.RuntimeBreakpointsCleared,
        MainWindowLanguage.RuntimeBreakpointsClearedDetails);

    public void ClearWatches() => ClearRuntimeState(
        runtime => runtime.ClearWatches(),
        MainWindowLanguage.RuntimeWatchesCleared,
        MainWindowLanguage.RuntimeWatchesClearedDetails);

    public void ClearDiagnostics() => ClearRuntimeState(
        runtime => runtime.ClearDiagnostics(),
        MainWindowLanguage.RuntimeDiagnosticsCleared,
        MainWindowLanguage.RuntimeDiagnosticsClearedDetails);

    public async Task ExecuteAsync(GraphRuntimeCommand command, string displayName)
    {
        var view = activeViewProvider();
        var runtime = view?.EditorContext.Runtime;
        if (view is null || runtime?.IsAvailable != true)
        {
            return;
        }

        var result = await runtime.ExecuteAsync(command);
        if (result is null)
        {
            return;
        }
        switch (result.Outcome)
        {
            case GraphRuntimeCommandOutcome.Failed:
                appendLog(
                    F(
                        MainWindowLanguage.RuntimeCommandFailed,
                        view.Session.Document.Title,
                        result.Message),
                    L(MainWindowLanguage.RuntimeCommandFailedStatus));
                refreshTools();
                return;
            case GraphRuntimeCommandOutcome.Faulted:
                appendLog(
                    F(
                        MainWindowLanguage.RuntimeFailed,
                        view.Session.Document.Title,
                        result.Message ?? L(MainWindowLanguage.UnknownError)),
                    L(MainWindowLanguage.RuntimeFailedStatus));
                refreshTools();
                return;
            case GraphRuntimeCommandOutcome.BreakpointHit when result.ActiveNodeId is { } activeNodeId:
                appendLog(
                    F(
                        MainWindowLanguage.BreakpointHitDetails,
                        view.Session.Document.Title,
                        activeNodeId),
                    F(MainWindowLanguage.BreakpointHitStatus, activeNodeId));
                refreshTools();
                return;
        }

        view.SetMessage(F(MainWindowLanguage.RuntimeStatusMessage, displayName));
        appendLog(
            F(MainWindowLanguage.RuntimeStatusDetails, view.Session.Document.Title, displayName),
            view.LastMessage);
        refreshTools();
    }

    private void ClearRuntimeState(
        Func<GraphRuntimeOperations, bool> clear,
        string statusKey,
        string detailsKey)
    {
        var view = activeViewProvider();
        var runtime = view?.EditorContext.Runtime;
        if (view is null || runtime is null || !clear(runtime))
        {
            return;
        }

        view.SetMessage(L(statusKey));
        appendLog(F(detailsKey, view.Session.Document.Title), view.LastMessage);
        refreshTools();
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
