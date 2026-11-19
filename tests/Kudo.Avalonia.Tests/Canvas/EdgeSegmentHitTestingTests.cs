using Avalonia;
using Avalonia.Input;
using Kudo;
using Shouldly;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class EdgeSegmentHitTestingTests
{
    [Theory]
    [InlineData(EdgeRoutingStyle.Orthogonal)]
    [InlineData(EdgeRoutingStyle.Manual)]
    public void Orthogonal_edge_segment_hit_test_accepts_orthogonal_routes(
        EdgeRoutingStyle routingStyle)
    {
        var sourcePort = new PortModel
        {
            Id = "out",
            Kind = PortKind.Output,
            Anchor = new GraphPoint(1, 0.5)
        };
        var targetPort = new PortModel
        {
            Id = "in",
            Kind = PortKind.Input,
            Anchor = new GraphPoint(0, 0.5)
        };
        var source = new NodeModel
        {
            Id = "source",
            Position = new GraphPoint(0, 0),
            Ports = [sourcePort]
        };
        var target = new NodeModel
        {
            Id = "target",
            Position = new GraphPoint(500, 200),
            Ports = [targetPort]
        };
        var edge = new EdgeModel
        {
            Id = "edge",
            Source = new PortReference(source.Id, sourcePort.Id),
            Target = new PortReference(target.Id, targetPort.Id),
            RoutingStyle = routingStyle,
            Waypoints = routingStyle == EdgeRoutingStyle.Manual
                ? [
                    new GraphPoint(220, 44),
                    new GraphPoint(220, 140),
                    new GraphPoint(420, 140),
                    new GraphPoint(420, 244)
                ]
                : []
        };
        var canvas = new GraphCanvas
        {
            Document = new GraphDocument
            {
                Nodes = [source, target],
                Edges = [edge]
            }
        };

        var route = canvas.GetEdgeScreenRoute(edge);
        route.ShouldNotBeNull();
        var horizontalSegment = route.Points
            .Zip(route.Points.Skip(1), (start, end) => (start, end))
            .First(segment => segment.start.Y == segment.end.Y &&
                              segment.start.X != segment.end.X);
        var midpoint = new Point(
            (horizontalSegment.start.X + horizontalSegment.end.X) / 2,
            horizontalSegment.start.Y);

        var hit = canvas.HitTestOrthogonalEdgeSegment(edge, midpoint);

        hit.ShouldNotBeNull();
        hit.Edge.ShouldBeSameAs(edge);
        hit.Orientation.ShouldBe(OrthogonalEdgeSegmentOrientation.Horizontal);

        var originalWaypoints = edge.Waypoints.ToArray();
        var session = new OrthogonalEdgeSegmentInteractionSession(
            canvas,
            pointer: null!,
            midpoint,
            hit);
        session.Update(
            new Point(midpoint.X, midpoint.Y + 24),
            KeyModifiers.None);

        edge.Waypoints.SequenceEqual(originalWaypoints).ShouldBeFalse();
        var movedPath = canvas.GetEdgeGraphPath(edge);
        movedPath.ShouldNotBeNull();
        movedPath.Points
            .Zip(movedPath.Points.Skip(1), (start, end) => start.X == end.X || start.Y == end.Y)
            .ShouldAllBe(isOrthogonal => isOrthogonal);

        session.Cancel();
        edge.Waypoints.ShouldBe(originalWaypoints);
    }
}
