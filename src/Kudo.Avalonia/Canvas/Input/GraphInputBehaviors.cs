using System.ComponentModel;
using Avalonia;
using Avalonia.Input;

namespace Kudo.Avalonia;

public enum GraphInputEventKind
{
    [Description("指针按下")]
    PointerPressed,
    [Description("指针移动")]
    PointerMoved,
    [Description("指针释放")]
    PointerReleased,
    [Description("指针离开")]
    PointerExited,
    [Description("指针滚轮变化")]
    PointerWheelChanged,
    [Description("按键按下")]
    KeyDown,
    [Description("按键释放")]
    KeyUp
}

public enum GraphInputBehaviorDecision
{
    [Description("继续默认处理")]
    Continue,
    [Description("阻止默认处理")]
    PreventDefault
}

public readonly record struct GraphInputBehaviorContext(
    GraphCanvas Canvas,
    GraphInputEventKind EventKind,
    object? Source,
    Point Position,
    KeyModifiers KeyModifiers,
    PointerUpdateKind PointerUpdateKind = PointerUpdateKind.Other,
    Vector WheelDelta = default,
    Key Key = Key.None,
    int ClickCount = 0);

public interface IGraphInputBehavior
{
    GraphInputBehaviorDecision Evaluate(in GraphInputBehaviorContext context);
}

public sealed class GraphInputBehaviorCollection
{
    private readonly List<Registration> _registrations = [];
    private Registration[] _orderedRegistrations = [];
    private long _nextOrder;

    public int Count => _registrations.Count;

    public IReadOnlyList<IGraphInputBehavior> Behaviors =>
        _orderedRegistrations
            .Select(registration => registration.Behavior)
            .ToArray();

    public IDisposable Register(IGraphInputBehavior behavior, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        var registration = new Registration(behavior, priority, _nextOrder++);
        _registrations.Add(registration);
        RebuildOrder();
        return new Subscription(this, registration);
    }

    public bool Unregister(IGraphInputBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        var registration = _registrations.FirstOrDefault(candidate => ReferenceEquals(candidate.Behavior, behavior));
        return registration is not null && Remove(registration);
    }

    public void Clear()
    {
        _registrations.Clear();
        _orderedRegistrations = [];
    }

    internal bool PreventsDefault(in GraphInputBehaviorContext context)
    {
        foreach (var registration in _orderedRegistrations)
        {
            if (registration.Behavior.Evaluate(context) == GraphInputBehaviorDecision.PreventDefault)
            {
                return true;
            }
        }

        return false;
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

    private sealed record Registration(IGraphInputBehavior Behavior, int Priority, long Order);

    private sealed class Subscription(GraphInputBehaviorCollection owner, Registration registration) : IDisposable
    {
        private GraphInputBehaviorCollection? _owner = owner;

        public void Dispose()
        {
            var currentOwner = Interlocked.Exchange(ref _owner, null);
            currentOwner?.Remove(registration);
        }
    }
}
