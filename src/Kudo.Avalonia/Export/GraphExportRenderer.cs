// 默认导出 Renderer：使用同一份几何语义绘制分组、连线、节点、端口和标注。
using Kudo.Core.Algorithms;
using Kudo.Avalonia.Designer;
using SkiaSharp;
using Svg.Skia;

namespace Kudo.Avalonia.Export;

public readonly record struct GraphExportRenderContext(
    SKCanvas Canvas,
    GraphDocument Document,
    GraphExportLayout Layout,
    GraphExportOptions Options,
    CancellationToken CancellationToken);

public interface IGraphExportRenderer
{
    void Render(in GraphExportRenderContext context);
}

public sealed class DefaultGraphExportRenderer : IGraphExportRenderer
{
    private const float NodeCornerRadius = 6;
    private const float MarkerSize = 9;
    public static DefaultGraphExportRenderer Instance { get; } = new();

    private DefaultGraphExportRenderer()
    {
    }

    public void Render(in GraphExportRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context.Canvas);
        ArgumentNullException.ThrowIfNull(context.Document);
        context.CancellationToken.ThrowIfCancellationRequested();
        var canvas = context.Canvas;
        var theme = context.Options.Theme ?? new GraphExportTheme();
        canvas.Clear(ParseColor(theme.BackgroundColor, SKColors.White));
        canvas.Save();
        canvas.Translate(context.Layout.OffsetX, context.Layout.OffsetY);
        canvas.Scale(context.Layout.Scale);

        var collapsedGroups = BuildCollapsedGroupMap(context.Document);
        var edgeLabels = new List<EdgeLabelLayout>();
        var edgeMarkers = new List<EdgeMarkerLayout>();
        RenderGroups(context, theme);
        RenderEdges(context, theme, collapsedGroups, edgeLabels, edgeMarkers);
        RenderNodes(context, theme, collapsedGroups);
        foreach (var marker in edgeMarkers)
        {
            RenderMarker(context.Canvas, marker.Marker, marker.From, marker.Tip, marker.Color);
        }

        foreach (var label in edgeLabels)
        {
            RenderEdgeLabel(context.Canvas, label, theme);
        }

        RenderAnnotations(context, theme);

