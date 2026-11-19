using Avalonia;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasSelectionOperations(
    GraphCanvasSelectionContext context,
    GraphCanvasSelectionQueries queries,
    Action<IEnumerable<string>, string?> setSelectedNodes,
    Action clearSelection)
{
    public int SelectNodesInGraphRect(Rect graphRect, bool extendSelection = false)
    {
        var document = context.Document();
        if (document is null)
        {
            if (!extendSelection)
            {
                clearSelection();
            }

            return 0;
        }

        var normalizedRect = NormalizeRect(graphRect.TopLeft, graphRect.BottomRight);
        var selectedIds = extendSelection
            ? context.SelectedNodeIds().ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        context.ElementSpatialIndex.RebuildIfNeeded(document);
        context.ElementSpatialIndex.AddIntersectingNodeIds(ToGraphRect(normalizedRect), selectedIds);

        var orderedIds = queries.GetDocumentOrderedNodeIds(selectedIds);
        setSelectedNodes(orderedIds, orderedIds.LastOrDefault());
        return orderedIds.Count;
    }

    public int SelectAllNodes()
    {
        var nodeIds = context.Document()?.Nodes.Select(node => node.Id).ToArray() ?? [];
        setSelectedNodes(nodeIds, context.SelectedNodeId());
        return nodeIds.Length;
    }

    public int Invert()
    {
        var document = context.Document();
        if (document is null)
        {
            clearSelection();
            return 0;
        }

        var selectedIds = context.SelectedNodeIds().ToHashSet(StringComparer.Ordinal);
        var invertedIds = document.Nodes
            .Where(node => !selectedIds.Contains(node.Id))
            .Select(node => node.Id)
            .ToArray();
        setSelectedNodes(invertedIds, invertedIds.FirstOrDefault());
        return context.SelectedNodeIds().Count;
    }

    public int SelectSameType()
    {
        var document = context.Document();
        var primaryNode = context.SelectedNodeId() is { } primaryNodeId
            ? document?.FindNode(primaryNodeId)
            : null;
        if (document is null || primaryNode is null)
        {
            return 0;
        }

        var nodeIds = document.Nodes
            .Where(node => string.Equals(node.Type, primaryNode.Type, StringComparison.Ordinal))
            .Select(node => node.Id)
            .ToArray();
        setSelectedNodes(nodeIds, primaryNode.Id);
        return context.SelectedNodeIds().Count;
    }

    public int SelectNodesInViewport(bool requireFullyContained = false)
    {
        var document = context.Document();
        var viewportBounds = context.GetViewportGraphBounds();
        if (document is null || viewportBounds is null)
        {
            return 0;
        }

        context.ElementSpatialIndex.RebuildIfNeeded(document);
        var selectedIds = context.ElementSpatialIndex
            .QueryNodes(ToGraphRect(viewportBounds.Value))
            .Where(node => !requireFullyContained || IsFullyContained(
                context.GetNodeGraphRect(node),
                viewportBounds.Value))
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var orderedIds = queries.GetDocumentOrderedNodeIds(selectedIds);
        setSelectedNodes(orderedIds, orderedIds.LastOrDefault());
        return orderedIds.Count;
    }

    public bool SelectReachable(GraphTraversalDirection direction)
    {
        var document = context.Document();
        var selectedNodeIds = context.SelectedNodeIds();
        if (document is null || selectedNodeIds.Count == 0)
        {
            return false;
        }

        var nodeIds = GraphTraversal.GetReachableNodeIds(document, selectedNodeIds, direction);
        if (nodeIds.Count == 0)
        {
            return false;
        }

        var selectedNodeId = context.SelectedNodeId();
        var primaryNodeId = selectedNodeId is not null &&
                            nodeIds.Contains(selectedNodeId, StringComparer.Ordinal)
            ? selectedNodeId
            : nodeIds[0];
        setSelectedNodes(nodeIds, primaryNodeId);
        return true;
    }

    public bool SelectAdjacent(GraphTraversalDirection direction)
    {
        var document = context.Document();
        var selectedNodeIds = context.SelectedNodeIds();
        if (document is null || selectedNodeIds.Count == 0)
        {
            return false;
        }

        var nodeIds = GraphTraversal.GetAdjacentNodeIds(document, selectedNodeIds, direction);
        if (nodeIds.Count == 0)
        {
            return false;
        }

        setSelectedNodes(nodeIds, context.SelectedNodeId());
        return true;
    }

    public bool SelectShortestPath()
    {
        var document = context.Document();
        var selectedNodeIds = context.SelectedNodeIds();
        var targetNodeId = context.SelectedNodeId();
        if (document is null || selectedNodeIds.Count != 2 || targetNodeId is null)
        {
            return false;
        }

        var sourceNodeId = selectedNodeIds.First(nodeId => nodeId != targetNodeId);
        var path = GraphTraversal.GetShortestPathNodeIds(
            document,
            sourceNodeId,
            targetNodeId,
            GraphTraversalDirection.Connected);
        if (path.Count == 0)
        {
            return false;
        }

        setSelectedNodes(path, targetNodeId);
        return true;
    }

    public bool SelectFirstCycle()
    {
        var document = context.Document();
        if (document is null)
        {
            return false;
        }

        var nodeIds = CycleDetector.FindCycleNodeIds(document);
        if (nodeIds.Count == 0)
        {
            return false;
        }

        setSelectedNodes(nodeIds, nodeIds[0]);
        return true;
    }

    public bool SelectByRole(GraphNodeRole role)
    {
        var document = context.Document();
        if (document is null)
        {
            return false;
        }

        var nodeIds = GraphStructureQueries.GetNodeIds(document, role);
        if (nodeIds.Count == 0)
        {
            return false;
        }

        setSelectedNodes(nodeIds, nodeIds[0]);
        return true;
    }

    public bool SelectCriticalPath()
    {
        var document = context.Document();
        if (document is null)
        {
            return false;
        }

        var nodeIds = GraphTraversal.GetLongestDirectedPathNodeIds(document);
        if (nodeIds.Count == 0)
        {
            return false;
        }

        setSelectedNodes(nodeIds, nodeIds[^1]);
        return true;
    }

    private static Rect NormalizeRect(Point first, Point second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        return new Rect(left, top, Math.Abs(second.X - first.X), Math.Abs(second.Y - first.Y));
    }

    private static GraphRect ToGraphRect(Rect rect) =>
        new(rect.X, rect.Y, rect.Width, rect.Height);

    private static bool IsFullyContained(Rect inner, Rect outer) =>
        inner.Left >= outer.Left &&
        inner.Top >= outer.Top &&
        inner.Right <= outer.Right &&
        inner.Bottom <= outer.Bottom;
}
