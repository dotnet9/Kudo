using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Kudo.Core.Algorithms;
using Kudo.Core.Runtime;
using RenderingLanguage = global::Localization.Kudo_Avalonia.Rendering.GraphRenderers;

namespace Kudo.Avalonia;

public readonly record struct GraphBackgroundRenderContext(
    DrawingContext DrawingContext,
    Rect Bounds,
    GraphPoint ViewportOffset,
    double Zoom,
    GraphGridSettings GridSettings,
    IBrush BackgroundBrush,
    IPen GridMinorPen,
    IPen GridMajorPen);

public interface IGraphBackgroundRenderer
{
    void Render(in GraphBackgroundRenderContext context);
}

public sealed class DefaultGraphBackgroundRenderer : IGraphBackgroundRenderer
{
    private const long MaximumLinesPerAxis = 512;

    public static DefaultGraphBackgroundRenderer Instance { get; } = new();

    private DefaultGraphBackgroundRenderer()
    {
    }

    public void Render(in GraphBackgroundRenderContext context)
    {
        context.DrawingContext.DrawRectangle(context.BackgroundBrush, null, context.Bounds);
        var settings = context.GridSettings.Normalize();
        if (!settings.IsVisible || context.Bounds.Width <= 0 || context.Bounds.Height <= 0)
        {
            return;
        }

        var metrics = GraphGridCalculator.GetRenderMetrics(settings, context.Zoom);
        var topLeft = ScreenToGraph(default, context.ViewportOffset, context.Zoom);
        var bottomRight = ScreenToGraph(
            new Point(context.Bounds.Width, context.Bounds.Height),
            context.ViewportOffset,
            context.Zoom);
        var firstX = (long)Math.Floor(topLeft.X / metrics.MinorSpacing) - 1;
        var lastX = (long)Math.Ceiling(bottomRight.X / metrics.MinorSpacing) + 1;
        var firstY = (long)Math.Floor(topLeft.Y / metrics.MinorSpacing) - 1;
        var lastY = (long)Math.Ceiling(bottomRight.Y / metrics.MinorSpacing) + 1;
        if (lastX - firstX > MaximumLinesPerAxis || lastY - firstY > MaximumLinesPerAxis)
        {
            return;
        }

        for (var index = firstX; index <= lastX; index++)
        {
            var screenX = index * metrics.MinorSpacing * context.Zoom + context.ViewportOffset.X;
            var pen = index % metrics.MajorLineInterval == 0 ? context.GridMajorPen : context.GridMinorPen;
            context.DrawingContext.DrawLine(
                pen,
                new Point(screenX, 0),
                new Point(screenX, context.Bounds.Height));
        }

        for (var index = firstY; index <= lastY; index++)
        {
            var screenY = index * metrics.MinorSpacing * context.Zoom + context.ViewportOffset.Y;
            var pen = index % metrics.MajorLineInterval == 0 ? context.GridMajorPen : context.GridMinorPen;
            context.DrawingContext.DrawLine(
                pen,
                new Point(0, screenY),
                new Point(context.Bounds.Width, screenY));
        }
    }

    private static GraphPoint ScreenToGraph(Point point, GraphPoint offset, double zoom) =>
        new((point.X - offset.X) / zoom, (point.Y - offset.Y) / zoom);
}

public readonly record struct GraphNodeRenderContext(
    DrawingContext DrawingContext,
    NodeModel Node,
    Rect Bounds,
    double Zoom,
    bool IsSelected,
    bool IsRuntimeActive,
    bool HasBreakpoint,
    GraphRuntimeNodeState? RuntimeState,
    IBrush FillBrush,
    IPen StrokePen,
    IBrush TitleBrush,
    IBrush BreakpointBrush,
    IPen BreakpointStrokePen)
{
    public IImage? BackgroundImage { get; init; }

    public bool IsHovered { get; init; }

    public bool ShowTitle { get; init; } = true;
}

public interface IGraphNodeRenderer
{
    void Render(in GraphNodeRenderContext context);
}

public interface IGraphNodeRendererDefaultCompatibility
{
    bool UsesDefaultRendering(NodeModel node);
}

public sealed class DefaultGraphNodeRenderer : IGraphNodeRenderer
{
    public static DefaultGraphNodeRenderer Instance { get; } = new();

    private DefaultGraphNodeRenderer()
    {
    }

    public void Render(in GraphNodeRenderContext context) => Render(context, false, true);

