using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public bool ReconnectEdge(string edgeId, PortReference source, PortReference target) =>
        _editController.ReconnectEdge(edgeId, source, target);

    public Point? GetEdgeLabelScreenPosition(EdgeModel edge) =>
        _editController.GetEdgeLabelScreenPosition(edge);

    public bool AddSelectedEdgeWaypoint(Point screenPoint) =>
        _editController.AddSelectedEdgeWaypoint(screenPoint);

    public bool RemoveSelectedEdgeWaypoint(int index) =>
        _editController.RemoveSelectedEdgeWaypoint(index);

    public bool DeleteSelectedEdges() =>
        _editController.DeleteSelectedEdges();
}
