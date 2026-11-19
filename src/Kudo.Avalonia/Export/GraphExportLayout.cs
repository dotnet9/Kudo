// 导出布局：计算文档或指定范围的输出尺寸、缩放和图坐标变换。
using Kudo.Core.Algorithms;
using Kudo.Avalonia.Designer;

namespace Kudo.Avalonia.Export;

public sealed record GraphExportLayout(
    GraphRect GraphBounds,
    int PixelWidth,
    int PixelHeight,
    float Scale,
    float OffsetX,
    float OffsetY)
{
    public static GraphExportLayout Create(GraphDocument document, GraphExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        var bounds = NormalizeBounds(options.Bounds ??
            GraphExportBounds.GetDocumentBounds(document, options));
        var padding = NormalizeNonNegative(options.Padding, 32);
        var paddedWidth = Math.Max(1, bounds.Width + padding * 2);
        var paddedHeight = Math.Max(1, bounds.Height + padding * 2);
        double scale;
        int pixelWidth;
        int pixelHeight;
        if (options.OutputSize is { } outputSize)
        {
            if (!double.IsFinite(outputSize.Width) || outputSize.Width <= 0 ||
                !double.IsFinite(outputSize.Height) || outputSize.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "OutputSize must contain finite positive values.");
            }

            pixelWidth = checked((int)Math.Ceiling(outputSize.Width));
            pixelHeight = checked((int)Math.Ceiling(outputSize.Height));
            scale = Math.Min(pixelWidth / paddedWidth, pixelHeight / paddedHeight);
        }
        else
        {
            scale = NormalizePositive(options.Scale, 1);
            pixelWidth = checked((int)Math.Ceiling(paddedWidth * scale));
            pixelHeight = checked((int)Math.Ceiling(paddedHeight * scale));
        }

        if (pixelWidth > 32_768 || pixelHeight > 32_768)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Export dimensions cannot exceed 32768 pixels per axis.");
        }

        var contentWidth = paddedWidth * scale;
        var contentHeight = paddedHeight * scale;
        var offsetX = (pixelWidth - contentWidth) / 2 + (padding - bounds.X) * scale;
        var offsetY = (pixelHeight - contentHeight) / 2 + (padding - bounds.Y) * scale;
        return new GraphExportLayout(
            bounds,
            Math.Max(1, pixelWidth),
            Math.Max(1, pixelHeight),
            (float)scale,
            (float)offsetX,
            (float)offsetY);
    }

    private static GraphRect NormalizeBounds(GraphRect bounds)
    {
        if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) ||
            !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height))
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "Export bounds must use finite values.");
        }

        return new GraphRect(bounds.X, bounds.Y, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
    }

    private static double NormalizePositive(double value, double fallback) =>
        double.IsFinite(value) && value > 0 ? value : fallback;

    private static double NormalizeNonNegative(double value, double fallback) =>
        double.IsFinite(value) && value >= 0 ? value : fallback;
}

public static class GraphExportBounds
{
    private static readonly GraphRect EmptyBounds = new(0, 0, 640, 480);

    public static GraphRect GetDocumentBounds(GraphDocument document)
        => GetDocumentBounds(document, (IGraphNodeGeometryProvider?)null);

    public static GraphRect GetDocumentBounds(
        GraphDocument document,
        IGraphNodeGeometryProvider? geometryProvider)
    {
        ArgumentNullException.ThrowIfNull(document);
        return GetBounds(
            document,
            document.Nodes.Select(node => node.Id),
            document.Edges.Select(edge => edge.Id),
            document.Groups.Select(group => group.Id),
            document.Annotations.Select(annotation => annotation.Id),
            geometryProvider,
            null);
    }

    internal static GraphRect GetDocumentBounds(
        GraphDocument document,
        GraphExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        return GetBounds(
            document,
            document.Nodes.Select(node => node.Id),
            document.Edges.Select(edge => edge.Id),
            document.Groups.Select(group => group.Id),
            document.Annotations.Select(annotation => annotation.Id),
            options.NodeGeometryProvider,
            options);
    }

    public static GraphRect GetSelectionBounds(
        GraphDocument document,
        IEnumerable<string>? nodeIds = null,
        IEnumerable<string>? edgeIds = null,
        IEnumerable<string>? groupIds = null,
        IEnumerable<string>? annotationIds = null,
        IGraphNodeGeometryProvider? geometryProvider = null) =>
        GetBounds(
            document,
            nodeIds ?? [],
            edgeIds ?? [],
            groupIds ?? [],
            annotationIds ?? [],
            geometryProvider,
            null);

