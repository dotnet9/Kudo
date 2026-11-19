using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public GraphEdgePath? GetEdgeGraphPath(EdgeModel edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        var document = Document;
        if (document is null)
        {
            return null;
        }

        _documentLookup.RebuildIfNeeded(document);
        var sourceNode = _documentLookup.FindNode(edge.Source.NodeId);
        var targetNode = _documentLookup.FindNode(edge.Target.NodeId);
        var sourcePort = _documentLookup.FindPort(edge.Source);
        var targetPort = _documentLookup.FindPort(edge.Target);
        if (sourceNode is null || targetNode is null || sourcePort is null || targetPort is null)
        {
            return null;
        }

        var sourceGroup = _documentLookup.FindGroupContainingNode(sourceNode.Id);
        var targetGroup = _documentLookup.FindGroupContainingNode(targetNode.Id);
        if (sourceGroup?.IsCollapsed == true && ReferenceEquals(sourceGroup, targetGroup))
        {
            return null;
        }

        var sourceCollapsedGroup = sourceGroup?.IsCollapsed == true ? sourceGroup : null;
        var targetCollapsedGroup = targetGroup?.IsCollapsed == true ? targetGroup : null;
        var start = sourceCollapsedGroup is not null
            ? GetCollapsedGroupProxyGraphPosition(sourceCollapsedGroup, edge, GraphEdgeEndpoint.Source)
            : GetPortGraphPosition(sourceNode, sourcePort);
        var end = targetCollapsedGroup is not null
            ? GetCollapsedGroupProxyGraphPosition(targetCollapsedGroup, edge, GraphEdgeEndpoint.Target)
            : GetPortGraphPosition(targetNode, targetPort);
        var (startDirection, endDirection) = GetEdgePortDirections(edge);
        if (IsEdgeRoutingCacheEnabled &&
            _edgePathCache.TryGet(
                edge,
                start,
                end,
                startDirection,
                endDirection,
                _smartEdgeObstacleRevision,
                out var cachedPath))
        {
            return cachedPath;
        }

        var path = RouteEdge(edge, start, end, startDirection, endDirection);
        if (IsEdgeRoutingCacheEnabled)
        {
            _edgePathCache.Set(
                edge,
                start,
                end,
                startDirection,
                endDirection,
                _smartEdgeObstacleRevision,
                path);
        }

        return path;
    }

    public GraphEdgeRoute? GetEdgeScreenRoute(EdgeModel edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        return GetEdgeGraphPath(edge) is { } path
            ? GraphEdgeRoute.Create(path, GraphToScreen)
            : null;
    }
}
