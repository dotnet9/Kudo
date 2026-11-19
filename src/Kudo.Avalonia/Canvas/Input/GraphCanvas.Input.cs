using Avalonia;
using Avalonia.Input;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private static readonly IGraphDefaultInputProcessor[] DefaultInputProcessors =
    [
        ResizeAndConnectionInputProcessor.Instance,
        NodeInputProcessor.Instance,
        EdgeInputProcessor.Instance,
        AnnotationInputProcessor.Instance,
        GroupInputProcessor.Instance,
        PanInputProcessor.Instance,
        CanvasSelectionInputProcessor.Instance
    ];

    private IGraphInteractionSession? _interactionSession => _interactionCoordinator.ActiveSession;

    private bool IsPointerInteracting => _interactionCoordinator.IsActive;

    private GraphInputHitSnapshot HitTestInput(Point point) => new(
        HitTestSelectedNodeRotationHandle(point),
        HitTestSelectedNodeResizeHandle(point),
        HitTestSelectedAnnotationResizeHandle(point),
        HitTestSelectedEdgeWaypoint(point),
        HitTestSelectedEdgeEndpoint(point),
        HitTestPort(point),
        HitTestNode(point),
        HitTestEdgeLabel(point),
        HitTestEdge(point),
        HitTestAnnotation(point),
        HitTestGroup(point));

    private bool ProcessDefaultPointerPressed(
        PointerPressedEventArgs e,
        Point point,
        PointerPoint current)
    {
        var context = new GraphPointerPressedContext(
            this,
            e,
            point,
            current.Properties.IsLeftButtonPressed,
            current.Properties.IsMiddleButtonPressed,
            current.Properties.IsRightButtonPressed,
            HitTestInput(point));
        foreach (var processor in DefaultInputProcessors)
        {
            if (processor.TryProcess(context))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryStartRegisteredInteraction(
        PointerPressedEventArgs e,
        Point point,
        PointerPoint current)
    {
        if (InteractionSessionStarters.Count == 0)
        {
            return false;
        }

        var hits = HitTestInput(point);
        var edgeSegment = hits.Edge is { } edge
            ? HitTestOrthogonalEdgeSegment(edge, point)
            : null;
        var session = InteractionSessionStarters.TryStart(
            new GraphInteractionSessionStartContext(
                this,
                e.Pointer,
                e.Source,
                point,
                ScreenToGraph(point),
                e.KeyModifiers,
                e.ClickCount,
                new GraphInteractionHitTestResult(
                    hits.NodeRotationHandle,
                    hits.NodeResizeHandle,
                    hits.AnnotationResizeHandle,
                    hits.EdgeWaypoint,
                    hits.EdgeEndpoint,
                    edgeSegment,
                    hits.Node,
                    hits.EdgeLabel ?? hits.Edge,
                    hits.EdgeLabel is not null,
                    hits.Annotation,
                    hits.Group,
                    hits.Port)));
        if (session is null)
        {
            return false;
        }

        if (!ReferenceEquals(session.Pointer, e.Pointer))
        {
            throw new InvalidOperationException(
                "A custom interaction session must use the pointer from its start context.");
        }

        StartInteraction(session);
        return true;
    }

    internal bool IsPrimaryGesture(in GraphPointerPressedContext context) =>
        context.Matches(InputGestures.Selection.Replace);

    internal bool IsDragGesture(in GraphPointerPressedContext context) =>
        context.Matches(InputGestures.Drag);

    internal bool IsEditGesture(in GraphPointerPressedContext context) =>
        context.Matches(InputGestures.Edit);

    internal bool IsConnectGesture(in GraphPointerPressedContext context) =>
        context.Matches(InputGestures.Connect);

    internal bool IsExtendSelectionGesture(in GraphPointerPressedContext context) =>
        context.Matches(InputGestures.Selection.Extend);

    internal bool IsSubtractSelectionGesture(in GraphPointerPressedContext context) =>
        context.Matches(InputGestures.Selection.Subtract);

    internal bool IsNodePushingGesture(in GraphPointerPressedContext context) =>
        context.Matches(InputGestures.NodePushing);

    internal bool IsEdgeCuttingGesture(in GraphPointerPressedContext context) =>
        context.Matches(InputGestures.EdgeCutting);

    internal bool TryGetPanGesture(
        in GraphPointerPressedContext context,
        out GraphPointerGesture gesture)
    {
        foreach (var candidate in InputGestures.Pan)
        {
            if (context.Matches(candidate))
            {
                gesture = candidate;
                return true;
            }
        }

        gesture = GraphPointerGesture.None;
        return false;
    }

    private void StartInteraction(IGraphInteractionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (_interactionSession is not null)
        {
            CancelActivePointerInteraction();
        }

        ResetAutoPan();
        _interactionCoordinator.Start(session);
        _keyboardController.UpdatePointerCursor();
        InvalidateVisual();
    }

    private void UpdateActivePointerInteraction(Point point, KeyModifiers modifiers) =>
        _interactionSession?.Update(point, modifiers);

    private void CompleteActivePointerInteraction(Point point) =>
        _interactionSession?.Complete(point);

    private void CompleteActivePointerInteractionAfterCaptureLost() =>
        _interactionSession?.CaptureLost();

    private void EndPointerInteraction(IPointer? pointer)
    {
        ResetAutoPan();
        _interactionCoordinator.End(pointer);
        _activeAlignmentGuides = [];
        _keyboardController.UpdatePointerCursor();
        InvalidateVisual();
    }

    private bool CancelActivePointerInteraction()
    {
        var session = _interactionSession;
        if (session is null)
        {
            return false;
        }

        session.Cancel();
        EndPointerInteraction(session.Pointer);
        return true;
    }

    private PortHitResult? GetConnectionStartPort() =>
        _interactionSession switch
        {
            ConnectPortsInteractionSession connection => connection.StartPort,
            ManualConnectionSegmentInteractionSession manual => manual.Draft.StartPort,
            _ => _manualConnectionDraft?.StartPort
        };

    private PortHitResult? GetConnectionCandidatePort() => _interactionSession switch
    {
        ConnectPortsInteractionSession connection => connection.CandidatePort,
        ManualConnectionSegmentInteractionSession manual => manual.CandidatePort,
        ReconnectEdgeInteractionSession reconnection => reconnection.CandidatePort,
        _ => null
    };

    private void UpdateHoveredPort(Point? point)
    {
        var hit = point is { } value ? HitTestPort(value) : null;
        var hoveredNodeId = point is { } nodePoint ? HitTestNode(nodePoint)?.Id : null;
        if (IsSamePort(_hoveredPort, hit) &&
            string.Equals(_hoveredNodeId, hoveredNodeId, StringComparison.Ordinal))
        {
            return;
        }

        _hoveredPort = hit;
        _hoveredNodeId = hoveredNodeId;
        InvalidateVisual();
    }

    private static bool IsSamePort(PortHitResult? first, PortHitResult? second) =>
        first is null && second is null ||
        first is not null &&
        second is not null &&
        string.Equals(first.Reference.NodeId, second.Reference.NodeId, StringComparison.Ordinal) &&
        string.Equals(first.Reference.PortId, second.Reference.PortId, StringComparison.Ordinal);

    private bool IsOverviewCacheBlockedByInteraction() =>
        _interactionSession is NodeDragInteractionSession or
            NodePushingInteractionSession or
            NodeResizeInteractionSession or
            NodeRotationInteractionSession or
            EdgeWaypointInteractionSession or
            OrthogonalEdgeSegmentInteractionSession or
            ManualConnectionSegmentInteractionSession or
            ReconnectEdgeInteractionSession or
            EdgeCuttingInteractionSession;

    private bool PreventsDefaultInput(in GraphInputBehaviorContext context) =>
        InputBehaviors.PreventsDefault(context);

    internal static GraphPointerButton? GetPressedButton(PointerPoint point) =>
        point.Properties.IsLeftButtonPressed
            ? GraphPointerButton.Left
            : point.Properties.IsMiddleButtonPressed
                ? GraphPointerButton.Middle
                : point.Properties.IsRightButtonPressed
                    ? GraphPointerButton.Right
                    : null;
}