    internal void Render(
        in GraphNodeRenderContext context,
        bool isSimplified,
        bool showLabel)
    {
        var visualMode = context.Node.Appearance?.VisualMode ?? GraphNodeVisualMode.Auto;
        var isSymbol = visualMode == GraphNodeVisualMode.Symbol;
        var isText = visualMode == GraphNodeVisualMode.Text;
        if (isSymbol)
        {
            RenderBackgroundImage(context, clipToRoundedBounds: false);
            if (context.IsSelected || context.IsRuntimeActive)
            {
                context.DrawingContext.DrawRectangle(null, context.StrokePen, context.Bounds, 2, 2);
            }
        }
        else if (isText)
        {
            if (context.IsSelected || context.IsRuntimeActive)
            {
                context.DrawingContext.DrawRectangle(null, context.StrokePen, context.Bounds, 2, 2);
            }
        }
        else if (isSimplified)
        {
            context.DrawingContext.DrawRectangle(context.FillBrush, null, context.Bounds);
            RenderBackgroundImage(context, clipToRoundedBounds: false);
            context.DrawingContext.DrawRectangle(null, context.StrokePen, context.Bounds);
        }
        else
        {
            context.DrawingContext.DrawRectangle(context.FillBrush, null, context.Bounds, 6, 6);
            RenderBackgroundImage(context, clipToRoundedBounds: true);
            context.DrawingContext.DrawRectangle(null, context.StrokePen, context.Bounds, 6, 6);
        }

        if (showLabel && context.ShowTitle)
        {
            RenderLabel(context, isSymbol || isText);
        }

        if (context.HasBreakpoint)
        {
            var center = new Point(context.Bounds.Right - 10, context.Bounds.Top + 10);
            context.DrawingContext.DrawEllipse(
                context.BreakpointBrush,
                context.BreakpointStrokePen,
                center,
                5,
                5);
        }

        if (context.Node.IsLocked)
        {
            RenderLockBadge(context);
        }
    }

    private static void RenderLockBadge(in GraphNodeRenderContext context)
    {
        var size = Math.Clamp(12 * context.Zoom, 8, 16);
        var margin = 6 * context.Zoom;
        var left = context.Bounds.Right - margin - size;
        var top = context.Bounds.Bottom - margin - size;
        var body = new Rect(left, top + size * 0.35, size, size * 0.65);
        var pen = new Pen(context.StrokePen.Brush, Math.Max(1, context.StrokePen.Thickness * 0.75));
        context.DrawingContext.DrawRectangle(context.StrokePen.Brush, pen, body, 2, 2);
        var shackleLeft = new Point(left + size * 0.25, top + size * 0.4);
        var shackleRight = new Point(left + size * 0.75, top + size * 0.4);
        context.DrawingContext.DrawLine(
            pen,
            shackleLeft,
            new Point(shackleLeft.X, top + size * 0.2));
        context.DrawingContext.DrawLine(
            pen,
            new Point(shackleLeft.X, top + size * 0.2),
            new Point(shackleRight.X, top + size * 0.2));
        context.DrawingContext.DrawLine(
            pen,
            new Point(shackleRight.X, top + size * 0.2),
            shackleRight);
    }

    private static void RenderBackgroundImage(
        in GraphNodeRenderContext context,
        bool clipToRoundedBounds)
    {
        if (context.BackgroundImage is not { } image ||
            context.Node.Appearance is not { } appearance ||
            appearance.Opacity <= 0 ||
            context.Bounds.Width <= 0 ||
            context.Bounds.Height <= 0 ||
            image.Size.Width <= 0 ||
            image.Size.Height <= 0)
        {
            return;
        }

        var source = new Rect(image.Size);
        var destination = context.Bounds;
        switch (appearance.Stretch)
        {
            case GraphImageStretch.None:
                destination = Center(
                    context.Bounds,
                    Math.Min(context.Bounds.Width, image.Size.Width * context.Zoom),
                    Math.Min(context.Bounds.Height, image.Size.Height * context.Zoom));
                break;
            case GraphImageStretch.Uniform:
                var uniformScale = Math.Min(
                    context.Bounds.Width / image.Size.Width,
                    context.Bounds.Height / image.Size.Height);
                destination = Center(
                    context.Bounds,
                    image.Size.Width * uniformScale,
                    image.Size.Height * uniformScale);
                break;
            case GraphImageStretch.UniformToFill:
                var sourceAspect = image.Size.Width / image.Size.Height;
                var targetAspect = context.Bounds.Width / context.Bounds.Height;
                if (sourceAspect > targetAspect)
                {
                    var width = image.Size.Height * targetAspect;
                    source = new Rect((image.Size.Width - width) / 2, 0, width, image.Size.Height);
                }
                else
                {
                    var height = image.Size.Width / targetAspect;
                    source = new Rect(0, (image.Size.Height - height) / 2, image.Size.Width, height);
                }

                break;
        }

        if (clipToRoundedBounds)
        {
            using (context.DrawingContext.PushClip(new RoundedRect(context.Bounds, 6, 6)))
            using (context.DrawingContext.PushOpacity(Math.Clamp(appearance.Opacity, 0, 1)))
            {
                context.DrawingContext.DrawImage(image, source, destination);
            }

            return;
        }

        using (context.DrawingContext.PushClip(context.Bounds))
        using (context.DrawingContext.PushOpacity(Math.Clamp(appearance.Opacity, 0, 1)))
        {
            context.DrawingContext.DrawImage(image, source, destination);
        }
    }

