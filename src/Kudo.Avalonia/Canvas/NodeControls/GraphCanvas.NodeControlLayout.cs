using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private Rect GetNodeControlScreenRect(NodeModel node) =>
        _nodeControlController.GetScreenRect(node);

    private void UpdateNodeControlHostLayout() =>
        _nodeControlController.UpdateLayout();

    internal void UpdateNodeControlHostLayout(IEnumerable<string> nodeIds) =>
        _nodeControlController.UpdateLayout(nodeIds);

    internal void UpdateNodeControlHostLayout(string nodeId) =>
        _nodeControlController.UpdateLayout(nodeId);
}
