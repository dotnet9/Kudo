using Avalonia;
using Avalonia.Media;
using Kudo;
using Kudo.Avalonia.Designer;
using Kudo.Core.Algorithms;
using Kudo.Core.Runtime;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasRenderCallbacks
{
    public required Func<EdgeModel, GraphEdgePath?> GetEdgeGraphPath { get; init; }

    public required Func<EdgeModel, GraphEdgeRoute?> GetEdgeScreenRoute { get; init; }

    public required Func<GraphPoint, Point> GraphToScreen { get; init; }

    public required Func<NodeModel, Rect> GetNodeScreenRect { get; init; }

    public required Func<NodeModel, Rect> GetNodeVisualScreenRect { get; init; }

    public required Func<GroupModel, Rect?> GetGroupScreenRect { get; init; }

    public required Func<GroupModel, IReadOnlyList<GraphGroupProxyPort>> GetCollapsedGroupProxyPorts { get; init; }

    public required Func<string, bool> IsNodeVisible { get; init; }

    public required Func<string, bool> IsNodeSelected { get; init; }

    public required Func<string, bool> IsGroupSelected { get; init; }

    public required Func<string, bool> HasBreakpoint { get; init; }

    public required Func<string, bool> IsEdgeCuttingCandidate { get; init; }

    public required Func<NodeModel, PortModel, Point> GetPortScreenPosition { get; init; }

    public required Action<DrawingContext, IEnumerable<AnnotationModel>, GraphRect?> RenderAnnotations { get; init; }
}

internal sealed class GraphCanvasRenderRequest
{
    public required DrawingContext Context { get; init; }

    public required Rect Bounds { get; init; }

    public required GraphPoint ViewportOffset { get; init; }

    public required double Zoom { get; init; }

    public required double MinimumZoom { get; init; }

    public required GraphGridSettings GridSettings { get; init; }

    public required GraphDocument? Document { get; init; }

    public required GraphRect? CullingBounds { get; init; }

    public required Rect? DocumentBounds { get; init; }

    public required bool IsLevelOfDetailEnabled { get; init; }

    public required bool ShowPorts { get; init; }

    public required double LodSimplifiedZoomThreshold { get; init; }

    public required double LodLabelsZoomThreshold { get; init; }

    public required double LodPortsZoomThreshold { get; init; }

    public required GraphLabelVisibility PortLabelVisibility { get; init; }

    public required GraphLabelVisibility SymbolNameVisibility { get; init; }

    public required GraphLabelVisibility SymbolDescriptionVisibility { get; init; }

    public required bool IsOverviewCacheBlockedByInteraction { get; init; }

    public required bool IsEdgeRoutingCacheEnabled { get; init; }

    public required IGraphBackgroundRenderer BackgroundRenderer { get; init; }

    public required IGraphNodeRenderer NodeRenderer { get; init; }

    public required IGraphPortRenderer PortRenderer { get; init; }

    public required IGraphEdgeRenderer EdgeRenderer { get; init; }

    public required IGraphGroupRenderer GroupRenderer { get; init; }

    public required IGraphImageResolver? ImageResolver { get; init; }

    public required IGraphNodeBackgroundImageResolver? BackgroundImageResolver { get; init; }

    public required GraphRuntimeSnapshot? RuntimeSnapshot { get; init; }

    public required IReadOnlySet<string> SelectedNodeIds { get; init; }

    public required IReadOnlyCollection<string> BreakpointNodeIds { get; init; }

    public required IReadOnlySet<string> SelectedEdgeIds { get; init; }

    public required string? EditingNodeTitleId { get; init; }

    public required EdgeModel? ReconnectingEdge { get; init; }

    public required PortHitResult? ConnectionStartPort { get; init; }

    public required PortHitResult? ConnectionCandidatePort { get; init; }

    public required PortHitResult? HoveredPort { get; init; }

    public required string? HoveredNodeId { get; init; }

    public required GraphCanvasRenderCallbacks Callbacks { get; init; }

    public required GraphCanvasRenderPalette Palette { get; init; }
}

internal sealed class GraphCanvasRenderPalette
{
    public required IBrush CanvasBackgroundBrush { get; init; }

    public required IBrush NodeFillBrush { get; init; }

    public required IBrush RuntimeRunningNodeFillBrush { get; init; }

    public required IBrush RuntimePausedNodeFillBrush { get; init; }

    public required IBrush RuntimeFaultedNodeFillBrush { get; init; }

    public required IBrush GroupFillBrush { get; init; }

    public required IBrush GroupHeaderFillBrush { get; init; }

    public required IBrush GroupTitleBrush { get; init; }

    public required IBrush InputPortBrush { get; init; }

    public required IBrush OutputPortBrush { get; init; }

    public required IBrush BidirectionalPortBrush { get; init; }

    public required IBrush ReferencePortBrush { get; init; }

    public required IBrush InvalidPortFillBrush { get; init; }

    public required IBrush InvalidPortStrokeBrush { get; init; }

    public required double PortGlyphRadius { get; init; }

    public required double ActivePortGlyphRadius { get; init; }

    public required double PortCandidateRadius { get; init; }

    public required double PortHighlightPadding { get; init; }

    public required double PortLabelFontSize { get; init; }

