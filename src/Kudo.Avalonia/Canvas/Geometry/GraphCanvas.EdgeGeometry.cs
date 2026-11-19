using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private int FindWaypointInsertionIndex(EdgeModel edge, GraphEdgeRoute route, Point screenPoint)
    {
        var anchors = new List<(Point Position, int InsertionIndex)>
        {
            (route.Start, 0)
        };
        anchors.AddRange(edge.Waypoints
            .Select((waypoint, index) => (Waypoint: waypoint, Index: index))
            .Where(item => double.IsFinite(item.Waypoint.X) && double.IsFinite(item.Waypoint.Y))
            .Select(item => (GraphToScreen(item.Waypoint), item.Index)));
        anchors.Add((route.End, edge.Waypoints.Count));

        var bestIndex = edge.Waypoints.Count;
        var bestDistance = double.PositiveInfinity;
        for (var index = 1; index < anchors.Count; index++)
        {
            var distance = GraphGeometryMath.DistanceToSegmentSquared(
                screenPoint,
                anchors[index - 1].Position,
                anchors[index].Position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = anchors[index].InsertionIndex;
            }
        }

        return bestIndex;
    }
}
