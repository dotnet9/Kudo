namespace Kudo.Avalonia;

internal sealed class GraphCanvasSelectionQueries(GraphCanvasSelectionContext context)
{
    public bool IsNodeSelected(string nodeId) =>
        !string.IsNullOrWhiteSpace(nodeId) && context.SelectedNodeIds().Contains(nodeId);

    public bool IsNodeVisible(string nodeId)
    {
        var document = context.Document();
        if (document is null)
        {
            return false;
        }

        context.DocumentLookup.RebuildIfNeeded(document);
        return context.DocumentLookup.FindNode(nodeId) is not null &&
               context.DocumentLookup.FindGroupContainingNode(nodeId)?.IsCollapsed != true;
    }

    public bool IsGroupSelected(string groupId)
    {
        var document = context.Document();
        var group = document?.FindGroup(groupId);
        if (document is null || group?.NodeIds is null)
        {
            return false;
        }

        var nodeIds = group.NodeIds
            .Where(nodeId => document.FindNode(nodeId) is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return nodeIds.Length > 0 && nodeIds.All(IsNodeSelected);
    }

    public bool HasBreakpoint(string nodeId) =>
        !string.IsNullOrWhiteSpace(nodeId) &&
        context.BreakpointNodeIds().Contains(nodeId, StringComparer.Ordinal);

    public IReadOnlyList<GroupModel> GetSelectedGroups() =>
        context.Document()?.Groups
            .Where(group => IsGroupSelected(group.Id))
            .ToArray() ?? [];

    public IReadOnlyList<string> GetDocumentOrderedNodeIds(IEnumerable<string> nodeIds)
    {
        var selectedIds = nodeIds.ToHashSet(StringComparer.Ordinal);
        var document = context.Document();
        if (document is null)
        {
            return selectedIds.ToArray();
        }

        return document.Nodes
            .Where(node => selectedIds.Contains(node.Id))
            .Select(node => node.Id)
            .ToArray();
    }

    public IReadOnlyList<string> GetDocumentOrderedEdgeIds(IEnumerable<string> edgeIds)
    {
        var selectedIds = edgeIds.ToHashSet(StringComparer.Ordinal);
        var document = context.Document();
        if (document is null)
        {
            return selectedIds.ToArray();
        }

        return document.Edges
            .Where(edge => selectedIds.Contains(edge.Id))
            .Select(edge => edge.Id)
            .ToArray();
    }

    public IReadOnlyList<string> GetDocumentOrderedGroupIds() =>
        context.Document()?.Groups
            .Where(group => IsGroupSelected(group.Id))
            .Select(group => group.Id)
            .ToArray() ?? [];
}