    private static GraphRect GetBounds(
        GraphDocument document,
        IEnumerable<string> nodeIds,
        IEnumerable<string> edgeIds,
        IEnumerable<string> groupIds,
        IEnumerable<string> annotationIds,
        IGraphNodeGeometryProvider? geometryProvider,
        GraphExportOptions? exportOptions)
    {
        ArgumentNullException.ThrowIfNull(document);
        var selectedNodeIds = nodeIds.ToHashSet(StringComparer.Ordinal);
        var selectedEdgeIds = edgeIds.ToHashSet(StringComparer.Ordinal);
        var selectedGroupIds = groupIds.ToHashSet(StringComparer.Ordinal);
        var selectedAnnotationIds = annotationIds.ToHashSet(StringComparer.Ordinal);
        var collapsedNodeIds = document.Groups
            .Where(group => group.IsCollapsed)
            .SelectMany(group => group.NodeIds ?? [])
            .ToHashSet(StringComparer.Ordinal);
        GraphRect? bounds = null;
        foreach (var node in document.Nodes.Where(node => selectedNodeIds.Contains(node.Id)))
        {
            var nodeBounds = GetNodeBounds(node, geometryProvider);
            if (exportOptions is not null && !collapsedNodeIds.Contains(node.Id))
            {
                var canvasSettings = (document.CanvasSettings ?? GraphCanvasSettings.Default).Normalize();
                var labels = GraphExportSymbolLabelLayout.Calculate(
                    node,
                    nodeBounds,
                    canvasSettings,
                    exportOptions.Theme ?? new GraphExportTheme());
                nodeBounds = GraphExportSymbolLabelLayout.Include(nodeBounds, labels);
            }

            bounds = Include(bounds, nodeBounds);
        }

        foreach (var group in document.Groups.Where(group => selectedGroupIds.Contains(group.Id)))
        {
            if (GraphGroupBoundsCalculator.Calculate(document, group) is { } groupBounds)
            {
                bounds = Include(bounds, groupBounds);
            }
        }

        foreach (var annotation in document.Annotations.Where(item => selectedAnnotationIds.Contains(item.Id)))
        {
            bounds = Include(bounds, new GraphRect(
                annotation.Position.X,
                annotation.Position.Y,
                annotation.Size.Width,
                annotation.Size.Height));
        }

        foreach (var edge in document.Edges.Where(edge => selectedEdgeIds.Contains(edge.Id)))
        {
            var source = document.FindNode(edge.Source.NodeId);
            var target = document.FindNode(edge.Target.NodeId);
            if (source is null || target is null)
            {
                continue;
            }

            // Edge 导出需要保留端点节点或折叠分组的完整范围，避免 Marker 和短连线标签被裁切。
            bounds = Include(bounds, GetVisibleNodeBounds(document, source, geometryProvider));
            bounds = Include(bounds, GetVisibleNodeBounds(document, target, geometryProvider));
            foreach (var point in edge.Waypoints ?? [])
            {
                bounds = IncludePoint(bounds, point);
            }
        }

        return bounds ?? EmptyBounds;
    }

    private static GraphRect GetVisibleNodeBounds(
        GraphDocument document,
        NodeModel node,
        IGraphNodeGeometryProvider? geometryProvider)
    {
        var collapsedGroup = document.Groups.FirstOrDefault(group =>
            group.IsCollapsed && (group.NodeIds ?? []).Contains(node.Id, StringComparer.Ordinal));
        return collapsedGroup is not null &&
               GraphGroupBoundsCalculator.Calculate(document, collapsedGroup) is { } groupBounds
            ? groupBounds
            : GetNodeBounds(node, geometryProvider);
    }

    private static GraphRect GetNodeBounds(
        NodeModel node,
        IGraphNodeGeometryProvider? geometryProvider) =>
        geometryProvider?.GetBounds(node) ?? GraphNodeGeometry.GetCanvasBounds(node);

    private static GraphRect Include(GraphRect? current, GraphRect value)
    {
        if (current is not { } bounds)
        {
            return value;
        }

        var left = Math.Min(bounds.X, value.X);
        var top = Math.Min(bounds.Y, value.Y);
        var right = Math.Max(bounds.X + bounds.Width, value.X + value.Width);
        var bottom = Math.Max(bounds.Y + bounds.Height, value.Y + value.Height);
        return new GraphRect(left, top, right - left, bottom - top);
    }

    private static GraphRect IncludePoint(GraphRect? current, GraphPoint point) =>
        Include(current, new GraphRect(point.X, point.Y, 1, 1));
}
