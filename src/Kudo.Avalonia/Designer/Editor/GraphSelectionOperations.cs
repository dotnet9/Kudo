using Avalonia;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia.Designer;

public sealed class GraphSelectionOperations
{
    private readonly GraphDocumentView _view;

    internal GraphSelectionOperations(GraphDocumentView view) => _view = view;

    public string? PrimaryNodeId => _view.Canvas.SelectedNodeId;

    public IReadOnlyList<string> NodeIds => _view.Canvas.SelectedNodeIds;

    public IReadOnlyList<string> GroupIds => _view.Canvas.SelectedGroupIds;

    public string? EdgeId => _view.Canvas.SelectedEdgeId;

    public IReadOnlyList<string> EdgeIds => _view.Canvas.SelectedEdgeIds;

    public string? AnnotationId => _view.Canvas.SelectedAnnotationId;

    public bool IsEmpty => NodeIds.Count == 0 && EdgeIds.Count == 0 && AnnotationId is null;

    public bool IsNodeSelected(string nodeId) => _view.Canvas.IsNodeSelected(nodeId);

    public bool IsGroupSelected(string groupId) => _view.Canvas.IsGroupSelected(groupId);

    public bool IsNodeVisible(string nodeId) => _view.Canvas.IsNodeVisible(nodeId);

    public void SetNodes(IEnumerable<string> nodeIds, string? primaryNodeId = null) =>
        _view.Canvas.SetSelectedNodes(nodeIds, primaryNodeId);

    public bool SetEdge(string? edgeId)
    {
        if (edgeId is not null &&
            _view.Session.Document.Edges.All(edge => edge.Id != edgeId))
        {
            return false;
        }

        _view.Canvas.SetSelectedEdges(edgeId is null ? [] : [edgeId], edgeId);
        return _view.Canvas.SelectedEdgeId == edgeId;
    }

    public bool SetEdges(IEnumerable<string> edgeIds, string? primaryEdgeId = null)
    {
        ArgumentNullException.ThrowIfNull(edgeIds);
        var requested = edgeIds
            .Where(edgeId => !string.IsNullOrWhiteSpace(edgeId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (requested.Any(edgeId =>
                _view.Session.Document.Edges.All(edge => edge.Id != edgeId)))
        {
            return false;
        }

        _view.Canvas.SetSelectedEdges(requested, primaryEdgeId);
        return requested.ToHashSet(StringComparer.Ordinal).SetEquals(_view.Canvas.SelectedEdgeIds);
    }

    public bool ToggleEdge(string edgeId) => _view.Canvas.ToggleEdgeSelection(edgeId);

    public bool SetAnnotation(string? annotationId) =>
        _view.Canvas.SetSelectedAnnotation(annotationId);

    public bool SetGroup(string groupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        var group = _view.Session.Document.FindGroup(groupId);
        if (group?.NodeIds is not { Count: > 0 })
        {
            return false;
        }

        var nodeIds = group.NodeIds
            .Where(nodeId => _view.Session.Document.FindNode(nodeId) is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (nodeIds.Length == 0)
        {
            return false;
        }

        _view.Canvas.SetSelectedNodes(nodeIds, nodeIds[0]);
        return _view.Canvas.IsGroupSelected(groupId);
    }

    public bool ToggleNode(string nodeId) => _view.Canvas.ToggleNodeSelection(nodeId);

    public void Clear() => _view.Canvas.ClearSelection();

    public int SelectAllNodes() => _view.Canvas.SelectAllNodes();

    public int InvertNodes() => _view.Canvas.InvertNodeSelection();

    public int SelectNodesOfSameType() => _view.Canvas.SelectNodesOfSameType();

    public int SelectNodesInViewport(bool requireFullyContained = false) =>
        _view.Canvas.SelectNodesInViewport(requireFullyContained);

    public int SelectNodesInGraphRect(Rect graphRect, bool extendSelection = false) =>
        _view.Canvas.SelectNodesInGraphRect(graphRect, extendSelection);

    public bool SelectReachable(GraphTraversalDirection direction) =>
        _view.Canvas.SelectReachableNodes(direction);

    public bool SelectAdjacent(
        GraphTraversalDirection direction = GraphTraversalDirection.Connected) =>
        _view.Canvas.SelectAdjacentNodes(direction);

    public bool SelectShortestPath() =>
        _view.Canvas.SelectShortestPathBetweenSelectedNodes();

    public bool SelectFirstCycle() => _view.Canvas.SelectFirstCycle();

    public bool SelectNodesByRole(GraphNodeRole role) =>
        _view.Canvas.SelectNodesByRole(role);

    public bool SelectCriticalPath() => _view.Canvas.SelectCriticalPath();
}
