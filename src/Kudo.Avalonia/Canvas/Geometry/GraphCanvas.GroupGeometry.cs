using Avalonia;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public Rect? GetGroupGraphBounds(GroupModel group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var bounds = Document is { } document
            ? GraphGroupBoundsCalculator.Calculate(document, group)
            : null;
        return bounds is { } value
            ? new Rect(value.X, value.Y, value.Width, value.Height)
            : null;
    }

    private Rect? GetGroupScreenRect(GroupModel group)
    {
        if (GetGroupGraphBounds(group) is not { } bounds)
        {
            return null;
        }

        var origin = GraphToScreen(new GraphPoint(bounds.X, bounds.Y));
        return new Rect(origin.X, origin.Y, bounds.Width * Viewport.Zoom, bounds.Height * Viewport.Zoom);
    }

    private Point GetCollapsedGroupProxyScreenPosition(
        GroupModel group,
        EdgeModel edge,
        GraphEdgeEndpoint endpoint) =>
        GraphToScreen(GetCollapsedGroupProxyGraphPosition(group, edge, endpoint));

    private GraphPoint GetCollapsedGroupProxyGraphPosition(
        GroupModel group,
        EdgeModel edge,
        GraphEdgeEndpoint endpoint)
    {
        var rect = GetGroupGraphBounds(group) ?? default;
        var externalEdges = GetCollapsedGroupExternalEdges(group, endpoint);
        var index = externalEdges.FindIndex(candidate => ReferenceEquals(candidate, edge));
        if (index < 0)
        {
            index = 0;
        }

        var y = rect.Top + rect.Height * (index + 1) / (externalEdges.Count + 1);
        return new GraphPoint(
            endpoint == GraphEdgeEndpoint.Source ? rect.Right : rect.Left,
            y);
    }

    private List<EdgeModel> GetCollapsedGroupExternalEdges(GroupModel group, GraphEdgeEndpoint endpoint)
    {
        var document = Document;
        if (document is null)
        {
            return [];
        }

        var memberIds = (group.NodeIds ?? []).ToHashSet(StringComparer.Ordinal);
        return document.Edges
            .Where(edge =>
            {
                var reference = endpoint == GraphEdgeEndpoint.Source ? edge.Source : edge.Target;
                var opposite = endpoint == GraphEdgeEndpoint.Source ? edge.Target : edge.Source;
                return memberIds.Contains(reference.NodeId) && !memberIds.Contains(opposite.NodeId);
            })
            .ToList();
    }

    private IReadOnlyList<GraphGroupProxyPort> GetCollapsedGroupProxyPorts(GroupModel group)
    {
        List<GraphGroupProxyPort> proxies = [];
        foreach (var endpoint in new[] { GraphEdgeEndpoint.Target, GraphEdgeEndpoint.Source })
        {
            foreach (var edge in GetCollapsedGroupExternalEdges(group, endpoint))
            {
                proxies.Add(new GraphGroupProxyPort(
                    edge,
                    endpoint,
                    GetCollapsedGroupProxyScreenPosition(group, edge, endpoint)));
            }
        }

        return proxies;
    }
}
