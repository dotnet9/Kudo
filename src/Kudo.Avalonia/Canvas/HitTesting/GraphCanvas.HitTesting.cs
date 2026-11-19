using Avalonia;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public GraphGridRenderMetrics GetGridRenderMetrics() =>
        _hitTester.GetGridRenderMetrics();

    public NodeModel? HitTestNode(Point screenPoint) =>
        _hitTester.HitTestNode(screenPoint);

    public PortHitResult? HitTestPort(Point screenPoint) =>
        _hitTester.HitTestPort(screenPoint);

    public GroupModel? HitTestGroup(Point screenPoint) =>
        _hitTester.HitTestGroup(screenPoint);

    public EdgeModel? HitTestEdge(Point screenPoint) =>
        _hitTester.HitTestEdge(screenPoint);

    public EdgeSegmentHitResult? HitTestOrthogonalEdgeSegment(
        EdgeModel edge,
        Point screenPoint) =>
        _hitTester.HitTestOrthogonalEdgeSegment(edge, screenPoint);

    public EdgeEndpointHitResult? HitTestSelectedEdgeEndpoint(Point screenPoint) =>
        _hitTester.HitTestSelectedEdgeEndpoint(screenPoint);

    public EdgeWaypointHitResult? HitTestSelectedEdgeWaypoint(Point screenPoint) =>
        _hitTester.HitTestSelectedEdgeWaypoint(screenPoint);

    public NodeResizeHandleHitResult? HitTestSelectedNodeResizeHandle(Point screenPoint) =>
        _hitTester.HitTestSelectedNodeResizeHandle(screenPoint);

    public NodeRotationHandleHitResult? HitTestSelectedNodeRotationHandle(Point screenPoint) =>
        _hitTester.HitTestSelectedNodeRotationHandle(screenPoint);
}
