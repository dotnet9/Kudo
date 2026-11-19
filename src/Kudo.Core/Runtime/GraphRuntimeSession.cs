// 运行时会话门面：协调生命周期命令、快照、断点、监视表达式和诊断发布。
using Kudo;

namespace Kudo.Core.Runtime;

public sealed class GraphRuntimeSession : IGraphRuntimeSession, IDisposable
{
    public const int DefaultDiagnosticCapacity = 200;

    public const int MaximumDiagnosticCapacity = 10_000;

    private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMilliseconds(500);
    private readonly object _gate = new();
    private readonly IGraphRuntimeStateProvider _stateProvider;
    private readonly IGraphRefreshScheduler _scheduler;
    private readonly bool _ownsScheduler;
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private readonly SemaphoreSlim _providerGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly HashSet<string> _breakpointNodeIds = new(StringComparer.Ordinal);
    private readonly List<string> _watchExpressions = [];
    private readonly GraphRuntimeDiagnosticBuffer _diagnostics;
    private CancellationTokenSource? _activeCommandCancellation;
    private GraphRuntimeSnapshot _currentSnapshot = new();
    private RuntimeStatus _status = RuntimeStatus.Stopped;
    private long _tickCount;
    private Exception? _lastError;
    private int _scheduledRefreshInProgress;
    private bool _isDisposing;
    private bool _isDisposed;

    public GraphRuntimeSession(
        GraphDocument document,
        IGraphRuntimeStateProvider stateProvider)
        : this(document, stateProvider, DefaultRefreshInterval, DefaultDiagnosticCapacity)
    {
    }

    public GraphRuntimeSession(
        GraphDocument document,
        IGraphRuntimeStateProvider stateProvider,
        TimeSpan refreshInterval)
        : this(document, stateProvider, refreshInterval, DefaultDiagnosticCapacity)
    {
    }

    public GraphRuntimeSession(
        GraphDocument document,
        IGraphRuntimeStateProvider stateProvider,
        int diagnosticCapacity)
        : this(document, stateProvider, DefaultRefreshInterval, diagnosticCapacity)
    {
    }

    public GraphRuntimeSession(
        GraphDocument document,
        IGraphRuntimeStateProvider stateProvider,
        TimeSpan refreshInterval,
        int diagnosticCapacity)
        : this(
            document,
            stateProvider,
            CreateOwnedScheduler(refreshInterval, diagnosticCapacity),
            ownsScheduler: true,
            diagnosticCapacity: diagnosticCapacity)
    {
    }

    public GraphRuntimeSession(
        GraphDocument document,
        IGraphRuntimeStateProvider stateProvider,
        IGraphRefreshScheduler scheduler)
        : this(document, stateProvider, scheduler, false, DefaultDiagnosticCapacity)
    {
    }

    public GraphRuntimeSession(
        GraphDocument document,
        IGraphRuntimeStateProvider stateProvider,
        IGraphRefreshScheduler scheduler,
        int diagnosticCapacity)
        : this(document, stateProvider, scheduler, false, diagnosticCapacity)
    {
    }

