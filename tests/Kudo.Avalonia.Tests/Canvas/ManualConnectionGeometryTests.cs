using Avalonia;
using Kudo;
using Shouldly;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class ManualConnectionGeometryTests
{
    [Fact]
    public void GetDirection_RotatesLeftAndRightInCanvasCoordinates()
    {
        var heading = new GraphPoint(1, 0);

        ManualConnectionGeometry.GetDirection(
                heading,
                ManualConnectionDirection.Forward)
            .ShouldBe(new GraphPoint(1, 0));
        ManualConnectionGeometry.GetDirection(
                heading,
                ManualConnectionDirection.Left)
            .ShouldBe(new GraphPoint(0, -1));
        ManualConnectionGeometry.GetDirection(
                heading,
                ManualConnectionDirection.Right)
            .ShouldBe(new GraphPoint(0, 1));
    }

    [Fact]
    public void ProjectOntoRay_ConstrainsPointAndRejectsBackwardDistance()
    {
        var origin = new GraphPoint(10, 20);

        ManualConnectionGeometry.ProjectOntoRay(
                origin,
                new GraphPoint(35, 46),
                new GraphPoint(1, 0))
            .ShouldBe(new GraphPoint(35, 20));
        ManualConnectionGeometry.ProjectOntoRay(
                origin,
                new GraphPoint(2, 30),
                new GraphPoint(1, 0))
            .ShouldBe(origin);
    }

    [Fact]
    public void HitTestDirectionHandle_ReturnsMatchingDirection()
    {
        var endpoint = new Point(100, 100);
        var heading = new GraphPoint(1, 0);

        ManualConnectionGeometry.HitTestDirectionHandle(
                new Point(130, 100),
                endpoint,
                heading)
            .ShouldBe(ManualConnectionDirection.Forward);
        ManualConnectionGeometry.HitTestDirectionHandle(
                new Point(100, 70),
                endpoint,
                heading)
            .ShouldBe(ManualConnectionDirection.Left);
        ManualConnectionGeometry.HitTestDirectionHandle(
                new Point(100, 130),
                endpoint,
                heading)
            .ShouldBe(ManualConnectionDirection.Right);
    }

    [Fact]
    public void ResolveHeading_UsesFallbackForCoincidentPoints()
    {
        ManualConnectionGeometry.ResolveHeading(
                new GraphPoint(10, 20),
                new GraphPoint(10, 20),
                new GraphPoint(0, -2))
            .ShouldBe(new GraphPoint(0, -1));
    }

    [Theory]
    [InlineData(5, 3, 1, 0)]
    [InlineData(-5, 3, -1, 0)]
    [InlineData(3, 5, 0, 1)]
    [InlineData(3, -5, 0, -1)]
    public void Normalize_QuantizesDirectionToCanvasAxis(
        double x,
        double y,
        double expectedX,
        double expectedY)
    {
        ManualConnectionGeometry.Normalize(new GraphPoint(x, y))
            .ShouldBe(new GraphPoint(expectedX, expectedY));
    }

    [Fact]
    public void CreateOrthogonalCompletionPath_UsesOnlyRightAngleSegments()
    {
        var path = ManualConnectionGeometry.CreateOrthogonalCompletionPath(
            [
                new GraphPoint(0, 0),
                new GraphPoint(80, 0),
                new GraphPoint(80, 100),
                new GraphPoint(150, 65)
            ],
            new GraphPoint(260, 180),
            new GraphPoint(1, -1),
            new GraphPoint(-1, 0),
            20);

        path[^1].ShouldBe(new GraphPoint(260, 180));
        path.Zip(path.Skip(1), (start, end) => start.X == end.X || start.Y == end.Y)
            .ShouldAllBe(isOrthogonal => isOrthogonal);
    }

    [Fact]
    public void CreateManual_FiltersDuplicateAndInvalidPreviewPoints()
    {
        var route = GraphEdgeRoute.CreateManual(
            new GraphPoint(0, 0),
            new GraphPoint(30, 10),
            [
                new GraphPoint(0, 0),
                new GraphPoint(10, 0),
                new GraphPoint(10, 0),
                new GraphPoint(double.NaN, 0)
            ],
            point => new Point(point.X, point.Y),
            new GraphPoint(20, 0));

        route.RoutingStyle.ShouldBe(EdgeRoutingStyle.Manual);
        route.Points.ShouldBe([
            new Point(0, 0),
            new Point(30, 0),
            new Point(30, 10)
        ]);
    }

    [Fact]
    public void ManualConnectionDraft_PreservesPreviewRouteAndFinalHeading()
    {
        var node = new NodeModel { Id = "node" };
        var port = new PortModel { Id = "output", Kind = PortKind.Output };
        node.Ports.Add(port);
        var startPort = new PortHitResult(
            node,
            port,
            new PortReference(node.Id, port.Id),
            default);

        var draft = new ManualConnectionDraft(
            startPort,
            [
                new GraphPoint(0, 0),
                new GraphPoint(40, 0),
                new GraphPoint(40, 80),
                new GraphPoint(100, 80)
            ],
            new GraphPoint(0, 1));

        draft.Waypoints.ShouldBe([
            new GraphPoint(40, 0),
            new GraphPoint(40, 80),
            new GraphPoint(100, 80)
        ]);
        draft.Endpoint.ShouldBe(new GraphPoint(100, 80));
        draft.Heading.ShouldBe(new GraphPoint(1, 0));
    }
}
