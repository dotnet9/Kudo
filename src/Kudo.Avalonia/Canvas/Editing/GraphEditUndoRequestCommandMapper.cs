using static Kudo.Avalonia.GraphEditRequestFactorySupport;

namespace Kudo.Avalonia;

internal static class GraphEditUndoRequestCommandMapper
{
    public static void Add(
        GraphDocument document,
        IGraphCommand command,
        ICollection<GraphEditRequest> requests)
    {
        if (command is CompositeGraphCommand composite)
        {
            foreach (var child in composite.Commands.Reverse())
            {
                Add(document, child, requests);
            }

            return;
        }

        requests.Add(command switch
        {
            AddNodeCommand addNode => Request(
                GraphEditOperation.DeleteNodes,
                document,
                command,
                nodes: [addNode.Node]),
            RemoveNodeCommand removeNode => Request(
                GraphEditOperation.AddNodes,
                document,
                command,
                nodes: removeNode.RemovedNode is { } removedNode ? [removedNode] : []),
            MoveNodeCommand moveNode => Request(
                GraphEditOperation.MoveNodes,
                document,
                command,
                nodes: FindNodes(document, [moveNode.NodeId])),
            SetNodePositionsCommand positions => Request(
                GraphEditOperation.MoveNodes,
                document,
                command,
                nodes: FindNodes(document, positions.OldPositions.Keys)),
            SetNodeOrderCommand order => Request(
                GraphEditOperation.ReorderNodes,
                document,
                command,
                nodes: FindNodes(document, order.AffectedNodeIds)),
            ResizeNodeCommand resizeNode => Request(
                GraphEditOperation.ResizeNode,
                document,
                command,
                nodes: FindNodes(document, [resizeNode.NodeId])),
            SetNodeLockedCommand nodeLock => Request(
                GraphEditOperation.SetNodeLocked,
                document,
                command,
                nodes: FindNodes(document, [nodeLock.NodeId])),
            SetNodePropertiesCommand nodeProperties => Request(
                GraphEditOperation.EditNodeProperties,
                document,
                command,
                nodes: FindNodes(document, [nodeProperties.NodeId])),
            SetNodeAppearanceCommand nodeAppearance => Request(
                GraphEditOperation.EditNodeProperties,
                document,
                command,
                nodes: FindNodes(document, [nodeAppearance.NodeId])),
            ReconfigureNodeCommand reconfigure => Request(
                GraphEditOperation.EditNodeProperties,
                document,
                command,
                nodes: FindNodes(document, [reconfigure.NodeId])),
            AddAnnotationCommand addAnnotation => Request(
                GraphEditOperation.DeleteAnnotation,
                document,
                command,
                annotation: addAnnotation.Annotation),
            RemoveAnnotationCommand removeAnnotation => Request(
                GraphEditOperation.AddAnnotation,
                document,
                command,
                annotation: removeAnnotation.RemovedAnnotation),
            MoveAnnotationCommand moveAnnotation => Request(
                GraphEditOperation.MoveAnnotation,
                document,
                command,
                annotation: document.FindAnnotation(moveAnnotation.AnnotationId)),
            ResizeAnnotationCommand resizeAnnotation => Request(
                GraphEditOperation.ResizeAnnotation,
                document,
                command,
                annotation: document.FindAnnotation(resizeAnnotation.AnnotationId)),
            SetAnnotationTextCommand annotationText => Request(
                GraphEditOperation.EditAnnotationText,
                document,
                command,
                annotation: document.FindAnnotation(annotationText.AnnotationId)),
            SetAnnotationPropertiesCommand annotationProperties => Request(
                GraphEditOperation.EditAnnotationProperties,
                document,
                command,
                annotation: document.FindAnnotation(annotationProperties.AnnotationId)),
            AddEdgeCommand addEdge => Request(
                GraphEditOperation.DeleteEdges,
                document,
                command,
                edge: addEdge.Edge),
            RemoveEdgeCommand removeEdge => Request(
                GraphEditOperation.ConnectPorts,
                document,
                command,
                edge: removeEdge.RemovedEdge,
                source: removeEdge.RemovedEdge?.Source,
                target: removeEdge.RemovedEdge?.Target),
            ReconnectEdgeCommand reconnect => Request(
                GraphEditOperation.ReconnectEdge,
                document,
                command,
                edge: FindEdge(document, reconnect.EdgeId),
                source: reconnect.OldSource,
                target: reconnect.OldTarget),
            SetEdgeRoutingStyleCommand routing => Request(
                GraphEditOperation.EditEdgeProperties,
                document,
                command,
                edge: FindEdge(document, routing.EdgeId)),
            SetEdgePropertiesCommand edgeProperties => Request(
                GraphEditOperation.EditEdgeProperties,
                document,
                command,
                edge: FindEdge(document, edgeProperties.EdgeId)),
            SetEdgeWaypointsCommand waypoints => Request(
                GraphEditOperation.MoveEdgeWaypoint,
                document,
                command,
                edge: FindEdge(document, waypoints.EdgeId)),
            AddEdgeWaypointCommand addWaypoint => Request(
                GraphEditOperation.RemoveEdgeWaypoint,
                document,
                command,
                edge: FindEdge(document, addWaypoint.EdgeId)),
            MoveEdgeWaypointCommand moveWaypoint => Request(
                GraphEditOperation.MoveEdgeWaypoint,
                document,
                command,
                edge: FindEdge(document, moveWaypoint.EdgeId)),
            RemoveEdgeWaypointCommand removeWaypoint => Request(
                GraphEditOperation.AddEdgeWaypoint,
                document,
                command,
                edge: FindEdge(document, removeWaypoint.EdgeId)),
            AddGroupCommand addGroup => Request(
                GraphEditOperation.RemoveGroups,
                document,
                command,
                group: addGroup.Group),
            RemoveGroupCommand removeGroup => Request(
                GraphEditOperation.CreateGroup,
                document,
                command,
                nodes: FindNodes(document, removeGroup.RemovedGroup?.NodeIds),
                group: removeGroup.RemovedGroup),
            SetGroupCollapsedCommand collapsed => Request(
                GraphEditOperation.SetGroupCollapsed,
                document,
                command,
                group: document.FindGroup(collapsed.GroupId)),
            SetGroupPropertiesCommand groupProperties => Request(
                GraphEditOperation.EditGroupProperties,
                document,
                command,
                group: document.FindGroup(groupProperties.GroupId)),
            SetDocumentTitleCommand or SetCanvasSettingsCommand => Request(
                GraphEditOperation.EditDocumentProperties,
                document,
                command),
            _ => Request(GraphEditOperation.ExecuteCommand, document, command)
        });
    }
}