        canvas.Restore();
    }

    private static void RenderGroups(in GraphExportRenderContext context, GraphExportTheme theme)
    {
        using var fill = CreatePaint(theme.GroupFillColor, SKPaintStyle.Fill);
        using var stroke = CreatePaint(theme.GroupStrokeColor, SKPaintStyle.Stroke, 1.5f);
        using var textPaint = CreatePaint(theme.NodeTextColor, SKPaintStyle.Fill);
        foreach (var group in context.Document.Groups)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (GraphGroupBoundsCalculator.Calculate(context.Document, group) is not { } bounds)
            {
                continue;
            }

            var rect = ToRect(bounds);
            context.Canvas.DrawRoundRect(rect, NodeCornerRadius, NodeCornerRadius, fill);
            context.Canvas.DrawRoundRect(rect, NodeCornerRadius, NodeCornerRadius, stroke);
            DrawText(
                context.Canvas,
                group.Title ?? group.Id,
                rect.Left + 10,
                rect.Top + 20,
                13,
                theme.FontFamily,
                textPaint);
        }
    }

    private static void RenderEdges(
        in GraphExportRenderContext context,
        GraphExportTheme theme,
        IReadOnlyDictionary<string, GraphRect> collapsedGroups,
        ICollection<EdgeLabelLayout> edgeLabels,
        ICollection<EdgeMarkerLayout> edgeMarkers)
    {
        foreach (var edge in context.Document.Edges)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var sourceNode = context.Document.FindNode(edge.Source.NodeId);
            var targetNode = context.Document.FindNode(edge.Target.NodeId);
            var sourcePort = context.Document.FindPort(edge.Source);
            var targetPort = context.Document.FindPort(edge.Target);
            if (sourceNode is null || targetNode is null || sourcePort is null || targetPort is null)
            {
                continue;
            }

            var sourceGroup = collapsedGroups.GetValueOrDefault(sourceNode.Id);
            var targetGroup = collapsedGroups.GetValueOrDefault(targetNode.Id);
            if (sourceGroup != default && sourceGroup == targetGroup)
            {
                continue;
            }

            var start = GetPortPosition(context.Options.PortPositionProvider, sourceNode, sourcePort);
            var end = GetPortPosition(context.Options.PortPositionProvider, targetNode, targetPort);
            if (sourceGroup != default)
            {
                start = ProjectToBounds(sourceGroup, end);
            }

            if (targetGroup != default)
            {
                end = ProjectToBounds(targetGroup, start);
            }

            var obstacles = edge.RoutingStyle == EdgeRoutingStyle.SmartOrthogonal
                ? BuildObstacles(context.Document, sourceNode.Id, targetNode.Id, collapsedGroups)
                : [];
            var route = context.Options.RoutingService.Route(new GraphEdgeRoutingRequest(edge.RoutingStyle, start, end)
            {
                Waypoints = edge.Waypoints ?? [],
                Obstacles = obstacles,
                Options = context.Options.RoutingOptions ?? GraphEdgeRoutingOptions.Default,
                StartDirection = sourceGroup != default || sourcePort.OutgoingDirection is not { } outgoing
                    ? null
                    : PortGeometry.ToCanvasDirection(sourceNode, outgoing),
                EndDirection = targetGroup != default || targetPort.IncomingDirection is not { } incoming
                    ? null
                    : PortGeometry.ToCanvasDirection(targetNode, incoming)
            });
            using var path = CreatePath(route);
            using var paint = CreateEdgePaint(edge, theme);
            context.Canvas.DrawPath(path, paint);
            edgeMarkers.Add(new EdgeMarkerLayout(
                edge.TargetMarker,
                route.Points[^2],
                route.End,
                paint.Color));
            edgeMarkers.Add(new EdgeMarkerLayout(
                edge.SourceMarker,
                route.Points[1],
                route.Start,
                paint.Color));
            if (!string.IsNullOrWhiteSpace(edge.Label))
            {
                edgeLabels.Add(new EdgeLabelLayout(
                    edge.Label,
                    route,
                    sourceGroup != default
                        ? sourceGroup
                        : new GraphRect(
                            sourceNode.Position.X,
                            sourceNode.Position.Y,
                            sourceNode.Size.Width,
                            sourceNode.Size.Height),
                    targetGroup != default
                        ? targetGroup
                        : new GraphRect(
                            targetNode.Position.X,
                            targetNode.Position.Y,
                            targetNode.Size.Width,
                            targetNode.Size.Height)));
            }
        }
    }

    /// <summary>
    /// 将标签放在路径中点法线方向，并绘制背景框，避免文字直接压在线条上。
    /// </summary>
    private static void RenderEdgeLabel(
        SKCanvas canvas,
        EdgeLabelLayout labelLayout,
        GraphExportTheme theme)
    {
        var (point, tangent) = GetLabelGeometry(labelLayout.Route);
        var tangentLength = Math.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
        using var textPaint = CreatePaint(theme.NodeTextColor, SKPaintStyle.Fill);
        using var font = CreateFont(labelLayout.Label, theme.FontFamily, 12);
        var textWidth = font.MeasureText(labelLayout.Label, textPaint);
        var metrics = font.Metrics;
        var textHeight = metrics.Descent - metrics.Ascent;
        const float paddingX = 6;
        const float paddingY = 4;
        var normalX = tangentLength <= double.Epsilon ? 0 : tangent.Y / tangentLength;
        var normalY = tangentLength <= double.Epsilon ? -1 : -tangent.X / tangentLength;
        var (centerX, centerY, bounds) = FindLabelPlacement(
            point,
            normalX,
            normalY,
            textWidth,
            textHeight,
            paddingX,
            paddingY,
            labelLayout.SourceBounds,
            labelLayout.TargetBounds);
        using var fill = CreatePaint(theme.EdgeLabelFillColor, SKPaintStyle.Fill);
        using var stroke = CreatePaint(theme.EdgeLabelStrokeColor, SKPaintStyle.Stroke, 0.75f);
        canvas.DrawRoundRect(bounds, 4, 4, fill);
        canvas.DrawRoundRect(bounds, 4, 4, stroke);
        var baseline = centerY - (metrics.Ascent + metrics.Descent) / 2;
        canvas.DrawText(
            labelLayout.Label,
            centerX - textWidth / 2,
            baseline,
            SKTextAlign.Center,
            font,
            textPaint);
    }

    private static (float CenterX, float CenterY, SKRect Bounds) FindLabelPlacement(
        GraphPoint point,
        double normalX,
        double normalY,
        float textWidth,
        float textHeight,
        float paddingX,
        float paddingY,
        GraphRect sourceBounds,
        GraphRect targetBounds)
    {
        ReadOnlySpan<double> offsets = [14, 38, 62, 86, 110, -14, -38, -62, -86, -110];
        foreach (var offset in offsets)
        {
            var centerX = (float)(point.X + normalX * offset);
            var centerY = (float)(point.Y + normalY * offset);
            var bounds = CreateLabelBounds(
                centerX,
                centerY,
                textWidth,
                textHeight,
                paddingX,
                paddingY);
            if (!Intersects(bounds, sourceBounds) && !Intersects(bounds, targetBounds))
            {
                return (centerX, centerY, bounds);
            }
        }

        var fallbackX = (float)(point.X + normalX * offsets[^1]);
        var fallbackY = (float)(point.Y + normalY * offsets[^1]);
        return (
            fallbackX,
            fallbackY,
            CreateLabelBounds(
                fallbackX,
                fallbackY,
                textWidth,
                textHeight,
                paddingX,
                paddingY));
    }

    private static SKRect CreateLabelBounds(
        float centerX,
        float centerY,
        float textWidth,
        float textHeight,
        float paddingX,
        float paddingY) => SKRect.Create(
        centerX - textWidth / 2 - paddingX,
        centerY - textHeight / 2 - paddingY,
        textWidth + paddingX * 2,
        textHeight + paddingY * 2);

    private static bool Intersects(SKRect labelBounds, GraphRect elementBounds)
    {
        const float margin = 4;
        var elementLeft = (float)elementBounds.X - margin;
        var elementTop = (float)elementBounds.Y - margin;
        var elementRight = (float)(elementBounds.X + elementBounds.Width) + margin;
        var elementBottom = (float)(elementBounds.Y + elementBounds.Height) + margin;
        return labelBounds.Left < elementRight &&
               labelBounds.Right > elementLeft &&
               labelBounds.Top < elementBottom &&
               labelBounds.Bottom > elementTop;
    }

    private static (GraphPoint Point, GraphPoint Tangent) GetLabelGeometry(GraphEdgePath route)
    {
        if (route.RoutingStyle == EdgeRoutingStyle.Bezier &&
            route.ControlPoint1 is { } control1 &&
            route.ControlPoint2 is { } control2)
        {
            const double t = 0.5;
            var point = CubicPoint(route.Start, control1, control2, route.End, t);
            var tangent = CubicTangent(route.Start, control1, control2, route.End, t);
            return (point, tangent);
        }

        if (route.Points.Count < 2)
        {
            return (route.Start, new GraphPoint(1, 0));
        }

        var lengths = new double[route.Points.Count - 1];
        var totalLength = 0d;
        for (var index = 0; index < route.Points.Count - 1; index++)
        {
            var length = Distance(route.Points[index], route.Points[index + 1]);
            lengths[index] = length;
            totalLength += length;
        }

        var targetDistance = totalLength / 2;
        var traversed = 0d;
        for (var index = 0; index < lengths.Length; index++)
        {
            var length = lengths[index];
            if (traversed + length < targetDistance || length <= double.Epsilon)
            {
                traversed += length;
                continue;
            }

            var start = route.Points[index];
            var end = route.Points[index + 1];
            var progress = (targetDistance - traversed) / length;
            return (
                new GraphPoint(
                    start.X + (end.X - start.X) * progress,
                    start.Y + (end.Y - start.Y) * progress),
                new GraphPoint(end.X - start.X, end.Y - start.Y));
        }

        var previous = route.Points[^2];
        return (route.End, new GraphPoint(route.End.X - previous.X, route.End.Y - previous.Y));
    }

    private static GraphPoint CubicPoint(
        GraphPoint start,
        GraphPoint control1,
        GraphPoint control2,
        GraphPoint end,
        double t)
    {
        var inverse = 1 - t;
        return new GraphPoint(
            inverse * inverse * inverse * start.X +
            3 * inverse * inverse * t * control1.X +
            3 * inverse * t * t * control2.X +
            t * t * t * end.X,
            inverse * inverse * inverse * start.Y +
            3 * inverse * inverse * t * control1.Y +
            3 * inverse * t * t * control2.Y +
            t * t * t * end.Y);
    }

    private static GraphPoint CubicTangent(
        GraphPoint start,
        GraphPoint control1,
        GraphPoint control2,
        GraphPoint end,
        double t)
    {
        var inverse = 1 - t;
        return new GraphPoint(
            3 * inverse * inverse * (control1.X - start.X) +
            6 * inverse * t * (control2.X - control1.X) +
            3 * t * t * (end.X - control2.X),
            3 * inverse * inverse * (control1.Y - start.Y) +
            6 * inverse * t * (control2.Y - control1.Y) +
            3 * t * t * (end.Y - control2.Y));
    }

    private static double Distance(GraphPoint first, GraphPoint second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static void RenderNodes(
        in GraphExportRenderContext context,
        GraphExportTheme theme,
        IReadOnlyDictionary<string, GraphRect> collapsedGroups)
    {
        using var fill = CreatePaint(theme.NodeFillColor, SKPaintStyle.Fill);
        using var stroke = CreatePaint(theme.NodeStrokeColor, SKPaintStyle.Stroke, 1.5f);
        using var textPaint = CreatePaint(theme.NodeTextColor, SKPaintStyle.Fill);
        var canvasSettings = (context.Document.CanvasSettings ?? GraphCanvasSettings.Default).Normalize();
        foreach (var node in context.Document.Nodes)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (collapsedGroups.ContainsKey(node.Id))
            {
                continue;
            }

            var appearance = (node.Appearance ?? new GraphNodeAppearance()).Normalize();
            var backgroundImage = context.Options.BackgroundImageResolver?.Resolve(node).Resource ??
                appearance.BackgroundImage;
            var visualBounds = context.Options.NodeGeometryProvider?.GetBounds(node) ??
                GraphNodeGeometry.GetCanvasBounds(node);
            var labels = GraphExportSymbolLabelLayout.Calculate(
                node,
                visualBounds,
                canvasSettings,
                theme);
            context.Canvas.Save();
            context.Canvas.RotateDegrees(
                (float)GraphNodeGeometry.NormalizeRotation(node.Rotation),
                (float)(node.Position.X + node.Size.Width / 2),
                (float)(node.Position.Y + node.Size.Height / 2));
            var rect = new SKRect(
                (float)node.Position.X,
                (float)node.Position.Y,
                (float)(node.Position.X + node.Size.Width),
                (float)(node.Position.Y + node.Size.Height));
            var customRenderer = context.Options.NodeRendererResolver?.ResolveExportRenderer(node);
            if (customRenderer is not null)
            {
                customRenderer.Render(new GraphNodeExportRenderContext(
                    context.Canvas,
                    context.Document,
                    node,
                    context.Layout,
                    context.Options,
                    theme,
                    new GraphRect(node.Position.X, node.Position.Y, node.Size.Width, node.Size.Height),
                    context.CancellationToken));
            }
            else
            {
                context.Canvas.DrawRoundRect(rect, NodeCornerRadius, NodeCornerRadius, fill);
                RenderBackgroundImage(context, rect, appearance, backgroundImage);
                RenderEmbeddedContent(context, node, rect, theme);
                context.Canvas.DrawRoundRect(rect, NodeCornerRadius, NodeCornerRadius, stroke);
                if (appearance.VisualMode != GraphNodeVisualMode.Symbol)
                {
                    DrawText(
                        context.Canvas,
                        node.Name ?? node.Type ?? node.Id,
                        rect.Left + 10,
                        rect.Top + 22,
                        14,
                        theme.FontFamily,
                        textPaint);
                }
            }

            context.Canvas.Restore();
            RenderNodePorts(context.Canvas, node, theme, context.Options.PortPositionProvider);
            GraphExportSymbolLabelLayout.Render(context.Canvas, labels, theme.FontFamily);
        }
    }

    private static void RenderBackgroundImage(
        in GraphExportRenderContext context,
        SKRect rect,
        GraphNodeAppearance appearance,
        string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || appearance.Opacity <= 0)
        {
            return;
        }

        var path = context.Options.BackgroundImagePathResolver?.Invoke(source);
        if (string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(source))
        {
            path = source;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var svg = new SKSvg();
            if (svg.Load(stream) is not { } picture)
            {
                return;
            }

            var bounds = picture.CullRect;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            var destination = rect;
            var scaleX = rect.Width / bounds.Width;
            var scaleY = rect.Height / bounds.Height;
            switch (appearance.Stretch)
            {
                case GraphImageStretch.None:
                    scaleX = scaleY = Math.Min(1, Math.Min(scaleX, scaleY));
                    destination = Center(rect, bounds.Width * scaleX, bounds.Height * scaleY);
                    break;
                case GraphImageStretch.Uniform:
                    scaleX = scaleY = Math.Min(scaleX, scaleY);
                    destination = Center(rect, bounds.Width * scaleX, bounds.Height * scaleY);
                    break;
                case GraphImageStretch.UniformToFill:
                    scaleX = scaleY = Math.Max(scaleX, scaleY);
                    break;
            }

            context.Canvas.Save();
            context.Canvas.ClipRect(rect);
            context.Canvas.Translate(
                destination.Left - bounds.Left * scaleX,
                destination.Top - bounds.Top * scaleY);
            context.Canvas.Scale(scaleX, scaleY);
            using var opacityPaint = new SKPaint
            {
                Color = SKColors.White.WithAlpha(
                    (byte)Math.Round(Math.Clamp(appearance.Opacity, 0, 1) * 255))
            };
            context.Canvas.DrawPicture(picture, opacityPaint);
            context.Canvas.Restore();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // 可选模型资源缺失或格式错误时，不应导致整个导出失败。
        }
    }

    private static SKRect Center(SKRect bounds, float width, float height) => SKRect.Create(
        bounds.Left + (bounds.Width - width) / 2,
        bounds.Top + (bounds.Height - height) / 2,
        width,
        height);

    private static void RenderNodePorts(
        SKCanvas canvas,
        NodeModel node,
        GraphExportTheme theme,
        IGraphPortPositionProvider? portPositionProvider)
    {
        foreach (var port in node.Ports)
        {
            var position = GetPortPosition(portPositionProvider, node, port);
            var radius = Math.Max(0.1f, theme.PortRadius);
            var localDirection = GraphPortGlyphDirection.Resolve(node, port);
            var direction = GraphNodeGeometry.RotateVector(localDirection, node.Rotation);
            var angle = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;
            using var paint = CreatePaint(
                GetPortColor(port.Kind, theme),
                SKPaintStyle.Stroke,
                Math.Max(0.1f, theme.PortStrokeWidth));
            canvas.Save();
            canvas.RotateDegrees((float)angle, (float)position.X, (float)position.Y);
            switch (port.Kind)
            {
                case PortKind.Input:
                    DrawPortChevron(
                        canvas,
                        GraphPortGlyphGeometry.GetInputChevronCenter(
                            position,
                            radius,
                            theme.PortChevronHalfWidthFactor),
                        radius,
                        paint,
                        theme.PortChevronHalfWidthFactor,
                        true);
                    break;
                case PortKind.Output:
                    DrawPortChevron(
                        canvas,
                        GraphPortGlyphGeometry.GetOutputChevronCenter(
                            position,
                            radius,
                            theme.PortChevronHalfWidthFactor),
                        radius,
                        paint,
                        theme.PortChevronHalfWidthFactor,
                        true);
                    break;
                case PortKind.InputOutput:
                    var offset = radius * theme.InputOutputPortOffsetFactor;
                    var scaledRadius = radius * theme.InputOutputPortScale;
                    DrawPortChevron(canvas, new GraphPoint(position.X - offset, position.Y), scaledRadius, paint,
                        theme.PortChevronHalfWidthFactor, true);
                    DrawPortChevron(canvas, new GraphPoint(position.X + offset, position.Y), scaledRadius, paint,
                        theme.PortChevronHalfWidthFactor, false);
                    break;
                case PortKind.Reference:
                    canvas.DrawCircle((float)position.X, (float)position.Y, radius, paint);
                    break;
            }

            canvas.Restore();
        }
    }

    private static string GetPortColor(PortKind kind, GraphExportTheme theme) => kind switch
    {
        PortKind.Input => theme.InputPortColor,
        PortKind.Output => theme.OutputPortColor,
        PortKind.InputOutput => theme.InputOutputPortColor,
        PortKind.Reference => theme.ReferencePortColor,
        _ => theme.InputOutputPortColor
    };

    private static void DrawPortChevron(
        SKCanvas canvas,
        GraphPoint center,
        float radius,
        SKPaint paint,
        float halfWidthFactor,
        bool pointsRight)
    {
        var direction = pointsRight ? 1f : -1f;
        var builder = new SKPathBuilder();
        builder.MoveTo(
            (float)center.X - direction * radius * halfWidthFactor,
            (float)center.Y - radius);
        builder.LineTo(
            (float)center.X + direction * radius * halfWidthFactor,
            (float)center.Y);
        builder.LineTo(
            (float)center.X - direction * radius * halfWidthFactor,
            (float)center.Y + radius);
        using var path = builder.Snapshot();
        canvas.DrawPath(path, paint);
    }

    private static void RenderAnnotations(in GraphExportRenderContext context, GraphExportTheme theme)
    {
        using var fill = CreatePaint(theme.AnnotationFillColor, SKPaintStyle.Fill);
        using var stroke = CreatePaint(theme.AnnotationStrokeColor, SKPaintStyle.Stroke, 1.25f);
        using var textPaint = CreatePaint(theme.NodeTextColor, SKPaintStyle.Fill);
        foreach (var annotation in context.Document.Annotations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var rect = new SKRect(
                (float)annotation.Position.X,
                (float)annotation.Position.Y,
                (float)(annotation.Position.X + annotation.Size.Width),
                (float)(annotation.Position.Y + annotation.Size.Height));
            context.Canvas.DrawRoundRect(rect, 4, 4, fill);
            context.Canvas.DrawRoundRect(rect, 4, 4, stroke);
            DrawWrappedText(
                context.Canvas,
                annotation.Text ?? string.Empty,
                rect,
                theme.FontFamily,
                textPaint);
        }
    }

    private static void RenderEmbeddedContent(
        in GraphExportRenderContext context,
        NodeModel node,
        SKRect rect,
        GraphExportTheme theme)
    {
        if (context.Options.HasEmbeddedContent?.Invoke(node) != true)
        {
            return;
        }

        var contentRect = new SKRect(rect.Left + 8, rect.Top + 30, rect.Right - 8, rect.Bottom - 8);
        if (contentRect.Width <= 0 || contentRect.Height <= 0 ||
            context.Options.EmbeddedContentPolicy == GraphEmbeddedContentPolicy.Skip)
        {
            return;
        }

        if (context.Options.EmbeddedContentPolicy == GraphEmbeddedContentPolicy.Snapshot)
        {
            using var stream = context.Options.EmbeddedContentSnapshot?.Invoke(node);
            using var image = stream is null ? null : SKImage.FromEncodedData(stream);
            if (image is not null)
            {
                context.Canvas.DrawImage(image, contentRect, new SKSamplingOptions(SKFilterMode.Linear));
                return;
            }
        }

        using var placeholder = CreatePaint(theme.EmbeddedContentPlaceholderColor, SKPaintStyle.Fill);
        using var placeholderText = CreatePaint(theme.NodeTextColor, SKPaintStyle.Fill);
        context.Canvas.DrawRect(contentRect, placeholder);
        DrawText(
            context.Canvas,
            context.Options.EmbeddedContentPlaceholderText,
            contentRect.Left + 8,
            contentRect.MidY + 4,
            12,
            theme.FontFamily,
            placeholderText);
    }

    private static SKPath CreatePath(GraphEdgePath route)
    {
        var builder = new SKPathBuilder();
        builder.MoveTo((float)route.Start.X, (float)route.Start.Y);
        if (route.RoutingStyle == EdgeRoutingStyle.Bezier &&
            route.ControlPoint1 is { } control1 &&
            route.ControlPoint2 is { } control2)
        {
            builder.CubicTo(
                (float)control1.X,
                (float)control1.Y,
                (float)control2.X,
                (float)control2.Y,
                (float)route.End.X,
                (float)route.End.Y);
            return builder.Snapshot();
        }

        foreach (var point in route.Points.Skip(1))
        {
            builder.LineTo((float)point.X, (float)point.Y);
        }

        return builder.Snapshot();
    }

    private static SKPaint CreateEdgePaint(EdgeModel edge, GraphExportTheme theme)
    {
        var appearance = (edge.Appearance ?? new GraphEdgeAppearance()).Normalize();
        var color = appearance.StrokeColor ?? theme.EdgeColor;
        var paint = CreatePaint(
            color,
            SKPaintStyle.Stroke,
            (float)(appearance.StrokeWidth ?? 1.5));
        paint.Color = paint.Color.WithAlpha((byte)Math.Round(appearance.Opacity * 255));
        var pattern = GetDashPattern(appearance);
        if (pattern.Length > 0)
        {
            paint.PathEffect = SKPathEffect.CreateDash(pattern, 0);
        }

        return paint;
    }

    private static float[] GetDashPattern(GraphEdgeAppearance appearance)
    {
        if (appearance.DashStyle == GraphEdgeDashStyle.Custom)
        {
            return (appearance.CustomDashPattern ?? []).Select(value => (float)value).ToArray();
        }

        return appearance.DashStyle switch
        {
            GraphEdgeDashStyle.Dash => [8, 5],
            GraphEdgeDashStyle.Dot => [2, 4],
            GraphEdgeDashStyle.DashDot => [8, 4, 2, 4],
            GraphEdgeDashStyle.LongDash => [14, 6],
            _ => []
        };
    }

    private static void RenderMarker(
        SKCanvas canvas,
        EdgeMarker marker,
        GraphPoint from,
        GraphPoint tip,
        SKColor color)
    {
        if (marker == EdgeMarker.None)
        {
            return;
        }

        var dx = tip.X - from.X;
        var dy = tip.Y - from.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= double.Epsilon)
        {
            return;
        }

        var ux = dx / length;
        var uy = dy / length;
        var px = -uy;
        var py = ux;
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = marker == EdgeMarker.Circle ? SKPaintStyle.Stroke : SKPaintStyle.Fill,
            StrokeWidth = 1.5f
        };
        if (marker == EdgeMarker.Circle)
        {
            canvas.DrawCircle((float)tip.X, (float)tip.Y, MarkerSize / 2, paint);
            return;
        }

        var backX = tip.X - ux * MarkerSize;
        var backY = tip.Y - uy * MarkerSize;
        var builder = new SKPathBuilder();
        if (marker == EdgeMarker.Arrow)
        {
            builder.MoveTo((float)tip.X, (float)tip.Y);
            builder.LineTo((float)(backX + px * MarkerSize * 0.55), (float)(backY + py * MarkerSize * 0.55));
            builder.LineTo((float)(backX - px * MarkerSize * 0.55), (float)(backY - py * MarkerSize * 0.55));
        }
        else
        {
            var middleX = tip.X - ux * MarkerSize * 0.55;
            var middleY = tip.Y - uy * MarkerSize * 0.55;
            var tailX = tip.X - ux * MarkerSize * 1.1;
            var tailY = tip.Y - uy * MarkerSize * 1.1;
            builder.MoveTo((float)tip.X, (float)tip.Y);
            builder.LineTo((float)(middleX + px * MarkerSize * 0.45), (float)(middleY + py * MarkerSize * 0.45));
            builder.LineTo((float)tailX, (float)tailY);
            builder.LineTo((float)(middleX - px * MarkerSize * 0.45), (float)(middleY - py * MarkerSize * 0.45));
        }

        builder.Close();
        using var path = builder.Snapshot();
        canvas.DrawPath(path, paint);
    }

    private static IReadOnlyDictionary<string, GraphRect> BuildCollapsedGroupMap(GraphDocument document)
    {
        var result = new Dictionary<string, GraphRect>(StringComparer.Ordinal);
        foreach (var group in document.Groups.Where(group => group.IsCollapsed))
        {
            if (GraphGroupBoundsCalculator.Calculate(document, group) is not { } bounds)
            {
                continue;
            }

            foreach (var nodeId in group.NodeIds ?? [])
            {
                result.TryAdd(nodeId, bounds);
            }
        }

        return result;
    }

    private static IReadOnlyList<GraphRect> BuildObstacles(
        GraphDocument document,
        string sourceNodeId,
        string targetNodeId,
        IReadOnlyDictionary<string, GraphRect> collapsedGroups)
    {
        var obstacles = document.Nodes
            .Where(node => node.Id != sourceNodeId &&
                           node.Id != targetNodeId &&
                           !collapsedGroups.ContainsKey(node.Id))
            .Select(GraphNodeGeometry.GetCanvasBounds)
            .ToList();
        obstacles.AddRange(collapsedGroups.Values.Distinct());
        return obstacles;
    }

    private static GraphPoint GetPortPosition(
        IGraphPortPositionProvider? provider,
        NodeModel node,
        PortModel port) =>
        provider?.GetCanvasPosition(node, port) ??
        GraphPortPositionCalculator.GetCanvasPosition(node, port);

    private static GraphPoint ProjectToBounds(GraphRect bounds, GraphPoint target)
    {
        var centerX = bounds.X + bounds.Width / 2;
        var centerY = bounds.Y + bounds.Height / 2;
        var dx = target.X - centerX;
        var dy = target.Y - centerY;
        if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
        {
            return new GraphPoint(bounds.X + bounds.Width, centerY);
        }

        var scaleX = Math.Abs(dx) < double.Epsilon ? double.PositiveInfinity : bounds.Width / 2 / Math.Abs(dx);
        var scaleY = Math.Abs(dy) < double.Epsilon ? double.PositiveInfinity : bounds.Height / 2 / Math.Abs(dy);
        var scale = Math.Min(scaleX, scaleY);
        return new GraphPoint(centerX + dx * scale, centerY + dy * scale);
    }

    private static void DrawWrappedText(
        SKCanvas canvas,
        string text,
        SKRect bounds,
        string fontFamily,
        SKPaint paint)
    {
        var fontSize = 13f;
        using var font = CreateFont(text, fontFamily, fontSize);
        var lines = WrapText(text, font, paint, Math.Max(1, bounds.Width - 16));
        var y = bounds.Top + fontSize + 8;
        foreach (var line in lines)
        {
            if (y > bounds.Bottom - 6)
            {
                break;
            }

            canvas.DrawText(line, bounds.Left + 8, y, SKTextAlign.Left, font, paint);
            y += fontSize * 1.45f;
        }
    }

    private static IReadOnlyList<string> WrapText(string text, SKFont font, SKPaint paint, float width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", string.Empty).Split('\n'))
        {
            var current = string.Empty;
            foreach (var character in paragraph)
            {
                var candidate = current + character;
                if (current.Length > 0 && font.MeasureText(candidate, paint) > width)
                {
                    lines.Add(current);
                    current = character.ToString();
                }
                else
                {
                    current = candidate;
                }
            }

            lines.Add(current);
        }

        return lines;
    }

    private static void DrawText(
        SKCanvas canvas,
        string? text,
        float x,
        float y,
        float size,
        string fontFamily,
        SKPaint paint)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        using var font = CreateFont(text, fontFamily, size);
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
    }

    private static SKFont CreateFont(string text, string fontFamily, float size)
    {
        var character = text.FirstOrDefault(value => value > 127);
        var typeface = character == default
            ? SKTypeface.FromFamilyName(fontFamily) ?? SKTypeface.Default
            : SKFontManager.Default.MatchCharacter(fontFamily, character) ??
              SKTypeface.FromFamilyName(fontFamily) ??
              SKTypeface.Default;
        return new SKFont(typeface, size);
    }

    private static SKPaint CreatePaint(
        string? color,
        SKPaintStyle style,
        float strokeWidth = 1) => new()
        {
            IsAntialias = true,
            Color = ParseColor(color, SKColors.Black),
            Style = style,
            StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

    private static SKColor ParseColor(string? color, SKColor fallback) =>
        !string.IsNullOrWhiteSpace(color) && SKColor.TryParse(color, out var parsed)
            ? parsed
            : fallback;

    private static SKRect ToRect(GraphRect rect) => new(
        (float)rect.X,
        (float)rect.Y,
        (float)(rect.X + rect.Width),
        (float)(rect.Y + rect.Height));

    private sealed record EdgeLabelLayout(
        string Label,
        GraphEdgePath Route,
        GraphRect SourceBounds,
        GraphRect TargetBounds);

    private sealed record EdgeMarkerLayout(
        EdgeMarker Marker,
        GraphPoint From,
        GraphPoint Tip,
        SKColor Color);
}
