using Kudo;

namespace Kudo.Core.Algorithms;

public static class GraphPortPositionCalculator
{
    public static GraphPoint GetCanvasPosition(NodeModel node, PortModel port)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(port);

        if (port.Anchor is { } anchor &&
            double.IsFinite(anchor.X) &&
            double.IsFinite(anchor.Y))
        {
            var clampedX = Math.Clamp(anchor.X, 0, 1);
            var clampedY = Math.Clamp(anchor.Y, 0, 1);
            return GraphNodeGeometry.TransformPoint(
                node,
                new GraphPoint(
                    node.Position.X + node.Size.Width * clampedX,
                    node.Position.Y + node.Size.Height * clampedY));
        }

        var sameSidePortCount = 0;
        var index = -1;
        foreach (var candidate in node.Ports)
        {
            if (ResolveDefaultPortSide(candidate) != ResolveDefaultPortSide(port))
            {
                continue;
            }

            if (index < 0 && candidate.Id == port.Id)
            {
                index = sameSidePortCount;
            }

            sameSidePortCount++;
        }

        if (index < 0)
        {
            index = 0;
        }

        var x = ResolveDefaultPortX(node, port);
        var y = node.Position.Y + node.Size.Height * (index + 1) / (sameSidePortCount + 1);
        return GraphNodeGeometry.TransformPoint(node, new GraphPoint(x, y));
    }

    private static double ResolveDefaultPortX(NodeModel node, PortModel port) =>
        ResolveDefaultPortSide(port) switch
        {
            DefaultPortSide.Left => node.Position.X,
            DefaultPortSide.Right => node.Position.X + node.Size.Width,
            _ => node.Position.X + node.Size.Width / 2
        };

    private static DefaultPortSide ResolveDefaultPortSide(PortModel port) => port.Kind switch
    {
        PortKind.Input => DefaultPortSide.Left,
        PortKind.Output => DefaultPortSide.Right,
        PortKind.InputOutput => (port.Anchor?.X ?? 0.5) <= 0.5
            ? DefaultPortSide.Left
            : DefaultPortSide.Right,
        _ => DefaultPortSide.Center
    };

    private enum DefaultPortSide
    {
        Left,
        Right,
        Center
    }
}
