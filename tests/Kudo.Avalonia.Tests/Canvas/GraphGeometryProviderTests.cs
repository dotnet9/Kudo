using Kudo.Core.Algorithms;
using Shouldly;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class GraphGeometryProviderTests
{
    [Fact]
    public void Canvas_uses_custom_port_position_provider_for_edges_and_hit_queries()
    {
        var sourcePort = new PortModel { Id = "out", Kind = PortKind.Output };
        var targetPort = new PortModel { Id = "in", Kind = PortKind.Input };
        var source = new NodeModel { Id = "source", Ports = [sourcePort] };
        var target = new NodeModel { Id = "target", Ports = [targetPort] };
        var edge = new EdgeModel
        {
            Id = "edge",
            Source = new PortReference(source.Id, sourcePort.Id),
            Target = new PortReference(target.Id, targetPort.Id),
            RoutingStyle = EdgeRoutingStyle.Direct
        };
        var canvas = new GraphCanvas
        {
            Document = new GraphDocument
            {
                Nodes = [source, target],
                Edges = [edge]
            },
            PortPositionProvider = FixedPortPositionProvider.Instance
        };

        canvas.GetPortGraphPosition(source, sourcePort).ShouldBe(new GraphPoint(20, 30));
        canvas.GetPortGraphPosition(target, targetPort).ShouldBe(new GraphPoint(180, 90));
        canvas.GetEdgeGraphPath(edge)!.Points.ShouldBe([
            new GraphPoint(20, 30),
            new GraphPoint(180, 90)
        ]);
    }

    private sealed class FixedPortPositionProvider : IGraphPortPositionProvider
    {
        public static FixedPortPositionProvider Instance { get; } = new();

        public GraphPoint GetCanvasPosition(NodeModel node, PortModel port) =>
            node.Id == "source" ? new GraphPoint(20, 30) : new GraphPoint(180, 90);
    }
}
