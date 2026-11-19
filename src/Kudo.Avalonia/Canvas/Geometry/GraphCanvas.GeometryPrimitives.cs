using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private Rect ToGraphRect(NodeModel node)
    {
        var bounds = NodeGeometryProvider.GetBounds(node);
        return new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private GraphRect? GetViewportCullingGraphBounds()
    {
        if (!IsViewportCullingEnabled || GetViewportGraphBounds() is not { } viewportBounds)
        {
            return null;
        }

        var margin = ViewportCullingMargin / Viewport.Zoom;
        return new GraphRect(
            viewportBounds.X - margin,
            viewportBounds.Y - margin,
            viewportBounds.Width + margin * 2,
            viewportBounds.Height + margin * 2);
    }

}
