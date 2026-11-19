using Kudo.Avalonia.Designer;

namespace Kudo.Demo.Desktop.ViewModels;

public sealed record MainWindowWorkspaceState
{
    public GraphPropertyEditorContext? PropertyEditorContext { get; init; }

    public bool CanUndo { get; init; }

    public bool CanRedo { get; init; }

    public string UndoTip { get; init; } = string.Empty;

    public string RedoTip { get; init; } = string.Empty;

    public bool CanCopy { get; init; }

    public bool CanCut { get; init; }

    public bool CanDuplicate { get; init; }

    public bool CanAddContent { get; init; }

    public bool CanDeleteSelection { get; init; }

    public bool CanPaste { get; init; }

    public bool CanSelectDocumentNodes { get; init; }

    public bool CanClearSelection { get; init; }

    public bool CanSelectSameType { get; init; }

    public bool CanTraverseSelection { get; init; }

    public bool CanSelectPath { get; init; }

    public bool CanAnalyzeDocument { get; init; }

    public bool CanGroup { get; init; }

    public bool CanUngroup { get; init; }

    public bool CanToggleGroup { get; init; }

    public bool CanChangeRouting { get; init; }

    public bool CanFitSelection { get; init; }

    public bool CanZoom { get; init; }

    public bool CanAlignSelection { get; init; }

    public bool CanDistributeSelection { get; init; }

    public bool CanStartRuntime { get; init; }

    public string StartRuntimeText { get; init; } = string.Empty;

    public bool CanPauseRuntime { get; init; }

    public bool CanStepRuntime { get; init; }

    public bool CanStopRuntime { get; init; }

    public bool CanResetRuntime { get; init; }

    public bool CanToggleBreakpoint { get; init; }

    public bool CanRefreshRuntime { get; init; }

    public bool CanClearRuntimeBreakpoints { get; init; }

    public bool CanClearRuntimeWatches { get; init; }

    public bool CanClearRuntimeDiagnostics { get; init; }

    public string ToggleBreakpointText { get; init; } = string.Empty;

    public string GridVisibilityText { get; init; } = string.Empty;

    public string GridSnapText { get; init; } = string.Empty;

    public string AlignmentGuidesText { get; init; } = string.Empty;

    public string ReadOnlyModeText { get; init; } = string.Empty;

    public string MinimapVisibilityText { get; init; } = string.Empty;

    public bool CanSetGridSpacing10 { get; init; }

    public bool CanSetGridSpacing20 { get; init; }

    public bool CanSetGridSpacing40 { get; init; }

    public bool CanSetGridSpacing80 { get; init; }
}