    private static Rect Center(Rect bounds, double width, double height) => new(
        bounds.X + (bounds.Width - width) / 2,
        bounds.Y + (bounds.Height - height) / 2,
        width,
        height);

    private static void RenderLabel(in GraphNodeRenderContext context, bool isCentered)
    {
        var label = string.IsNullOrWhiteSpace(context.Node.Name)
            ? context.Node.Type
            : context.Node.Name;
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var appearance = (context.Node.Appearance ?? new GraphNodeAppearance()).Normalize();
        var textAppearance = appearance.TextAppearance.Normalize();
        var text = new FormattedText(
            label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            CreateTypeface(textAppearance.FontWeight),
            textAppearance.FontSize * context.Zoom,
            ResolveTextBrush(textAppearance.Foreground, context.TitleBrush));
        using (context.DrawingContext.PushClip(context.Bounds.Deflate(8 * context.Zoom)))
        {
            var origin = isCentered
                ? new Point(
                    context.Bounds.Left + Math.Max(
                        8 * context.Zoom,
                        (context.Bounds.Width - text.Width) / 2),
                    context.Bounds.Top + Math.Max(
                        0,
                        (context.Bounds.Height - text.Height) / 2))
                : new Point(
                    context.Bounds.Left + 14 * context.Zoom,
                    context.Bounds.Top + 10 * context.Zoom);
            context.DrawingContext.DrawText(
                text,
                origin);
        }
    }

    private static Typeface CreateTypeface(GraphFontWeight weight) => new(
        FontFamily.Default,
        FontStyle.Normal,
        weight switch
        {
            GraphFontWeight.Medium => FontWeight.Medium,
            GraphFontWeight.SemiBold => FontWeight.SemiBold,
            GraphFontWeight.Bold => FontWeight.Bold,
            _ => FontWeight.Normal
        });

    private static IBrush ResolveTextBrush(string? value, IBrush fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return new SolidColorBrush(Color.Parse(value));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return fallback;
        }
    }
}

public readonly record struct GraphPortRenderContext(
    DrawingContext DrawingContext,
    NodeModel Node,
    PortModel Port,
    Point Position,
    double Zoom,
    bool IsConnectionStart,
    bool IsConnectionCandidate,
    bool IsPointerOver,
    bool IsConnectionRejected,
    IBrush FillBrush,
    IPen StrokePen,
    IBrush TextBrush,
    IPen ConnectionCandidatePen)
{
    public bool ShowLabel { get; init; } = true;

    public bool HasOutgoingConnection { get; init; }

    public bool HasIncomingConnection { get; init; }

    public required GraphPortRenderMetrics Metrics { get; init; }

    public required IBrush InvalidFillBrush { get; init; }

    public required IBrush InvalidStrokeBrush { get; init; }
}

public readonly record struct GraphPortRenderMetrics(
    double Radius,
    double ActiveRadius,
    double CandidateRadius,
    double HighlightPadding,
    double LabelFontSize,
    double LabelOffset,
    double DisabledOpacity,
    double HighlightOpacity,
    double InvalidHighlightOpacity,
    double ActiveStrokeThickness,
    double StrokeThickness,
    double InvalidStrokeThickness,
    double InputOutputOffsetFactor,
    double InputOutputScale,
    double ChevronHalfWidthFactor);

public interface IGraphPortRenderer
{
    void Render(in GraphPortRenderContext context);
}

