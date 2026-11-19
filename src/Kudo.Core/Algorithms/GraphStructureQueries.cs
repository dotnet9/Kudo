using System.ComponentModel;
using Kudo;

namespace Kudo.Core.Algorithms;

public enum GraphNodeRole
{
    [Description("源节点")]
    Source,

    [Description("汇节点")]
    Sink,

    [Description("孤立节点")]
    Isolated
}

public static class GraphStructureQueries
{
    public static IReadOnlyList<string> GetNodeIds(GraphDocument document, GraphNodeRole role)
    {
        ArgumentNullException.ThrowIfNull(document);
        var nodeIds = document.Nodes
            .Select(node => node.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var knownNodeIds = nodeIds.ToHashSet(StringComparer.Ordinal);
        var incomingCounts = nodeIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var outgoingCounts = nodeIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (var edge in document.Edges)
        {
            if (!knownNodeIds.Contains(edge.Source.NodeId) || !knownNodeIds.Contains(edge.Target.NodeId))
            {
                continue;
            }

            outgoingCounts[edge.Source.NodeId]++;
            incomingCounts[edge.Target.NodeId]++;
        }

        return nodeIds.Where(nodeId => role switch
        {
            GraphNodeRole.Source => incomingCounts[nodeId] == 0 && outgoingCounts[nodeId] > 0,
            GraphNodeRole.Sink => outgoingCounts[nodeId] == 0 && incomingCounts[nodeId] > 0,
            GraphNodeRole.Isolated => incomingCounts[nodeId] == 0 && outgoingCounts[nodeId] == 0,
            _ => false
        }).ToArray();
    }
}
