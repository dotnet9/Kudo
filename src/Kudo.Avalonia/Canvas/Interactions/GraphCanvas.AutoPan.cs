using Avalonia;
using Avalonia.Input;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public static readonly StyledProperty<bool> IsAutoPanEnabledProperty =
        AvaloniaProperty.Register<GraphCanvas, bool>(nameof(IsAutoPanEnabled), true);

    public static readonly StyledProperty<double> AutoPanEdgeDistanceProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(
            nameof(AutoPanEdgeDistance),
            40.0,
            coerce: static (_, value) => NormalizeAutoPanValue(value, 40.0));

    public static readonly StyledProperty<double> AutoPanSpeedProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(
            nameof(AutoPanSpeed),
            480.0,
            coerce: static (_, value) => NormalizeAutoPanValue(value, 480.0));

    public bool IsAutoPanEnabled
    {
        get => GetValue(IsAutoPanEnabledProperty);
        set => SetValue(IsAutoPanEnabledProperty, value);
    }

    public double AutoPanEdgeDistance
    {
        get => GetValue(AutoPanEdgeDistanceProperty);
        set => SetValue(AutoPanEdgeDistanceProperty, value);
    }

    public double AutoPanSpeed
    {
        get => GetValue(AutoPanSpeedProperty);
        set => SetValue(AutoPanSpeedProperty, value);
    }

    public bool IsAutoPanActive => _autoPanController.IsActive;

    private static double NormalizeAutoPanValue(double value, double fallback) =>
        double.IsFinite(value) && value >= 0 ? value : fallback;

    private void UpdateAutoPanPointer(Point point, KeyModifiers modifiers) =>
        _autoPanController.UpdatePointer(point, modifiers);

    private void UpdateAutoPanState() =>
        _autoPanController.UpdateState();

    private void ResetAutoPan() =>
        _autoPanController.Reset();
}
