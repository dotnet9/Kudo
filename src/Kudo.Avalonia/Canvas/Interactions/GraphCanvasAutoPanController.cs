using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasAutoPanController
{
    private static readonly TimeSpan TimerInterval =
        TimeSpan.FromMilliseconds(1000.0 / 60);

    private readonly GraphCanvasAutoPanContext _context;
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render)
    {
        Interval = TimerInterval
    };
    private Point _pointerPoint;
    private KeyModifiers _keyModifiers;
    private bool _hasPointerSample;
    private long _lastTimestamp;

    public GraphCanvasAutoPanController(GraphCanvasAutoPanContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timer.Tick += HandleTick;
    }

    public bool IsActive => _timer.IsEnabled;

    public void UpdatePointer(Point point, KeyModifiers modifiers)
    {
        _pointerPoint = point;
        _keyModifiers = modifiers;
        _hasPointerSample = true;
        UpdateState();
    }

    public void UpdateState()
    {
        if (!CanAutoPan() || GetDirection(_pointerPoint) == default)
        {
            Stop();
            return;
        }

        if (_timer.IsEnabled)
        {
            return;
        }

        Advance(_timer.Interval.TotalSeconds);
        if (CanAutoPan() && GetDirection(_pointerPoint) != default)
        {
            _lastTimestamp = Stopwatch.GetTimestamp();
            _timer.Start();
        }
    }

    public void Reset()
    {
        _hasPointerSample = false;
        Stop();
    }

    private bool CanAutoPan()
    {
        var bounds = _context.Bounds();
        var edgeDistance = _context.EdgeDistance();
        var speed = _context.Speed();
        return _context.IsEnabled() &&
            _hasPointerSample &&
            _context.InteractionSession()?.SupportsAutoPan == true &&
            bounds.Width > 0 &&
            bounds.Height > 0 &&
            double.IsFinite(edgeDistance) &&
            edgeDistance > 0 &&
            double.IsFinite(speed) &&
            speed > 0;
    }

    private GraphPoint GetDirection(Point point)
    {
        var bounds = _context.Bounds();
        var edgeDistance = _context.EdgeDistance();
        var startPoint = _context.InteractionSession()?.StartPointerPoint ?? point;
        var x = 0;
        if (bounds.Width > edgeDistance * 2)
        {
            if (point.X <= edgeDistance &&
                (startPoint.X > edgeDistance || point.X < startPoint.X))
            {
                x = 1;
            }
            else if (point.X >= bounds.Width - edgeDistance &&
                     (startPoint.X < bounds.Width - edgeDistance || point.X > startPoint.X))
            {
                x = -1;
            }
        }

        var y = 0;
        if (bounds.Height > edgeDistance * 2)
        {
            if (point.Y <= edgeDistance &&
                (startPoint.Y > edgeDistance || point.Y < startPoint.Y))
            {
                y = 1;
            }
            else if (point.Y >= bounds.Height - edgeDistance &&
                     (startPoint.Y < bounds.Height - edgeDistance || point.Y > startPoint.Y))
            {
                y = -1;
            }
        }

        return new GraphPoint(x, y);
    }

    private void HandleTick(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsedSeconds = _lastTimestamp == 0
            ? _timer.Interval.TotalSeconds
            : Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
        _lastTimestamp = now;
        if (!Advance(Math.Clamp(elapsedSeconds, 0, 0.05)))
        {
            Stop();
        }
    }

    private bool Advance(double elapsedSeconds)
    {
        if (!CanAutoPan())
        {
            return false;
        }

        var direction = GetDirection(_pointerPoint);
        if (direction == default)
        {
            return false;
        }

        var distance = _context.Speed() * elapsedSeconds;
        if (!double.IsFinite(distance) || distance <= 0)
        {
            return false;
        }

        _context.PanBy(new Vector(direction.X * distance, direction.Y * distance));
        _context.InteractionSession()?.Update(_pointerPoint, _keyModifiers);
        return true;
    }

    private void Stop()
    {
        _timer.Stop();
        _lastTimestamp = 0;
    }
}

internal sealed class GraphCanvasAutoPanContext
{
    public required Func<bool> IsEnabled { get; init; }

    public required Func<double> EdgeDistance { get; init; }

    public required Func<double> Speed { get; init; }

    public required Func<Rect> Bounds { get; init; }

    public required Func<IGraphInteractionSession?> InteractionSession { get; init; }

    public required Action<Vector> PanBy { get; init; }
}
