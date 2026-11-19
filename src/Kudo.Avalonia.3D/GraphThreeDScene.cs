using System.ComponentModel;
using Avalonia.Media;

namespace Kudo.Avalonia.ThreeD;

public enum GraphThreeDShapeKind
{
    [Description("立方体")]
    Cube,

    [Description("棱锥")]
    Pyramid
}

public enum GraphThreeDRenderMode
{
    [Description("自动选择渲染器")]
    Auto,

    [Description("OpenGL 渲染")]
    OpenGl,

    [Description("软件渲染")]
    Software
}

public enum GraphThreeDActiveRenderer
{
    [Description("未启用渲染器")]
    None,

    [Description("当前使用 OpenGL")]
    OpenGl,

    [Description("当前使用软件渲染")]
    Software
}

public readonly record struct GraphThreeDColor(byte R, byte G, byte B, byte A = 255)
{
    public static GraphThreeDColor Accent { get; } = new(35, 120, 213);

    public static GraphThreeDColor FromAvaloniaColor(Color color) =>
        new(color.R, color.G, color.B, color.A);

    public Color ToAvaloniaColor() => Color.FromArgb(A, R, G, B);
}

public readonly record struct GraphThreeDPoint(double X, double Y, double Z)
{
    public static GraphThreeDPoint Zero { get; } = new(0, 0, 0);

    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);

    public GraphThreeDPoint Normalize() => IsFinite ? this : Zero;
}

public readonly record struct GraphThreeDSize(double Width, double Height, double Depth)
{
    public static GraphThreeDSize Default { get; } = new(1.0, 0.55, 0.35);

    public GraphThreeDSize Normalize() =>
        new(
            NormalizeDimension(Width, Default.Width, 0.25, 2.4),
            NormalizeDimension(Height, Default.Height, 0.18, 1.4),
            NormalizeDimension(Depth, Default.Depth, 0.12, 1.2));

    private static double NormalizeDimension(double value, double fallback, double minimum, double maximum) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}

public sealed record GraphThreeDNode
{
    public string Id { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? Type { get; init; }

    public GraphThreeDPoint Position { get; init; } = GraphThreeDPoint.Zero;

    public GraphThreeDSize Size { get; init; } = GraphThreeDSize.Default;

    public GraphThreeDShapeKind? ShapeKind { get; init; }

    public GraphThreeDColor Color { get; init; } = GraphThreeDColor.Accent;

    public bool IsSelected { get; init; }

    public GraphThreeDNode Normalize() =>
        this with
        {
            Id = Id ?? string.Empty,
            Position = Position.Normalize(),
            Size = Size.Normalize()
        };
}

public sealed record GraphThreeDEdge
{
    public string Id { get; init; } = string.Empty;

    public string SourceNodeId { get; init; } = string.Empty;

    public string TargetNodeId { get; init; } = string.Empty;

    public bool IsSelected { get; init; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Id) &&
        !string.IsNullOrWhiteSpace(SourceNodeId) &&
        !string.IsNullOrWhiteSpace(TargetNodeId);
}

public sealed record GraphThreeDScene
{
    public static GraphThreeDScene Default { get; } = new();

    public GraphThreeDColor Color { get; init; } = GraphThreeDColor.Accent;

    public GraphThreeDShapeKind ShapeKind { get; init; } = GraphThreeDShapeKind.Cube;

    public double Zoom { get; init; } = 1.0;

    public int ShapeCount { get; init; } = 1;

    public bool AutoRotate { get; init; } = true;

    public double Pitch { get; init; } = 24;

    public double Yaw { get; init; } = 34;

    public IReadOnlyList<GraphThreeDNode> Nodes { get; init; } = [];

    public IReadOnlyList<GraphThreeDEdge> Edges { get; init; } = [];

    public IReadOnlyList<string> SelectedNodeIds { get; init; } = [];

    public IReadOnlyList<string> SelectedEdgeIds { get; init; } = [];

    public bool HasGraph => Nodes?.Count > 0;

    public GraphThreeDScene Normalize()
    {
        var nodes = NormalizeNodes(Nodes, SelectedNodeIds);
        var edges = NormalizeEdges(Edges, nodes, SelectedEdgeIds);
        var selectedNodeIds = NormalizeSelectedNodeIds(SelectedNodeIds, nodes);
        var selectedEdgeIds = edges
            .Where(edge => edge.IsSelected)
            .Select(edge => edge.Id)
            .ToArray();

        return this with
        {
            Zoom = double.IsFinite(Zoom) ? Math.Clamp(Zoom, 0.35, 2.5) : 1.0,
            ShapeCount = Math.Clamp(ShapeCount, 1, 9),
            Pitch = double.IsFinite(Pitch) ? Math.Clamp(Pitch, -65, 65) : 24,
            Yaw = double.IsFinite(Yaw) ? Yaw : 34,
            Nodes = nodes,
            Edges = edges,
            SelectedNodeIds = selectedNodeIds,
            SelectedEdgeIds = selectedEdgeIds
        };
    }

    private static IReadOnlyList<GraphThreeDNode> NormalizeNodes(
        IReadOnlyList<GraphThreeDNode>? nodes,
        IReadOnlyList<string>? selectedNodeIds)
    {
        var selectedIds = (selectedNodeIds ?? [])
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .ToHashSet(StringComparer.Ordinal);
        return (nodes ?? [])
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .Select(node =>
            {
                var normalized = node.Normalize();
                return normalized with
                {
                    IsSelected = normalized.IsSelected || selectedIds.Contains(normalized.Id)
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<GraphThreeDEdge> NormalizeEdges(
        IReadOnlyList<GraphThreeDEdge>? edges,
        IReadOnlyList<GraphThreeDNode>? nodes,
        IReadOnlyList<string>? selectedEdgeIds)
    {
        var nodeIds = (nodes ?? [])
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var selectedIds = (selectedEdgeIds ?? [])
            .Where(edgeId => !string.IsNullOrWhiteSpace(edgeId))
            .ToHashSet(StringComparer.Ordinal);
        return (edges ?? [])
            .Where(edge =>
                edge.IsValid &&
                nodeIds.Contains(edge.SourceNodeId) &&
                nodeIds.Contains(edge.TargetNodeId))
            .Select(edge => edge with
            {
                IsSelected = edge.IsSelected ||
                    selectedIds.Contains(edge.Id)
            })
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeSelectedNodeIds(
        IReadOnlyList<string>? selectedNodeIds,
        IReadOnlyList<GraphThreeDNode>? nodes)
    {
        var nodeIds = (nodes ?? [])
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var selectedByNode = (nodes ?? [])
            .Where(node => node.IsSelected)
            .Select(node => node.Id);
        return (selectedNodeIds ?? [])
            .Concat(selectedByNode)
            .Where(nodeId => nodeIds.Contains(nodeId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
