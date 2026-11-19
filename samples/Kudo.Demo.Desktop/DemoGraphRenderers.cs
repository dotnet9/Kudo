using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Kudo.Avalonia;

namespace Kudo.Demo.Desktop;

internal static class DemoGraphRendererShowcase
{
    public static void Apply(GraphCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.BackgroundRenderer = ShowcaseBackgroundRenderer.Instance;
        canvas.NodeRenderer = ShowcaseNodeRenderer.Instance;
        canvas.PortRenderer = ShowcasePortRenderer.Instance;
        canvas.EdgeRenderer = ShowcaseEdgeRenderer.Instance;
        canvas.GroupRenderer = ShowcaseGroupRenderer.Instance;
    }

    private sealed class ShowcaseBackgroundRenderer : IGraphBackgroundRenderer
    {
        public static ShowcaseBackgroundRenderer Instance { get; } = new();

        public void Render(in GraphBackgroundRenderContext context)
        {
            DefaultGraphBackgroundRenderer.Instance.Render(context);
            context.DrawingContext.DrawLine(
                context.GridMajorPen,
                new Point(context.ViewportOffset.X, 0),
                new Point(context.ViewportOffset.X, context.Bounds.Height));
            context.DrawingContext.DrawLine(
                context.GridMajorPen,
                new Point(0, context.ViewportOffset.Y),
                new Point(context.Bounds.Width, context.ViewportOffset.Y));
        }
    }

    private sealed class ShowcaseNodeRenderer : IGraphNodeRenderer
    {
        public static ShowcaseNodeRenderer Instance { get; } = new();

        public void Render(in GraphNodeRenderContext context)
        {
            var cornerRadius = 8 * context.Zoom;
            context.DrawingContext.DrawRectangle(
                context.FillBrush,
                context.StrokePen,
                context.Bounds,
                cornerRadius,
                cornerRadius);
            var headerBounds = new Rect(
                context.Bounds.X,
                context.Bounds.Y,
                context.Bounds.Width,
                Math.Min(context.Bounds.Height, 30 * context.Zoom));
            context.DrawingContext.DrawRectangle(
                context.StrokePen.Brush,
                null,
                headerBounds,
                cornerRadius,
                cornerRadius);
            DrawText(
                context.DrawingContext,
                string.IsNullOrWhiteSpace(context.Node.Name) ? context.Node.Type : context.Node.Name,
                13 * context.Zoom,
                context.TitleBrush,
                new Point(headerBounds.Left + 12 * context.Zoom, headerBounds.Top + 6 * context.Zoom));

            var typeLabel = context.Node.Type.ToUpperInvariant();
            var typeText = CreateText(typeLabel, 9 * context.Zoom, context.TitleBrush);
            var badgeBounds = new Rect(
                context.Bounds.Right - typeText.Width - 16 * context.Zoom,
                context.Bounds.Bottom - typeText.Height - 10 * context.Zoom,
                typeText.Width + 10 * context.Zoom,
                typeText.Height + 4 * context.Zoom);
            context.DrawingContext.DrawRectangle(
                context.FillBrush,
                context.StrokePen,
                badgeBounds,
                4 * context.Zoom,
                4 * context.Zoom);
            context.DrawingContext.DrawText(
                typeText,
                new Point(badgeBounds.Left + 5 * context.Zoom, badgeBounds.Top + 2 * context.Zoom));

            if (context.HasBreakpoint)
            {
                context.DrawingContext.DrawEllipse(
                    context.BreakpointBrush,
                    context.BreakpointStrokePen,
                    new Point(context.Bounds.Right - 10, context.Bounds.Top + 10),
                    5,
                    5);
            }
        }
    }

    private sealed class ShowcasePortRenderer : IGraphPortRenderer
    {
        public static ShowcasePortRenderer Instance { get; } = new();

        public void Render(in GraphPortRenderContext context)
        {
            DefaultGraphPortRenderer.Instance.Render(context);
            var size = context.IsConnectionStart ? 5 : 3;
            context.DrawingContext.DrawRectangle(
                context.StrokePen.Brush,
                null,
                new Rect(context.Position.X - size, context.Position.Y - size, size * 2, size * 2),
                1,
                1);
        }
    }

    private sealed class ShowcaseEdgeRenderer : IGraphEdgeRenderer
    {
        public static ShowcaseEdgeRenderer Instance { get; } = new();

        public void Render(in GraphEdgeRenderContext context)
        {
            if (context.Layer != GraphEdgeRenderLayer.Geometry)
            {
                DefaultGraphEdgeRenderer.Instance.Render(context);
                return;
            }

            context.Route.Draw(
                context.DrawingContext,
                new Pen(context.LabelFillBrush, context.StrokePen.Thickness + 4),
                context.Edge.SourceMarker,
                context.Edge.TargetMarker);
            context.Route.Draw(
                context.DrawingContext,
                new Pen(context.StrokePen.Brush, context.StrokePen.Thickness + 0.75),
                context.Edge.SourceMarker,
                context.Edge.TargetMarker);
        }
    }

    private sealed class ShowcaseGroupRenderer : IGraphGroupRenderer
    {
        public static ShowcaseGroupRenderer Instance { get; } = new();

        public void Render(in GraphGroupRenderContext context)
        {
            DefaultGraphGroupRenderer.Instance.Render(context);
            var underlineY = Math.Min(
                context.Bounds.Bottom,
                context.Bounds.Top + 32 * context.Zoom);
            context.DrawingContext.DrawLine(
                context.StrokePen,
                new Point(context.Bounds.Left + 8 * context.Zoom, underlineY),
                new Point(context.Bounds.Right - 8 * context.Zoom, underlineY));
        }
    }

    private static void DrawText(
        DrawingContext context,
        string text,
        double fontSize,
        IBrush brush,
        Point position) =>
        context.DrawText(CreateText(text, fontSize, brush), position);

    private static FormattedText CreateText(string text, double fontSize, IBrush brush) =>
        new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            brush);
}
