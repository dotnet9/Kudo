using Avalonia;
using Avalonia.Media;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphMinimapContentRenderer
{
    public void Render(
        DrawingContext context,
        GraphCanvas canvas,
        GraphDocument document,
        GraphMinimapProjection projection,
        GraphMinimapRenderPalette palette)
    {
        foreach (var group in document.Groups)
        {
            if (GraphGroupBoundsCalculator.Calculate(document, group) is not { } bounds)
            {
                continue;
            }

            context.DrawRectangle(
                palette.GroupBrush,
                null,
                projection.GraphToMinimap(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height)),
                2,
                2);
        }

        foreach (var edge in document.Edges)
        {
            if (canvas.GetEdgeGraphPath(edge) is not { } path)
            {
                continue;
            }

            GraphPoint? previous = null;
            foreach (var point in path.Points)
            {
                if (previous is { } start)
                {
                    context.DrawLine(
                        palette.EdgePen,
                        projection.GraphToMinimap(start),
                        projection.GraphToMinimap(point));
                }

                previous = point;
            }
        }

        foreach (var node in document.Nodes)
        {
            if (!canvas.IsNodeVisible(node.Id))
            {
                continue;
            }

            var bounds = GraphNodeGeometry.GetCanvasBounds(node);
            var position = projection.GraphToMinimap(new GraphPoint(bounds.X, bounds.Y));
            context.DrawRectangle(
                palette.NodeBrush,
                null,
                new Rect(
                    position.X,
                    position.Y,
                    Math.Max(4, bounds.Width * projection.Scale),
                    Math.Max(3, bounds.Height * projection.Scale)),
                2,
                2);
        }
    }
}

internal readonly record struct GraphMinimapRenderPalette(
    IBrush BackgroundBrush,
    IBrush NodeBrush,
    IBrush SelectedNodeBrush,
    IBrush GroupBrush,
    IBrush SelectedGroupBrush,
    IPen EdgePen,
    IBrush ViewportBrush,
    IPen ViewportPen,
    IPen BorderPen);
