namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public event EventHandler? GraphChanged;

    public event EventHandler<GraphEditCommittedEventArgs>? GraphEditCommitted;

    public event EventHandler<GraphEditRejectedEventArgs>? GraphEditRejected;

    public event EventHandler? EditPolicyChanged;

    public event EventHandler? SelectionChanged;

    public event EventHandler<ConnectionValidationResult>? ConnectionValidationChanged;

    public event EventHandler<GraphViewportChangedEventArgs>? ViewportChanged;

    internal event EventHandler? EdgeRoutingConfigurationChanged;

    internal event EventHandler? NodeGeometryChanged;
}
