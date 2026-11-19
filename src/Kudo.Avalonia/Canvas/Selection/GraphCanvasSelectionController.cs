using Avalonia;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasSelectionController
{
    private readonly GraphCanvasSelectionContext _context;
    private readonly GraphCanvasSelectionQueries _queries;
    private readonly GraphCanvasSelectionSnapshots _snapshots;
    private readonly GraphCanvasSelectionOperations _operations;

    public GraphCanvasSelectionController(GraphCanvasSelectionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _queries = new GraphCanvasSelectionQueries(_context);
        _snapshots = new GraphCanvasSelectionSnapshots(_context, _queries);
        _operations = new GraphCanvasSelectionOperations(
            _context,
            _queries,
            SetSelectedNodes,
            ClearSelection);
    }

    public bool IsNodeSelected(string nodeId) =>
        _queries.IsNodeSelected(nodeId);

    public bool IsEdgeSelected(string edgeId) =>
        _context.SelectedEdgeIds().Contains(edgeId);

    public bool HasBreakpoint(string nodeId) =>
        _queries.HasBreakpoint(nodeId);

    public IReadOnlyList<GroupModel> GetSelectedGroups() =>
        _queries.GetSelectedGroups();

    public bool IsNodeVisible(string nodeId) =>
        _queries.IsNodeVisible(nodeId);

    public bool IsGroupSelected(string groupId) =>
        _queries.IsGroupSelected(groupId);

    public void SetSelectedNodes(IEnumerable<string> nodeIds, string? primaryNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        var document = _context.Document();
        var orderedIds = nodeIds
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .Where(nodeId => document is null || document.FindNode(nodeId) is not null)
            .Where(nodeId => document?.FindNode(nodeId) is not { } node ||
                _context.CanPerformEdit(new GraphEditRequest(
                    GraphEditOperation.SelectNodes,
                    document,
                    Nodes: [node])))
            .ToArray();
        var selectedIds = orderedIds.ToHashSet(StringComparer.Ordinal);
        var primary = !string.IsNullOrWhiteSpace(primaryNodeId) && selectedIds.Contains(primaryNodeId)
            ? primaryNodeId
            : _context.SelectedNodeId() is { } currentPrimary && selectedIds.Contains(currentPrimary)
                ? currentPrimary
                : orderedIds.FirstOrDefault();
        ApplyNodeSelection(orderedIds, primary, clearEdgeSelection: true);
    }

    public bool ToggleNodeSelection(string nodeId)
    {
        var document = _context.Document();
        if (string.IsNullOrWhiteSpace(nodeId) ||
            (document is not null && document.FindNode(nodeId) is null))
        {
            return false;
        }

        var selectedIds = _context.SelectedNodeIds().ToHashSet(StringComparer.Ordinal);
        string? primary;
        if (!selectedIds.Add(nodeId))
        {
            selectedIds.Remove(nodeId);
            primary = _context.SelectedNodeId() == nodeId
                ? GetDocumentOrderedNodeIds(selectedIds).FirstOrDefault()
                : _context.SelectedNodeId();
        }
        else
        {
            primary = nodeId;
        }

        ApplyNodeSelection(GetDocumentOrderedNodeIds(selectedIds), primary, clearEdgeSelection: true);
        return true;
    }

    public void SetSelectedEdges(IEnumerable<string> edgeIds, string? primaryEdgeId = null)
    {
        ArgumentNullException.ThrowIfNull(edgeIds);
        var document = _context.Document();
        var orderedIds = edgeIds
            .Where(edgeId => !string.IsNullOrWhiteSpace(edgeId))
            .Distinct(StringComparer.Ordinal)
            .Where(edgeId => document is null || document.Edges.Any(edge => edge.Id == edgeId))
            .Where(edgeId => document?.Edges.FirstOrDefault(edge => edge.Id == edgeId) is not { } edge ||
                _context.CanPerformEdit(new GraphEditRequest(
                    GraphEditOperation.SelectEdges,
                    document,
                    Edge: edge)))
            .ToArray();
        var selectedIds = orderedIds.ToHashSet(StringComparer.Ordinal);
        var primary = !string.IsNullOrWhiteSpace(primaryEdgeId) && selectedIds.Contains(primaryEdgeId)
            ? primaryEdgeId
            : _context.SelectedEdgeId() is { } currentPrimary && selectedIds.Contains(currentPrimary)
                ? currentPrimary
                : orderedIds.FirstOrDefault();
        ApplyEdgeSelection(orderedIds, primary, clearOtherSelection: true);
    }

    public bool ToggleEdgeSelection(string edgeId)
    {
        var document = _context.Document();
        var edge = document?.Edges.FirstOrDefault(candidate => candidate.Id == edgeId);
        if (edge is null ||
            !_context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.SelectEdges,
                document!,
                Edge: edge)))
        {
            return false;
        }

        var selectedIds = _context.SelectedEdgeIds().ToHashSet(StringComparer.Ordinal);
        string? primary;
        if (!selectedIds.Add(edgeId))
        {
            selectedIds.Remove(edgeId);
            primary = _context.SelectedEdgeId() == edgeId
                ? GetDocumentOrderedEdgeIds(selectedIds).LastOrDefault()
                : _context.SelectedEdgeId();
        }
        else
        {
            primary = edgeId;
        }

        ApplyEdgeSelection(GetDocumentOrderedEdgeIds(selectedIds), primary, clearOtherSelection: true);
        return true;
    }

    public int SelectNodesInGraphRect(Rect graphRect, bool extendSelection = false) =>
        _operations.SelectNodesInGraphRect(graphRect, extendSelection);

    public void ClearSelection() => ApplyNodeSelection([], null, clearEdgeSelection: true);

    public int SelectAllNodes() =>
        _operations.SelectAllNodes();

    public int Invert() =>
        _operations.Invert();

    public int SelectSameType() =>
        _operations.SelectSameType();

    public int SelectNodesInViewport(bool requireFullyContained = false) =>
        _operations.SelectNodesInViewport(requireFullyContained);

    public bool SelectReachable(GraphTraversalDirection direction) =>
        _operations.SelectReachable(direction);

    public bool SelectAdjacent(GraphTraversalDirection direction) =>
        _operations.SelectAdjacent(direction);

    public bool SelectShortestPath() =>
        _operations.SelectShortestPath();

    public bool SelectFirstCycle() =>
        _operations.SelectFirstCycle();

    public bool SelectByRole(GraphNodeRole role) =>
        _operations.SelectByRole(role);

    public bool SelectCriticalPath() =>
        _operations.SelectCriticalPath();

    public void ApplyNodeSelection(
        IReadOnlyCollection<string> nodeIds,
        string? primaryNodeId,
        bool clearEdgeSelection)
    {
        var selectionChanged = !_context.SelectedNodeIds().SetEquals(nodeIds) ||
            _context.SelectedNodeId() != primaryNodeId ||
            (clearEdgeSelection &&
             (_context.SelectedEdgeIds().Count > 0 || _context.SelectedAnnotationId() is not null));
        if (!selectionChanged)
        {
            return;
        }

        _context.SetSuppressSelectionChanged(true);
        _context.SetIsUpdatingNodeSelection(true);
        if (clearEdgeSelection)
        {
            _context.SetIsUpdatingEdgeSelection(true);
        }
        try
        {
            _context.SelectedNodeIds().Clear();
            _context.SelectedNodeIds().UnionWith(nodeIds);
            _context.SetSelectedNodeId(primaryNodeId);
            if (clearEdgeSelection)
            {
                _context.SelectedEdgeIds().Clear();
                _context.SetSelectedEdgeId(null);
                _context.SetSelectedAnnotationId(null);
            }
        }
        finally
        {
            if (clearEdgeSelection)
            {
                _context.SetIsUpdatingEdgeSelection(false);
            }
            _context.SetIsUpdatingNodeSelection(false);
            _context.SetSuppressSelectionChanged(false);
        }

        _context.InvalidateVisual();
        RaiseSelectionChanged();
        _context.RefreshNodeControlStates();
    }

    public void ApplyEdgeSelection(
        IReadOnlyCollection<string> edgeIds,
        string? primaryEdgeId,
        bool clearOtherSelection)
    {
        var selectedIds = edgeIds.ToHashSet(StringComparer.Ordinal);
        var selectionChanged = !_context.SelectedEdgeIds().SetEquals(selectedIds) ||
            _context.SelectedEdgeId() != primaryEdgeId ||
            (clearOtherSelection &&
             (_context.SelectedNodeIds().Count > 0 ||
              _context.SelectedNodeId() is not null ||
              _context.SelectedAnnotationId() is not null));
        if (!selectionChanged)
        {
            return;
        }

        _context.SetSuppressSelectionChanged(true);
        _context.SetIsUpdatingEdgeSelection(true);
        _context.SetIsUpdatingNodeSelection(true);
        try
        {
            _context.SelectedEdgeIds().Clear();
            _context.SelectedEdgeIds().UnionWith(selectedIds);
            _context.SetSelectedEdgeId(primaryEdgeId);
            if (clearOtherSelection)
            {
                _context.SelectedNodeIds().Clear();
                _context.SetSelectedNodeId(null);
                _context.SetSelectedAnnotationId(null);
            }
        }
        finally
        {
            _context.SetIsUpdatingNodeSelection(false);
            _context.SetIsUpdatingEdgeSelection(false);
            _context.SetSuppressSelectionChanged(false);
        }

        _context.InvalidateVisual();
        RaiseSelectionChanged();
        _context.RefreshNodeControlStates();
    }

    public void RaiseSelectionChanged()
        => _snapshots.RaiseSelectionChanged();

    public bool UpdateSelectionSnapshots()
    {
        var selectionStateChanged = SynchronizeSelectionWithDocument();
        return _snapshots.UpdateSelectionSnapshots() || selectionStateChanged;
    }

    public IReadOnlyList<string> GetDocumentOrderedNodeIds(IEnumerable<string> nodeIds)
        => _queries.GetDocumentOrderedNodeIds(nodeIds);

    public IReadOnlyList<string> GetDocumentOrderedEdgeIds(IEnumerable<string> edgeIds)
        => _queries.GetDocumentOrderedEdgeIds(edgeIds);

    private bool SynchronizeSelectionWithDocument()
    {
        var document = _context.Document();
        var nodeIds = document?.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal) ?? [];
        var edgeIds = document?.Edges.Select(edge => edge.Id).ToHashSet(StringComparer.Ordinal) ?? [];
        var selectedNodeIds = _context.SelectedNodeIds()
            .Where(nodeIds.Contains)
            .ToHashSet(StringComparer.Ordinal);
        var selectedEdgeIds = _context.SelectedEdgeIds()
            .Where(edgeIds.Contains)
            .ToHashSet(StringComparer.Ordinal);
        var primaryNodeId = _context.SelectedNodeId() is { } selectedNodeId &&
                            selectedNodeIds.Contains(selectedNodeId)
            ? selectedNodeId
            : _queries.GetDocumentOrderedNodeIds(selectedNodeIds).FirstOrDefault();
        var primaryEdgeId = _context.SelectedEdgeId() is { } selectedEdgeId &&
                            selectedEdgeIds.Contains(selectedEdgeId)
            ? selectedEdgeId
            : _queries.GetDocumentOrderedEdgeIds(selectedEdgeIds).LastOrDefault();
        var annotationId = _context.SelectedAnnotationId();
        if (document?.FindAnnotation(annotationId ?? string.Empty) is null)
        {
            annotationId = null;
        }

        var changed = !_context.SelectedNodeIds().SetEquals(selectedNodeIds) ||
            !_context.SelectedEdgeIds().SetEquals(selectedEdgeIds) ||
            _context.SelectedNodeId() != primaryNodeId ||
            _context.SelectedEdgeId() != primaryEdgeId ||
            _context.SelectedAnnotationId() != annotationId;
        if (!changed)
        {
            return false;
        }

        _context.SetSuppressSelectionChanged(true);
        _context.SetIsUpdatingNodeSelection(true);
        _context.SetIsUpdatingEdgeSelection(true);
        try
        {
            _context.SelectedNodeIds().Clear();
            _context.SelectedNodeIds().UnionWith(selectedNodeIds);
            _context.SelectedEdgeIds().Clear();
            _context.SelectedEdgeIds().UnionWith(selectedEdgeIds);
            _context.SetSelectedNodeId(primaryNodeId);
            _context.SetSelectedEdgeId(primaryEdgeId);
            _context.SetSelectedAnnotationId(annotationId);
        }
        finally
        {
            _context.SetIsUpdatingEdgeSelection(false);
            _context.SetIsUpdatingNodeSelection(false);
            _context.SetSuppressSelectionChanged(false);
        }

        return true;
    }

}

