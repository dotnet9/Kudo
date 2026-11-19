using Avalonia;
using Avalonia.Input;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    internal void StartPan(Point point, IPointer pointer, bool openContextMenuOnRelease) =>
        StartInteraction(new PanInteractionSession(
            this,
            pointer,
            point,
            Viewport.Offset,
            openContextMenuOnRelease));

    internal void StartNodeDrag(Point point, string primaryNodeId, IPointer pointer)
    {
        var document = Document;
        var nodes = document?.Nodes
            .Where(node => _selectedNodeIds.Contains(node.Id) && !node.IsLocked)
            .ToArray() ?? [];
        if (document is null || nodes.Length == 0 ||
            document.FindNode(primaryNodeId)?.IsLocked == true ||
            !CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.MoveNodes,
                document,
                Nodes: nodes)))
        {
            return;
        }

        var startPositions = document.Nodes
            .Where(node => IsNodeSelected(node.Id) && !node.IsLocked)
            .ToDictionary(node => node.Id, node => node.Position, StringComparer.Ordinal);
        StartInteraction(new NodeDragInteractionSession(
            this,
            pointer,
            point,
            primaryNodeId,
            startPositions));
    }

    internal void StartNodeResize(Point point, NodeResizeHandleHitResult hit, IPointer pointer)
    {
        if (Document is not { } document ||
            hit.Node.IsLocked ||
            !CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.ResizeNode,
                document,
                Nodes: [hit.Node])))
        {
            return;
        }

        StartInteraction(new NodeResizeInteractionSession(this, pointer, point, hit));
        Cursor = _keyboardController.GetResizeCursor(hit.Handle);
    }

    internal void StartNodeRotation(Point point, NodeRotationHandleHitResult hit, IPointer pointer)
    {
        if (Document is not { } document ||
            hit.SelectedNodes.Any(node => node.IsLocked) ||
            !CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.EditNodeProperties,
                document,
                Nodes: hit.SelectedNodes)))
        {
            return;
        }

        StartInteraction(new NodeRotationInteractionSession(this, pointer, point, hit));
        Cursor = _keyboardController.GetRotationCursor();
    }

    internal void StartAnnotationDrag(Point point, AnnotationModel annotation, IPointer pointer)
    {
        if (Document is not { } document ||
            !CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.MoveAnnotation,
                document,
                Annotation: annotation)))
        {
            return;
        }

        StartInteraction(new AnnotationDragInteractionSession(
            this,
            pointer,
            point,
            annotation));
    }

    internal void StartAnnotationResize(
        Point point,
        AnnotationResizeHandleHitResult hit,
        IPointer pointer)
    {
        if (Document is not { } document ||
            !CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.ResizeAnnotation,
                document,
                Annotation: hit.Annotation)))
        {
            return;
        }

        StartInteraction(new AnnotationResizeInteractionSession(this, pointer, point, hit));
        Cursor = _keyboardController.GetResizeCursor(hit.Handle);
    }

    internal void StartEdgeWaypointMove(EdgeWaypointHitResult hit, IPointer pointer)
    {
        if (Document is not { } document ||
            !CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.MoveEdgeWaypoint,
                document,
                Edge: hit.Edge)))
        {
            return;
        }

        StartInteraction(new EdgeWaypointInteractionSession(this, pointer, hit));
    }

    internal void StartEdgeSegmentMove(
        Point point,
        EdgeSegmentHitResult hit,
        IPointer pointer)
    {
        if (Document is not { } document ||
            !CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.MoveEdgeWaypoint,
                document,
                Edge: hit.Edge)))
        {
            return;
        }

        StartInteraction(new OrthogonalEdgeSegmentInteractionSession(this, pointer, point, hit));
        Cursor = _keyboardController.GetResizeCursor(
            hit.Orientation == OrthogonalEdgeSegmentOrientation.Horizontal
                ? GraphNodeResizeHandle.Top
                : GraphNodeResizeHandle.Left);
    }

    internal void StartConnection(Point point, PortHitResult startPort, IPointer pointer)
    {
        LastConnectionValidation = null;
        StartInteraction(new ConnectPortsInteractionSession(this, pointer, point, startPort));
    }

    internal void StartEdgeReconnection(EdgeEndpointHitResult hit, IPointer pointer)
    {
        LastConnectionValidation = null;
        StartInteraction(new ReconnectEdgeInteractionSession(this, pointer, hit));
    }

    internal void StartRectangleSelection(
        Point point,
        IPointer pointer,
        RectangleSelectionMode mode)
    {
        var startNodeIds = GetDocumentOrderedNodeIds(_selectedNodeIds);
        var startPrimaryNodeId = SelectedNodeId;
        var startEdgeIds = GetDocumentOrderedEdgeIds(_selectedEdgeIds);
        var startEdgeId = SelectedEdgeId;
        var startAnnotationId = SelectedAnnotationId;
        var baseNodeIds = mode == RectangleSelectionMode.Replace ? [] : startNodeIds;
        if (mode == RectangleSelectionMode.Replace)
        {
            ClearSelection();
        }

        StartInteraction(new RectangleSelectionInteractionSession(
            this,
            pointer,
            point,
            mode,
            baseNodeIds,
            startNodeIds,
            startPrimaryNodeId,
            startEdgeIds,
            startEdgeId,
            startAnnotationId));
    }
}
