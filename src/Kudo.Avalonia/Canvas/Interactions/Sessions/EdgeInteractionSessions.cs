using Avalonia;
using Avalonia.Input;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class EdgeWaypointInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    EdgeWaypointHitResult hit)
    : GraphInteractionSession(canvas, pointer, hit.ScreenPosition)
{
    private readonly GraphPoint _startPosition = hit.Waypoint;

    public override void Update(Point point, KeyModifiers modifiers)
    {
        if (hit.Index < 0 || hit.Index >= hit.Edge.Waypoints.Count)
        {
            return;
        }

        hit.Edge.Waypoints[hit.Index] = Canvas.ScreenToGraph(point);
        Canvas.InvalidateEdgeGeometry(hit.Edge);
        Canvas.InvalidateVisual();
    }

    public override void Complete(Point point)
    {
        if (hit.Index < 0 || hit.Index >= hit.Edge.Waypoints.Count)
        {
            return;
        }

        var newPosition = hit.Edge.Waypoints[hit.Index];
        if (newPosition == _startPosition)
        {
            return;
        }

        Canvas.CommitGraphEdit(new MoveEdgeWaypointCommand(
            hit.Edge.Id,
            hit.Index,
            _startPosition,
            newPosition,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.MoveEdgeWaypointCommand)));
    }

    public override void CaptureLost() => Cancel();

    public override void Cancel()
    {
        if (hit.Index < 0 || hit.Index >= hit.Edge.Waypoints.Count)
        {
            return;
        }

        hit.Edge.Waypoints[hit.Index] = _startPosition;
        Canvas.InvalidateEdgeGeometry(hit.Edge);
    }
}
internal sealed class OrthogonalEdgeSegmentInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint,
    EdgeSegmentHitResult hit)
    : GraphInteractionSession(canvas, pointer, startScreenPoint)
{
    private readonly GraphPoint[] _oldWaypoints = hit.Edge.Waypoints.ToArray();
    private readonly GraphPoint[] _routePoints = hit.RoutePoints.ToArray();

    public override void Update(Point point, KeyModifiers modifiers)
    {
        var delta = GetGraphDragDelta(point);
        var offset = hit.Orientation == OrthogonalEdgeSegmentOrientation.Horizontal
            ? delta.Y
            : delta.X;
        if (!double.IsFinite(offset))
        {
            return;
        }

        if (offset == 0)
        {
            ApplyWaypoints(_oldWaypoints);
            return;
        }

        ApplyWaypoints(CreateMovedWaypoints(offset));
    }

    public override void Complete(Point point)
    {
        var newWaypoints = hit.Edge.Waypoints.ToArray();
        if (_oldWaypoints.SequenceEqual(newWaypoints))
        {
            return;
        }

        Canvas.CommitGraphEdit(new SetEdgeWaypointsCommand(
            hit.Edge.Id,
            _oldWaypoints,
            newWaypoints,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.MoveEdgeSegmentCommand)));
    }

    public override void CaptureLost() => Cancel();

    public override void Cancel() => ApplyWaypoints(_oldWaypoints);

    private IReadOnlyList<GraphPoint> CreateMovedWaypoints(double offset)
    {
        var segmentStartIndex = hit.SegmentIndex;
        var segmentEndIndex = segmentStartIndex + 1;
        if (segmentStartIndex < 0 || segmentEndIndex >= _routePoints.Length)
        {
            return _oldWaypoints;
        }

        var points = new List<GraphPoint>(_routePoints.Length + 2);
        for (var index = 0; index < segmentStartIndex; index++)
        {
            AddPoint(points, _routePoints[index]);
        }

        if (segmentStartIndex == 0)
        {
            AddPoint(points, _routePoints[0]);
        }

        AddPoint(points, MovePerpendicular(_routePoints[segmentStartIndex], offset));
        AddPoint(points, MovePerpendicular(_routePoints[segmentEndIndex], offset));

        if (segmentEndIndex == _routePoints.Length - 1)
        {
            AddPoint(points, _routePoints[^1]);
        }

        for (var index = segmentEndIndex + 1; index < _routePoints.Length; index++)
        {
            AddPoint(points, _routePoints[index]);
        }

        return points.Count <= 2 ? [] : points.Skip(1).SkipLast(1).ToArray();
    }

    private GraphPoint MovePerpendicular(GraphPoint point, double offset) =>
        hit.Orientation == OrthogonalEdgeSegmentOrientation.Horizontal
            ? new GraphPoint(point.X, point.Y + offset)
            : new GraphPoint(point.X + offset, point.Y);

    private void ApplyWaypoints(IReadOnlyList<GraphPoint> waypoints)
    {
        if (hit.Edge.Waypoints.SequenceEqual(waypoints))
        {
            return;
        }

        hit.Edge.Waypoints = [.. waypoints];
        Canvas.InvalidateEdgeGeometry(hit.Edge);
        Canvas.InvalidateVisual();
    }

    private static void AddPoint(List<GraphPoint> points, GraphPoint point)
    {
        if (points.Count == 0 || points[^1] != point)
        {
            points.Add(point);
        }
    }
}
internal sealed class ConnectPortsInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint,
    PortHitResult startPort)
    : GraphInteractionSession(canvas, pointer, startScreenPoint)
{
    private GraphEdgePath? _previewPath;

    public PortHitResult StartPort { get; } = startPort;

    public PortHitResult? CandidatePort { get; private set; }

    public Point PreviewPoint { get; private set; } = startScreenPoint;

    public GraphEdgePath PreviewPath => _previewPath ??=
        Canvas.CreateConnectionPreviewPath(StartPort, PreviewPoint, CandidatePort);

    public override void Update(Point point, KeyModifiers modifiers)
    {
        var candidate = Canvas.FindCompatibleConnectionCandidate(point, StartPort);
        var previewPoint = candidate?.ScreenPosition ?? point;
        if (PreviewPoint == previewPoint &&
            CandidatePort?.Reference == candidate?.Reference)
        {
            return;
        }

        CandidatePort = candidate;
        PreviewPoint = previewPoint;
        _previewPath = null;
        Canvas.InvalidateVisual();
    }

    public override void Complete(Point point)
    {
        Update(point, KeyModifiers.None);
        Canvas.CompleteConnection(StartPort, point, PreviewPath);
    }

    public override void Cancel() => Canvas.LastConnectionValidation = null;
}
internal sealed class ManualConnectionSegmentInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint,
    ManualConnectionDraft draft,
    ManualConnectionDirection direction)
    : GraphInteractionSession(canvas, pointer, startScreenPoint)
{
    private GraphEdgePath? _previewPath;

    public ManualConnectionDraft Draft { get; } = draft;

    public ManualConnectionDirection Direction { get; } = direction;

    public GraphPoint SegmentDirection { get; } =
        ManualConnectionGeometry.GetDirection(draft.Heading, direction);

    public PortHitResult? CandidatePort { get; private set; }

    public GraphPoint SegmentPoint { get; private set; } = draft.Endpoint;

    public GraphEdgePath PreviewPath => _previewPath ??=
        Canvas.CreateManualConnectionSegmentPath(
            Draft,
            SegmentPoint,
            SegmentDirection,
            CandidatePort);

    public override void Update(Point point, KeyModifiers modifiers)
    {
        var segmentPoint = ManualConnectionGeometry.ProjectOntoRay(
            Draft.Endpoint,
            Canvas.ScreenToGraph(point),
            SegmentDirection);
        var candidate = Canvas.FindCompatibleConnectionCandidate(point, Draft.StartPort);
        var segmentStart = Canvas.GraphToScreen(Draft.Endpoint);
        var segmentEnd = Canvas.GraphToScreen(segmentPoint);
        candidate = candidate is not null &&
            GraphGeometryMath.DistanceSquared(segmentStart, segmentEnd) >=
            ManualConnectionGeometry.MinimumSegmentScreenLength *
            ManualConnectionGeometry.MinimumSegmentScreenLength
                ? candidate
                : null;
        if (SegmentPoint == segmentPoint &&
            CandidatePort?.Reference == candidate?.Reference)
        {
            return;
        }

        SegmentPoint = segmentPoint;
        CandidatePort = candidate;
        _previewPath = null;
        Canvas.InvalidateVisual();
    }

    public override void Complete(Point point)
    {
        Update(point, KeyModifiers.None);
        Canvas.CompleteManualConnectionSegment(this);
    }

    public override void CaptureLost() => Cancel();

    public override void Cancel()
    {
        CandidatePort = null;
        Canvas.LastConnectionValidation = null;
        Canvas.InvalidateVisual();
    }
}
internal sealed class ReconnectEdgeInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    EdgeEndpointHitResult hit)
    : GraphInteractionSession(canvas, pointer, hit.ScreenPosition)
{
    public EdgeModel Edge { get; } = hit.Edge;

    public GraphEdgeEndpoint Endpoint { get; } = hit.Endpoint;

    public PortHitResult? CandidatePort { get; private set; }

    public Point PreviewPoint { get; private set; } = hit.ScreenPosition;

    public override void Update(Point point, KeyModifiers modifiers)
    {
        CandidatePort = Canvas.FindCompatibleConnectionCandidate(point, Edge, Endpoint);
        PreviewPoint = CandidatePort?.ScreenPosition ?? point;
        Canvas.InvalidateVisual();
    }

    public override void Complete(Point point) => Canvas.CompleteEdgeReconnection(Edge, Endpoint, point);

    public override void Cancel() => Canvas.LastConnectionValidation = null;
}
internal sealed class EdgeCuttingInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint)
    : GraphInteractionSession(canvas, pointer, startScreenPoint)
{
    private readonly HashSet<string> _candidateEdgeIds = new(StringComparer.Ordinal);

    public GraphPoint StartGraphPoint { get; } = canvas.ScreenToGraph(startScreenPoint);

    public GraphPoint CurrentGraphPoint { get; private set; } = canvas.ScreenToGraph(startScreenPoint);

    public IReadOnlyList<EdgeModel> CandidateEdges { get; private set; } = [];

    public bool ContainsEdge(string edgeId) => _candidateEdgeIds.Contains(edgeId);

    public override void Update(Point point, KeyModifiers modifiers)
    {
        CurrentGraphPoint = Canvas.ScreenToGraph(point);
        CandidateEdges = Canvas.FindEdgesIntersectingGraphSegment(
            StartGraphPoint,
            CurrentGraphPoint);
        _candidateEdgeIds.Clear();
        _candidateEdgeIds.UnionWith(CandidateEdges.Select(edge => edge.Id));
        Canvas.InvalidateVisual();
    }

    public override void Complete(Point point)
    {
        CurrentGraphPoint = Canvas.ScreenToGraph(point);
        Canvas.CutEdgesIntersectingSegment(StartGraphPoint, CurrentGraphPoint);
    }

    public override void Cancel()
    {
        CandidateEdges = [];
        _candidateEdgeIds.Clear();
        Canvas.InvalidateVisual();
    }
}
