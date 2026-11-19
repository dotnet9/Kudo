using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private IReadOnlyList<GraphRect> GetEdgeRoutingObstacles(EdgeModel edge)
    {
        var document = Document;
        var sourceNode = document?.FindNode(edge.Source.NodeId);
        var targetNode = document?.FindNode(edge.Target.NodeId);
        if (document is null || sourceNode is null || targetNode is null)
        {
            return [];
        }

        var sourceGroup = document.FindGroupContainingNode(sourceNode.Id);
        var targetGroup = document.FindGroupContainingNode(targetNode.Id);
        var request = new GraphEdgeObstacleRequest(
            document,
            edge,
            sourceNode,
            targetNode,
            sourceGroup?.IsCollapsed == true ? sourceGroup : null,
            targetGroup?.IsCollapsed == true ? targetGroup : null);
        if (EdgeObstacleProvider is not GraphCanvasEdgeObstacleProvider defaultProvider)
        {
            return EdgeObstacleProvider.GetObstacles(request);
        }

        if (_defaultEdgeObstacleSnapshotRevision != _smartEdgeObstacleRevision ||
            _defaultEdgeObstacleSnapshot is null ||
            !ReferenceEquals(_defaultEdgeObstacleSnapshot.Document, document))
        {
            _defaultEdgeObstacleSnapshot = defaultProvider.CreateSnapshot(document);
            _defaultEdgeObstacleSnapshotRevision = _smartEdgeObstacleRevision;
        }

        return defaultProvider.GetObstacles(request, _defaultEdgeObstacleSnapshot);
    }

    private GraphEdgePath RouteEdge(EdgeModel edge, GraphPoint start, GraphPoint end)
    {
        var (startDirection, endDirection) = GetEdgePortDirections(edge);
        return RouteEdge(edge, start, end, startDirection, endDirection);
    }

    private GraphEdgePath RouteEdge(
        EdgeModel edge,
        GraphPoint start,
        GraphPoint end,
        GraphPoint? startDirection,
        GraphPoint? endDirection)
    {
        return EdgeRoutingService.Route(new GraphEdgeRoutingRequest(edge.RoutingStyle, start, end)
        {
            Waypoints = edge.Waypoints ?? [],
            Obstacles = edge.RoutingStyle == EdgeRoutingStyle.SmartOrthogonal
                ? GetEdgeRoutingObstacles(edge)
                : [],
            Options = EdgeRoutingOptions,
            StartDirection = startDirection,
            EndDirection = endDirection
        });
    }

    private (GraphPoint? Start, GraphPoint? End) GetEdgePortDirections(EdgeModel edge)
    {
        var sourceNode = Document?.FindNode(edge.Source.NodeId);
        var targetNode = Document?.FindNode(edge.Target.NodeId);
        var sourcePort = Document?.FindPort(edge.Source);
        var targetPort = Document?.FindPort(edge.Target);
        return (
            sourceNode is null
                ? null
                : PortGeometry.ToCanvasDirection(sourceNode, sourcePort?.OutgoingDirection),
            targetNode is null
                ? null
                : PortGeometry.ToCanvasDirection(targetNode, targetPort?.IncomingDirection));
    }

    private void OnEdgeRoutingConfigurationChanged()
    {
        InvalidateEdgeRoutingCache();
    }

    private void ClearEdgeRoutingCache()
    {
        _edgePathCache.Clear();
        _edgeSpatialIndex.Invalidate();
        _defaultEdgeObstacleSnapshot = null;
        _defaultEdgeObstacleSnapshotRevision = -1;
        _smartEdgeObstacleRevision++;
        InvalidateDocumentGraphBounds();
        InvalidateOverviewDrawing();
    }

    internal void InvalidateEdgeGeometry(EdgeModel edge)
    {
        if (Document is not { } document ||
            !_edgeSpatialIndex.TryUpdateEdge(document.Edges, edge, _edgeGraphPathProvider))
        {
            _edgeSpatialIndex.Invalidate();
        }

        InvalidateDocumentGraphBounds();
        InvalidateOverviewDrawing();
        UpdateEdgeLabelEditorLayout();
    }
}
