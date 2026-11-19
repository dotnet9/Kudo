using Avalonia;
using Avalonia.Input;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private const double DefaultManualConnectionStartLength = 36;

    internal GraphEdgePath CreateConnectionPreviewPath(
        PortHitResult startPort,
        Point previewPoint,
        PortHitResult? candidatePort)
    {
        var start = GetPortGraphPosition(startPort.Node, startPort.Port);
        var end = candidatePort is { } candidate
            ? GetPortGraphPosition(candidate.Node, candidate.Port)
            : ScreenToGraph(previewPoint);
        var routingStyle = candidatePort is null
            ? EdgeRoutingStyle.Orthogonal
            : NewEdgeRoutingStyle;
        return EdgeRoutingService.Route(new GraphEdgeRoutingRequest(
            routingStyle,
            start,
            end)
        {
            Options = EdgeRoutingOptions,
            StartDirection = ResolveManualConnectionStartDirection(startPort),
            EndDirection = candidatePort is { } target
                ? ResolveManualConnectionEndDirection(startPort, target)
                : null
        });
    }

    private void BeginManualConnectionDraft(
        PortHitResult startPort,
        Point releasePoint,
        GraphEdgePath previewPath)
    {
        if (Document is null || IsReadOnly)
        {
            return;
        }

        var start = GetPortGraphPosition(startPort.Node, startPort.Port);
        var endpoint = ScreenToGraph(releasePoint);
        var fallback = ResolveManualConnectionStartDirection(startPort);
        var startScreenPoint = GraphToScreen(start);
        var isShortRelease = GraphGeometryMath.DistanceSquared(
                startScreenPoint,
                releasePoint) <
            ManualConnectionGeometry.MinimumSegmentScreenLength *
            ManualConnectionGeometry.MinimumSegmentScreenLength;
        var heading = isShortRelease
            ? ManualConnectionGeometry.Normalize(fallback)
            : ManualConnectionGeometry.ResolveHeading(start, endpoint, fallback);
        if (isShortRelease)
        {
            var distance = DefaultManualConnectionStartLength / Math.Max(Viewport.Zoom, MinimumLodZoom);
            endpoint = new GraphPoint(
                start.X + heading.X * distance,
                start.Y + heading.Y * distance);
            previewPath = CreateConnectionPreviewPath(
                startPort,
                GraphToScreen(endpoint),
                candidatePort: null);
        }

        _manualConnectionDraft = new ManualConnectionDraft(
            startPort,
            previewPath.Points,
            heading);
        UpdateManualConnectionDraftHover(releasePoint);
        InvalidateVisual();
    }

    private GraphPoint ResolveManualConnectionStartDirection(PortHitResult startPort)
    {
        var outgoing = PortGeometry.ToCanvasDirection(
            startPort.Node,
            startPort.Port.OutgoingDirection);
        if (outgoing is { } outgoingDirection)
        {
            return outgoingDirection;
        }

        var incoming = PortGeometry.ToCanvasDirection(
            startPort.Node,
            startPort.Port.IncomingDirection);
        if (incoming is { } incomingDirection)
        {
            return new GraphPoint(-incomingDirection.X, -incomingDirection.Y);
        }

        var center = GraphNodeGeometry.GetCenter(startPort.Node);
        var portPosition = GetPortGraphPosition(startPort.Node, startPort.Port);
        return ManualConnectionGeometry.ResolveHeading(
            center,
            portPosition,
            new GraphPoint(1, 0));
    }

    private static GraphPoint? ResolveManualConnectionEndDirection(
        PortHitResult startPort,
        PortHitResult endPort)
    {
        var (_, target) = NormalizeConnection(startPort, endPort);
        var direction = target == endPort.Reference
            ? endPort.Port.IncomingDirection
            : endPort.Port.OutgoingDirection;
        return PortGeometry.ToCanvasDirection(endPort.Node, direction);
    }

    private bool TryStartManualConnectionSegment(Point point, IPointer pointer)
    {
        if (IsReadOnly || _manualConnectionDraft is not { } draft)
        {
            return false;
        }

        var endpoint = GraphToScreen(draft.Endpoint);
        var direction = ManualConnectionGeometry.HitTestDirectionHandle(
            point,
            endpoint,
            draft.Heading);
        if (direction is null)
        {
            return false;
        }

        draft.AreDirectionHandlesVisible = false;
        draft.HoveredDirection = null;
        LastConnectionValidation = null;
        StartInteraction(new ManualConnectionSegmentInteractionSession(
            this,
            pointer,
            point,
            draft,
            direction.Value));
        return true;
    }

    internal void CompleteManualConnectionSegment(
        ManualConnectionSegmentInteractionSession session)
    {
        var draft = session.Draft;
        if (!ReferenceEquals(_manualConnectionDraft, draft))
        {
            return;
        }

        if (session.CandidatePort is { } candidate)
        {
            var waypoints = session.PreviewPath.Points
                .Skip(1)
                .SkipLast(1)
                .ToArray();
            if (TryCompleteConnection(
                    draft.StartPort,
                    candidate.ScreenPosition,
                    waypoints))
            {
                CancelManualConnectionDraft();
                return;
            }
        }

        var start = GraphToScreen(draft.Endpoint);
        var end = GraphToScreen(session.SegmentPoint);
        if (GraphGeometryMath.DistanceSquared(start, end) <
            ManualConnectionGeometry.MinimumSegmentScreenLength *
            ManualConnectionGeometry.MinimumSegmentScreenLength)
        {
            return;
        }

        draft.AddEndpoint(session.SegmentPoint, session.SegmentDirection);
        LastConnectionValidation = null;
        InvalidateVisual();
    }

    internal GraphEdgePath CreateManualConnectionSegmentPath(
        ManualConnectionDraft draft,
        GraphPoint segmentPoint,
        GraphPoint segmentDirection,
        PortHitResult? candidatePort)
    {
        var start = GetPortGraphPosition(draft.StartPort.Node, draft.StartPort.Port);
        var prefix = new GraphPoint[draft.Waypoints.Count + 2];
        prefix[0] = start;
        for (var index = 0; index < draft.Waypoints.Count; index++)
        {
            prefix[index + 1] = draft.Waypoints[index];
        }

        prefix[^1] = segmentPoint;
        if (candidatePort is not { } candidate)
        {
            return new GraphEdgePath(
                EdgeRoutingStyle.Manual,
                ManualConnectionGeometry.CreateOrthogonalPath(prefix, segmentDirection));
        }

        var target = GetPortGraphPosition(candidate.Node, candidate.Port);
        var targetDirection = ResolveManualConnectionEndDirection(
            draft.StartPort,
            candidate);
        return new GraphEdgePath(
            EdgeRoutingStyle.Manual,
            ManualConnectionGeometry.CreateOrthogonalCompletionPath(
                prefix,
                target,
                segmentDirection,
                targetDirection,
                EdgeRoutingOptions.PortDirectionLeadLength));
    }

    private void UpdateManualConnectionDraftHover(Point? point)
    {
        if (_manualConnectionDraft is not { } draft)
        {
            return;
        }

        if (point is not { } pointer ||
            _interactionSession is ManualConnectionSegmentInteractionSession)
        {
            draft.AreDirectionHandlesVisible = false;
            draft.HoveredDirection = null;
            InvalidateVisual();
            return;
        }

        var endpoint = GraphToScreen(draft.Endpoint);
        var hoveredDirection = ManualConnectionGeometry.HitTestDirectionHandle(
            pointer,
            endpoint,
            draft.Heading);
        var isVisible = hoveredDirection is not null ||
            ManualConnectionGeometry.IsNearEndpoint(pointer, endpoint);
        if (draft.AreDirectionHandlesVisible == isVisible &&
            draft.HoveredDirection == hoveredDirection)
        {
            return;
        }

        draft.AreDirectionHandlesVisible = isVisible;
        draft.HoveredDirection = hoveredDirection;
        InvalidateVisual();
    }

    private bool CancelManualConnectionDraft()
    {
        if (_manualConnectionDraft is null)
        {
            return false;
        }

        _manualConnectionDraft = null;
        LastConnectionValidation = null;
        InvalidateVisual();
        return true;
    }
}