public interface IGraphPortRendererDefaultCompatibility
{
    bool UsesDefaultRendering(NodeModel node, PortModel port);
}

internal static class GraphPortGlyphDirection
{
    public static GraphPoint Resolve(NodeModel node, PortModel port, PortKind? displayKind = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(port);
        var configured = (displayKind ?? port.Kind) switch
        {
            PortKind.Input => port.IncomingDirection is { } incoming ? Negate(incoming) : null,
            PortKind.Output => port.OutgoingDirection,
            PortKind.InputOutput => port.OutgoingDirection ??
                (port.IncomingDirection is { } bidirectionalIncoming ? Negate(bidirectionalIncoming) : null),
            _ => null
        };
        return PortGeometry.ToNodeLocalDirection(configured, node.Size) ?? new GraphPoint(1, 0);
    }

    private static GraphPoint Negate(GraphPoint point) => new(-point.X, -point.Y);
}

internal static class GraphPortGlyphGeometry
{
    public static GraphPoint GetInputChevronCenter(
        GraphPoint anchor,
        double radius,
        double halfWidthFactor) =>
        OffsetAlongLocalXAxis(anchor, -radius * halfWidthFactor);

    public static GraphPoint GetOutputChevronCenter(
        GraphPoint anchor,
        double radius,
        double halfWidthFactor) =>
        OffsetAlongLocalXAxis(anchor, radius * halfWidthFactor);

    private static GraphPoint OffsetAlongLocalXAxis(
        GraphPoint point,
        double distance) =>
        new(
            point.X + distance,
            point.Y);
}

public sealed class DefaultGraphPortRenderer : IGraphPortRenderer
{
    public static DefaultGraphPortRenderer Instance { get; } = new();

    private DefaultGraphPortRenderer()
    {
    }

    public void Render(in GraphPortRenderContext context)
    {
        var isHighlighted =
            context.IsPointerOver ||
            context.IsConnectionStart ||
            context.IsConnectionCandidate ||
            context.Port.State == PortState.Highlighted;
        var isRejected =
            context.IsConnectionRejected ||
            context.Port.State == PortState.Invalid;
        var hideSourceGlyph =
            context.Port.Kind is PortKind.Output or PortKind.InputOutput &&
            (context.IsConnectionStart || context.HasOutgoingConnection);
        var metrics = context.Metrics;
        var radius = isHighlighted || isRejected ? metrics.ActiveRadius : metrics.Radius;
        var glyphBrush = isRejected
            ? context.InvalidStrokeBrush
            : isHighlighted
                ? context.ConnectionCandidatePen.Brush ?? context.FillBrush
                : context.FillBrush;
        var stroke = new Pen(
            glyphBrush,
            isRejected
                ? metrics.InvalidStrokeThickness
                : isHighlighted
                    ? metrics.ActiveStrokeThickness
                    : metrics.StrokeThickness);
        if (context.Port.State == PortState.Disabled)
        {
            if (!hideSourceGlyph)
            {
                using (context.DrawingContext.PushOpacity(metrics.DisabledOpacity))
                {
                    DrawPort(context, stroke, radius);
                }
            }

            RenderLabel(context);
            return;
        }

        if (isHighlighted || isRejected)
        {
            using (context.DrawingContext.PushOpacity(
                       isRejected ? metrics.InvalidHighlightOpacity : metrics.HighlightOpacity))
            {
                context.DrawingContext.DrawEllipse(
                    isRejected ? context.InvalidFillBrush : context.FillBrush,
                    null,
                    context.Position,
                    radius + metrics.HighlightPadding,
                    radius + metrics.HighlightPadding);
            }
        }

        if (!hideSourceGlyph)
        {
            DrawPort(context, stroke, radius);
        }

        if (context.IsConnectionCandidate)
        {
            context.DrawingContext.DrawEllipse(
                null,
                context.ConnectionCandidatePen,
                context.Position,
                metrics.CandidateRadius,
                metrics.CandidateRadius);
        }

        RenderLabel(context);
    }

