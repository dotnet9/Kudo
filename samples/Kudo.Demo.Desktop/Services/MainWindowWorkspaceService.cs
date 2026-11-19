using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using Kudo.Avalonia.Docking;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowWorkspaceService(
    GraphDockLayoutFactory dockFactory,
    DemoApplicationSettingsStore settingsStore,
    Func<bool> isRibbonExpanded,
    Func<GraphDocumentView?> activeViewProvider,
    Action<string, string?> appendLog,
    Action updateWorkspace)
{
    private bool _settingsSaveFailureReported;

    public GraphGridSettings GridSettings =>
        activeViewProvider()?.Session.Document.CanvasSettings.Grid ?? GraphGridSettings.Default;

    public void ToggleTool(string toolId, string displayName)
    {
        if (!dockFactory.ToggleTool(toolId))
        {
            appendLog(
                F(MainWindowLanguage.ToolPanelNotFound, displayName),
                L(MainWindowLanguage.ToolPanelOperationFailed));
            return;
        }

        var action = dockFactory.IsToolVisible(toolId)
            ? L(MainWindowLanguage.ToolPanelShown)
            : L(MainWindowLanguage.ToolPanelHidden);
        appendLog(
            F(MainWindowLanguage.ToolPanelVisibilityChanged, action, displayName),
            F(MainWindowLanguage.ToolPanelVisibilityStatus, action, displayName));
    }

    public void PersistSettings()
    {
        var settings = new DemoWorkspaceSettings
        {
            IsRibbonExpanded = isRibbonExpanded(),
            ToolVisibility = new Dictionary<string, bool>(
                dockFactory.CaptureToolVisibility(),
                StringComparer.Ordinal)
        };
        if (settingsStore.TrySaveWorkspace(settings, out var error))
        {
            if (_settingsSaveFailureReported)
            {
                _settingsSaveFailureReported = false;
                GraphDemoLogging.Information(L(MainWindowLanguage.WorkspaceSettingsSaveRecovered));
            }

            return;
        }

        if (!_settingsSaveFailureReported)
        {
            _settingsSaveFailureReported = true;
            GraphDemoLogging.Warning(error ?? L(MainWindowLanguage.WorkspaceSettingsSaveFailed));
        }
    }

    public void ToggleGrid() => ApplyGridSettings(
        GridSettings with { IsVisible = !GridSettings.IsVisible },
        L(MainWindowLanguage.GridVisibilityUpdated));

    public void ToggleGridSnap() => ApplyGridSettings(
        GridSettings with { IsSnapEnabled = !GridSettings.IsSnapEnabled },
        L(MainWindowLanguage.GridSnapUpdated));

    public void ToggleAlignmentGuides() => ApplyGridSettings(
        GridSettings with { IsGuidesEnabled = !GridSettings.IsGuidesEnabled },
        L(MainWindowLanguage.GuidesVisibilityUpdated));

    public void ToggleReadOnlyMode()
    {
        var view = activeViewProvider();
        if (view is null)
        {
            return;
        }

        view.EditorContext.Document.IsReadOnly = !view.EditorContext.Document.IsReadOnly;
        var message = view.EditorContext.Document.IsReadOnly
            ? L(MainWindowLanguage.ReadOnlyEntered)
            : L(MainWindowLanguage.ReadOnlyExited);
        view.SetMessage(message);
        appendLog(
            F(MainWindowLanguage.ReadOnlyChangedDetails, view.Session.Document.Title, message),
            message);
    }

    public void SetGridSpacing(double spacing) => ApplyGridSettings(
        GridSettings with { Spacing = spacing },
        F(MainWindowLanguage.GridSpacingUpdated, spacing));

    public void SetZoomPercentage(double percentage)
    {
        var view = activeViewProvider();
        if (view is null)
        {
            return;
        }

        view.EditorContext.Viewport.SetZoom(percentage / 100d);
        view.SetMessage(F(MainWindowLanguage.ZoomUpdated, view.Viewport.Zoom));

        updateWorkspace();
    }

    public void ZoomBy(double factor)
    {
        var view = activeViewProvider();
        if (view is null)
        {
            return;
        }

        if (!view.EditorContext.Viewport.ZoomBy(factor))
        {
            return;
        }

        view.SetMessage(F(MainWindowLanguage.ZoomUpdated, view.Viewport.Zoom));
        updateWorkspace();
    }

    public void SetActualSize() => SetZoomPercentage(100);

    public void ToggleMinimap()
    {
        var view = activeViewProvider();
        if (view is null)
        {
            return;
        }

        if (view.EditorContext.Viewport.SetMinimapVisible(
                !view.EditorContext.Viewport.IsMinimapVisible))
        {
            var message = view.EditorContext.Viewport.IsMinimapVisible
                ? L(MainWindowLanguage.MinimapShown)
                : L(MainWindowLanguage.MinimapHidden);
            appendLog(message, message);
        }

        updateWorkspace();
    }

    private void ApplyGridSettings(GraphGridSettings settings, string message)
    {
        var view = activeViewProvider();
        if (view is null)
        {
            updateWorkspace();
            return;
        }

        var normalized = settings.Normalize();
        var canvasSettings = view.Session.Document.CanvasSettings.Normalize();
        if (normalized == canvasSettings.Grid)
        {
            updateWorkspace();
            return;
        }

        if (!view.EditorContext.Viewport.SetGrid(normalized))
        {
            updateWorkspace();
            return;
        }

        appendLog(message, message);
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
