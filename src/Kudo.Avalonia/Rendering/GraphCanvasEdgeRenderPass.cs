// 边层渲染：先绘制几何，再绘制运行态覆盖层和标签。
using Avalonia;
using Avalonia.Media;
using Kudo;
using Kudo.Core.Algorithms;
using Kudo.Core.Runtime;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasEdgeRenderPass
{
    private readonly GraphCanvasRenderState _renderState;
    private readonly List<GraphRenderedEdgeDetail> _renderedEdgeDetails = [];

    public GraphCanvasEdgeRenderPass(GraphCanvasRenderState renderState)
    {
        _renderState = renderState ?? throw new ArgumentNullException(nameof(renderState));
    }

    public void Render(
        GraphCanvasRenderRequest request,
        GraphCanvasRenderPlan renderPlan)
    {
        _renderedEdgeDetails.Clear();
        foreach (var edge in renderPlan.Edges)
        {
            if (ReferenceEquals(edge, request.ReconnectingEdge))
            {
                continue;
            }

            var useSimplifiedEdge = renderPlan.IsDefaultEdgeRenderer &&
                                    renderPlan.UseSimplifiedDefaultRendering &&
                                    edge.Appearance?.IsDefault != false;
            GraphEdgePath? edgePath = null;
            GraphEdgeRoute? route = null;
            if (useSimplifiedEdge)
            {
                edgePath = request.Callbacks.GetEdgeGraphPath(edge);
                if (edgePath is null)
                {
                    continue;
                }

            }
            else
            {
                route = request.Callbacks.GetEdgeScreenRoute(edge);
                if (route is null)
                {
                    continue;
                }
            }

            var isRuntimeActive = _renderState.RuntimeEdgeStates.TryGetValue(
                                      edge.Id,
                                      out var runtimeState) &&
                                  runtimeState.IsActive;
            var isCuttingCandidate = request.Callbacks.IsEdgeCuttingCandidate(edge.Id);
            var isSelected = request.SelectedEdgeIds.Contains(edge.Id);
            var isStrokeOverridden = isCuttingCandidate || isSelected || isRuntimeActive;
            var pen = isCuttingCandidate
                ? request.Palette.EdgeCuttingPreviewPen
                : isSelected
                    ? request.Palette.SelectedEdgePen
                    : isRuntimeActive
                        ? request.Palette.RuntimeActiveEdgePen
                        : request.Palette.EdgePen;
            if (useSimplifiedEdge)
            {
                DefaultGraphEdgeRenderer.RenderSimplifiedGeometry(
                    request.Context,
                    edgePath!,
                    request.Callbacks.GraphToScreen,
                    pen);
            }
            else
            {
                RenderEdgeLayer(
                    request,
                    edge,
                    route!,
                    GraphEdgeRenderLayer.Geometry,
                    isSelected,
                    runtimeState,
                    pen,
                    isStrokeOverridden: isStrokeOverridden);
            }

            var renderRuntimeOverlay = runtimeState?.IsActive == true;
            var renderAnimationOverlay = !isStrokeOverridden &&
                                         edge.Appearance?.RequiresAnimationOverlay == true;
            var renderLabel = !string.IsNullOrWhiteSpace(edge.Label) &&
                              (!renderPlan.IsDefaultEdgeRenderer || renderPlan.ShowDefaultLabels);
            if (!renderRuntimeOverlay && !renderAnimationOverlay && !renderLabel)
            {
                continue;
            }

            route ??= GraphEdgeRoute.Create(edgePath!, request.Callbacks.GraphToScreen);
            _renderedEdgeDetails.Add(new GraphRenderedEdgeDetail(
                edge,
                route,
                isSelected,
                runtimeState,
                pen,
                isStrokeOverridden));
        }

        foreach (var rendered in _renderedEdgeDetails)
        {
            if (rendered.RuntimeState?.IsActive == true ||
                !rendered.IsStrokeOverridden &&
                rendered.Edge.Appearance?.RequiresAnimationOverlay == true)
            {
                RenderEdgeLayer(
                    request,
                    rendered.Edge,
                    rendered.Route,
                    GraphEdgeRenderLayer.RuntimeOverlay,
                    rendered.IsSelected,
                    rendered.RuntimeState,
                    rendered.Pen,
                    isStrokeOverridden: rendered.IsStrokeOverridden);
            }

            if (!string.IsNullOrWhiteSpace(rendered.Edge.Label) &&
                (!renderPlan.IsDefaultEdgeRenderer || renderPlan.ShowDefaultLabels))
            {
                RenderEdgeLayer(
                    request,
                    rendered.Edge,
                    rendered.Route,
                    GraphEdgeRenderLayer.Label,
                    rendered.IsSelected,
                    rendered.RuntimeState,
                    rendered.Pen,
                    isStrokeOverridden: rendered.IsStrokeOverridden);
            }
        }
    }

    private void RenderEdgeLayer(
        GraphCanvasRenderRequest request,
        EdgeModel edge,
        GraphEdgeRoute route,
        GraphEdgeRenderLayer layer,
        bool isSelected,
        GraphRuntimeEdgeState? runtimeState,
        IPen strokePen,
        bool isSimplified = false,
        bool isStrokeOverridden = false)
    {
        var renderContext = new GraphEdgeRenderContext(
            request.Context,
            edge,
            route,
            layer,
            request.Zoom,
            isSelected,
            runtimeState,
            _renderState.RuntimeFlowProgress,
            strokePen,
            request.Palette.RuntimeSignalBrush,
            request.Palette.RuntimeSignalStrokePen,
            request.Palette.EdgeLabelFillBrush,
            request.Palette.EdgeLabelStrokePen,
            request.Palette.EdgeLabelTextBrush)
        {
            IsStrokeOverridden = isStrokeOverridden
        };
        if (ReferenceEquals(request.EdgeRenderer, DefaultGraphEdgeRenderer.Instance))
        {
            DefaultGraphEdgeRenderer.Instance.Render(renderContext, isSimplified);
        }
        else
        {
            request.EdgeRenderer.Render(renderContext);
        }
    }
}
