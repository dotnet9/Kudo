using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public bool ResizeNode(string nodeId, GraphPoint position, GraphSize size) =>
        _editController.ResizeNode(nodeId, position, size);

    public bool RotateNode(string nodeId, double rotation) =>
        _editController.RotateNode(nodeId, rotation);

    public bool NudgeSelectedNodes(GraphPoint offset) =>
        _editController.NudgeSelectedNodes(offset);

    public bool DeleteSelectedNode() =>
        _editController.DeleteSelectedNode();

    public bool CanDeleteSelectedNodes() =>
        _editController.CanDeleteSelectedNodes();

    public bool SetSelectedNodesLocked(bool value) =>
        _editController.SetSelectedNodesLocked(value);

    public bool ToggleSelectedNodesLocked() =>
        _editController.ToggleSelectedNodesLocked();
}
