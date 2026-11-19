using Kudo;

namespace Kudo.Core.Algorithms;

public sealed record LayoutNode(string Id, GraphPoint Position, GraphSize Size);

public sealed record LayoutEdge(string Id, string SourceNodeId, string TargetNodeId);

public sealed class GraphLayoutRequest
{
    public GraphLayoutRequest(GraphDocument document)
    {
        Document = document;
    }

    public GraphDocument Document { get; }

    public double HorizontalSpacing { get; init; } = 240;

    public double VerticalSpacing { get; init; } = 140;

    public GraphPoint Origin { get; init; } = GraphPoint.Zero;
}

public sealed class GraphLayoutResult
{
    public GraphLayoutResult(IReadOnlyDictionary<string, GraphPoint> nodePositions)
    {
        NodePositions = nodePositions;
    }

    public IReadOnlyDictionary<string, GraphPoint> NodePositions { get; }

    public void ApplyTo(GraphDocument document)
    {
        foreach (var node in document.Nodes)
        {
            if (NodePositions.TryGetValue(node.Id, out var position))
            {
                node.Position = position;
            }
        }
    }
}

public interface IGraphLayoutAlgorithm
{
    string Name { get; }

    GraphLayoutResult Layout(GraphLayoutRequest request);
}

public static class TopologicalSort
{
    public static IReadOnlyList<string> Sort(GraphDocument document)
    {
        var nodeIds = document.Nodes.Select(node => node.Id).Distinct(StringComparer.Ordinal).ToList();
        var indegree = nodeIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var outgoing = nodeIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);

        foreach (var edge in document.Edges)
        {
            if (!indegree.ContainsKey(edge.Source.NodeId) || !indegree.ContainsKey(edge.Target.NodeId))
            {
                continue;
            }

            outgoing[edge.Source.NodeId].Add(edge.Target.NodeId);
            indegree[edge.Target.NodeId]++;
        }

        var queue = new Queue<string>(indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var result = new List<string>(nodeIds.Count);

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            result.Add(nodeId);

            foreach (var target in outgoing[nodeId])
            {
                indegree[target]--;
                if (indegree[target] == 0)
                {
                    queue.Enqueue(target);
                }
            }
        }

        if (result.Count != nodeIds.Count)
        {
            throw new InvalidOperationException("The graph contains at least one cycle.");
        }

        return result;
    }
}

public static class CycleDetector
{
    public static bool HasCycle(GraphDocument document)
    {
        try
        {
            _ = TopologicalSort.Sort(document);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    public static IReadOnlyList<string> FindCycleNodeIds(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var nodeIds = document.Nodes
            .Select(node => node.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var knownNodeIds = nodeIds.ToHashSet(StringComparer.Ordinal);
        var adjacency = nodeIds.ToDictionary(
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

        var states = new Dictionary<string, byte>(StringComparer.Ordinal);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var startNodeId in nodeIds)
        {
            if (states.GetValueOrDefault(startNodeId) != 0)
            {
                continue;
            }

            states[startNodeId] = 1;
            var stack = new Stack<(string NodeId, int NextIndex)>();
            stack.Push((startNodeId, 0));
            while (stack.TryPop(out var frame))
            {
                var neighbors = adjacency[frame.NodeId];
                if (frame.NextIndex >= neighbors.Count)
                {
                    states[frame.NodeId] = 2;
                    continue;
                }

                stack.Push((frame.NodeId, frame.NextIndex + 1));
                var neighborNodeId = neighbors[frame.NextIndex];
                var neighborState = states.GetValueOrDefault(neighborNodeId);
                if (neighborState == 0)
                {
                    parents[neighborNodeId] = frame.NodeId;
                    states[neighborNodeId] = 1;
                    stack.Push((neighborNodeId, 0));
                    continue;
                }

                if (neighborState == 1)
                {
                    return BuildCycle(parents, frame.NodeId, neighborNodeId);
                }
            }
        }

        return [];
    }

    private static IReadOnlyList<string> BuildCycle(
        IReadOnlyDictionary<string, string> parents,
        string currentNodeId,
        string cycleStartNodeId)
    {
        if (currentNodeId == cycleStartNodeId)
        {
            return [cycleStartNodeId];
        }

        List<string> tail = [];
        var current = currentNodeId;
        while (current != cycleStartNodeId)
        {
            tail.Add(current);
            current = parents[current];
        }

        tail.Reverse();
        return [cycleStartNodeId, .. tail];
    }
}

public sealed class LayeredLayoutAlgorithm : IGraphLayoutAlgorithm
{
    public string Name => "layered";

    public GraphLayoutResult Layout(GraphLayoutRequest request)
    {
        var document = request.Document;
        var layers = BuildLayers(document);
        var positions = new Dictionary<string, GraphPoint>(StringComparer.Ordinal);

        for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var layer = layers[layerIndex];
            for (var index = 0; index < layer.Count; index++)
            {
                positions[layer[index]] = new GraphPoint(
                    request.Origin.X + layerIndex * request.HorizontalSpacing,
                    request.Origin.Y + index * request.VerticalSpacing);
            }
        }

        return new GraphLayoutResult(positions);
    }

    private static List<List<string>> BuildLayers(GraphDocument document)
    {
        var nodeIds = document.Nodes.Select(node => node.Id).Distinct(StringComparer.Ordinal).ToList();
        var layerByNode = nodeIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var sorted = CycleDetector.HasCycle(document) ? nodeIds : TopologicalSort.Sort(document);

        foreach (var nodeId in sorted)
        {
            var outgoing = document.Edges.Where(edge => edge.Source.NodeId == nodeId);
            foreach (var edge in outgoing)
            {
                if (layerByNode.ContainsKey(edge.Target.NodeId))
                {
                    layerByNode[edge.Target.NodeId] = Math.Max(layerByNode[edge.Target.NodeId], layerByNode[nodeId] + 1);
                }
            }
        }

        return layerByNode
            .GroupBy(pair => pair.Value)
            .OrderBy(group => group.Key)
            .Select(group => group.Select(pair => pair.Key).Order(StringComparer.Ordinal).ToList())
            .ToList();
    }
}
