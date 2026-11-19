using Kudo.Core.Algorithms;
using Shouldly;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class IncrementalRoutingInvalidationTests
{
    [Fact]
    public void Moving_distant_obstacle_preserves_unaffected_smart_routes()
    {
        var fixture = CreateFixture();

        var upperPath = fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        var lowerPath = fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge);
        fixture.Router.RouteCount.ShouldBe(2);

        fixture.Obstacle.Position = new GraphPoint(400, 3_000);
        ApplyNodeGeometryChange(fixture.Canvas, fixture.Obstacle.Id);

        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge).ShouldBeSameAs(upperPath);
        fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge).ShouldBeSameAs(lowerPath);
        fixture.Router.RouteCount.ShouldBe(2);
    }

    [Fact]
    public void Moving_obstacle_into_and_out_of_path_invalidates_only_intersecting_route()
    {
        var fixture = CreateFixture();
        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        var lowerPath = fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge);

        fixture.Obstacle.Position = new GraphPoint(400, 0);
        ApplyNodeGeometryChange(fixture.Canvas, fixture.Obstacle.Id);

        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge).ShouldBeSameAs(lowerPath);
        fixture.Router.RouteCount.ShouldBe(3);

        fixture.Obstacle.Position = new GraphPoint(400, 2_000);
        ApplyNodeGeometryChange(fixture.Canvas, fixture.Obstacle.Id);

        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge).ShouldBeSameAs(lowerPath);
        fixture.Router.RouteCount.ShouldBe(4);
    }

    [Fact]
    public void Moving_endpoint_invalidates_only_connected_route()
    {
        var fixture = CreateFixture();
        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        var lowerPath = fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge);

        fixture.UpperSource.Position = new GraphPoint(0, 120);
        ApplyNodeGeometryChange(fixture.Canvas, fixture.UpperSource.Id);

        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge).ShouldBeSameAs(lowerPath);
        fixture.Router.RouteCount.ShouldBe(3);
    }

    [Fact]
    public void Custom_obstacle_provider_falls_back_to_full_smart_route_invalidation()
    {
        var fixture = CreateFixture();
        fixture.Canvas.EdgeObstacleProvider = new EmptyObstacleProvider();
        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge);
        fixture.Router.RouteCount.ShouldBe(2);

        fixture.Obstacle.Position = new GraphPoint(400, 3_000);
        ApplyNodeGeometryChange(fixture.Canvas, fixture.Obstacle.Id);

        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge);
        fixture.Router.RouteCount.ShouldBe(4);
    }

    [Fact]
    public void Routing_configuration_change_still_invalidates_all_routes()
    {
        var fixture = CreateFixture();
        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge);

        fixture.Canvas.EdgeRoutingOptions = new GraphEdgeRoutingOptions
        {
            SmartOrthogonal = new OrthogonalRouteOptions { ObstaclePadding = 24 }
        };

        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge);
        fixture.Router.RouteCount.ShouldBe(4);
    }

    [Fact]
    public void Missing_smart_snapshot_does_not_invalidate_unrelated_direct_routes()
    {
        var fixture = CreateFixture();
        fixture.UpperEdge.RoutingStyle = EdgeRoutingStyle.Direct;
        fixture.LowerEdge.RoutingStyle = EdgeRoutingStyle.Direct;
        var router = new CountingDirectRouter();
        fixture.Canvas.EdgeRoutingService = GraphEdgeRoutingService.Default.WithRouter(router);
        var upperPath = fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        var lowerPath = fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge);

        fixture.Obstacle.Position = new GraphPoint(400, 3_000);
        ApplyNodeGeometryChange(fixture.Canvas, fixture.Obstacle.Id);

        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge).ShouldBeSameAs(upperPath);
        fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge).ShouldBeSameAs(lowerPath);
        router.RouteCount.ShouldBe(2);
    }

    [Fact]
    public void Incremental_invalidation_updates_route_and_edge_spatial_index()
    {
        var fixture = CreateFixture();
        fixture.Canvas.EdgeRoutingService = GraphEdgeRoutingService.Default;
        var original = fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        original.ShouldNotBeNull();
        fixture.Canvas.HitTestEdge(new global::Avalonia.Point(450, 44))
            .ShouldBeSameAs(fixture.UpperEdge);

        fixture.Obstacle.Position = new GraphPoint(400, 0);
        ApplyNodeGeometryChange(fixture.Canvas, fixture.Obstacle.Id);

        var updated = fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        updated.ShouldNotBeNull();
        updated.Points.Count.ShouldBeGreaterThan(original.Points.Count);
        fixture.Canvas.HitTestEdge(new global::Avalonia.Point(450, 44)).ShouldBeNull();
    }

    [Fact]
    public void Batch_obstacle_changes_invalidate_each_route_once()
    {
        var fixture = CreateFixture();
        var secondObstacle = new NodeModel
        {
            Id = "second-obstacle",
            Position = new GraphPoint(500, 3_000),
            Size = new GraphSize(100, 88)
        };
        fixture.Canvas.Document!.Nodes.Add(secondObstacle);
        fixture.Canvas.RefreshGraph();
        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        var lowerPath = fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge);

        fixture.Obstacle.Position = new GraphPoint(400, 0);
        secondObstacle.Position = new GraphPoint(500, 0);
        ApplyNodeGeometryChange(
            fixture.Canvas,
            fixture.Obstacle.Id,
            secondObstacle.Id);

        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge).ShouldBeSameAs(lowerPath);
        fixture.Router.RouteCount.ShouldBe(3);
    }

    [Fact]
    public void Moving_collapsed_group_member_invalidates_routes_near_group_bounds()
    {
        var fixture = CreateFixture();
        fixture.Canvas.Document!.Groups.Add(new GroupModel
        {
            Id = "collapsed",
            IsCollapsed = true,
            NodeIds = [fixture.Obstacle.Id]
        });
        fixture.Canvas.RefreshGraph();
        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        var lowerPath = fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge);

        fixture.Obstacle.Position = new GraphPoint(400, 52);
        ApplyNodeGeometryChange(fixture.Canvas, fixture.Obstacle.Id);

        fixture.Canvas.GetEdgeGraphPath(fixture.UpperEdge);
        fixture.Canvas.GetEdgeGraphPath(fixture.LowerEdge).ShouldBeSameAs(lowerPath);
        fixture.Router.RouteCount.ShouldBe(3);
    }

    private static Fixture CreateFixture()
    {
        var upperSource = CreateNode("upper-source", 0, 0, PortKind.Output);
        var upperTarget = CreateNode("upper-target", 800, 0, PortKind.Input);
        var lowerSource = CreateNode("lower-source", 0, 1_000, PortKind.Output);
        var lowerTarget = CreateNode("lower-target", 800, 1_000, PortKind.Input);
        var obstacle = new NodeModel
        {
            Id = "obstacle",
            Position = new GraphPoint(400, 2_000),
            Size = new GraphSize(100, 88)
        };
        var upperEdge = CreateEdge("upper", upperSource, upperTarget);
        var lowerEdge = CreateEdge("lower", lowerSource, lowerTarget);
        var document = new GraphDocument
        {
            Nodes = [upperSource, upperTarget, lowerSource, lowerTarget, obstacle],
            Edges = [upperEdge, lowerEdge]
        };
        var router = new CountingSmartOrthogonalRouter();
        var canvas = new GraphCanvas
        {
            Document = document,
            EdgeRoutingService = GraphEdgeRoutingService.Default.WithRouter(router)
        };
        return new Fixture(
            canvas,
            router,
            upperSource,
            obstacle,
            upperEdge,
            lowerEdge);
    }

    private static NodeModel CreateNode(
        string id,
        double x,
        double y,
        PortKind portKind) => new()
    {
        Id = id,
        Position = new GraphPoint(x, y),
        Ports =
        [
            new PortModel
            {
                Id = "port",
                Kind = portKind,
                Anchor = portKind == PortKind.Output
                    ? new GraphPoint(1, 0.5)
                    : new GraphPoint(0, 0.5)
            }
        ]
    };

    private static EdgeModel CreateEdge(
        string id,
        NodeModel source,
        NodeModel target) => new()
    {
        Id = id,
        Source = new PortReference(source.Id, source.Ports[0].Id),
        Target = new PortReference(target.Id, target.Ports[0].Id),
        RoutingStyle = EdgeRoutingStyle.SmartOrthogonal
    };

    private static void ApplyNodeGeometryChange(GraphCanvas canvas, params string[] nodeIds) =>
        canvas.ApplyGraphChanges(new GraphChangeSet(
            GraphChangeKind.Geometry,
            new GraphElementChangeSet([], [], nodeIds),
            GraphElementChangeSet.Empty,
            GraphElementChangeSet.Empty,
            GraphElementChangeSet.Empty));

    private sealed class CountingSmartOrthogonalRouter : IGraphEdgeRouter
    {
        public EdgeRoutingStyle RoutingStyle => EdgeRoutingStyle.SmartOrthogonal;

        public int RouteCount { get; private set; }

        public GraphEdgePath Route(GraphEdgeRoutingRequest request)
        {
            RouteCount++;
            return new GraphEdgePath(request.RoutingStyle, [request.Start, request.End]);
        }
    }

    private sealed class CountingDirectRouter : IGraphEdgeRouter
    {
        public EdgeRoutingStyle RoutingStyle => EdgeRoutingStyle.Direct;

        public int RouteCount { get; private set; }

        public GraphEdgePath Route(GraphEdgeRoutingRequest request)
        {
            RouteCount++;
            return new GraphEdgePath(request.RoutingStyle, [request.Start, request.End]);
        }
    }

    private sealed class EmptyObstacleProvider : IGraphEdgeObstacleProvider
    {
        public IReadOnlyList<GraphRect> GetObstacles(GraphEdgeObstacleRequest request) => [];
    }

    private sealed record Fixture(
        GraphCanvas Canvas,
        CountingSmartOrthogonalRouter Router,
        NodeModel UpperSource,
        NodeModel Obstacle,
        EdgeModel UpperEdge,
        EdgeModel LowerEdge);
}
