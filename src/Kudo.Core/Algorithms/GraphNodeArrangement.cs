using System.ComponentModel;
using Kudo;

namespace Kudo.Core.Algorithms;

public enum NodeAlignment
{
    [Description("左对齐")]
    Left,

    [Description("水平居中对齐")]
    HorizontalCenter,

    [Description("右对齐")]
    Right,

    [Description("顶部对齐")]
    Top,

    [Description("垂直居中对齐")]
    VerticalCenter,

    [Description("底部对齐")]
    Bottom
}

public enum NodeDistribution
{
    [Description("水平均匀分布")]
    Horizontal,

    [Description("垂直均匀分布")]
    Vertical
}

public static class GraphNodeArrangement
{
    public static GraphLayoutResult Align(IEnumerable<NodeModel> nodes, NodeAlignment alignment)
    {
        var selectedNodes = GetDistinctNodes(nodes);
        if (selectedNodes.Count < 2)
        {
            return CreateCurrentLayout(selectedNodes);
        }

        var boundsById = selectedNodes.ToDictionary(
            node => node.Id,
            GraphNodeGeometry.GetCanvasBounds,
            StringComparer.Ordinal);
        var left = boundsById.Values.Min(bounds => bounds.X);
        var top = boundsById.Values.Min(bounds => bounds.Y);
        var right = boundsById.Values.Max(bounds => bounds.X + bounds.Width);
        var bottom = boundsById.Values.Max(bounds => bounds.Y + bounds.Height);
        var horizontalCenter = (left + right) / 2;
        var verticalCenter = (top + bottom) / 2;
        var positions = selectedNodes.ToDictionary(
            node => node.Id,
            node => AlignNode(
                node,
                boundsById[node.Id],
                alignment,
                left,
                top,
                right,
                bottom,
                horizontalCenter,
                verticalCenter),
            StringComparer.Ordinal);
        return new GraphLayoutResult(positions);
    }

    private static GraphPoint AlignNode(
        NodeModel node,
        GraphRect bounds,
        NodeAlignment alignment,
        double left,
        double top,
        double right,
        double bottom,
        double horizontalCenter,
        double verticalCenter) => alignment switch
        {
            NodeAlignment.Left => new GraphPoint(
                node.Position.X + left - bounds.X,
                node.Position.Y),
            NodeAlignment.HorizontalCenter => new GraphPoint(
                node.Position.X + horizontalCenter - (bounds.X + bounds.Width / 2),
                node.Position.Y),
            NodeAlignment.Right => new GraphPoint(
                node.Position.X + right - (bounds.X + bounds.Width),
                node.Position.Y),
            NodeAlignment.Top => new GraphPoint(
                node.Position.X,
                node.Position.Y + top - bounds.Y),
            NodeAlignment.VerticalCenter => new GraphPoint(
                node.Position.X,
                node.Position.Y + verticalCenter - (bounds.Y + bounds.Height / 2)),
            NodeAlignment.Bottom => new GraphPoint(
                node.Position.X,
                node.Position.Y + bottom - (bounds.Y + bounds.Height)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(alignment),
                alignment,
                "Unknown node alignment.")
        };

    public static GraphLayoutResult Distribute(IEnumerable<NodeModel> nodes, NodeDistribution distribution)
    {
        var selectedNodes = GetDistinctNodes(nodes);
        if (selectedNodes.Count < 3)
        {
            return CreateCurrentLayout(selectedNodes);
        }

        return distribution switch
        {
            NodeDistribution.Horizontal => DistributeHorizontally(selectedNodes),
            NodeDistribution.Vertical => DistributeVertically(selectedNodes),
            _ => throw new ArgumentOutOfRangeException(
                nameof(distribution),
                distribution,
                "Unknown node distribution.")
        };
    }

    private static GraphLayoutResult DistributeHorizontally(IReadOnlyCollection<NodeModel> nodes)
    {
        var boundsById = nodes.ToDictionary(
            node => node.Id,
            GraphNodeGeometry.GetCanvasBounds,
            StringComparer.Ordinal);
        var orderedNodes = nodes
            .OrderBy(node => boundsById[node.Id].X)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
        var totalWidth = orderedNodes.Sum(node => boundsById[node.Id].Width);
        var firstBounds = boundsById[orderedNodes[0].Id];
        var lastBounds = boundsById[orderedNodes[^1].Id];
        var span = lastBounds.X + lastBounds.Width - firstBounds.X;
        var spacing = (span - totalWidth) / (orderedNodes.Length - 1);
        var positions = new Dictionary<string, GraphPoint>(StringComparer.Ordinal)
        {
            [orderedNodes[0].Id] = orderedNodes[0].Position
        };
        var x = firstBounds.X + firstBounds.Width + spacing;
        for (var index = 1; index < orderedNodes.Length - 1; index++)
        {
            var node = orderedNodes[index];
            var bounds = boundsById[node.Id];
            positions[node.Id] = new GraphPoint(
                node.Position.X + x - bounds.X,
                node.Position.Y);
            x += bounds.Width + spacing;
        }

        positions[orderedNodes[^1].Id] = orderedNodes[^1].Position;
        return new GraphLayoutResult(positions);
    }

    private static GraphLayoutResult DistributeVertically(IReadOnlyCollection<NodeModel> nodes)
    {
        var boundsById = nodes.ToDictionary(
            node => node.Id,
            GraphNodeGeometry.GetCanvasBounds,
            StringComparer.Ordinal);
        var orderedNodes = nodes
            .OrderBy(node => boundsById[node.Id].Y)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
        var totalHeight = orderedNodes.Sum(node => boundsById[node.Id].Height);
        var firstBounds = boundsById[orderedNodes[0].Id];
        var lastBounds = boundsById[orderedNodes[^1].Id];
        var span = lastBounds.Y + lastBounds.Height - firstBounds.Y;
        var spacing = (span - totalHeight) / (orderedNodes.Length - 1);
        var positions = new Dictionary<string, GraphPoint>(StringComparer.Ordinal)
        {
            [orderedNodes[0].Id] = orderedNodes[0].Position
        };
        var y = firstBounds.Y + firstBounds.Height + spacing;
        for (var index = 1; index < orderedNodes.Length - 1; index++)
        {
            var node = orderedNodes[index];
            var bounds = boundsById[node.Id];
            positions[node.Id] = new GraphPoint(
                node.Position.X,
                node.Position.Y + y - bounds.Y);
            y += bounds.Height + spacing;
        }

        positions[orderedNodes[^1].Id] = orderedNodes[^1].Position;
        return new GraphLayoutResult(positions);
    }

    private static IReadOnlyList<NodeModel> GetDistinctNodes(IEnumerable<NodeModel> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        return nodes
            .DistinctBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static GraphLayoutResult CreateCurrentLayout(IEnumerable<NodeModel> nodes) =>
        new(nodes.ToDictionary(node => node.Id, node => node.Position, StringComparer.Ordinal));
}
