using Kudo;

namespace Kudo.Core.Algorithms;

/// <summary>
/// Resolves a port's canvas position. Hosts can replace this provider for
/// non-rectangular nodes or domain-specific port anchors.
/// </summary>
public interface IGraphPortPositionProvider
{
    GraphPoint GetCanvasPosition(NodeModel node, PortModel port);
}

/// <summary>
/// Uses the standard normalized port anchor and side-based fallback layout.
/// </summary>
public sealed class DefaultGraphPortPositionProvider : IGraphPortPositionProvider
{
    public static DefaultGraphPortPositionProvider Instance { get; } = new();

    private DefaultGraphPortPositionProvider()
    {
    }

    public GraphPoint GetCanvasPosition(NodeModel node, PortModel port) =>
        GraphPortPositionCalculator.GetCanvasPosition(node, port);
}
