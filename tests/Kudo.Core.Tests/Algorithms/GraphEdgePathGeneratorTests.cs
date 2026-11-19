using Kudo.Core.Algorithms;
using Shouldly;

namespace Kudo.Core.Tests.Algorithms;

public sealed class GraphEdgePathGeneratorTests
{
    [Fact]
    public void Routing_service_applies_generator_after_router()
    {
        var service = GraphEdgeRoutingService.Default.WithPathGenerator(new OffsetPathGenerator());

        var path = service.Route(new GraphEdgeRoutingRequest(
            EdgeRoutingStyle.Direct,
            new GraphPoint(10, 20),
            new GraphPoint(100, 40)));

        path.Points.ShouldBe([
            new GraphPoint(15, 25),
            new GraphPoint(105, 45)
        ]);
    }

    [Fact]
    public void Routing_service_can_remove_path_generator()
    {
        var service = GraphEdgeRoutingService.Default
            .WithPathGenerator(EdgeRoutingStyle.Direct, new OffsetPathGenerator())
            .WithPathGenerator(EdgeRoutingStyle.Direct, null);

        var path = service.Route(new GraphEdgeRoutingRequest(
            EdgeRoutingStyle.Direct,
            new GraphPoint(10, 20),
            new GraphPoint(100, 40)));

        path.Points.ShouldBe([
            new GraphPoint(10, 20),
            new GraphPoint(100, 40)
        ]);
    }

    private sealed class OffsetPathGenerator : IGraphEdgePathGenerator
    {
        public EdgeRoutingStyle RoutingStyle => EdgeRoutingStyle.Direct;

        public GraphEdgePath Generate(GraphEdgeRoutingRequest request, GraphEdgePath routedPath) =>
            new(
                routedPath.RoutingStyle,
                routedPath.Points.Select(point => new GraphPoint(point.X + 5, point.Y + 5)),
                routedPath.ControlPoint1,
                routedPath.ControlPoint2,
                routedPath.IsFallback);
    }
}
