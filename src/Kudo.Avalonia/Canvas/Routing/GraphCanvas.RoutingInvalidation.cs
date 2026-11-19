namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public void InvalidateEdgeRoutingCache()
    {
        ClearEdgeRoutingCache();
        InvalidateVisual();
        EdgeRoutingConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
}