    private static void DrawPort(
        in GraphPortRenderContext context,
        IPen stroke,
        double radius)
    {
        var displayKind = context.Port.Kind == PortKind.InputOutput && context.HasIncomingConnection
            ? PortKind.Input
            : context.Port.Kind;
        var direction = GraphPortGlyphDirection.Resolve(context.Node, context.Port, displayKind);
        var angle = Math.Atan2(direction.Y, direction.X);
        using var transform = context.DrawingContext.PushTransform(
            Matrix.CreateRotation(angle, context.Position));
        switch (displayKind)
        {
            case PortKind.Input:
                DrawChevron(
                    context.DrawingContext,
                    ToPoint(GraphPortGlyphGeometry.GetInputChevronCenter(
                        ToGraphPoint(context.Position),
                        radius,
                        context.Metrics.ChevronHalfWidthFactor)),
                    radius,
                    stroke,
                    context.Metrics.ChevronHalfWidthFactor,
                    pointsRight: true);
                break;
            case PortKind.Output:
                DrawChevron(
                    context.DrawingContext,
                    ToPoint(GraphPortGlyphGeometry.GetOutputChevronCenter(
                        ToGraphPoint(context.Position),
                        radius,
                        context.Metrics.ChevronHalfWidthFactor)),
                    radius,
                    stroke,
                    context.Metrics.ChevronHalfWidthFactor,
                    pointsRight: true);
                break;
            case PortKind.InputOutput:
                DrawChevron(
                    context.DrawingContext,
                    new Point(context.Position.X - radius * context.Metrics.InputOutputOffsetFactor, context.Position.Y),
                    radius * context.Metrics.InputOutputScale,
                    stroke,
                    context.Metrics.ChevronHalfWidthFactor,
                    pointsRight: true);
                DrawChevron(
                    context.DrawingContext,
                    new Point(context.Position.X + radius * context.Metrics.InputOutputOffsetFactor, context.Position.Y),
                    radius * context.Metrics.InputOutputScale,
                    stroke,
                    context.Metrics.ChevronHalfWidthFactor,
                    pointsRight: false);
                break;
            case PortKind.Reference:
                context.DrawingContext.DrawEllipse(null, stroke, context.Position, radius, radius);
                break;
        }
    }

    private static void DrawChevron(
        DrawingContext drawingContext,
        Point center,
        double radius,
        IPen stroke,
        double halfWidthFactor,
        bool pointsRight)
    {
        var direction = pointsRight ? 1d : -1d;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(center.X - direction * radius * halfWidthFactor, center.Y - radius), false);
            path.LineTo(new Point(center.X + direction * radius * halfWidthFactor, center.Y));
            path.LineTo(new Point(center.X - direction * radius * halfWidthFactor, center.Y + radius));
        }

        drawingContext.DrawGeometry(null, stroke, geometry);
    }

    private static GraphPoint ToGraphPoint(Point point) => new(point.X, point.Y);

    private static Point ToPoint(GraphPoint point) => new(point.X, point.Y);

    private static void RenderLabel(in GraphPortRenderContext context)
    {
        if (!context.ShowLabel || string.IsNullOrWhiteSpace(context.Port.Name))
        {
            return;
        }

        var text = new FormattedText(
            context.Port.Name,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            context.Metrics.LabelFontSize * context.Zoom,
            context.TextBrush);
        var offset = context.Metrics.LabelOffset * context.Zoom;
        var renderOnRight = context.Port.Kind == PortKind.Input ||
                            context.Port.Kind is PortKind.InputOutput or PortKind.Reference &&
                            (context.Port.Anchor?.X ?? 0) <= 0.5;
        var origin = renderOnRight
            ? new Point(context.Position.X + offset, context.Position.Y - text.Height / 2)
            : new Point(context.Position.X - text.Width - offset, context.Position.Y - text.Height / 2);
        context.DrawingContext.DrawText(text, origin);
    }
}

public enum GraphEdgeRenderLayer
{
    [Description("基础几何层")]
    Geometry,
    [Description("运行时叠加层")]
    RuntimeOverlay,
    [Description("标签层")]
    Label
}

public readonly record struct GraphEdgeRenderContext(
    DrawingContext DrawingContext,
    EdgeModel Edge,
    GraphEdgeRoute Route,
    GraphEdgeRenderLayer Layer,
    double Zoom,
    bool IsSelected,
    GraphRuntimeEdgeState? RuntimeState,
    double RuntimeProgress,
    IPen StrokePen,
    IBrush RuntimeSignalBrush,
    IPen RuntimeSignalStrokePen,
    IBrush LabelFillBrush,
    IPen LabelStrokePen,
    IBrush LabelTextBrush)
{
    public bool IsStrokeOverridden { get; init; }
}

public interface IGraphEdgeRenderer
{
    void Render(in GraphEdgeRenderContext context);
}

