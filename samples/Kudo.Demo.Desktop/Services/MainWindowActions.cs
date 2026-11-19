using Kudo.Avalonia.Designer;
using Kudo.Avalonia.Export;
using Kudo.Core.Documents;
using Kudo.Core.Runtime;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowActions(
    MainWindowWorkspaceService workspaceService,
    MainWindowSampleService sampleService,
    MainWindowFileService fileService,
    MainWindowOutputService outputService,
    MainWindowModelingService modelingService,
    MainWindowDocumentService documentService,
    MainWindowSelectionService selectionService,
    MainWindowLayoutService layoutService,
    MainWindowEditService editService,
    MainWindowClipboardCoordinator clipboardCoordinator,
    MainWindowRuntimeService runtimeService,
    Action toggleToolbox,
    Action toggleDocumentExplorer,
    Action toggleProperties,
    Action toggleDiagnostics,
    Action toggleValidation,
    Action openRuntimeVariables,
    Action openRuntimeBreakpoints,
    Action openRuntimeCallStack,
    Action openRuntimeTrace,
    Action openCanvasDisplaySettings) : IMainWindowActions
{
    public void CreateGraph() => sampleService.CreateGraph();

    public Task OpenGraphAsync() => fileService.OpenGraphAsync();

    public Task SaveGraphAsync(bool forcePicker) =>
        fileService.SaveActiveGraphAsync(forcePicker);

    public Task ExportGraphAsync(GraphExportFormat format) => outputService.ExportAsync(format);

    public Task PrintGraphAsync() => outputService.PrintAsync();

    public void ValidateGraph() => outputService.Validate();

    public Task OpenSampleTaskAsync(string taskDirectory) =>
        fileService.OpenTaskDirectoryAsync(taskDirectory);

    public void ZoomBy(double factor) => workspaceService.ZoomBy(factor);

    public void SetZoomPercentage(double percentage) =>
        workspaceService.SetZoomPercentage(percentage);

    public void AddNode(GraphNodeDefinition definition) => modelingService.AddNode(definition);

    public void ActivateDocument(GraphDocumentSession session) => documentService.Activate(session);

    public Task RenameDocumentAsync(GraphDocumentSession session) =>
        documentService.RenameAsync(session);

    public Task DuplicateDocumentAsync(GraphDocumentSession session) =>
        documentService.DuplicateAsync(session);

    public Task CloseDocumentAsync(GraphDocumentSession session) => documentService.CloseAsync(session);

    public Task CloseActiveDocumentAsync() => documentService.CloseActiveAsync();

    public Task CreateLargeGraphAsync(int nodeCount) => sampleService.CreateLargeGraphAsync(nodeCount);

    public Task ExecuteRuntimeCommandAsync(GraphRuntimeCommand command, string displayName) =>
        runtimeService.ExecuteAsync(command, displayName);

    public void ToggleBreakpoint() => runtimeService.ToggleBreakpoint();

    public Task RefreshRuntimeAsync() => runtimeService.RefreshAsync();

    public void ClearRuntimeBreakpoints() => runtimeService.ClearBreakpoints();

    public void ClearRuntimeWatches() => runtimeService.ClearWatches();

    public void ClearRuntimeDiagnostics() => runtimeService.ClearDiagnostics();

    public void OpenRuntimeVariables() => openRuntimeVariables();

    public void OpenRuntimeBreakpoints() => openRuntimeBreakpoints();

    public void OpenRuntimeCallStack() => openRuntimeCallStack();

    public void OpenRuntimeTrace() => openRuntimeTrace();

    public void ToggleToolbox() => toggleToolbox();

    public void ToggleDocumentExplorer() => toggleDocumentExplorer();

    public void ToggleProperties() => toggleProperties();

    public void OpenCanvasDisplaySettings() => openCanvasDisplaySettings();

    public void ToggleDiagnostics() => toggleDiagnostics();

    public void ToggleValidation() => toggleValidation();

    public void ToggleGrid() => workspaceService.ToggleGrid();

    public void ToggleGridSnap() => workspaceService.ToggleGridSnap();

    public void ToggleAlignmentGuides() => workspaceService.ToggleAlignmentGuides();

    public void ToggleReadOnlyMode() => workspaceService.ToggleReadOnlyMode();

    public void ToggleMinimap() => workspaceService.ToggleMinimap();

    public void SetGridSpacing(double spacing) => workspaceService.SetGridSpacing(spacing);

    public void ExecuteSelectionAction(MainWindowSelectionAction action) =>
        selectionService.Execute(action);

    public void ExecuteCanvasViewAction(MainWindowCanvasViewAction action) =>
        layoutService.ExecuteCanvasViewAction(action);

    public void ExecuteArrangementAction(MainWindowArrangementAction action) =>
        layoutService.ExecuteArrangementAction(action);

    public void SetRoutingStyle(EdgeRoutingStyle style) => editService.SetRoutingStyle(style);

    public void ClearEdgeWaypoints() => editService.ClearEdgeWaypoints();

    public void ExecuteDocumentEditAction(MainWindowDocumentEditAction action) =>
        editService.Execute(action);

    public void ExecuteModelingAction(MainWindowModelingAction action) =>
        modelingService.Execute(action);

    public Task ExecuteClipboardActionAsync(MainWindowClipboardAction action) => action switch
    {
        MainWindowClipboardAction.Copy => clipboardCoordinator.CopySelectionAsync(false),
        MainWindowClipboardAction.Cut => clipboardCoordinator.CopySelectionAsync(true),
        MainWindowClipboardAction.Paste => clipboardCoordinator.PasteSelectionAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };
}
