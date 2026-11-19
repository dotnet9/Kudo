using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public bool IsNodeSelected(string nodeId) =>
        _selectionController.IsNodeSelected(nodeId);

    public bool IsEdgeSelected(string edgeId) =>
        _selectionController.IsEdgeSelected(edgeId);

    public bool IsNodeVisible(string nodeId) =>
        _selectionController.IsNodeVisible(nodeId);

    public bool IsGroupSelected(string groupId) =>
        _selectionController.IsGroupSelected(groupId);

    public bool HasBreakpoint(string nodeId) =>
        _selectionController.HasBreakpoint(nodeId);

    private IReadOnlyList<GroupModel> GetSelectedGroups() =>
        _selectionController.GetSelectedGroups();
}