public interface IGraphEdgeRendererDefaultCompatibility
{
    bool UsesDefaultRendering(EdgeModel edge);
}

public sealed class DefaultGraphEdgeRenderer : IGraphEdgeRenderer
{
    private const double RuntimeSignalRadius = 4.5;
    private const double LabelProgress = 0.5;
    private static readonly double[] FlowDashPattern = [8, 5];

    public static DefaultGraphEdgeRenderer Instance { get; } = new();

    private DefaultGraphEdgeRenderer()
    {
    }

    public void Render(in GraphEdgeRenderContext context) => Render(context, false);

    internal static void RenderSimplifiedGeometry(
        DrawingContext context,
        GraphEdgePath path,
        Func<GraphPoint, Point> transform,
        IPen pen)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(transform);

        if (path.RoutingStyle == EdgeRoutingStyle.Bezier &&
            path.ControlPoint1 is { } controlPoint1 &&
            path.ControlPoint2 is { } controlPoint2)
        {
            var geometry = new StreamGeometry();
            using (var geometryContext = geometry.Open())
            {
                geometryContext.BeginFigure(transform(path.Start), false);
                geometryContext.CubicBezierTo(
                    transform(controlPoint1),
                    transform(controlPoint2),
                    transform(path.End));
                geometryContext.EndFigure(false);
            }

            context.DrawGeometry(null, pen, geometry);
            return;
        }

