namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private void SynchronizeNodeControlHosts() =>
        _nodeControlController.Synchronize();

    private void RefreshNodeControlStates() =>
        _nodeControlController.RefreshStates();

    private GraphNodeControlHost? FindNodeControlHost(object? source) =>
        _nodeControlController.FindHost(source);
}
