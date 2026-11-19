using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public sealed record GraphEdgeObstacleRequest(
    GraphDocument Document,
    EdgeModel Edge,
    NodeModel SourceNode,
    NodeModel TargetNode,
    GroupModel? SourceCollapsedGroup,
    GroupModel? TargetCollapsedGroup);

public interface IGraphEdgeObstacleProvider
{
    IReadOnlyList<GraphRect> GetObstacles(GraphEdgeObstacleRequest request);
}

public sealed class GraphCanvasEdgeObstacleProvider : IGraphEdgeObstacleProvider
{
    public static GraphCanvasEdgeObstacleProvider Default { get; } = new();

    public IReadOnlyList<GraphRect> GetObstacles(GraphEdgeObstacleRequest request)
    {
        ValidateRequest(request);
        return GetObstacles(request, CreateSnapshot(request.Document));
    }

    internal GraphEdgeObstacleSnapshot CreateSnapshot(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var collapsedGroups = document.Groups.Where(group => group.IsCollapsed).ToArray();
        var collapsedNodeIds = collapsedGroups
            .SelectMany(group => group.NodeIds ?? [])
            .ToHashSet(StringComparer.Ordinal);
        var obstacles = new List<GraphRect>(document.Nodes.Count + collapsedGroups.Length);
        var nodeIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        var groupIndices = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var node in document.Nodes)
        {
            if (collapsedNodeIds.Contains(node.Id))
            {
                continue;
            }

            nodeIndices.TryAdd(node.Id, obstacles.Count);
            obstacles.Add(GraphNodeGeometry.GetCanvasBounds(node));
        }

        foreach (var group in collapsedGroups)
        {
            if (GraphGroupBoundsCalculator.Calculate(document, group) is not { } bounds)
            {
                continue;
            }

            groupIndices.TryAdd(group.Id, obstacles.Count);
            obstacles.Add(bounds);
        }

        return new GraphEdgeObstacleSnapshot(
            document,
            obstacles.ToArray(),
            nodeIndices,
            groupIndices);
    }

    internal IReadOnlyList<GraphRect> GetObstacles(
        GraphEdgeObstacleRequest request,
        GraphEdgeObstacleSnapshot snapshot)
    {
        ValidateRequest(request);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!ReferenceEquals(request.Document, snapshot.Document))
        {
            throw new ArgumentException(
                "The obstacle snapshot belongs to a different document.",
                nameof(snapshot));
        }

        return snapshot.CreateView(request);
    }

    private static void ValidateRequest(GraphEdgeObstacleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        ArgumentNullException.ThrowIfNull(request.Edge);
        ArgumentNullException.ThrowIfNull(request.SourceNode);
        ArgumentNullException.ThrowIfNull(request.TargetNode);
    }
}
