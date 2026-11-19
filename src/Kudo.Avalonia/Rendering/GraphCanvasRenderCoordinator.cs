// 画布渲染协调器：集中编排 Renderer、LOD、空间裁剪、总览缓存和运行态覆盖层。
using Avalonia;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasRenderCoordinator
{
    private readonly GraphCanvasRenderState _renderState;
    private readonly GraphCanvasRenderPlanner _planner;
    private readonly GraphCanvasGroupRenderPass _groups;
    private readonly GraphCanvasEdgeRenderPass _edges;
    private readonly GraphCanvasNodeRenderPass _nodes;
    private readonly GraphCanvasOverviewRenderer _overview;

    public GraphCanvasRenderCoordinator(
        GraphCanvasGeometryState geometryState,
        GraphCanvasRenderState renderState)
    {
        ArgumentNullException.ThrowIfNull(geometryState);
        _renderState = renderState ?? throw new ArgumentNullException(nameof(renderState));

        var ports = new GraphCanvasPortRenderPass(geometryState.DocumentLookup);
        _planner = new GraphCanvasRenderPlanner(geometryState);
        _groups = new GraphCanvasGroupRenderPass();
        _edges = new GraphCanvasEdgeRenderPass(renderState);
        _nodes = new GraphCanvasNodeRenderPass(renderState, ports);
        _overview = new GraphCanvasOverviewRenderer(geometryState, renderState);
    }

    /// <summary>
    /// 执行单帧图形层渲染。Canvas 只提供不可变帧参数和预先缓存的窄回调。
    /// </summary>
    public bool Render(GraphCanvasRenderRequest request)
    {
        request.BackgroundRenderer.Render(new GraphBackgroundRenderContext(
            request.Context,
            request.Bounds,
            request.ViewportOffset,
            request.Zoom,
            request.GridSettings,
            request.Palette.CanvasBackgroundBrush,
            request.Palette.GridMinorPen,
            request.Palette.GridMajorPen));

        if (request.Document is not { } document)
        {
            return false;
        }

        var renderPlan = _planner.Create(new GraphCanvasRenderPlanRequest
        {
            Document = document,
            CullingBounds = request.CullingBounds,
            DocumentBounds = request.DocumentBounds,
            Zoom = request.Zoom,
            IsLevelOfDetailEnabled = request.IsLevelOfDetailEnabled,
            ShowPorts = request.ShowPorts,
            LodSimplifiedZoomThreshold = request.LodSimplifiedZoomThreshold,
            LodLabelsZoomThreshold = request.LodLabelsZoomThreshold,
            LodPortsZoomThreshold = request.LodPortsZoomThreshold,
            NodeRenderer = request.NodeRenderer,
            PortRenderer = request.PortRenderer,
            EdgeRenderer = request.EdgeRenderer,
            GroupRenderer = request.GroupRenderer,
            ImageResolver = request.ImageResolver,
            BackgroundImageResolver = request.BackgroundImageResolver,
            IsOverviewCacheBlockedByInteraction = request.IsOverviewCacheBlockedByInteraction,
            IsEdgeRoutingCacheEnabled = request.IsEdgeRoutingCacheEnabled,
            EdgePathProvider = request.Callbacks.GetEdgeGraphPath
        });

        _groups.Render(request, renderPlan.Groups);
        request.Callbacks.RenderAnnotations(
            request.Context,
            document.Annotations,
            renderPlan.CullingBounds);

        if (renderPlan.CanUseOverviewBitmapCache &&
            renderPlan.DocumentBounds is { } overviewBounds &&
            _overview.TryRender(new GraphCanvasOverviewRenderRequest
            {
                Context = request.Context,
                Document = document,
                DocumentBounds = overviewBounds,
                Zoom = request.Zoom,
                MinimumZoom = request.MinimumZoom,
                LodLabelsZoomThreshold = request.LodLabelsZoomThreshold,
                LodSimplifiedZoomThreshold = request.LodSimplifiedZoomThreshold,
                ViewportOffset = request.ViewportOffset,
                ImageResolver = request.ImageResolver,
                BackgroundImageResolver = request.BackgroundImageResolver,
                EdgePen = request.Palette.EdgePen,
                NodeStrokePen = request.Palette.NodeStrokePen,
                NodeFillBrush = request.Palette.NodeFillBrush,
                EdgePathProvider = request.Callbacks.GetEdgeGraphPath
            }))
        {
            _overview.RenderStateOverlays(request, document);
        }
        else
        {
            _edges.Render(request, renderPlan);
            _nodes.Render(request, renderPlan);
        }

        return true;
    }

    public void InvalidateOverview() => _renderState.InvalidateOverview();
}
