using Avalonia;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public GraphPoint GetPortGraphPosition(NodeModel node, PortModel port) =>
        PortPositionProvider.GetCanvasPosition(node, port);

    public Point GetPortScreenPosition(NodeModel node, PortModel port) =>
        GraphToScreen(GetPortGraphPosition(node, port));
}
