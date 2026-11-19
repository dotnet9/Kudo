// 总览渲染：维护低缩放位图，并在其上补绘选择、断点和运行态。
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Kudo;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasOverviewRenderer
{
    private const double BitmapPadding = 4;
    private const double MaxBitmapDimension = 4096;
    private const double MaxBitmapPixels = 16 * 1024 * 1024;
    private const double RuntimeSignalRadius = 4.5;

    private readonly GraphCanvasGeometryState _geometryState;
    private readonly GraphCanvasRenderState _renderState;
    private readonly HashSet<string> _overviewEdgeIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _overviewNodeIds = new(StringComparer.Ordinal);

    public GraphCanvasOverviewRenderer(
        GraphCanvasGeometryState geometryState,
        GraphCanvasRenderState renderState)
    {
        _geometryState = geometryState ?? throw new ArgumentNullException(nameof(geometryState));
        _renderState = renderState ?? throw new ArgumentNullException(nameof(renderState));
    }

    public void RenderStateOverlays(
        GraphCanvasRenderRequest request,
        GraphDocument document)
    {
        _geometryState.DocumentLookup.RebuildIfNeeded(document);
        _overviewEdgeIds.Clear();
        _overviewEdgeIds.UnionWith(request.SelectedEdgeIds);

        foreach (var state in _renderState.RuntimeEdgeStates.Values)
        {
            if (state.IsActive)
            {
                _overviewEdgeIds.Add(state.EdgeId);
            }
        }

        foreach (var edgeId in _overviewEdgeIds)
        {
            var edge = _geometryState.DocumentLookup.FindEdge(edgeId);
            var path = edge is null ? null : request.Callbacks.GetEdgeGraphPath(edge);
            if (edge is null || path is null)
            {
                continue;
            }

            var isRuntimeActive = _renderState.RuntimeEdgeStates.TryGetValue(
                                      edge.Id,
                                      out var runtimeState) &&
                                  runtimeState.IsActive;
            var pen = request.SelectedEdgeIds.Contains(edge.Id)
                ? request.Palette.SelectedEdgePen
                : request.Palette.RuntimeActiveEdgePen;
            DefaultGraphEdgeRenderer.RenderSimplifiedGeometry(
                request.Context,
                path,
                request.Callbacks.GraphToScreen,
                pen);
            if (!isRuntimeActive)
            {
                continue;
            }

            var progress = Math.Clamp(_renderState.RuntimeFlowProgress, 0, 1);
            var route = GraphEdgeRoute.Create(path, request.Callbacks.GraphToScreen);
            request.Context.DrawEllipse(
                request.Palette.RuntimeSignalBrush,
                request.Palette.RuntimeSignalStrokePen,
                route.GetPointAtProgress(progress),
                RuntimeSignalRadius,
                RuntimeSignalRadius);
        }

        _overviewNodeIds.Clear();
        _overviewNodeIds.UnionWith(request.SelectedNodeIds);
        _overviewNodeIds.UnionWith(request.BreakpointNodeIds);
        _overviewNodeIds.UnionWith(_renderState.RuntimeNodeStates.Keys);
        if (request.RuntimeSnapshot?.ActiveNodeId is { } activeNodeId)
        {
            _overviewNodeIds.Add(activeNodeId);
        }

        foreach (var nodeId in _overviewNodeIds)
        {
            var node = _geometryState.DocumentLookup.FindNode(nodeId);
            if (node is null || !request.Callbacks.IsNodeVisible(node.Id))
            {
                continue;
            }

            var isSelected = request.Callbacks.IsNodeSelected(node.Id);
            var isRuntimeActive = request.RuntimeSnapshot?.ActiveNodeId == node.Id;
            _renderState.RuntimeNodeStates.TryGetValue(node.Id, out var runtimeState);
            DefaultGraphNodeRenderer.Instance.Render(
                new GraphNodeRenderContext(
                    request.Context,
                    node,
                    request.Callbacks.GetNodeScreenRect(node),
                    request.Zoom,
                    isSelected,
                    isRuntimeActive,
                    request.Callbacks.HasBreakpoint(node.Id),
                    runtimeState,
                    GraphRuntimeNodeBrushResolver.GetFill(_renderState, request.Palette, node.Id),
                    isSelected
                        ? request.Palette.SelectedNodeStrokePen
                        : isRuntimeActive
                            ? request.Palette.RuntimeActiveNodeStrokePen
                            : request.Palette.NodeStrokePen,
                    request.Palette.TitleBrush,
                    request.Palette.BreakpointBrush,
                    request.Palette.BreakpointStrokePen)
                {
                    BackgroundImage = (request.BackgroundImageResolver?.Resolve(node).Resource ??
                                       node.Appearance?.BackgroundImage) is { } backgroundImage
                        ? request.ImageResolver?.Resolve(backgroundImage)
                        : null,
                    ShowTitle = false
                },
                isSimplified: true,
                showLabel: false);
        }
    }

    /// <summary>
    /// 绘制或复用低缩放级别的总览位图；返回 false 时继续走矢量渲染。
    /// </summary>
    public bool TryRender(GraphCanvasOverviewRenderRequest request)
    {
        if (request.DocumentBounds.Width <= 0 || request.DocumentBounds.Height <= 0)
        {
            return false;
        }

        if (_renderState.IsOverviewDirty ||
            !ReferenceEquals(_renderState.OverviewDocument, request.Document) ||
            _renderState.OverviewBitmap is null)
        {
            var overviewScale = Math.Max(
                request.Zoom,
                Math.Min(request.LodLabelsZoomThreshold, request.LodSimplifiedZoomThreshold));
            var cacheScale = Math.Min(
                overviewScale,
                Math.Min(
                    MaxBitmapDimension / request.DocumentBounds.Width,
                    MaxBitmapDimension / request.DocumentBounds.Height));
            cacheScale = Math.Min(
                cacheScale,
                Math.Sqrt(MaxBitmapPixels /
                          (request.DocumentBounds.Width * request.DocumentBounds.Height)));
            if (!double.IsFinite(cacheScale) || cacheScale <= 0)
            {
                return false;
            }

            var pixelWidth = Math.Max(
                1,
                (int)Math.Ceiling(request.DocumentBounds.Width * cacheScale + BitmapPadding * 2));
            var pixelHeight = Math.Max(
                1,
                (int)Math.Ceiling(request.DocumentBounds.Height * cacheScale + BitmapPadding * 2));
            var bitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight));
            using (var drawingContext = bitmap.CreateDrawingContext())
            using (drawingContext.PushTransform(new Matrix(
                       cacheScale,
                       0,
                       0,
                       cacheScale,
                       -request.DocumentBounds.X * cacheScale + BitmapPadding,
                       -request.DocumentBounds.Y * cacheScale + BitmapPadding)))
            {
                var referenceScale = Math.Sqrt(
                    request.MinimumZoom * Math.Max(request.MinimumZoom, cacheScale));
                var cachedEdgePen = new Pen(
                    request.EdgePen.Brush,
                    request.EdgePen.Thickness / referenceScale);
                foreach (var edge in request.Document.Edges)
                {
                    var path = request.EdgePathProvider(edge);
                    if (path is null)
                    {
                        continue;
                    }

                    DefaultGraphEdgeRenderer.RenderSimplifiedGeometry(
                        drawingContext,
                        path,
                        static point => new Point(point.X, point.Y),
                        cachedEdgePen);
                }

                var cachedNodePen = new Pen(
                    request.NodeStrokePen.Brush,
                    request.NodeStrokePen.Thickness / referenceScale);
                foreach (var node in request.Document.Nodes)
                {
                    var bounds = GraphNodeGeometry.GetCanvasBounds(node);
                    var rect = new Rect(
                            bounds.X,
                            bounds.Y,
                            bounds.Width,
                            bounds.Height);
                    DefaultGraphNodeRenderer.Instance.Render(
                        new GraphNodeRenderContext(
                            drawingContext,
                            node,
                            rect,
                            1,
                            false,
                            false,
                            false,
                            null,
                            request.NodeFillBrush,
                            cachedNodePen,
                            Brushes.Transparent,
                            Brushes.Transparent,
                            cachedNodePen)
                        {
                            BackgroundImage = (request.BackgroundImageResolver?.Resolve(node).Resource ??
                                               node.Appearance?.BackgroundImage) is { } backgroundImage
                                ? request.ImageResolver?.Resolve(backgroundImage)
                                : null,
                            ShowTitle = false
                        },
                        isSimplified: true,
                        showLabel: false);
                }
            }

            _renderState.OverviewBitmap = bitmap;
            _renderState.OverviewDocument = request.Document;
            _renderState.OverviewGraphBounds = request.DocumentBounds;
            _renderState.OverviewScale = cacheScale;
            _renderState.IsOverviewDirty = false;
        }

        if (_renderState.OverviewBitmap is null || _renderState.OverviewScale <= 0)
        {
            return false;
        }

        var displayScale = request.Zoom / _renderState.OverviewScale;
        var destination = new Rect(
            request.ViewportOffset.X +
            _renderState.OverviewGraphBounds.X * request.Zoom -
            BitmapPadding * displayScale,
            request.ViewportOffset.Y +
            _renderState.OverviewGraphBounds.Y * request.Zoom -
            BitmapPadding * displayScale,
            _renderState.OverviewBitmap.Size.Width * displayScale,
            _renderState.OverviewBitmap.Size.Height * displayScale);
        try
        {
            request.Context.DrawImage(
                _renderState.OverviewBitmap,
                new Rect(_renderState.OverviewBitmap.Size),
                destination);
        }
        catch (NotImplementedException)
        {
            // DrawingGroup 的记录上下文不支持位图命令，设计时和测试场景回退到矢量渲染。
            return false;
        }

        return true;
    }
}
