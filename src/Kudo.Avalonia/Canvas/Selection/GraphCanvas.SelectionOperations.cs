using Avalonia;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public void SetSelectedNodes(IEnumerable<string> nodeIds, string? primaryNodeId = null) =>
        _selectionController.SetSelectedNodes(nodeIds, primaryNodeId);

    public bool ToggleNodeSelection(string nodeId) =>
        _selectionController.ToggleNodeSelection(nodeId);

    public void SetSelectedEdges(IEnumerable<string> edgeIds, string? primaryEdgeId = null) =>
        _selectionController.SetSelectedEdges(edgeIds, primaryEdgeId);

    public bool ToggleEdgeSelection(string edgeId) =>
        _selectionController.ToggleEdgeSelection(edgeId);

    public int SelectNodesInGraphRect(Rect graphRect, bool extendSelection = false) =>
        _selectionController.SelectNodesInGraphRect(graphRect, extendSelection);

    public void ClearSelection() => _selectionController.ClearSelection();

    public int SelectAllNodes() => _selectionController.SelectAllNodes();

    public int InvertNodeSelection() => _selectionController.Invert();

    public int SelectNodesOfSameType() => _selectionController.SelectSameType();

    public int SelectNodesInViewport(bool requireFullyContained = false) =>
        _selectionController.SelectNodesInViewport(requireFullyContained);

    public bool SelectReachableNodes(GraphTraversalDirection direction) =>
        _selectionController.SelectReachable(direction);

    public bool SelectAdjacentNodes(GraphTraversalDirection direction = GraphTraversalDirection.Connected)
        => _selectionController.SelectAdjacent(direction);

    public bool SelectShortestPathBetweenSelectedNodes() =>
        _selectionController.SelectShortestPath();

    public bool SelectFirstCycle() => _selectionController.SelectFirstCycle();

    public bool SelectNodesByRole(GraphNodeRole role) => _selectionController.SelectByRole(role);

    public bool SelectCriticalPath() => _selectionController.SelectCriticalPath();
}
