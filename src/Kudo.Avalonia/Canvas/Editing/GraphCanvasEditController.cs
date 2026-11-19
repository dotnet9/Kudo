using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasEditController
{
    private readonly GraphCanvasEdgeEditController _edge;
    private readonly GraphCanvasNodeEditController _node;
    private readonly GraphCanvasDeleteSelectionController _deleteSelection;

    public GraphCanvasEditController(
        GraphCanvasEditContext context,
        GraphCanvasEditMetrics metrics)
    {
        _edge = new GraphCanvasEdgeEditController(context, metrics);
        _node = new GraphCanvasNodeEditController(context);
        _deleteSelection = new GraphCanvasDeleteSelectionController(context, _edge, _node);
    }

    public bool ReconnectEdge(string edgeId, PortReference source, PortReference target) =>
        _edge.ReconnectEdge(edgeId, source, target);

    public Point? GetEdgeLabelScreenPosition(EdgeModel edge) =>
        _edge.GetEdgeLabelScreenPosition(edge);

    public bool AddSelectedEdgeWaypoint(Point screenPoint) =>
        _edge.AddSelectedEdgeWaypoint(screenPoint);

    public bool RemoveSelectedEdgeWaypoint(int index) =>
        _edge.RemoveSelectedEdgeWaypoint(index);

    public bool DeleteSelectedEdges() =>
        _edge.DeleteSelectedEdges();

    public bool ResizeNode(string nodeId, GraphPoint position, GraphSize size) =>
        _node.ResizeNode(nodeId, position, size);

    public bool RotateNode(string nodeId, double rotation) =>
        _node.RotateNode(nodeId, rotation);

    public bool SetSelectedNodesLocked(bool value) =>
        _node.SetSelectedNodesLocked(value);

    public bool ToggleSelectedNodesLocked() =>
        _node.ToggleSelectedNodesLocked();

    public bool NudgeSelectedNodes(GraphPoint offset) =>
        _node.NudgeSelectedNodes(offset);

    public bool DeleteSelectedNode() =>
        _node.DeleteSelectedNode();

    public bool CanDeleteSelectedNodes() =>
        _node.CanDeleteSelectedNodes();

    public bool CanDeleteSelection() =>
        _deleteSelection.CanDeleteSelection();

    public bool TryAuthorizeDeleteSelection() =>
        _deleteSelection.TryAuthorizeDeleteSelection();

    public bool DeleteSelection() =>
        _deleteSelection.DeleteSelection();
}

internal sealed record GraphCanvasEditMetrics(
    double EdgeHitTolerance,
    double EdgeLabelProgress);

internal sealed class GraphCanvasEditContext
{
    public required Func<GraphDocument?> Document { get; init; }

    public required Func<bool> IsReadOnly { get; init; }

    public required Func<bool> IsPointerInteracting { get; init; }

    public required Func<HashSet<string>> SelectedNodeIds { get; init; }

    public required Func<HashSet<string>> SelectedEdgeIds { get; init; }

    public required Func<string?> SelectedEdgeId { get; init; }

    public required Action<string?> SetSelectedEdgeId { get; init; }

    public required Action<IEnumerable<string>, string?> SetSelectedEdges { get; init; }

    public required Func<string?> SelectedAnnotationId { get; init; }

    public required Action<ConnectionValidationResult?> SetLastConnectionValidation { get; init; }

    public required Func<
        PortReference,
        PortReference,
        GraphRelationKind,
        string?,
        ConnectionValidationResult> ValidateConnection { get; init; }

    public required Func<GraphEditRequest, bool> CanPerformEdit { get; init; }

    public required Func<GraphEditRequest, bool> CanAuthorizeEdit { get; init; }

    public required Func<GraphEditRequest, bool> TryAuthorizeEdit { get; init; }

    public required Action<IEnumerable<string>, string?> SetSelectedNodes { get; init; }

    public required Action ClearSelection { get; init; }

    public required Func<IGraphCommand, bool> CommitGraphEdit { get; init; }

    public required Func<NodeModel, GraphSize, GraphSize> ClampNodeSize { get; init; }

    public required Func<EdgeModel, GraphEdgeRoute?> GetEdgeScreenRoute { get; init; }

    public required Func<Point, GraphPoint> ScreenToGraph { get; init; }

    public required Func<EdgeModel, GraphEdgeRoute, Point, int> FindWaypointInsertionIndex { get; init; }

    public required Func<bool> DeleteSelectedAnnotation { get; init; }
}
