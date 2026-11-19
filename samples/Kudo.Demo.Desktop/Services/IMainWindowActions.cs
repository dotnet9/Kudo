using Kudo.Avalonia.Designer;
using Kudo.Avalonia.Export;
using Kudo.Core.Documents;
using Kudo.Core.Runtime;

namespace Kudo.Demo.Desktop.Services;

internal interface IMainWindowActions
{
    void CreateGraph();

    Task OpenGraphAsync();

    Task SaveGraphAsync(bool forcePicker);

    Task ExportGraphAsync(GraphExportFormat format);

    Task PrintGraphAsync();

    void ValidateGraph();

    Task OpenSampleTaskAsync(string taskDirectory);

    void ZoomBy(double factor);

    void SetZoomPercentage(double percentage);

    void AddNode(GraphNodeDefinition definition);

    void ActivateDocument(GraphDocumentSession session);

    Task RenameDocumentAsync(GraphDocumentSession session);

    Task DuplicateDocumentAsync(GraphDocumentSession session);

    Task CloseDocumentAsync(GraphDocumentSession session);

    Task CloseActiveDocumentAsync();

    Task CreateLargeGraphAsync(int nodeCount);

    Task ExecuteRuntimeCommandAsync(GraphRuntimeCommand command, string displayName);

    void ToggleBreakpoint();

    Task RefreshRuntimeAsync();

    void ClearRuntimeBreakpoints();

    void ClearRuntimeWatches();

    void ClearRuntimeDiagnostics();

    void OpenRuntimeVariables();

    void OpenRuntimeBreakpoints();

    void OpenRuntimeCallStack();

    void OpenRuntimeTrace();

    void ToggleToolbox();

    void ToggleDocumentExplorer();

    void ToggleProperties();

    void OpenCanvasDisplaySettings();

    void ToggleDiagnostics();

    void ToggleValidation();

    void ToggleGrid();

    void ToggleGridSnap();

    void ToggleAlignmentGuides();

    void ToggleReadOnlyMode();

    void ToggleMinimap();

    void SetGridSpacing(double spacing);

    void ExecuteSelectionAction(MainWindowSelectionAction action);

    void ExecuteCanvasViewAction(MainWindowCanvasViewAction action);

    void ExecuteArrangementAction(MainWindowArrangementAction action);

    void SetRoutingStyle(EdgeRoutingStyle style);

    void ClearEdgeWaypoints();

    void ExecuteDocumentEditAction(MainWindowDocumentEditAction action);

    void ExecuteModelingAction(MainWindowModelingAction action);

    Task ExecuteClipboardActionAsync(MainWindowClipboardAction action);
}
