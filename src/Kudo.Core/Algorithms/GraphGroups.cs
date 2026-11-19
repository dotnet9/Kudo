namespace Kudo.Core.Algorithms;

public static class GraphGroupBoundsCalculator
{
    public const double HeaderHeight = 28;
    public const double CollapsedWidth = 180;
    public const double CollapsedHeight = 44;

    public static GraphRect? Calculate(GraphDocument document, GroupModel group)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Calculate(group, document.Nodes);
    }

    public static GraphRect? Calculate(GroupModel group, IEnumerable<NodeModel> nodes)
    {
        var expandedBounds = CalculateExpanded(group, nodes);
        if (expandedBounds is not { } bounds || !group.IsCollapsed)
        {
            return expandedBounds;
        }

        return new GraphRect(bounds.X, bounds.Y, CollapsedWidth, CollapsedHeight);
    }

    public static GraphRect? CalculateExpanded(GraphDocument document, GroupModel group)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CalculateExpanded(group, document.Nodes);
    }

    public static GraphRect? CalculateExpanded(GroupModel group, IEnumerable<NodeModel> nodes)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(nodes);

        var nodeIds = (group.NodeIds ?? [])
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .ToHashSet(StringComparer.Ordinal);
        if (nodeIds.Count == 0)
        {
            return null;
        }

        var members = nodes.Where(node => nodeIds.Contains(node.Id)).ToArray();
        if (members.Length == 0)
        {
            return null;
        }

        var padding = double.IsFinite(group.Padding) && group.Padding >= 0 ? group.Padding : 0;
        var memberBounds = members.Select(GraphNodeGeometry.GetCanvasBounds).ToArray();
        var minX = memberBounds.Min(bounds => bounds.X);
        var minY = memberBounds.Min(bounds => bounds.Y);
        var maxX = memberBounds.Max(bounds => bounds.X + bounds.Width);
        var maxY = memberBounds.Max(bounds => bounds.Y + bounds.Height);
        return new GraphRect(
            minX - padding,
            minY - padding - HeaderHeight,
            Math.Max(1, maxX - minX + padding * 2),
            Math.Max(1, maxY - minY + padding * 2 + HeaderHeight));
    }
}
