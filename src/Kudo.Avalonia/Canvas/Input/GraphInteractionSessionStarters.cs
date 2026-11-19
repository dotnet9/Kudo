using Avalonia;
using Avalonia.Input;
using Kudo;

namespace Kudo.Avalonia;

public sealed record GraphInteractionHitTestResult(
    NodeRotationHandleHitResult? NodeRotationHandle,
    NodeResizeHandleHitResult? NodeResizeHandle,
    AnnotationResizeHandleHitResult? AnnotationResizeHandle,
    EdgeWaypointHitResult? EdgeWaypoint,
    EdgeEndpointHitResult? EdgeEndpoint,
    EdgeSegmentHitResult? EdgeSegment,
    NodeModel? Node,
    EdgeModel? Edge,
    bool IsEdgeLabel,
    AnnotationModel? Annotation,
    GroupModel? Group,
    PortHitResult? Port);

public readonly record struct GraphInteractionSessionStartContext(
    GraphCanvas Canvas,
    IPointer Pointer,
    object? Source,
    Point ScreenPosition,
    GraphPoint GraphPosition,
    KeyModifiers KeyModifiers,
    int ClickCount,
    GraphInteractionHitTestResult HitTest);

/// <summary>
/// Starts a host-defined pointer interaction before GraphCanvas handles its built-in interaction.
/// Returning <see langword="null"/> leaves the event for later starters and built-in handling.
/// </summary>
public interface IGraphInteractionSessionStarter
{
    IGraphInteractionSession? TryStart(in GraphInteractionSessionStartContext context);
}

public sealed class GraphInteractionSessionStarterCollection
{
    private readonly List<Registration> _registrations = [];
    private Registration[] _orderedRegistrations = [];
    private long _nextOrder;

    public int Count => _registrations.Count;

    public IReadOnlyList<IGraphInteractionSessionStarter> Starters =>
        _orderedRegistrations
            .Select(registration => registration.Starter)
            .ToArray();

    public IDisposable Register(IGraphInteractionSessionStarter starter, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(starter);
        var registration = new Registration(starter, priority, _nextOrder++);
        _registrations.Add(registration);
        RebuildOrder();
        return new Subscription(this, registration);
    }

    public bool Unregister(IGraphInteractionSessionStarter starter)
    {
        ArgumentNullException.ThrowIfNull(starter);
        var registration = _registrations.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.Starter, starter));
        return registration is not null && Remove(registration);
    }

    public void Clear()
    {
        _registrations.Clear();
        _orderedRegistrations = [];
    }

    internal IGraphInteractionSession? TryStart(in GraphInteractionSessionStartContext context)
    {
        foreach (var registration in _orderedRegistrations)
        {
            var session = registration.Starter.TryStart(context);
            if (session is not null)
            {
                return session;
            }
        }

        return null;
    }

    private bool Remove(Registration registration)
    {
        if (!_registrations.Remove(registration))
        {
            return false;
        }

        RebuildOrder();
        return true;
    }

    private void RebuildOrder() =>
        _orderedRegistrations = _registrations
            .OrderByDescending(registration => registration.Priority)
            .ThenBy(registration => registration.Order)
            .ToArray();

    private sealed record Registration(
        IGraphInteractionSessionStarter Starter,
        int Priority,
        long Order);

    private sealed class Subscription(
        GraphInteractionSessionStarterCollection owner,
        Registration registration) : IDisposable
    {
        private GraphInteractionSessionStarterCollection? _owner = owner;

        public void Dispose()
        {
            var currentOwner = Interlocked.Exchange(ref _owner, null);
            currentOwner?.Remove(registration);
        }
    }
}