        var previous = transform(path.Points[0]);
        for (var index = 1; index < path.Points.Count; index++)
        {
            var current = transform(path.Points[index]);
            context.DrawLine(pen, previous, current);
            previous = current;
        }
    }

    internal void Render(in GraphEdgeRenderContext context, bool isSimplified)
    {
        switch (context.Layer)
        {
            case GraphEdgeRenderLayer.Geometry:
                RenderGeometry(context, isSimplified);
                break;
            case GraphEdgeRenderLayer.RuntimeOverlay when context.RuntimeState?.IsActive == true:
                context.DrawingContext.DrawEllipse(
                    context.RuntimeSignalBrush,
                    context.RuntimeSignalStrokePen,
                    context.Route.GetPointAtProgress(context.RuntimeProgress),
                    RuntimeSignalRadius,
                    RuntimeSignalRadius);
                break;
            case GraphEdgeRenderLayer.RuntimeOverlay when
                !context.IsStrokeOverridden &&
                (context.Edge.Appearance ?? new GraphEdgeAppearance()).Normalize().RequiresAnimationOverlay:
                RenderSignalAnimation(context);
                break;
            case GraphEdgeRenderLayer.Label when !string.IsNullOrWhiteSpace(context.Edge.Label):
                RenderLabel(context);
                break;
        }
    }

    private static void RenderGeometry(in GraphEdgeRenderContext context, bool isSimplified)
    {
        var appearance = (context.Edge.Appearance ?? new GraphEdgeAppearance()).Normalize();
        if (context.IsStrokeOverridden || appearance.IsDefault)
        {
            DrawRoute(context, context.StrokePen, isSimplified);
            return;
        }

        var progress = GetAnimationProgress(appearance, context.RuntimeProgress);
        var brush = ResolveAppearanceBrush(context, appearance, progress);
        var thickness = appearance.StrokeWidth ?? context.StrokePen.Thickness;
        var opacity = appearance.Opacity;
        if (appearance.Animation == GraphEdgeAnimationKind.Pulse)
        {
            var pulse = 0.5 + 0.5 * Math.Sin(progress * Math.PI * 2);
            thickness *= 0.85 + pulse * 0.5;
            opacity *= 0.55 + pulse * 0.45;
        }

        var dashStyle = CreateDashStyle(appearance, progress);
        var pen = new Pen(brush, thickness)
        {
            DashStyle = dashStyle,
            LineCap = PenLineCap.Round
        };

        if (appearance.GlowIntensity > 0)
        {
            var glowBrush = ResolveColorBrush(appearance.GlowColor) ?? brush;
            var glowPen = new Pen(
                glowBrush,
                thickness + 4 + appearance.GlowIntensity * 8)
            {
                LineCap = PenLineCap.Round
            };
            using (context.DrawingContext.PushOpacity(0.08 + appearance.GlowIntensity * 0.28))
            {
                context.Route.Draw(context.DrawingContext, glowPen);
            }
        }

        using (context.DrawingContext.PushOpacity(opacity))
        {
            DrawRoute(context, pen, isSimplified);
        }
    }

    private static void DrawRoute(
        in GraphEdgeRenderContext context,
        IPen pen,
        bool isSimplified)
    {
        if (isSimplified)
        {
            context.Route.Draw(context.DrawingContext, pen);
            return;
        }

        context.Route.Draw(
            context.DrawingContext,
            pen,
            context.Edge.SourceMarker,
            context.Edge.TargetMarker);
    }

    private static DashStyle? CreateDashStyle(
        GraphEdgeAppearance appearance,
        double animationProgress)
    {
        IReadOnlyList<double>? pattern = appearance.DashStyle switch
        {
            GraphEdgeDashStyle.Dash => [7, 4],
            GraphEdgeDashStyle.Dot => [1, 4],
            GraphEdgeDashStyle.DashDot => [7, 3, 1, 3],
            GraphEdgeDashStyle.LongDash => [12, 5],
            GraphEdgeDashStyle.Custom when appearance.CustomDashPattern.Count > 0 =>
                appearance.CustomDashPattern,
            _ => null
        };
        if (appearance.Animation == GraphEdgeAnimationKind.Flow && pattern is null)
        {
            pattern = FlowDashPattern;
        }

        if (pattern is null)
        {
            return null;
        }

        var offset = appearance.Animation == GraphEdgeAnimationKind.Flow
            ? -animationProgress * pattern.Sum() * 2
            : 0;
        return new DashStyle(pattern, offset);
    }

    private static IBrush ResolveAppearanceBrush(
        in GraphEdgeRenderContext context,
        GraphEdgeAppearance appearance,
        double animationProgress)
    {
        if (appearance.Animation == GraphEdgeAnimationKind.Rainbow)
        {
            return new SolidColorBrush(CreateRainbowColor(animationProgress));
        }

        return ResolveColorBrush(appearance.StrokeColor) ??
               context.StrokePen.Brush ??
               Brushes.Transparent;
    }

    private static IBrush? ResolveColorBrush(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return new SolidColorBrush(Color.Parse(value));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static double GetAnimationProgress(GraphEdgeAppearance appearance, double progress)
    {
        var scaled = progress * appearance.AnimationSpeed;
        scaled -= Math.Floor(scaled);
        return appearance.AnimationDirection switch
        {
            GraphEdgeAnimationDirection.Reverse => 1 - scaled,
            GraphEdgeAnimationDirection.Bidirectional => scaled <= 0.5
                ? scaled * 2
                : (1 - scaled) * 2,
            _ => scaled
        };
    }

    private static Color CreateRainbowColor(double progress)
    {
        var hue = (progress - Math.Floor(progress)) * 6;
        var sector = (int)Math.Floor(hue);
        var fraction = hue - sector;
        var low = (byte)46;
        var high = (byte)235;
        var rising = (byte)Math.Round(low + (high - low) * fraction);
        var falling = (byte)Math.Round(high - (high - low) * fraction);
        return sector switch
        {
            0 => Color.FromRgb(high, rising, low),
            1 => Color.FromRgb(falling, high, low),
            2 => Color.FromRgb(low, high, rising),
            3 => Color.FromRgb(low, falling, high),
            4 => Color.FromRgb(rising, low, high),
            _ => Color.FromRgb(high, low, falling)
        };
    }

    private static void RenderSignalAnimation(in GraphEdgeRenderContext context)
    {
        var appearance = (context.Edge.Appearance ?? new GraphEdgeAppearance()).Normalize();
        var progress = GetAnimationProgress(appearance, context.RuntimeProgress);
        var brush = ResolveColorBrush(appearance.StrokeColor) ?? context.RuntimeSignalBrush;
        for (var index = 0; index < 3; index++)
        {
            var signalProgress = progress - index * 0.22;
            signalProgress -= Math.Floor(signalProgress);
            var radius = RuntimeSignalRadius - index * 0.7;
            using (context.DrawingContext.PushOpacity(appearance.Opacity * (1 - index * 0.22)))
            {
                context.DrawingContext.DrawEllipse(
                    brush,
                    context.RuntimeSignalStrokePen,
                    context.Route.GetPointAtProgress(signalProgress),
                    radius,
                    radius);
            }
        }
    }

    private static void RenderLabel(in GraphEdgeRenderContext context)
    {
        var label = context.Edge.Label;
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var text = new FormattedText(
            label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12 * context.Zoom,
            context.LabelTextBrush);
        var position = context.Route.GetPointAtProgress(LabelProgress);
        var bounds = GetLabelBounds(position, text.Width, text.Height, context.Zoom);
        context.DrawingContext.DrawRectangle(
            context.LabelFillBrush,
            context.LabelStrokePen,
            bounds,
            4 * context.Zoom,
            4 * context.Zoom);
        context.DrawingContext.DrawText(
            text,
            new Point(position.X - text.Width / 2, position.Y - text.Height / 2));
    }

    internal static Rect? GetLabelBounds(
        string? label,
        Point position,
        double zoom)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var text = new FormattedText(
            label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12 * zoom,
            Brushes.Transparent);
        return GetLabelBounds(position, text.Width, text.Height, zoom);
    }

    private static Rect GetLabelBounds(
        Point position,
        double textWidth,
        double textHeight,
        double zoom)
    {
        var padding = 5 * zoom;
        return new Rect(
            position.X - textWidth / 2 - padding,
            position.Y - textHeight / 2 - padding,
            textWidth + padding * 2,
            textHeight + padding * 2);
    }
}

