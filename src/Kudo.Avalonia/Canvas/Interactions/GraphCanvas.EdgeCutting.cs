using Avalonia;
using Avalonia.Input;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public static readonly StyledProperty<bool> IsEdgeCuttingEnabledProperty =
        AvaloniaProperty.Register<GraphCanvas, bool>(nameof(IsEdgeCuttingEnabled), true);

    public bool IsEdgeCuttingEnabled
    {
        get => GetValue(IsEdgeCuttingEnabledProperty);
        set => SetValue(IsEdgeCuttingEnabledProperty, value);
    }

    public bool IsEdgeCutting => _interactionSession is EdgeCuttingInteractionSession;

    public IReadOnlyList<string> EdgeCuttingCandidateIds =>
        (_interactionSession as EdgeCuttingInteractionSession)?.CandidateEdges
        .Select(edge => edge.Id)
        .ToArray() ?? [];

    public int CutEdgesIntersectingSegment(GraphPoint start, GraphPoint end)
        => _edgeCutController.CutEdgesIntersectingSegment(start, end);

    internal void StartEdgeCutting(Point point, IPointer pointer) =>
        StartInteraction(new EdgeCuttingInteractionSession(this, pointer, point));

    internal IReadOnlyList<EdgeModel> FindEdgesIntersectingGraphSegment(
        GraphPoint start,
        GraphPoint end)
        => _edgeCutController.FindEdgesIntersectingSegment(start, end);

    private bool IsEdgeCuttingCandidate(string edgeId) =>
        _interactionSession is EdgeCuttingInteractionSession cutting &&
        cutting.ContainsEdge(edgeId);
}
