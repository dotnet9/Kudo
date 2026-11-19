using System.ComponentModel;
using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

public sealed record PortHitResult(
    NodeModel Node,
    PortModel Port,
    PortReference Reference,
    Point ScreenPosition);

public enum GraphEdgeEndpoint
{
    [Description("连线起点")]
    Source,
    [Description("连线终点")]
    Target
}

public sealed record EdgeEndpointHitResult(
    EdgeModel Edge,
    GraphEdgeEndpoint Endpoint,
    PortReference Reference,
    Point ScreenPosition);

public sealed record EdgeWaypointHitResult(
    EdgeModel Edge,
    int Index,
    GraphPoint Waypoint,
    Point ScreenPosition);

public enum OrthogonalEdgeSegmentOrientation
{
    Horizontal,
    Vertical
}

public sealed record EdgeSegmentHitResult(
    EdgeModel Edge,
    int SegmentIndex,
    OrthogonalEdgeSegmentOrientation Orientation,
    IReadOnlyList<GraphPoint> RoutePoints);

public enum GraphNodeResizeHandle
{
    [Description("左上角")]
    TopLeft,
    [Description("顶部")]
    Top,
    [Description("右上角")]
    TopRight,
    [Description("右侧")]
    Right,
    [Description("右下角")]
    BottomRight,
    [Description("底部")]
    Bottom,
    [Description("左下角")]
    BottomLeft,
    [Description("左侧")]
    Left
}

public sealed record NodeResizeHandleHitResult(
    NodeModel Node,
    GraphNodeResizeHandle Handle,
    Point ScreenPosition);

public sealed record NodeRotationHandleHitResult(
    NodeModel Node,
    Point ScreenPosition,
    IReadOnlyList<NodeModel>? Nodes = null)
{
    /// <summary>
    /// Gets the nodes affected by the rotation gesture. A missing value represents
    /// the legacy single-node handle.
    /// </summary>
    public IReadOnlyList<NodeModel> SelectedNodes => Nodes ?? [Node];
}

public sealed record GraphNodePlacementPreview(
    GraphPoint Position,
    GraphSize Size,
    string? Title,
    bool IsValid,
    bool IsGuideSnapX,
    bool IsGuideSnapY,
    bool IsGridSnapX,
    bool IsGridSnapY,
    GraphNodeVisualMode VisualMode = GraphNodeVisualMode.Card)
{
    public bool IsSnapped => IsGuideSnapX || IsGuideSnapY || IsGridSnapX || IsGridSnapY;
}

public sealed class GraphEditCommittedEventArgs(IGraphCommand command) : EventArgs
{
    public IGraphCommand Command { get; } = command ?? throw new ArgumentNullException(nameof(command));
}

public sealed class GraphSelectionChangedEventArgs(
    IReadOnlyList<string> selectedNodeIds,
    IReadOnlyList<string> selectedGroupIds,
    string? selectedNodeId,
    IReadOnlyList<string> selectedEdgeIds,
    string? selectedEdgeId,
    string? selectedAnnotationId) : EventArgs
{
    public IReadOnlyList<string> SelectedNodeIds { get; } =
        selectedNodeIds ?? throw new ArgumentNullException(nameof(selectedNodeIds));

    public IReadOnlyList<string> SelectedGroupIds { get; } =
        selectedGroupIds ?? throw new ArgumentNullException(nameof(selectedGroupIds));

    public string? SelectedNodeId { get; } = selectedNodeId;

    public IReadOnlyList<string> SelectedEdgeIds { get; } =
        selectedEdgeIds ?? throw new ArgumentNullException(nameof(selectedEdgeIds));

    public string? SelectedEdgeId { get; } = selectedEdgeId;

    public string? SelectedAnnotationId { get; } = selectedAnnotationId;

    public bool IsEmpty =>
        SelectedNodeIds.Count == 0 &&
        SelectedGroupIds.Count == 0 &&
        SelectedEdgeIds.Count == 0 &&
        SelectedAnnotationId is null;
}
