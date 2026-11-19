// 渲染计划：根据视口、LOD 和 Renderer 类型选择可见集合及总览路径。
using Avalonia;
using Kudo;
using Kudo.Avalonia.Designer;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasRenderPlanner
{
    private readonly GraphCanvasGeometryState _geometryState;

    public GraphCanvasRenderPlanner(GraphCanvasGeometryState geometryState)
    {
        _geometryState = geometryState ?? throw new ArgumentNullException(nameof(geometryState));
    }

    public GraphCanvasRenderPlan Create(GraphCanvasRenderPlanRequest request)
    {
        var useSimplifiedDefaultRendering =
            request.IsLevelOfDetailEnabled && request.Zoom < request.LodSimplifiedZoomThreshold;
        var showDefaultLabels =
            !request.IsLevelOfDetailEnabled || request.Zoom >= request.LodLabelsZoomThreshold;
        var showDefaultPorts =
            request.ShowPorts &&
            (!request.IsLevelOfDetailEnabled || request.Zoom >= request.LodPortsZoomThreshold);
        var isDefaultNodeRenderer = UsesDefaultNodeRendering(request);
        var isDefaultPortRenderer = UsesDefaultPortRendering(request);
        var isDefaultEdgeRenderer = UsesDefaultEdgeRendering(request);
        var isDefaultGroupRenderer = UsesDefaultGroupRendering(request);
        var canUseOverviewBitmapCache = isDefaultNodeRenderer &&
                                        isDefaultPortRenderer &&
                                        isDefaultEdgeRenderer &&
                                        isDefaultGroupRenderer &&
                                        useSimplifiedDefaultRendering &&
                                        !showDefaultLabels &&
                                        !showDefaultPorts &&
                                        !request.IsOverviewCacheBlockedByInteraction &&
                                        request.Document.Groups.Count == 0 &&
                                        request.Document.Edges.All(edge => edge.Appearance?.IsDefault != false) &&
                                        !HasAnimatedNodeImages(
                                            request.Document,
                                            request.ImageResolver,
                                            request.BackgroundImageResolver);

        IReadOnlyList<GroupModel> groups = request.Document.Groups;
        IReadOnlyList<NodeModel> nodes = request.Document.Nodes;
        IReadOnlyList<EdgeModel> edges = request.Document.Edges;
        var rendersEntireDocument = request.CullingBounds is { } viewportBounds &&
                                    request.DocumentBounds is { } documentBounds &&
                                    Contains(viewportBounds, documentBounds);
        if (request.CullingBounds is { } visibleBounds &&
            !rendersEntireDocument &&
            !canUseOverviewBitmapCache)
        {
            _geometryState.ElementSpatialIndex.RebuildIfNeeded(request.Document);
            groups = _geometryState.ElementSpatialIndex.QueryGroups(visibleBounds);
            nodes = _geometryState.ElementSpatialIndex.QueryNodes(visibleBounds);
            if (request.IsEdgeRoutingCacheEnabled)
            {
                _geometryState.EdgeSpatialIndex.RebuildIfNeeded(
                    request.Document.Edges,
                    request.EdgePathProvider);
                edges = _geometryState.EdgeSpatialIndex.QueryEdges(visibleBounds);
            }
        }

        return new GraphCanvasRenderPlan
        {
            Groups = groups,
            Nodes = nodes,
            Edges = edges,
            CullingBounds = request.CullingBounds,
            DocumentBounds = request.DocumentBounds,
            UseSimplifiedDefaultRendering = useSimplifiedDefaultRendering,
            ShowDefaultLabels = showDefaultLabels,
            ShowDefaultPorts = showDefaultPorts,
            IsDefaultNodeRenderer = isDefaultNodeRenderer,
            IsDefaultPortRenderer = isDefaultPortRenderer,
            IsDefaultEdgeRenderer = isDefaultEdgeRenderer,
            CanUseOverviewBitmapCache = canUseOverviewBitmapCache
        };
    }

    private static bool Contains(GraphRect outer, Rect inner) =>
        inner.Left >= outer.X &&
        inner.Top >= outer.Y &&
        inner.Right <= outer.X + outer.Width &&
        inner.Bottom <= outer.Y + outer.Height;

    private static bool UsesDefaultNodeRendering(GraphCanvasRenderPlanRequest request) =>
        ReferenceEquals(request.NodeRenderer, DefaultGraphNodeRenderer.Instance) ||
        request.NodeRenderer is IGraphNodeRendererDefaultCompatibility compatibility &&
        request.Document.Nodes.All(compatibility.UsesDefaultRendering);

    private static bool UsesDefaultPortRendering(GraphCanvasRenderPlanRequest request) =>
        ReferenceEquals(request.PortRenderer, DefaultGraphPortRenderer.Instance) ||
        request.PortRenderer is IGraphPortRendererDefaultCompatibility compatibility &&
        request.Document.Nodes.All(node => node.Ports.All(port =>
            compatibility.UsesDefaultRendering(node, port)));

    private static bool UsesDefaultEdgeRendering(GraphCanvasRenderPlanRequest request) =>
        ReferenceEquals(request.EdgeRenderer, DefaultGraphEdgeRenderer.Instance) ||
        request.EdgeRenderer is IGraphEdgeRendererDefaultCompatibility compatibility &&
        request.Document.Edges.All(compatibility.UsesDefaultRendering);

    private static bool UsesDefaultGroupRendering(GraphCanvasRenderPlanRequest request) =>
        ReferenceEquals(request.GroupRenderer, DefaultGraphGroupRenderer.Instance) ||
        request.GroupRenderer is IGraphGroupRendererDefaultCompatibility compatibility &&
        request.Document.Groups.All(compatibility.UsesDefaultRendering);

    private static bool HasAnimatedNodeImages(
        GraphDocument document,
        IGraphImageResolver? imageResolver,
        IGraphNodeBackgroundImageResolver? backgroundImageResolver)
    {
        if (imageResolver is not IGraphAnimatedImageResolver animatedResolver)
        {
            return false;
        }

        return document.Nodes.Any(node =>
            (backgroundImageResolver?.Resolve(node).Resource ??
             node.Appearance?.BackgroundImage) is { } source &&
            animatedResolver.IsAnimated(source));
    }
}
