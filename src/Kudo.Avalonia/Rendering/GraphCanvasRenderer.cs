using Avalonia;
using Avalonia.Media;
using Kudo;
using Kudo.Avalonia.Designer;
using Kudo.Core.Runtime;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasRenderer
{
    private readonly GraphCanvasRenderCoordinator _coordinator;
    private readonly GraphCanvasRendererContext _context;
    private readonly GraphCanvasRenderCallbacks _callbacks;

    public GraphCanvasRenderer(
        GraphCanvasGeometryState geometryState,
        GraphCanvasRenderState renderState,
        GraphCanvasRendererContext context,
        GraphCanvasRenderCallbacks callbacks)
    {
        _coordinator = new GraphCanvasRenderCoordinator(geometryState, renderState);
        _context = context;
        _callbacks = callbacks;
    }

    public void Render(DrawingContext drawingContext)
    {
        var document = _context.Document();
        var viewport = _context.Viewport();
        var hasDocument = _coordinator.Render(new GraphCanvasRenderRequest
        {
            Context = drawingContext,
            Bounds = _context.Bounds(),
            ViewportOffset = viewport.Offset,
            Zoom = viewport.Zoom,
            MinimumZoom = _context.ViewportOptions().MinimumZoom,
            GridSettings = _context.GridSettings(),
            Document = document,
            CullingBounds = document is null ? null : _context.GetViewportCullingGraphBounds(),
            DocumentBounds = document is null ? null : _context.GetDocumentGraphBounds(),
            IsLevelOfDetailEnabled = _context.IsLevelOfDetailEnabled(),
            ShowPorts = _context.ShowPorts(),
            LodSimplifiedZoomThreshold = _context.LodSimplifiedZoomThreshold(),
            LodLabelsZoomThreshold = _context.LodLabelsZoomThreshold(),
            LodPortsZoomThreshold = _context.LodPortsZoomThreshold(),
            PortLabelVisibility = _context.PortLabelVisibility(),
            SymbolNameVisibility = _context.SymbolNameVisibility(),
            SymbolDescriptionVisibility = _context.SymbolDescriptionVisibility(),
            IsOverviewCacheBlockedByInteraction = _context.IsOverviewCacheBlockedByInteraction(),
            IsEdgeRoutingCacheEnabled = _context.IsEdgeRoutingCacheEnabled(),
            BackgroundRenderer = _context.BackgroundRenderer(),
            NodeRenderer = _context.NodeRenderer(),
            PortRenderer = _context.PortRenderer(),
            EdgeRenderer = _context.EdgeRenderer(),
            GroupRenderer = _context.GroupRenderer(),
            ImageResolver = _context.ImageResolver(),
            BackgroundImageResolver = _context.BackgroundImageResolver(),
            RuntimeSnapshot = _context.RuntimeSnapshot(),
            SelectedNodeIds = _context.SelectedNodeIds(),
            BreakpointNodeIds = _context.BreakpointNodeIds(),
            SelectedEdgeIds = _context.SelectedEdgeIds(),
            EditingNodeTitleId = _context.EditingNodeTitleId(),
            ReconnectingEdge = _context.ReconnectingEdge(),
            ConnectionStartPort = _context.GetConnectionStartPort(),
            ConnectionCandidatePort = _context.GetConnectionCandidatePort(),
            HoveredPort = _context.HoveredPort(),
            HoveredNodeId = _context.HoveredNodeId(),
            Callbacks = _callbacks,
            Palette = _context.CreatePalette()
        });
        if (hasDocument)
        {
            _context.RenderInteractionOverlays(drawingContext);
        }
    }

    public void InvalidateOverview() => _coordinator.InvalidateOverview();
}

internal sealed class GraphCanvasRendererContext
{
    public required Func<Rect> Bounds { get; init; }

    public required Func<GraphViewport> Viewport { get; init; }

    public required Func<GraphViewportOptions> ViewportOptions { get; init; }

    public required Func<GraphGridSettings> GridSettings { get; init; }

    public required Func<GraphDocument?> Document { get; init; }

    public required Func<GraphRect?> GetViewportCullingGraphBounds { get; init; }

    public required Func<Rect?> GetDocumentGraphBounds { get; init; }

    public required Func<bool> IsLevelOfDetailEnabled { get; init; }

    public required Func<bool> ShowPorts { get; init; }

    public required Func<double> LodSimplifiedZoomThreshold { get; init; }

    public required Func<double> LodLabelsZoomThreshold { get; init; }

    public required Func<double> LodPortsZoomThreshold { get; init; }

    public required Func<GraphLabelVisibility> PortLabelVisibility { get; init; }

    public required Func<GraphLabelVisibility> SymbolNameVisibility { get; init; }

    public required Func<GraphLabelVisibility> SymbolDescriptionVisibility { get; init; }

    public required Func<bool> IsOverviewCacheBlockedByInteraction { get; init; }

    public required Func<bool> IsEdgeRoutingCacheEnabled { get; init; }

    public required Func<IGraphBackgroundRenderer> BackgroundRenderer { get; init; }

    public required Func<IGraphNodeRenderer> NodeRenderer { get; init; }

    public required Func<IGraphPortRenderer> PortRenderer { get; init; }

    public required Func<IGraphEdgeRenderer> EdgeRenderer { get; init; }

    public required Func<IGraphGroupRenderer> GroupRenderer { get; init; }

    public required Func<IGraphImageResolver?> ImageResolver { get; init; }

    public required Func<IGraphNodeBackgroundImageResolver?> BackgroundImageResolver { get; init; }

    public required Func<GraphRuntimeSnapshot?> RuntimeSnapshot { get; init; }

    public required Func<IReadOnlySet<string>> SelectedNodeIds { get; init; }

    public required Func<IReadOnlyCollection<string>> BreakpointNodeIds { get; init; }

    public required Func<IReadOnlySet<string>> SelectedEdgeIds { get; init; }

    public required Func<string?> EditingNodeTitleId { get; init; }

    public required Func<EdgeModel?> ReconnectingEdge { get; init; }

    public required Func<PortHitResult?> GetConnectionStartPort { get; init; }

    public required Func<PortHitResult?> GetConnectionCandidatePort { get; init; }

    public required Func<PortHitResult?> HoveredPort { get; init; }

    public required Func<string?> HoveredNodeId { get; init; }

    public required Func<GraphCanvasRenderPalette> CreatePalette { get; init; }

    public required Action<DrawingContext> RenderInteractionOverlays { get; init; }
}