internal sealed class GraphCanvasSelectionContext
{
    public required Func<GraphDocument?> Document { get; init; }

    public required Func<HashSet<string>> SelectedNodeIds { get; init; }

    public required Func<string?> SelectedNodeId { get; init; }

    public required Action<string?> SetSelectedNodeId { get; init; }

    public required Func<string?> SelectedEdgeId { get; init; }

    public required Func<HashSet<string>> SelectedEdgeIds { get; init; }

    public required Action<string?> SetSelectedEdgeId { get; init; }

    public required Func<string?> SelectedAnnotationId { get; init; }

    public required Action<string?> SetSelectedAnnotationId { get; init; }

    public required Action<bool> SetIsUpdatingNodeSelection { get; init; }

    public required Action<bool> SetIsUpdatingEdgeSelection { get; init; }

    public required Action<bool> SetSuppressSelectionChanged { get; init; }

    public required Func<IReadOnlyCollection<string>> BreakpointNodeIds { get; init; }

    public required Func<IReadOnlyList<string>> SelectedNodeIdsSnapshot { get; init; }

    public required Action<IReadOnlyList<string>> SetSelectedNodeIdsSnapshot { get; init; }

    public required Func<IReadOnlyList<string>> SelectedEdgeIdsSnapshot { get; init; }

    public required Action<IReadOnlyList<string>> SetSelectedEdgeIdsSnapshot { get; init; }

    public required Func<IReadOnlyList<string>> SelectedGroupIdsSnapshot { get; init; }

    public required Action<IReadOnlyList<string>> SetSelectedGroupIdsSnapshot { get; init; }

    public required GraphDocumentLookup DocumentLookup { get; init; }

    public required GraphElementSpatialIndex ElementSpatialIndex { get; init; }

    public required Func<GraphEditRequest, bool> CanPerformEdit { get; init; }

    public required Func<Rect?> GetViewportGraphBounds { get; init; }

    public required Func<NodeModel, Rect> GetNodeGraphRect { get; init; }

    public required Action InvalidateVisual { get; init; }

    public required Action RefreshNodeControlStates { get; init; }

    public required Action<GraphSelectionChangedEventArgs> NotifySelectionChanged { get; init; }
}