public readonly record struct GraphGroupProxyPort(
    EdgeModel Edge,
    GraphEdgeEndpoint Endpoint,
    Point Position);

public readonly record struct GraphGroupRenderContext(
    DrawingContext DrawingContext,
    GroupModel Group,
    Rect Bounds,
    double Zoom,
    bool IsSelected,
    IReadOnlyList<GraphGroupProxyPort> ProxyPorts,
    IBrush FillBrush,
    IBrush HeaderFillBrush,
    IPen StrokePen,
    IBrush TitleBrush,
    IBrush InputPortBrush,
    IBrush OutputPortBrush,
    IPen PortStrokePen);

public interface IGraphGroupRenderer
{
    void Render(in GraphGroupRenderContext context);
}

public interface IGraphGroupRendererDefaultCompatibility
{
    bool UsesDefaultRendering(GroupModel group);
}

public sealed class DefaultGraphGroupRenderer : IGraphGroupRenderer
{
    private const double PortRadius = 5;

    public static DefaultGraphGroupRenderer Instance { get; } = new();

    private DefaultGraphGroupRenderer()
    {
    }

    public void Render(in GraphGroupRenderContext context)
    {
        var cornerRadius = 8 * context.Zoom;
        context.DrawingContext.DrawRectangle(context.FillBrush, null, context.Bounds, cornerRadius, cornerRadius);
        var headerHeight = context.Group.IsCollapsed
            ? context.Bounds.Height
            : Math.Min(context.Bounds.Height, GraphGroupBoundsCalculator.HeaderHeight * context.Zoom);
        var headerBounds = new Rect(
            context.Bounds.X,
            context.Bounds.Y,
            context.Bounds.Width,
            headerHeight);
        context.DrawingContext.DrawRectangle(
            context.HeaderFillBrush,
            null,
            headerBounds,
            cornerRadius,
            cornerRadius);
        context.DrawingContext.DrawRectangle(
            null,
            context.StrokePen,
            context.Bounds,
            cornerRadius,
            cornerRadius);
        RenderTitle(context, headerBounds);
        RenderProxyPorts(context);
    }

    private static void RenderTitle(in GraphGroupRenderContext context, Rect headerBounds)
    {
        var label = string.IsNullOrWhiteSpace(context.Group.Title)
            ? GraphLocalization.Get(RenderingLanguage.DefaultGroupTitle)
            : context.Group.Title;
        var text = new FormattedText(
            $"{(context.Group.IsCollapsed ? "▸" : "▾")}  {label}",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12 * context.Zoom,
            context.TitleBrush);
        using (context.DrawingContext.PushClip(headerBounds.Deflate(6 * context.Zoom)))
        {
            context.DrawingContext.DrawText(
                text,
                new Point(
                    headerBounds.Left + 10 * context.Zoom,
                    headerBounds.Top + Math.Max(0, (headerBounds.Height - text.Height) / 2)));
        }
    }

    private static void RenderProxyPorts(in GraphGroupRenderContext context)
    {
        foreach (var proxy in context.ProxyPorts)
        {
            context.DrawingContext.DrawEllipse(
                proxy.Endpoint == GraphEdgeEndpoint.Source
                    ? context.OutputPortBrush
                    : context.InputPortBrush,
                context.PortStrokePen,
                proxy.Position,
                PortRadius,
                PortRadius);
        }
    }
}
