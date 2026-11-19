// 端口层渲染：端口定位、连接态和端口度量参数。
using Avalonia;
using Avalonia.Media;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasPortRenderPass(GraphDocumentLookup documentLookup)
{
    public void Render(
        GraphCanvasRenderRequest request,
        NodeModel node,
        Rect nodeBounds)
    {
        if (!request.ShowPorts)
        {
            return;
        }

        if (request.Document is { } document)
        {
            documentLookup.RebuildIfNeeded(document);
        }

        var showLabels = GraphLabelVisibilityResolver.IsPortLabelVisible(
            node,
            request.PortLabelVisibility,
            request.SelectedNodeIds.Contains(node.Id),
            string.Equals(request.HoveredNodeId, node.Id, StringComparison.Ordinal));
        foreach (var port in node.Ports)
        {
            if (!port.IsVisible)
            {
                continue;
            }
            var brush = ResolvePortBrush(request, port.Kind);
            var rotatedPosition = request.Callbacks.GetPortScreenPosition(node, port);
            var position = RotateScreenPoint(
                rotatedPosition,
                nodeBounds.Center,
                -node.Rotation);
            var isConnectionStart = IsMatchingPort(request.ConnectionStartPort, node, port);
            var isConnectionCandidate = IsMatchingPort(request.ConnectionCandidatePort, node, port);
            var isPointerOver = IsMatchingPort(request.HoveredPort, node, port);
            var portReference = new PortReference(node.Id, port.Id);
            var hasOutgoingConnection = documentLookup.HasOutgoingConnection(portReference);
            var hasIncomingConnection = documentLookup.HasIncomingConnection(portReference);
            request.PortRenderer.Render(new GraphPortRenderContext(
                request.Context,
                node,
                port,
                position,
                request.Zoom,
                isConnectionStart,
                isConnectionCandidate,
                isPointerOver,
                IsConnectionRejected(request, isPointerOver, isConnectionStart, isConnectionCandidate),
                brush,
                request.Palette.PortStrokePen,
                request.Palette.PortTextBrush,
                request.Palette.SelectedEdgePen)
            {
                ShowLabel = showLabels,
                HasOutgoingConnection = hasOutgoingConnection,
                HasIncomingConnection = hasIncomingConnection,
                Metrics = new GraphPortRenderMetrics(
                    request.Palette.PortGlyphRadius,
                    request.Palette.ActivePortGlyphRadius,
                    request.Palette.PortCandidateRadius,
                    request.Palette.PortHighlightPadding,
                    request.Palette.PortLabelFontSize,
                    request.Palette.PortLabelOffset,
                    request.Palette.PortDisabledOpacity,
                    request.Palette.PortHighlightOpacity,
                    request.Palette.InvalidPortHighlightOpacity,
                    request.Palette.ActivePortStrokeThickness,
                    request.Palette.PortGlyphStrokeThickness,
                    request.Palette.InvalidPortStrokeThickness,
                    request.Palette.InputOutputPortGlyphOffsetFactor,
                    request.Palette.InputOutputPortGlyphScale,
                    request.Palette.PortChevronHalfWidthFactor),
                InvalidFillBrush = request.Palette.InvalidPortFillBrush,
                InvalidStrokeBrush = request.Palette.InvalidPortStrokeBrush
            });
        }
    }

    private static IBrush ResolvePortBrush(
        GraphCanvasRenderRequest request,
        PortKind kind) =>
        kind switch
        {
            PortKind.Input => request.Palette.InputPortBrush,
            PortKind.Output => request.Palette.OutputPortBrush,
            PortKind.InputOutput => request.Palette.BidirectionalPortBrush,
            _ => request.Palette.ReferencePortBrush
        };

    private static bool IsConnectionRejected(
        GraphCanvasRenderRequest request,
        bool isPointerOver,
        bool isConnectionStart,
        bool isConnectionCandidate) =>
        isPointerOver &&
        !isConnectionStart &&
        !isConnectionCandidate &&
        (request.ConnectionStartPort is not null || request.ReconnectingEdge is not null);

    private static Point RotateScreenPoint(Point point, Point center, double rotation)
    {
        var transformed = GraphNodeGeometry.RotatePoint(
            new GraphPoint(point.X, point.Y),
            new GraphPoint(center.X, center.Y),
            rotation);
        return new Point(transformed.X, transformed.Y);
    }

    private static bool IsMatchingPort(PortHitResult? hit, NodeModel node, PortModel port) =>
        hit is not null && ReferenceEquals(hit.Node, node) && ReferenceEquals(hit.Port, port);
}
