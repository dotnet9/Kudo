using Kudo.Core.Runtime;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public GraphRuntimeNodeState? GetRuntimeNodeState(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        return _runtimeController.GetNodeState(nodeId);
    }

    public GraphRuntimeEdgeState? GetRuntimeEdgeState(string edgeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edgeId);
        return _runtimeController.GetEdgeState(edgeId);
    }
}