    public required double PortLabelOffset { get; init; }

    public required double SymbolNameOffset { get; init; }

    public required double SymbolDescriptionOffset { get; init; }

    public required double SymbolDescriptionFontScale { get; init; }

    public required double PortDisabledOpacity { get; init; }

    public required double PortHighlightOpacity { get; init; }

    public required double InvalidPortHighlightOpacity { get; init; }

    public required double ActivePortStrokeThickness { get; init; }

    public required double PortGlyphStrokeThickness { get; init; }

    public required double InvalidPortStrokeThickness { get; init; }

    public required double InputOutputPortGlyphOffsetFactor { get; init; }

    public required double InputOutputPortGlyphScale { get; init; }

    public required double PortChevronHalfWidthFactor { get; init; }

    public required IBrush TitleBrush { get; init; }

    public required IBrush DescriptionBrush { get; init; }

    public required IBrush PortTextBrush { get; init; }

    public required IBrush BreakpointBrush { get; init; }

    public required IBrush RuntimeSignalBrush { get; init; }

    public required IBrush EdgeLabelFillBrush { get; init; }

    public required IBrush EdgeLabelTextBrush { get; init; }

    public required IPen GridMinorPen { get; init; }

    public required IPen GridMajorPen { get; init; }

    public required IPen NodeStrokePen { get; init; }

    public required IPen SelectedNodeStrokePen { get; init; }

    public required IPen RuntimeActiveNodeStrokePen { get; init; }

    public required IPen GroupStrokePen { get; init; }

    public required IPen SelectedGroupStrokePen { get; init; }

    public required IPen EdgePen { get; init; }

    public required IPen SelectedEdgePen { get; init; }

    public required IPen RuntimeActiveEdgePen { get; init; }

    public required IPen RuntimeSignalStrokePen { get; init; }

    public required IPen EdgeLabelStrokePen { get; init; }

    public required IPen PortStrokePen { get; init; }

    public required IPen BreakpointStrokePen { get; init; }

    public required IPen EdgeCuttingPreviewPen { get; init; }
}

internal sealed class GraphCanvasRenderPlanRequest
{
    public required GraphDocument Document { get; init; }

    public required GraphRect? CullingBounds { get; init; }

    public required Rect? DocumentBounds { get; init; }

    public required double Zoom { get; init; }

    public required bool IsLevelOfDetailEnabled { get; init; }

    public required bool ShowPorts { get; init; }

    public required double LodSimplifiedZoomThreshold { get; init; }

    public required double LodLabelsZoomThreshold { get; init; }

    public required double LodPortsZoomThreshold { get; init; }

    public required IGraphNodeRenderer NodeRenderer { get; init; }

    public required IGraphPortRenderer PortRenderer { get; init; }

    public required IGraphEdgeRenderer EdgeRenderer { get; init; }

    public required IGraphGroupRenderer GroupRenderer { get; init; }

    public required IGraphImageResolver? ImageResolver { get; init; }

    public required IGraphNodeBackgroundImageResolver? BackgroundImageResolver { get; init; }

    public required bool IsOverviewCacheBlockedByInteraction { get; init; }

    public required bool IsEdgeRoutingCacheEnabled { get; init; }

    public required Func<EdgeModel, GraphEdgePath?> EdgePathProvider { get; init; }
}

internal sealed class GraphCanvasRenderPlan
{
    public required IReadOnlyList<GroupModel> Groups { get; init; }

    public required IReadOnlyList<NodeModel> Nodes { get; init; }

    public required IReadOnlyList<EdgeModel> Edges { get; init; }

    public required GraphRect? CullingBounds { get; init; }

    public required Rect? DocumentBounds { get; init; }

    public required bool UseSimplifiedDefaultRendering { get; init; }

    public required bool ShowDefaultLabels { get; init; }

    public required bool ShowDefaultPorts { get; init; }

    public required bool IsDefaultNodeRenderer { get; init; }

    public required bool IsDefaultPortRenderer { get; init; }

    public required bool IsDefaultEdgeRenderer { get; init; }

    public required bool CanUseOverviewBitmapCache { get; init; }
}

internal sealed class GraphCanvasOverviewRenderRequest
{
    public required DrawingContext Context { get; init; }

    public required GraphDocument Document { get; init; }

    public required Rect DocumentBounds { get; init; }

    public required double Zoom { get; init; }

    public required double MinimumZoom { get; init; }

    public required double LodLabelsZoomThreshold { get; init; }

    public required double LodSimplifiedZoomThreshold { get; init; }

    public required GraphPoint ViewportOffset { get; init; }

    public required IGraphImageResolver? ImageResolver { get; init; }

    public required IGraphNodeBackgroundImageResolver? BackgroundImageResolver { get; init; }

    public required IPen EdgePen { get; init; }

    public required IPen NodeStrokePen { get; init; }

    public required IBrush NodeFillBrush { get; init; }

    public required Func<EdgeModel, GraphEdgePath?> EdgePathProvider { get; init; }
}

internal readonly record struct GraphRenderedEdgeDetail(
    EdgeModel Edge,
    GraphEdgeRoute Route,
    bool IsSelected,
    GraphRuntimeEdgeState? RuntimeState,
    IPen Pen,
    bool IsStrokeOverridden);
