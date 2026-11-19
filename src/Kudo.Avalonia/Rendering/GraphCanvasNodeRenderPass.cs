// 节点层渲染：节点主体、节点内端口和符号标签。
using Avalonia;
using Avalonia.Media;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasNodeRenderPass
{
    private readonly GraphCanvasRenderState _renderState;
    private readonly GraphCanvasPortRenderPass _ports;

    public GraphCanvasNodeRenderPass(
        GraphCanvasRenderState renderState,
        GraphCanvasPortRenderPass ports)
    {
        _renderState = renderState ?? throw new ArgumentNullException(nameof(renderState));
        _ports = ports ?? throw new ArgumentNullException(nameof(ports));
    }

    public void Render(
        GraphCanvasRenderRequest request,
        GraphCanvasRenderPlan renderPlan)
    {
        foreach (var node in renderPlan.Nodes)
        {
            if (!ShouldRenderNode(node, request.Callbacks.IsNodeVisible))
            {
                continue;
            }

            var rect = request.Callbacks.GetNodeScreenRect(node);
            var isSelected = request.Callbacks.IsNodeSelected(node.Id);
            var isHovered = string.Equals(request.HoveredNodeId, node.Id, StringComparison.Ordinal);
            var appearance = (node.Appearance ?? new GraphNodeAppearance()).Normalize();
            var isSymbol = appearance.VisualMode == GraphNodeVisualMode.Symbol;
            var isRuntimeActive = request.RuntimeSnapshot?.ActiveNodeId == node.Id;
            var stroke = isSelected
                ? request.Palette.SelectedNodeStrokePen
                : isRuntimeActive
                    ? request.Palette.RuntimeActiveNodeStrokePen
                    : request.Palette.NodeStrokePen;
            _renderState.RuntimeNodeStates.TryGetValue(node.Id, out var runtimeState);
            var renderContext = new GraphNodeRenderContext(
                request.Context,
                node,
                rect,
                request.Zoom,
                isSelected,
                isRuntimeActive,
                request.Callbacks.HasBreakpoint(node.Id),
                runtimeState,
                GraphRuntimeNodeBrushResolver.GetFill(_renderState, request.Palette, node.Id),
                stroke,
                request.Palette.TitleBrush,
                request.Palette.BreakpointBrush,
                request.Palette.BreakpointStrokePen)
            {
                BackgroundImage = (request.BackgroundImageResolver?.Resolve(node).Resource ??
                                   node.Appearance?.BackgroundImage) is { } backgroundImage
                    ? request.ImageResolver?.Resolve(backgroundImage)
                    : null,
                IsHovered = isHovered,
                ShowTitle = !isSymbol && GraphLabelVisibilityResolver.IsNodeNameVisible(
                    node,
                    request.SymbolNameVisibility,
                    isSelected,
                    isHovered)
            };
            using (request.Context.PushTransform(CreateNodeRotationTransform(rect, node.Rotation)))
            {
                if (renderPlan.IsDefaultNodeRenderer)
                {
                    DefaultGraphNodeRenderer.Instance.Render(
                        renderContext,
                        renderPlan.UseSimplifiedDefaultRendering,
                        renderPlan.ShowDefaultLabels && node.Id != request.EditingNodeTitleId);
                }
                else
                {
                    request.NodeRenderer.Render(renderContext);
                }

                if (renderPlan.ShowDefaultPorts || !renderPlan.IsDefaultPortRenderer)
                {
                    _ports.Render(request, node, rect);
                }
            }

            GraphSymbolLabelRenderer.Render(
                request.Context,
                node,
                request.Callbacks.GetNodeVisualScreenRect(node),
                request.Zoom,
                renderPlan.ShowDefaultLabels &&
                node.Id != request.EditingNodeTitleId &&
                GraphLabelVisibilityResolver.IsNodeNameVisible(
                    node,
                    request.SymbolNameVisibility,
                    isSelected,
                    isHovered),
                renderPlan.ShowDefaultLabels &&
                GraphLabelVisibilityResolver.IsNodeDescriptionVisible(
                    node,
                    request.SymbolDescriptionVisibility,
                    isSelected,
                    isHovered),
                request.Palette.TitleBrush,
                request.Palette.DescriptionBrush,
                request.Palette.SymbolNameOffset,
                request.Palette.SymbolDescriptionOffset,
                request.Palette.SymbolDescriptionFontScale);
        }
    }

    internal static bool ShouldRenderNode(
        NodeModel node,
        Func<string, bool> isNodeVisible)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(isNodeVisible);
        return isNodeVisible(node.Id);
    }

    private static Matrix CreateNodeRotationTransform(Rect bounds, double rotation)
    {
        var radians = GraphNodeGeometry.NormalizeRotation(rotation) * Math.PI / 180;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        var centerX = bounds.Center.X;
        var centerY = bounds.Center.Y;
        return new Matrix(
            cosine,
            sine,
            -sine,
            cosine,
            centerX - cosine * centerX + sine * centerY,
            centerY - sine * centerX - cosine * centerY);
    }
}
