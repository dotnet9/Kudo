using System.ComponentModel;
using Kudo;

namespace Kudo.Core.Algorithms;

public enum GraphTraversalDirection
{
    [Description("上游节点")]
    Upstream,

    [Description("下游节点")]
    Downstream,

    [Description("全部相连节点")]
    Connected
}

public static class GraphTraversal
{
    public static IReadOnlyList<string> GetAdjacentNodeIds(
        GraphDocument document,
        IEnumerable<string> startNodeIds,
        GraphTraversalDirection direction = GraphTraversalDirection.Connected,
        bool includeStart = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(startNodeIds);
        var nodeIds = document.Nodes
            .Select(node => node.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var knownNodeIds = nodeIds.ToHashSet(StringComparer.Ordinal);
        var starts = startNodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && knownNodeIds.Contains(id))
            .ToHashSet(StringComparer.Ordinal);
        if (starts.Count == 0)
        {
            return [];
        }

        var result = includeStart
            ? starts.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in document.Edges)
        {
            if (!knownNodeIds.Contains(edge.Source.NodeId) || !knownNodeIds.Contains(edge.Target.NodeId))
            {
                continue;
            }

            if (starts.Contains(edge.Source.NodeId) &&
                direction is GraphTraversalDirection.Downstream or GraphTraversalDirection.Connected)
            {
                result.Add(edge.Target.NodeId);
            }

            if (starts.Contains(edge.Target.NodeId) &&
                direction is GraphTraversalDirection.Upstream or GraphTraversalDirection.Connected)
            {
                result.Add(edge.Source.NodeId);
            }
        }

        return nodeIds.Where(result.Contains).ToArray();
    }

    public static IReadOnlyList<string> GetReachableNodeIds(
        GraphDocument document,
        IEnumerable<string> startNodeIds,
        GraphTraversalDirection direction,
        bool includeStart = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(startNodeIds);
        var nodeIds = document.Nodes
            .Select(node => node.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var knownNodeIds = nodeIds.ToHashSet(StringComparer.Ordinal);
        var starts = startNodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && knownNodeIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (starts.Length == 0)
        {
            return [];
        }

        var adjacency = nodeIds.ToDictionary(
            id => id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (var edge in document.Edges)
        {
            if (!knownNodeIds.Contains(edge.Source.NodeId) || !knownNodeIds.Contains(edge.Target.NodeId))
            {
                continue;
            }

            if (direction is GraphTraversalDirection.Downstream or GraphTraversalDirection.Connected)
            {
                adjacency[edge.Source.NodeId].Add(edge.Target.NodeId);
            }

            if (direction is GraphTraversalDirection.Upstream or GraphTraversalDirection.Connected)
            {
                adjacency[edge.Target.NodeId].Add(edge.Source.NodeId);
            }
        }

        var visited = starts.ToHashSet(StringComparer.Ordinal);
        var queue = new Queue<string>(starts);
        while (queue.TryDequeue(out var nodeId))
        {
            foreach (var adjacentNodeId in adjacency[nodeId])
            {
                if (visited.Add(adjacentNodeId))
                {
                    queue.Enqueue(adjacentNodeId);
                }
            }
        }

        if (!includeStart)
        {
            visited.ExceptWith(starts);
        }

        return nodeIds.Where(visited.Contains).ToArray();
    }

    public static IReadOnlyList<string> GetShortestPathNodeIds(
        GraphDocument document,
        string sourceNodeId,
        string targetNodeId,
        GraphTraversalDirection direction = GraphTraversalDirection.Connected)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeId);
        var knownNodeIds = document.Nodes
            .Select(node => node.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (!knownNodeIds.Contains(sourceNodeId) || !knownNodeIds.Contains(targetNodeId))
        {
            return [];
        }

        if (sourceNodeId == targetNodeId)
        {
            return [sourceNodeId];
        }

        var adjacency = knownNodeIds.ToDictionary(
            id => id,
            _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (var edge in document.Edges)
        {
            if (!knownNodeIds.Contains(edge.Source.NodeId) || !knownNodeIds.Contains(edge.Target.NodeId))
            {
                continue;
            }

            if (direction is GraphTraversalDirection.Downstream or GraphTraversalDirection.Connected)
            {
                adjacency[edge.Source.NodeId].Add(edge.Target.NodeId);
            }

            if (direction is GraphTraversalDirection.Upstream or GraphTraversalDirection.Connected)
            {
                adjacency[edge.Target.NodeId].Add(edge.Source.NodeId);
            }
        }

        var previous = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [sourceNodeId] = null
        };
        var queue = new Queue<string>();
        queue.Enqueue(sourceNodeId);
        while (queue.TryDequeue(out var nodeId))
        {
            foreach (var adjacentNodeId in adjacency[nodeId])
            {
                if (!previous.TryAdd(adjacentNodeId, nodeId))
                {
                    continue;
                }

                if (adjacentNodeId == targetNodeId)
                {
                    return BuildPath(previous, targetNodeId);
                }

                queue.Enqueue(adjacentNodeId);
            }
        }

        return [];
    }

    public static IReadOnlyList<string> GetLongestDirectedPathNodeIds(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        IReadOnlyList<string> topologicalOrder;
        try
        {
            topologicalOrder = TopologicalSort.Sort(document);
        }
        catch (InvalidOperationException)
        {
            return [];
        }

        var knownNodeIds = topologicalOrder.ToHashSet(StringComparer.Ordinal);
        var adjacency = topologicalOrder.ToDictionary(
            id => id,
            _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (var edge in document.Edges)
        {
            if (knownNodeIds.Contains(edge.Source.NodeId) && knownNodeIds.Contains(edge.Target.NodeId))
            {
                adjacency[edge.Source.NodeId].Add(edge.Target.NodeId);
            }
        }

        var distances = topologicalOrder.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var previous = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var nodeId in topologicalOrder)
        {
            foreach (var targetNodeId in adjacency[nodeId])
            {
                var candidateDistance = distances[nodeId] + 1;
                if (candidateDistance <= distances[targetNodeId])
                {
                    continue;
                }

                distances[targetNodeId] = candidateDistance;
                previous[targetNodeId] = nodeId;
            }
        }

        var endNodeId = topologicalOrder
            .OrderByDescending(nodeId => distances[nodeId])
            .FirstOrDefault();
        if (endNodeId is null || distances[endNodeId] == 0)
        {
            return [];
        }

        List<string> path = [];
        string? current = endNodeId;
        while (current is not null)
        {
            path.Add(current);
            current = previous.GetValueOrDefault(current);
        }

        path.Reverse();
        return path;
    }

    private static IReadOnlyList<string> BuildPath(
        IReadOnlyDictionary<string, string?> previous,
        string targetNodeId)
    {
        List<string> path = [];
        string? current = targetNodeId;
        while (current is not null)
        {
            path.Add(current);
            current = previous[current];
        }

        path.Reverse();
        return path;
    }
}
