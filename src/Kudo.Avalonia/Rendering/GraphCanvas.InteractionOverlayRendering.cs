using Avalonia;
using Avalonia.Media;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    /// <summary>
    /// 绘制仅在编辑期间出现的手柄、参考线和手势预览，不参与图形 Renderer 扩展边界。
    /// </summary>
    private void RenderInteractionOverlays(DrawingContext context)
    {
        if (!IsReadOnly)
        {
            RenderSelectedNodeResizeHandles(context);
            RenderSelectedAnnotationResizeHandles(context);
            RenderSelectedEdgeEndpoints(context);
            RenderSelectedEdgeWaypoints(context);
        }

        RenderNodePlacementPreview(context);
        RenderAlignmentGuides(context);

        RenderConnectionPreview(context);

        RenderEdgeReconnectionPreview(context);

        if (_interactionSession is RectangleSelectionInteractionSession selection)
        {
            var selectionRect = GraphGeometryMath.NormalizeRect(
                GraphToScreen(selection.StartGraphPoint),
                GraphToScreen(selection.CurrentGraphPoint));
            context.DrawRectangle(SelectionRectangleFillBrush, SelectionRectanglePen, selectionRect);
        }

        if (_interactionSession is NodePushingInteractionSession
            {
                Axis: { } pushAxis
            } pushing)
        {
            var start = GraphToScreen(pushing.StartGraphPoint);
            var endGraphPoint = pushAxis == GraphNodePushAxis.Horizontal
                ? new GraphPoint(pushing.StartGraphPoint.X + pushing.Amount, pushing.StartGraphPoint.Y)
                : new GraphPoint(pushing.StartGraphPoint.X, pushing.StartGraphPoint.Y + pushing.Amount);
            var end = GraphToScreen(endGraphPoint);
            var pushedArea = pushAxis == GraphNodePushAxis.Horizontal
                ? new Rect(Math.Min(start.X, end.X), 0, Math.Abs(end.X - start.X), Bounds.Height)
                : new Rect(0, Math.Min(start.Y, end.Y), Bounds.Width, Math.Abs(end.Y - start.Y));
            context.DrawRectangle(SelectionRectangleFillBrush, NodePushAreaPen, pushedArea);
        }

        if (_interactionSession is EdgeCuttingInteractionSession cutting)
        {
            var start = GraphToScreen(cutting.StartGraphPoint);
            var end = GraphToScreen(cutting.CurrentGraphPoint);
            context.DrawLine(EdgeCuttingLinePen, start, end);
            context.DrawEllipse(DangerBrush, null, start, 3, 3);
            context.DrawEllipse(DangerBrush, null, end, 3, 3);
        }
    }

    private void RenderConnectionPreview(DrawingContext context)
    {
        if (_interactionSession is ConnectPortsInteractionSession connection)
        {
            GraphEdgeRoute
                .Create(connection.PreviewPath, GraphToScreen)
                .Draw(context, EdgePen);
            return;
        }

        if (_interactionSession is ManualConnectionSegmentInteractionSession segment)
        {
            GraphEdgeRoute
                .Create(segment.PreviewPath, GraphToScreen)
                .Draw(context, SelectedEdgePen, EdgeMarker.None, EdgeMarker.Arrow);
            return;
        }

        if (_manualConnectionDraft is not { } draft)
        {
            return;
        }

        RenderManualConnectionPath(
            context,
            draft,
            draft.Endpoint);
        if (draft.AreDirectionHandlesVisible)
        {
            RenderManualConnectionDirectionHandles(context, draft);
        }
    }

    private void RenderManualConnectionPath(
        DrawingContext context,
        ManualConnectionDraft draft,
        GraphPoint endpoint,
        GraphPoint? trailingWaypoint = null)
    {
        GraphEdgeRoute
            .CreateManual(
                GetPortGraphPosition(draft.StartPort.Node, draft.StartPort.Port),
                endpoint,
                draft.Waypoints,
                GraphToScreen,
                trailingWaypoint)
            .Draw(context, SelectedEdgePen, EdgeMarker.None, EdgeMarker.Arrow);
    }

    private void RenderManualConnectionDirectionHandles(
        DrawingContext context,
        ManualConnectionDraft draft)
    {
        var endpoint = GraphToScreen(draft.Endpoint);
        var borderPen = new Pen(SelectedEdgeBrush, 1.5);
        var hoveredArrowPen = new Pen(OverlayStrokeBrush, 1.5);
        foreach (var direction in ManualConnectionGeometry.Directions)
        {
            var center = ManualConnectionGeometry.GetDirectionHandleCenter(
                endpoint,
                draft.Heading,
                direction);
            var isHovered = draft.HoveredDirection == direction;
            context.DrawEllipse(
                isHovered ? SelectedEdgeBrush : CanvasBackgroundBrush,
                borderPen,
                center,
                ManualConnectionGeometry.DirectionHandleRadius,
                ManualConnectionGeometry.DirectionHandleRadius);
            RenderManualConnectionDirectionArrow(
                context,
                center,
                ManualConnectionGeometry.GetDirection(draft.Heading, direction),
                isHovered ? hoveredArrowPen : borderPen);
        }
    }

    private static void RenderManualConnectionDirectionArrow(
        DrawingContext context,
        Point center,
        GraphPoint direction,
        IPen pen)
    {
        var vector = ManualConnectionGeometry.Normalize(direction);
        var normal = new GraphPoint(-vector.Y, vector.X);
        var tail = new Point(center.X - vector.X * 4, center.Y - vector.Y * 4);
        var tip = new Point(center.X + vector.X * 5, center.Y + vector.Y * 5);
        var wingCenter = new Point(tip.X - vector.X * 4, tip.Y - vector.Y * 4);
        context.DrawLine(pen, tail, tip);
        context.DrawLine(
            pen,
            tip,
            new Point(wingCenter.X + normal.X * 3, wingCenter.Y + normal.Y * 3));
        context.DrawLine(
            pen,
            tip,
            new Point(wingCenter.X - normal.X * 3, wingCenter.Y - normal.Y * 3));
    }

    private void RenderAlignmentGuides(DrawingContext context)
    {
        if (!GridSettings.IsGuidesEnabled || _activeAlignmentGuides.Count == 0)
        {
            return;
        }

        var alignmentGuidePen = AlignmentGuidePen;
        foreach (var guide in _activeAlignmentGuides)
        {
            if (guide.Orientation == GraphGuideOrientation.Vertical)
            {
                var screenX = GraphToScreen(new GraphPoint(guide.Position, 0)).X;
                context.DrawLine(
                    alignmentGuidePen,
                    new Point(screenX, 0),
                    new Point(screenX, Bounds.Height));
            }
            else
            {
                var screenY = GraphToScreen(new GraphPoint(0, guide.Position)).Y;
                context.DrawLine(
                    alignmentGuidePen,
                    new Point(0, screenY),
                    new Point(Bounds.Width, screenY));
            }
        }
    }

    private void RenderNodePlacementPreview(DrawingContext context)
    {
        if (NodePlacementPreview is not { } preview)
        {
            return;
        }

        var origin = GraphToScreen(preview.Position);
        var rect = new Rect(
            origin.X,
            origin.Y,
            preview.Size.Width * Viewport.Zoom,
            preview.Size.Height * Viewport.Zoom);
        var previewPen = preview.IsValid ? PlacementPreviewPen : InvalidPlacementPreviewPen;
        context.DrawRectangle(
            preview.VisualMode == GraphNodeVisualMode.Symbol
                ? null
                : preview.IsValid
                    ? PlacementPreviewFillBrush
                    : InvalidPlacementPreviewFillBrush,
            previewPen,
            rect,
            preview.VisualMode == GraphNodeVisualMode.Symbol ? 2 : 6,
            preview.VisualMode == GraphNodeVisualMode.Symbol ? 2 : 6);
        if (preview.VisualMode == GraphNodeVisualMode.Symbol || string.IsNullOrWhiteSpace(preview.Title))
        {
            return;
        }

        var text = GraphCanvasVisualFactory.CreateText(preview.Title, 14 * Viewport.Zoom, TitleBrush);
        using (context.PushClip(rect.Deflate(8 * Viewport.Zoom)))
        {
            context.DrawText(text, new Point(rect.Left + 14 * Viewport.Zoom, rect.Top + 10 * Viewport.Zoom));
        }
    }
}
