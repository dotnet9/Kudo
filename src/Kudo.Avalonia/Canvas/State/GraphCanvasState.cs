// 画布瞬态状态：统一保存选择、交互和内联编辑状态，不参与模型持久化。
namespace Kudo.Avalonia;

internal sealed class GraphCanvasState
{
    public HashSet<string> SelectedNodeIds { get; } = new(StringComparer.Ordinal);

    public HashSet<string> SelectedEdgeIds { get; } = new(StringComparer.Ordinal);

    internal IReadOnlyList<string> SelectedNodeIdsSnapshot = Array.Empty<string>();

    internal IReadOnlyList<string> SelectedEdgeIdsSnapshot = Array.Empty<string>();

    internal IReadOnlyList<string> SelectedGroupIdsSnapshot = Array.Empty<string>();

    public bool IsUpdatingNodeSelection { get; set; }

    public bool IsUpdatingEdgeSelection { get; set; }

    public bool SuppressSelectionChanged { get; set; }

    public IReadOnlyList<GraphAlignmentGuide> ActiveAlignmentGuides { get; set; } = [];

    public PortHitResult? HoveredPort { get; set; }

    public string? HoveredNodeId { get; set; }

    public IGraphInteractionSession? ActiveInteraction { get; set; }

    public ManualConnectionDraft? ManualConnectionDraft { get; set; }

    public NodeModel? EditingNodeTitle { get; set; }

    public EdgeModel? EditingEdgeLabel { get; set; }

    public AnnotationModel? EditingAnnotation { get; set; }

    public bool IsEndingNodeTitleEdit { get; set; }

    public bool IsEndingEdgeLabelEdit { get; set; }

    public bool IsEndingAnnotationTextEdit { get; set; }

    public GraphCanvasRenderState Render { get; } = new();
}
