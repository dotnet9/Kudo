// 导出选项与主题：定义格式、范围、尺寸、背景和嵌入内容降级策略。
using System.ComponentModel;
using Kudo.Avalonia.Designer;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia.Export;

public enum GraphExportFormat
{
    [Description("PNG 位图")]
    Png,

    [Description("SVG 矢量图")]
    Svg,

    [Description("PDF 文档")]
    Pdf
}

public enum GraphEmbeddedContentPolicy
{
    [Description("绘制占位内容")]
    Placeholder,

    [Description("跳过嵌入内容")]
    Skip,

    [Description("使用调用方提供的快照")]
    Snapshot
}

public sealed record GraphExportTheme
{
    public string BackgroundColor { get; init; } = "#FFFFFF";

    public string NodeFillColor { get; init; } = "#F8FAFC";

    public string NodeStrokeColor { get; init; } = "#475569";

    public string NodeTextColor { get; init; } = "#0F172A";

    public string SymbolNameColor { get; init; } = "#0F172A";

    public string SymbolDescriptionColor { get; init; } = "#64748B";

    public float SymbolNameOffset { get; init; } = 6;

    public float SymbolDescriptionOffset { get; init; } = 6;

    public float SymbolDescriptionFontScale { get; init; } = 0.86f;

    public string InputPortColor { get; init; } = "#22C55E";

    public string OutputPortColor { get; init; } = "#F59E0B";

    public string InputOutputPortColor { get; init; } = "#0E9F8A";

    public string ReferencePortColor { get; init; } = "#64748B";

    public float PortRadius { get; init; } = 5;

    public float PortStrokeWidth { get; init; } = 1.5f;

    public float InputOutputPortOffsetFactor { get; init; } = 0.42f;

    public float InputOutputPortScale { get; init; } = 0.72f;

    public float PortChevronHalfWidthFactor { get; init; } = 0.55f;

    public string EdgeColor { get; init; } = "#64748B";

    public string EdgeLabelFillColor { get; init; } = "#F8FAFC";

    public string EdgeLabelStrokeColor { get; init; } = "#CBD5E1";

    public string GroupFillColor { get; init; } = "#44E2E8F0";

    public string GroupStrokeColor { get; init; } = "#64748B";

    public string AnnotationFillColor { get; init; } = "#FEF3C7";

    public string AnnotationStrokeColor { get; init; } = "#D97706";

    public string EmbeddedContentPlaceholderColor { get; init; } = "#CBD5E1";

    public string FontFamily { get; init; } = "Microsoft YaHei UI";
}

public sealed record GraphExportOptions
{
    public GraphExportFormat Format { get; init; } = GraphExportFormat.Png;

    public GraphRect? Bounds { get; init; }

    public GraphSize? OutputSize { get; init; }

    public double Scale { get; init; } = 1;

    public double Padding { get; init; } = 32;

    public double Dpi { get; init; } = 96;

    public GraphExportTheme Theme { get; init; } = new();

    public GraphEdgeRoutingOptions RoutingOptions { get; init; } = GraphEdgeRoutingOptions.Default;

    /// <summary>
    /// Routing service used by export. Defaults to the SDK service with the built-in routers.
    /// </summary>
    public GraphEdgeRoutingService RoutingService { get; init; } = GraphEdgeRoutingService.Default;

    public GraphEmbeddedContentPolicy EmbeddedContentPolicy { get; init; } =
        GraphEmbeddedContentPolicy.Placeholder;

    public string EmbeddedContentPlaceholderText { get; init; } = "嵌入内容";

    public Func<NodeModel, bool>? HasEmbeddedContent { get; init; }

    public Func<NodeModel, Stream?>? EmbeddedContentSnapshot { get; init; }

    /// <summary>
    /// 将背景图解析器返回的任务相对资源解析为文件路径。
    /// </summary>
    public Func<string, string?>? BackgroundImagePathResolver { get; init; }

    /// <summary>
    /// 使用与画布相同的解析结果获取属性值驱动的背景图。
    /// </summary>
    public IGraphNodeBackgroundImageResolver? BackgroundImageResolver { get; init; }

    public IGraphExportRenderer Renderer { get; init; } = DefaultGraphExportRenderer.Instance;

    /// <summary>
    /// 可选的业务节点导出路由；未命中的节点仍由默认导出器绘制。
    /// </summary>
    public IGraphNodeExportRendererResolver? NodeRendererResolver { get; init; }

    /// <summary>
    /// 可选的业务节点几何边界提供器，供导出裁剪范围使用。
    /// </summary>
    public IGraphNodeGeometryProvider? NodeGeometryProvider { get; init; }

    /// <summary>
    /// Optional port position provider shared with the canvas domain.
    /// </summary>
    public IGraphPortPositionProvider? PortPositionProvider { get; init; }
}