    public GraphRuntimeSession(
        GraphDocument document,
        IGraphRuntimeStateProvider stateProvider,
        IGraphRefreshScheduler scheduler,
        bool ownsScheduler,
        int diagnosticCapacity)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        DiagnosticCapacity = ValidateDiagnosticCapacity(diagnosticCapacity);
        _diagnostics = new GraphRuntimeDiagnosticBuffer(DiagnosticCapacity);
        _ownsScheduler = ownsScheduler;
        _scheduler.Tick += HandleSchedulerTick;
    }

    public GraphDocument Document { get; }

    public int DiagnosticCapacity { get; }

    public RuntimeStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public GraphRuntimeSnapshot CurrentSnapshot
    {
        get
        {
            lock (_gate)
            {
                return _currentSnapshot;
            }
        }
    }

    public long TickCount
    {
        get
        {
            lock (_gate)
            {
                return _tickCount;
            }
        }
    }

    public IReadOnlyCollection<string> BreakpointNodeIds
    {
        get
        {
            lock (_gate)
            {
                return Document.Nodes
                    .Where(node => _breakpointNodeIds.Contains(node.Id))
                    .Select(node => node.Id)
                    .ToArray();
            }
        }
    }

    public IReadOnlyList<string> WatchExpressions
    {
        get
        {
            lock (_gate)
            {
                return _watchExpressions.ToArray();
            }
        }
    }

    public IReadOnlyList<GraphRuntimeDiagnostic> RecentDiagnostics
    {
        get
        {
            lock (_gate)
            {
                return _diagnostics.Snapshot();
            }
        }
    }

    public Exception? LastError
    {
        get
        {
            lock (_gate)
            {
                return _lastError;
            }
        }
    }

    public event EventHandler<GraphRuntimeSnapshot>? SnapshotChanged;

    public event EventHandler<GraphRuntimeBreakpointHitEventArgs>? BreakpointHit;

    public event EventHandler? BreakpointsChanged;

    public event EventHandler? WatchesChanged;

    public event EventHandler<GraphRuntimeDiagnosticEventArgs>? DiagnosticEmitted;

    public event EventHandler? DiagnosticsChanged;

    public bool AddBreakpoint(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        bool changed;
        lock (_gate)
        {
            ThrowIfDisposed();
            changed = Document.FindNode(nodeId) is not null && _breakpointNodeIds.Add(nodeId);
        }

        if (changed)
        {
            BreakpointsChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    public bool RemoveBreakpoint(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        bool changed;
        lock (_gate)
        {
            ThrowIfDisposed();
            changed = _breakpointNodeIds.Remove(nodeId);
        }

        if (changed)
        {
            BreakpointsChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    public void ClearBreakpoints()
    {
        bool changed;
        lock (_gate)
        {
            ThrowIfDisposed();
            changed = _breakpointNodeIds.Count > 0;
            _breakpointNodeIds.Clear();
        }

        if (changed)
        {
            BreakpointsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async ValueTask<bool> AddWatchAsync(
        string expression,
        CancellationToken cancellationToken = default)
    {
        var normalizedExpression = expression?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedExpression))
        {
            return false;
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            if (_watchExpressions.Contains(normalizedExpression, StringComparer.Ordinal))
            {
                return false;
            }

            _watchExpressions.Add(normalizedExpression);
        }

        try
        {
            await RefreshWatchResultsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
            {
                _watchExpressions.Remove(normalizedExpression);
            }

            throw;
        }

        WatchesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool RemoveWatch(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        var normalizedExpression = expression.Trim();
        bool changed;
        lock (_gate)
        {
            ThrowIfDisposed();
            changed = _watchExpressions.Remove(normalizedExpression);
            if (changed)
            {
                _currentSnapshot = GraphRuntimeSnapshotFactory.Copy(
                    _currentSnapshot,
                    _currentSnapshot.WatchResults
                        .Where(result => !string.Equals(
                            result.Expression,
                            normalizedExpression,
                            StringComparison.Ordinal))
                        .ToArray());
            }
        }

        if (changed)
        {
            WatchesChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    public void ClearWatches()
    {
        bool changed;
        lock (_gate)
        {
            ThrowIfDisposed();
            changed = _watchExpressions.Count > 0;
            _watchExpressions.Clear();
            if (changed)
            {
                _currentSnapshot = GraphRuntimeSnapshotFactory.Copy(_currentSnapshot, []);
            }
        }

        if (changed)
        {
            WatchesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearDiagnostics()
    {
        bool changed;
        lock (_gate)
        {
            ThrowIfDisposed();
            changed = _diagnostics.Count > 0;
            _diagnostics.Clear();
        }

        if (changed)
        {
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default) =>
        ExecuteSerializedCommandAsync(StartCoreAsync, cancellationToken);

    public async ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancelActiveCommand();
        await ExecuteSerializedCommandAsync(PauseCoreAsync, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask StepAsync(CancellationToken cancellationToken = default) =>
        ExecuteSerializedCommandAsync(StepCoreAsync, cancellationToken);

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancelActiveCommand();
        await ExecuteSerializedCommandAsync(StopCoreAsync, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask ResetAsync(CancellationToken cancellationToken = default) =>
        ExecuteSerializedCommandAsync(ResetCoreAsync, cancellationToken);

    private async ValueTask StartCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GraphRuntimeSnapshot startingSnapshot;
        GraphRuntimeSnapshot previousSnapshot;
        RuntimeStatus previousStatus;
        Exception? previousError;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_status is RuntimeStatus.Starting or RuntimeStatus.Running or RuntimeStatus.Stopping)
            {
                return;
            }

            previousSnapshot = _currentSnapshot;
            previousStatus = _status;
            previousError = _lastError;
            _lastError = null;
            _status = RuntimeStatus.Starting;
            startingSnapshot = GraphRuntimeSnapshotFactory.CreateTransition(_status, _currentSnapshot);
            _currentSnapshot = startingSnapshot;
        }

        Publish(startingSnapshot);
        lock (_gate)
        {
            if (_isDisposed || _isDisposing || _status != RuntimeStatus.Starting)
            {
                return;
            }

            _status = RuntimeStatus.Running;
        }

        try
        {
            await RefreshCoreAsync(throwIfDisposed: true, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            GraphRuntimeSnapshot? restoredSnapshot = null;
            lock (_gate)
            {
                if (!_isDisposed && !_isDisposing && _status == RuntimeStatus.Running)
                {
                    _status = previousStatus;
                    _lastError = previousError;
                    _currentSnapshot = previousSnapshot;
                    restoredSnapshot = previousSnapshot;
                }
            }

            if (restoredSnapshot is not null)
            {
                Publish(restoredSnapshot);
            }

            throw;
        }

        if (Status == RuntimeStatus.Running && !IsDisposing)
        {
            try
            {
                _scheduler.Start();
            }
            catch (Exception exception)
            {
                TransitionToFaulted(exception);
            }
        }

    }

    private ValueTask PauseCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GraphRuntimeSnapshot? snapshot = null;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_status == RuntimeStatus.Running)
            {
                _status = RuntimeStatus.Paused;
                snapshot = GraphRuntimeSnapshotFactory.CreateTransition(_status, _currentSnapshot);
                _currentSnapshot = snapshot;
            }
        }

        if (snapshot is not null)
        {
            _scheduler.Stop();
            Publish(snapshot);
            EmitDiagnostic(
                snapshot,
                GraphRuntimeDiagnosticCodes.Paused,
                GraphRuntimeDiagnosticSeverity.Information,
                "Runtime paused.");
        }

        return ValueTask.CompletedTask;
    }

    private async ValueTask StepCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_status is RuntimeStatus.Running or RuntimeStatus.Starting or RuntimeStatus.Stopping)
            {
                throw new InvalidOperationException("Runtime must be stopped, paused, or faulted before stepping.");
            }
        }

        _scheduler.Stop();
        GraphRuntimeSnapshot providedSnapshot;
        try
        {
            providedSnapshot = await GetProviderSnapshotAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            TransitionToFaulted(exception);
            return;
        }

        GraphRuntimeSnapshot snapshot;
        lock (_gate)
        {
            if (_isDisposed || _isDisposing)
            {
                return;
            }

            _lastError = null;
            _status = RuntimeStatus.Paused;
            _tickCount++;
            snapshot = GraphRuntimeSnapshotFactory.Normalize(providedSnapshot, _status, _tickCount);
            _currentSnapshot = snapshot;
        }

        Publish(snapshot);
        EmitDiagnostic(
            snapshot,
            GraphRuntimeDiagnosticCodes.Stepped,
            GraphRuntimeDiagnosticSeverity.Information,
            snapshot.Message ?? "Runtime stepped.",
            snapshot.ActiveNodeId);
    }

    private ValueTask StopCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GraphRuntimeSnapshot? stoppingSnapshot = null;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_status != RuntimeStatus.Stopped)
            {
                _status = RuntimeStatus.Stopping;
                _lastError = null;
                stoppingSnapshot = GraphRuntimeSnapshotFactory.CreateTransition(_status, _currentSnapshot);
                _currentSnapshot = stoppingSnapshot;
            }
        }

        if (stoppingSnapshot is null)
        {
            return ValueTask.CompletedTask;
        }

        _scheduler.Stop();
        Publish(stoppingSnapshot);
        GraphRuntimeSnapshot stoppedSnapshot;
        lock (_gate)
        {
            if (_isDisposed || _status != RuntimeStatus.Stopping)
            {
                return ValueTask.CompletedTask;
            }

            _status = RuntimeStatus.Stopped;
            _tickCount = 0;
            stoppedSnapshot = new GraphRuntimeSnapshot
            {
                Status = RuntimeStatus.Stopped,
                Message = "Runtime stopped."
            };
            _currentSnapshot = stoppedSnapshot;
        }

        Publish(stoppedSnapshot);
        EmitDiagnostic(
            stoppedSnapshot,
            GraphRuntimeDiagnosticCodes.Stopped,
            GraphRuntimeDiagnosticSeverity.Information,
            "Runtime stopped.");
        return ValueTask.CompletedTask;
    }

    private async ValueTask ResetCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ThrowIfDisposed();
        }

        _scheduler.Stop();
        if (_stateProvider is IGraphRuntimeResettableStateProvider resettableStateProvider)
        {
            try
            {
                await _providerGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await resettableStateProvider
                        .ResetAsync(Document, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _providerGate.Release();
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (Status == RuntimeStatus.Running)
                {
                    _scheduler.Start();
                }

                throw;
            }
            catch (Exception exception)
            {
                TransitionToFaulted(exception);
                return;
            }
        }

        GraphRuntimeSnapshot snapshot;
        lock (_gate)
        {
            if (_isDisposed || _isDisposing)
            {
                return;
            }

            _lastError = null;
            _status = RuntimeStatus.Stopped;
            _tickCount = 0;
            snapshot = new GraphRuntimeSnapshot
            {
                Status = RuntimeStatus.Stopped,
                Message = "Runtime reset."
            };
            _currentSnapshot = snapshot;
        }

        Publish(snapshot);
        EmitDiagnostic(
            snapshot,
            GraphRuntimeDiagnosticCodes.Reset,
            GraphRuntimeDiagnosticSeverity.Information,
            "Runtime reset.");
    }

    private async ValueTask ExecuteSerializedCommandAsync(
        Func<CancellationToken, ValueTask> command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        CancellationTokenSource? commandCancellation = null;
        try
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                commandCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _lifetimeCancellation.Token);
                _activeCommandCancellation = commandCancellation;
            }

            try
            {
                await command(commandCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested && commandCancellation.IsCancellationRequested)
            {
                // Stop、Pause 或 Dispose 会取消正在等待 Provider 的命令。
            }
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_activeCommandCancellation, commandCancellation))
                {
                    _activeCommandCancellation = null;
                }
            }

            commandCancellation?.Dispose();
            _commandGate.Release();
        }
    }

    private void CancelActiveCommand()
    {
        CancellationTokenSource? commandCancellation;
        lock (_gate)
        {
            commandCancellation = _activeCommandCancellation;
        }

        try
        {
            commandCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 命令可能刚好完成并释放自己的 CancellationTokenSource。
        }
    }

    private bool IsDisposing
    {
        get
        {
            lock (_gate)
            {
                return _isDisposing;
            }
        }
    }

    public ValueTask RefreshAsync(CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(throwIfDisposed: true, cancellationToken);

    private async ValueTask RefreshCoreAsync(
        bool throwIfDisposed,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_isDisposed || _isDisposing)
            {
                if (throwIfDisposed)
                {
                    ThrowIfDisposed();
                }

                return;
            }

            if (_status != RuntimeStatus.Running)
            {
                return;
            }
        }

        GraphRuntimeSnapshot providedSnapshot;
        try
        {
            providedSnapshot = await GetProviderSnapshotAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && _lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            TransitionToFaulted(exception);
            return;
        }

        string? breakpointNodeId;
        lock (_gate)
        {
            if (_isDisposed || _isDisposing || _status != RuntimeStatus.Running)
            {
                return;
            }

            _tickCount++;
            var snapshot = GraphRuntimeSnapshotFactory.Normalize(
                providedSnapshot,
                RuntimeStatus.Running,
                _tickCount);
            breakpointNodeId = FindBreakpointNodeId(snapshot);
            if (breakpointNodeId is not null)
            {
                _status = RuntimeStatus.Paused;
                snapshot = GraphRuntimeSnapshotFactory.CreateBreakpoint(snapshot, breakpointNodeId);
            }

            _currentSnapshot = snapshot;
            providedSnapshot = _currentSnapshot;
        }

        if (breakpointNodeId is not null)
        {
            _scheduler.Stop();
        }

        Publish(providedSnapshot);
        if (breakpointNodeId is not null)
        {
            BreakpointHit?.Invoke(
                this,
                new GraphRuntimeBreakpointHitEventArgs(breakpointNodeId, providedSnapshot));
            EmitDiagnostic(
                providedSnapshot,
                GraphRuntimeDiagnosticCodes.BreakpointHit,
                GraphRuntimeDiagnosticSeverity.Warning,
                providedSnapshot.Message ?? $"Breakpoint hit at node '{breakpointNodeId}'.",
                breakpointNodeId);
        }
        else
        {
            EmitDiagnostic(
                providedSnapshot,
                GraphRuntimeDiagnosticCodes.SnapshotRefreshed,
                GraphRuntimeDiagnosticSeverity.Trace,
                providedSnapshot.Message ?? "Runtime snapshot refreshed.",
                providedSnapshot.ActiveNodeId);
        }
    }

    public void Dispose()
    {
        if (!BeginDispose())
        {
            return;
        }

        CompleteDispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (!BeginDispose())
        {
            return;
        }

        await _commandGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _providerGate.WaitAsync().ConfigureAwait(false);
            try
            {
                CompleteDispose();
            }
            finally
            {
                _providerGate.Release();
            }
        }
        finally
        {
            _commandGate.Release();
        }
    }

    private bool BeginDispose()
    {
        lock (_gate)
        {
            if (_isDisposed || _isDisposing)
            {
                return false;
            }

            _isDisposing = true;
        }

        _lifetimeCancellation.Cancel();
        CancelActiveCommand();
        _scheduler.Stop();
        _scheduler.Tick -= HandleSchedulerTick;
        return true;
    }

    private void CompleteDispose()
    {
        lock (_gate)
        {
            _isDisposed = true;
            _isDisposing = false;
            _status = RuntimeStatus.Stopped;
            _tickCount = 0;
            _breakpointNodeIds.Clear();
            _watchExpressions.Clear();
            _diagnostics.Clear();
            _currentSnapshot = new GraphRuntimeSnapshot();
        }

        if (_ownsScheduler && _scheduler is IDisposable disposable)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private async void HandleSchedulerTick(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _scheduledRefreshInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            await RefreshCoreAsync(throwIfDisposed: false, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            try
            {
                TransitionToFaulted(exception);
            }
            catch
            {
                // Scheduler callbacks must not terminate the process because an observer failed.
            }
        }
        finally
        {
            Volatile.Write(ref _scheduledRefreshInProgress, 0);
        }
    }

    private async ValueTask<GraphRuntimeSnapshot> GetProviderSnapshotAsync(
        CancellationToken cancellationToken)
    {
        using var providerCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        await _providerGate.WaitAsync(providerCancellation.Token).ConfigureAwait(false);
        try
        {
            var snapshot = await _stateProvider
                .GetSnapshotAsync(Document, providerCancellation.Token)
                .ConfigureAwait(false);
            string[] watchExpressions;
            lock (_gate)
            {
                watchExpressions = _watchExpressions.ToArray();
            }

            var watchResults = await GraphRuntimeWatchEvaluator.EvaluateAsync(
                    Document,
                    _stateProvider,
                    snapshot,
                    watchExpressions,
                    providerCancellation.Token)
                .ConfigureAwait(false);
            return GraphRuntimeSnapshotFactory.Copy(snapshot, watchResults);
        }
        finally
        {
            _providerGate.Release();
        }
    }

    private async ValueTask RefreshWatchResultsAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        await _providerGate.WaitAsync(linkedCancellation.Token).ConfigureAwait(false);
        try
        {
            GraphRuntimeSnapshot snapshot;
            string[] watchExpressions;
            lock (_gate)
            {
                ThrowIfDisposed();
                snapshot = _currentSnapshot;
                watchExpressions = _watchExpressions.ToArray();
            }

            var watchResults = await GraphRuntimeWatchEvaluator.EvaluateAsync(
                    Document,
                    _stateProvider,
                    snapshot,
                    watchExpressions,
                    linkedCancellation.Token)
                .ConfigureAwait(false);
            lock (_gate)
            {
                if (_isDisposed || _isDisposing)
                {
                    return;
                }

                _currentSnapshot = GraphRuntimeSnapshotFactory.Copy(_currentSnapshot, watchResults);
            }
        }
        finally
        {
            _providerGate.Release();
        }
    }

    private void TransitionToFaulted(Exception exception)
    {
        _scheduler.Stop();
        GraphRuntimeSnapshot snapshot;
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _lastError = exception;
            _status = RuntimeStatus.Faulted;
            snapshot = new GraphRuntimeSnapshot
            {
                Status = RuntimeStatus.Faulted,
                TickCount = _tickCount,
                ActiveNodeId = null,
                Message = exception.Message,
                Nodes = _currentSnapshot.Nodes,
                Edges = _currentSnapshot.Edges
                    .Select(edge => edge with { IsActive = false })
                    .ToArray(),
                CallStack = _currentSnapshot.CallStack,
                Variables = _currentSnapshot.Variables,
                WatchResults = _currentSnapshot.WatchResults
            };
            _currentSnapshot = snapshot;
        }

        Publish(snapshot);
        EmitDiagnostic(
            snapshot,
            GraphRuntimeDiagnosticCodes.Faulted,
            GraphRuntimeDiagnosticSeverity.Error,
            exception.Message,
            exception: exception);
    }

    private void Publish(GraphRuntimeSnapshot snapshot) => SnapshotChanged?.Invoke(this, snapshot);

    private void EmitDiagnostic(
        GraphRuntimeSnapshot snapshot,
        string code,
        GraphRuntimeDiagnosticSeverity severity,
        string message,
        string? nodeId = null,
        Exception? exception = null)
    {
        var diagnostic = new GraphRuntimeDiagnostic(
            code,
            severity,
            snapshot.Status,
            snapshot.TickCount,
            message,
            DateTimeOffset.UtcNow,
            nodeId,
            exception);
        lock (_gate)
        {
            if (_isDisposed || _isDisposing)
            {
                return;
            }

            _diagnostics.Add(diagnostic);
        }

        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        DiagnosticEmitted?.Invoke(this, new GraphRuntimeDiagnosticEventArgs(diagnostic));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed || _isDisposing, this);

    private static PeriodicGraphRefreshScheduler CreateOwnedScheduler(
        TimeSpan interval,
        int diagnosticCapacity)
    {
        ValidateDiagnosticCapacity(diagnosticCapacity);
        return new PeriodicGraphRefreshScheduler(interval);
    }

    private static int ValidateDiagnosticCapacity(int diagnosticCapacity)
    {
        if (diagnosticCapacity <= 0 || diagnosticCapacity > MaximumDiagnosticCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diagnosticCapacity),
                diagnosticCapacity,
                $"Diagnostic capacity must be between 1 and {MaximumDiagnosticCapacity}.");
        }

        return diagnosticCapacity;
    }

    private string? FindBreakpointNodeId(GraphRuntimeSnapshot snapshot) =>
        snapshot.ActiveNodeId is { } nodeId && _breakpointNodeIds.Contains(nodeId)
            ? nodeId
            : null;
}
