// 周期刷新调度器：隔离 Timer、同步上下文和释放生命周期，避免会话直接持有计时细节。
namespace Kudo.Core.Runtime;

public sealed class PeriodicGraphRefreshScheduler : IGraphRefreshScheduler, IDisposable
{
    private static readonly TimeSpan MaximumInterval = TimeSpan.FromHours(1);
    private readonly object _gate = new();
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly Timer _timer;
    private int _isRunning;
    private int _isTicking;
    private bool _isDisposed;

    public PeriodicGraphRefreshScheduler(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero || interval > MaximumInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                interval,
                $"Refresh interval must be greater than zero and no longer than {MaximumInterval}.");
        }

        Interval = interval;
        _synchronizationContext = SynchronizationContext.Current;
        _timer = new Timer(
            static state => ((PeriodicGraphRefreshScheduler)state!).HandleTimerTick(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    public TimeSpan Interval { get; }

    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

    public event EventHandler? Tick;

    public event EventHandler<Exception>? TickFailed;

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            Volatile.Write(ref _isRunning, 1);
            _timer.Change(Interval, Interval);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            Volatile.Write(ref _isRunning, 0);
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Volatile.Write(ref _isRunning, 0);
            _timer.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void HandleTimerTick()
    {
        if (!IsRunning || Interlocked.Exchange(ref _isTicking, 1) != 0)
        {
            return;
        }

        try
        {
            if (_synchronizationContext is not null)
            {
                _synchronizationContext.Post(
                    static state => ((PeriodicGraphRefreshScheduler)state!).RaiseTick(),
                    this);
                return;
            }

            RaiseTick();
        }
        catch (Exception exception)
        {
            Volatile.Write(ref _isTicking, 0);
            ReportTickFailure(exception);
        }
    }

    private void RaiseTick()
    {
        try
        {
            if (IsRunning)
            {
                Tick?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception exception)
        {
            ReportTickFailure(exception);
        }
        finally
        {
            Volatile.Write(ref _isTicking, 0);
        }
    }

    private void ReportTickFailure(Exception exception)
    {
        try
        {
            TickFailed?.Invoke(this, exception);
        }
        catch
        {
            // 调度器观察者失败不能终止计时线程或宿主进程。
        }
    }
}
