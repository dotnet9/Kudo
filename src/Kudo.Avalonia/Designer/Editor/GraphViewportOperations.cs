using Avalonia;
using Kudo;

namespace Kudo.Avalonia.Designer;

public sealed class GraphViewportOperations
{
    private readonly GraphDocumentView _view;

    internal GraphViewportOperations(GraphDocumentView view) => _view = view;

    public GraphViewport Value
    {
        get => _view.Viewport;
        set => _view.Viewport = value;
    }

    public GraphViewportOptions Options
    {
        get => _view.ViewportOptions;
        set => _view.ViewportOptions = value;
    }

    public double Zoom => _view.Viewport.Zoom;

    public Rect? VisibleGraphBounds => _view.VisibleGraphBounds;

    public GraphGridSettings Grid => _view.Canvas.GridSettings;

    public GraphCanvasSettings Settings =>
        _view.Session.Document.CanvasSettings.Normalize();

    public bool IsMinimapVisible => _view.IsMinimapVisible;

    public bool SetZoom(double zoom) => _view.SetZoom(zoom);

    public bool ZoomAt(Point screenPoint, double targetZoom) =>
        _view.ZoomAt(screenPoint, targetZoom);

    public bool ZoomBy(double factor) => _view.ZoomBy(factor);

    public bool ZoomBy(double factor, Point screenPoint) =>
        _view.ZoomBy(factor, screenPoint);

    public bool PanBy(Vector screenDelta) => _view.PanBy(screenDelta);

    public bool Center(GraphPoint graphPoint) => _view.CenterViewport(graphPoint);

    public bool Reset() => _view.ResetViewport();

    public bool FitDocument(Size? viewportSize = null, double padding = 48) =>
        _view.FitToDocument(viewportSize, padding);

    public bool FitSelection(Size? viewportSize = null, double padding = 64) =>
        _view.FitToSelection(viewportSize, padding);

    public bool SetGrid(GraphGridSettings grid) =>
        ApplySettings(settings => settings with { Grid = grid });

    public bool SetGridVisible(bool value) =>
        ApplySettings(settings => settings with
        {
            Grid = settings.Grid with { IsVisible = value }
        });

    public bool SetSnapEnabled(bool value) =>
        ApplySettings(settings => settings with
        {
            Grid = settings.Grid with { IsSnapEnabled = value }
        });

    public bool SetGuidesEnabled(bool value) =>
        ApplySettings(settings => settings with
        {
            Grid = settings.Grid with { IsGuidesEnabled = value }
        });

    public bool SetMinimapVisible(bool value) =>
        ApplySettings(settings => settings with { IsMinimapVisible = value });

    public bool SetLevelOfDetailEnabled(bool value) =>
        ApplySettings(settings => settings with { IsLevelOfDetailEnabled = value });

    public bool SetPortsVisible(bool value) =>
        ApplySettings(settings => settings with { ShowPorts = value });

    public bool SetLevelOfDetailThresholds(
        double portsZoomThreshold,
        double labelsZoomThreshold,
        double simplifiedZoomThreshold) =>
        ApplySettings(settings => settings with
        {
            LodPortsZoomThreshold = portsZoomThreshold,
            LodLabelsZoomThreshold = labelsZoomThreshold,
            LodSimplifiedZoomThreshold = simplifiedZoomThreshold
        });

    public bool SetPortLabelVisibility(GraphLabelVisibility value) =>
        ApplySettings(settings => settings with { PortLabelVisibility = value });

    public bool SetSymbolNameVisibility(GraphLabelVisibility value) =>
        ApplySettings(settings => settings with { SymbolNameVisibility = value });

    public bool SetSymbolDescriptionVisibility(GraphLabelVisibility value) =>
        ApplySettings(settings => settings with { SymbolDescriptionVisibility = value });

    public bool SetSettings(GraphCanvasSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _view.ApplyCanvasSettings(settings.Normalize());
    }

    public bool ApplySettings(Func<GraphCanvasSettings, GraphCanvasSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        return SetSettings(update(Settings));
    }
}
