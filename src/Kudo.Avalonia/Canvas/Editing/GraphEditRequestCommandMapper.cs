using static Kudo.Avalonia.GraphEditRequestFactorySupport;

namespace Kudo.Avalonia;

internal static class GraphEditRequestCommandMapper
{
    public static void Add(
        GraphDocument document,
        IGraphCommand command,
        ICollection<GraphEditRequest> requests)
    {
        if (command is CompositeGraphCommand composite)
        {
            foreach (var child in composite.Commands)
            {
                Add(document, child, requests);
            }

            return;
        }

        requests.Add(command switch
        {
            AddNodeCommand addNode => Request(
                GraphEditOperation.AddNodes,
                document,
                command,
                nodes: [addNode.Node]),
            RemoveNodeCommand removeNode => Request(
                GraphEditOperation.DeleteNodes,
                document,
                command,
                nodes: FindNodes(document, [removeNode.NodeId])),
            MoveNodeCommand moveNode => Request(
                GraphEditOperation.MoveNodes,
                document,
                command,
                nodes: FindNodes(document, [moveNode.NodeId])),
            SetNodeOrderCommand order => Request(
                GraphEditOperation.ReorderNodes,
                document,
                command,
                nodes: FindNodes(document, order.AffectedNodeIds)),
            SetNodePositionsCommand positions => Request(
                GraphEditOperation.MoveNodes,
                document,
                command,
                nodes: FindNodes(document, positions.NewPositions.Keys)),
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
                GraphEditOperation.AddAnnotation,
                document,
                command,
                annotation: addAnnotation.Annotation),
            RemoveAnnotationCommand removeAnnotation => Request(
                GraphEditOperation.DeleteAnnotation,
                document,
                command,
                annotation: document.FindAnnotation(removeAnnotation.AnnotationId)),
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
                GraphEditOperation.ConnectPorts,
                document,
                command,
                edge: addEdge.Edge,
                source: addEdge.Edge.Source,
                target: addEdge.Edge.Target),
            RemoveEdgeCommand removeEdge => Request(
                GraphEditOperation.DeleteEdges,
                document,
                command,
                edge: FindEdge(document, removeEdge.EdgeId)),
            ReconnectEdgeCommand reconnect => Request(
                GraphEditOperation.ReconnectEdge,
                document,
                command,
                edge: FindEdge(document, reconnect.EdgeId),
                source: reconnect.NewSource,
                target: reconnect.NewTarget),
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
                GraphEditOperation.AddEdgeWaypoint,
                document,
                command,
                edge: FindEdge(document, addWaypoint.EdgeId)),
            MoveEdgeWaypointCommand moveWaypoint => Request(
                GraphEditOperation.MoveEdgeWaypoint,
                document,
                command,
                edge: FindEdge(document, moveWaypoint.EdgeId)),
            RemoveEdgeWaypointCommand removeWaypoint => Request(
                GraphEditOperation.RemoveEdgeWaypoint,
                document,
                command,
                edge: FindEdge(document, removeWaypoint.EdgeId)),
            AddGroupCommand addGroup => Request(
                GraphEditOperation.CreateGroup,
                document,
                command,
                nodes: FindNodes(document, addGroup.Group.NodeIds),
                group: addGroup.Group),
            RemoveGroupCommand removeGroup => Request(
                GraphEditOperation.RemoveGroups,
                document,
                command,
                group: document.FindGroup(removeGroup.GroupId)),
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
