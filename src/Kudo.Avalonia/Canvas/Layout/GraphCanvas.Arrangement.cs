using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public bool ApplyLayout(GraphLayoutResult result, string? commandName = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var document = Document;
        if (IsReadOnly || document is null || IsPointerInteracting)
        {
            return false;
        }

        return ApplyNodePositions(
            document,
            document.Nodes,
            result,
            string.IsNullOrWhiteSpace(commandName)
                ? L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.ApplyLayoutCommand)
                : commandName);
    }

    public bool AlignSelectedNodes(NodeAlignment alignment, string? commandName = null)
    {
        if (!Enum.IsDefined(alignment))
        {
            throw new ArgumentOutOfRangeException(nameof(alignment));
        }

        return ArrangeSelectedNodes(
            minimumNodeCount: 2,
            nodes => GraphNodeArrangement.Align(nodes, alignment),
            string.IsNullOrWhiteSpace(commandName)
                ? L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.AlignSelectedNodesCommand)
                : commandName);
    }

    public bool DistributeSelectedNodes(NodeDistribution distribution, string? commandName = null)
    {
        if (!Enum.IsDefined(distribution))
        {
            throw new ArgumentOutOfRangeException(nameof(distribution));
        }

        return ArrangeSelectedNodes(
            minimumNodeCount: 3,
            nodes => GraphNodeArrangement.Distribute(nodes, distribution),
            string.IsNullOrWhiteSpace(commandName)
                ? L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.DistributeSelectedNodesCommand)
                : commandName);
    }

    private bool ArrangeSelectedNodes(
        int minimumNodeCount,
        Func<IReadOnlyList<NodeModel>, GraphLayoutResult> arrange,
        string commandName)
    {
        var document = Document;
        if (IsReadOnly || document is null || IsPointerInteracting)
        {
            return false;
        }

        var nodes = document.Nodes
            .Where(node => _selectedNodeIds.Contains(node.Id) && !node.IsLocked)
            .ToArray();
        if (nodes.Length < minimumNodeCount)
        {
            return false;
        }

        return ApplyNodePositions(document, nodes, arrange(nodes), commandName);
    }

    private bool ApplyNodePositions(
        GraphDocument document,
        IReadOnlyCollection<NodeModel> candidateNodes,
        GraphLayoutResult result,
        string commandName)
    {
        ArgumentNullException.ThrowIfNull(result.NodePositions);
        var changedNodes = new List<NodeModel>();
        var oldPositions = new Dictionary<string, GraphPoint>(StringComparer.Ordinal);
        var newPositions = new Dictionary<string, GraphPoint>(StringComparer.Ordinal);
        foreach (var node in candidateNodes)
        {
            if (node.IsLocked)
            {
                continue;
            }

            if (!result.NodePositions.TryGetValue(node.Id, out var position) ||
                position == node.Position)
            {
                continue;
            }

            if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
            {
                throw new ArgumentException(
                    $"Layout position for node '{node.Id}' must use finite coordinates.",
                    nameof(result));
            }

            changedNodes.Add(node);
            oldPositions[node.Id] = node.Position;
            newPositions[node.Id] = position;
        }

        if (changedNodes.Count == 0 ||
            !CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.MoveNodes,
                document,
                Nodes: changedNodes)))
        {
            return false;
        }

        var command = new SetNodePositionsCommand(oldPositions, newPositions, commandName);
        command.Execute(document);
        return CommitGraphEdit(command);
    }
}
