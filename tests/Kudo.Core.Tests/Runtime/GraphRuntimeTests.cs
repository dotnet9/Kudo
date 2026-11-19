using Kudo.Core.Runtime;
using Shouldly;

namespace Kudo.Core.Tests.Runtime;

public class GraphRuntimeTests
{
    [Fact]
    public void Runtime_snapshot_defaults_to_stopped()
    {
        var snapshot = new GraphRuntimeSnapshot();

        snapshot.Status.ShouldBe(RuntimeStatus.Stopped);
        snapshot.ActiveNodeId.ShouldBeNull();
        snapshot.Nodes.ShouldBeEmpty();
        snapshot.Edges.ShouldBeEmpty();
        snapshot.CallStack.ShouldBeEmpty();
        snapshot.Variables.ShouldBeEmpty();
        snapshot.WatchResults.ShouldBeEmpty();
    }

    [Fact]
    public async Task Runtime_session_refreshes_and_preserves_lifecycle_state()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "node" });
        document.AddEdge(new EdgeModel { Id = "edge" });
        var scheduler = new ManualRefreshScheduler();
        var provider = new CountingStateProvider();
        using var session = new GraphRuntimeSession(document, provider, scheduler);
        var statuses = new List<RuntimeStatus>();
        var diagnostics = new List<GraphRuntimeDiagnostic>();
        session.SnapshotChanged += (_, snapshot) => statuses.Add(snapshot.Status);
        session.DiagnosticEmitted += (_, e) => diagnostics.Add(e.Diagnostic);

        await session.StartAsync(TestContext.Current.CancellationToken);

        session.Status.ShouldBe(RuntimeStatus.Running);
        session.TickCount.ShouldBe(1);
        scheduler.IsRunning.ShouldBeTrue();
        provider.RefreshCount.ShouldBe(1);
        session.CurrentSnapshot.Nodes.Single().NodeId.ShouldBe("node");
        session.CurrentSnapshot.Edges.Single().IsActive.ShouldBeTrue();
        statuses.ShouldBe([RuntimeStatus.Starting, RuntimeStatus.Running]);

        scheduler.RaiseTick();

        provider.RefreshCount.ShouldBe(2);
        session.TickCount.ShouldBe(2);
        session.CurrentSnapshot.TickCount.ShouldBe(2);
        session.CurrentSnapshot.Message.ShouldBe("refresh-2");

        await session.PauseAsync(TestContext.Current.CancellationToken);

        session.Status.ShouldBe(RuntimeStatus.Paused);
        scheduler.IsRunning.ShouldBeFalse();
        session.CurrentSnapshot.Nodes.ShouldNotBeEmpty();
        session.CurrentSnapshot.Variables.Single().Value.ShouldBe(2);
        scheduler.RaiseTick();
        provider.RefreshCount.ShouldBe(2);

        await session.StopAsync(TestContext.Current.CancellationToken);

        session.Status.ShouldBe(RuntimeStatus.Stopped);
        session.TickCount.ShouldBe(0);
        session.CurrentSnapshot.Nodes.ShouldBeEmpty();
        session.CurrentSnapshot.Edges.ShouldBeEmpty();
        session.CurrentSnapshot.CallStack.ShouldBeEmpty();
        session.CurrentSnapshot.Variables.ShouldBeEmpty();
        statuses.TakeLast(2).ShouldBe([RuntimeStatus.Stopping, RuntimeStatus.Stopped]);
        diagnostics.Select(diagnostic => diagnostic.Code).ShouldBe([
            GraphRuntimeDiagnosticCodes.SnapshotRefreshed,
            GraphRuntimeDiagnosticCodes.SnapshotRefreshed,
            GraphRuntimeDiagnosticCodes.Paused,
            GraphRuntimeDiagnosticCodes.Stopped
        ]);
        diagnostics[1].TickCount.ShouldBe(2);
        diagnostics[1].Severity.ShouldBe(GraphRuntimeDiagnosticSeverity.Trace);
    }

    [Fact]
    public async Task Runtime_session_steps_once_without_starting_scheduler()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "node" });
        var scheduler = new ManualRefreshScheduler();
        var provider = new CountingStateProvider();
        using var session = new GraphRuntimeSession(document, provider, scheduler);
        GraphRuntimeDiagnostic? diagnostic = null;
        session.DiagnosticEmitted += (_, e) => diagnostic = e.Diagnostic;

        await session.StepAsync(TestContext.Current.CancellationToken);

        provider.RefreshCount.ShouldBe(1);
        scheduler.IsRunning.ShouldBeFalse();
        session.Status.ShouldBe(RuntimeStatus.Paused);
        session.TickCount.ShouldBe(1);
        session.CurrentSnapshot.TickCount.ShouldBe(1);
        session.CurrentSnapshot.Nodes.Single().Status.ShouldBe(RuntimeStatus.Paused);
        session.CurrentSnapshot.Variables.Single().Name.ShouldBe("刷新次数");
        diagnostic.ShouldNotBeNull();
        diagnostic.Code.ShouldBe(GraphRuntimeDiagnosticCodes.Stepped);
        diagnostic.Status.ShouldBe(RuntimeStatus.Paused);

        await session.StepAsync(TestContext.Current.CancellationToken);

        provider.RefreshCount.ShouldBe(2);
        session.TickCount.ShouldBe(2);
    }

    [Fact]
    public async Task Runtime_session_rejects_step_while_running()
    {
        var scheduler = new ManualRefreshScheduler();
        var provider = new CountingStateProvider();
        using var session = new GraphRuntimeSession(new GraphDocument(), provider, scheduler);
        await session.StartAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await session.StepAsync(TestContext.Current.CancellationToken));

        provider.RefreshCount.ShouldBe(1);
        scheduler.IsRunning.ShouldBeTrue();
        session.Status.ShouldBe(RuntimeStatus.Running);
    }

    [Fact]
    public async Task Runtime_session_reset_stops_scheduler_and_clears_tick_count()
    {
        var scheduler = new ManualRefreshScheduler();
        var provider = new ResettableStateProvider();
        using var session = new GraphRuntimeSession(new GraphDocument(), provider, scheduler);
        await session.StartAsync(TestContext.Current.CancellationToken);
        scheduler.RaiseTick();

        session.CurrentSnapshot.Variables.ShouldNotBeEmpty();

        await session.ResetAsync(TestContext.Current.CancellationToken);

        provider.ResetCount.ShouldBe(1);
        scheduler.IsRunning.ShouldBeFalse();
        session.Status.ShouldBe(RuntimeStatus.Stopped);
        session.TickCount.ShouldBe(0);
        session.CurrentSnapshot.TickCount.ShouldBe(0);
        session.CurrentSnapshot.Nodes.ShouldBeEmpty();
        session.CurrentSnapshot.Edges.ShouldBeEmpty();
        session.CurrentSnapshot.CallStack.ShouldBeEmpty();
        session.CurrentSnapshot.Variables.ShouldBeEmpty();
    }

    [Fact]
    public async Task Runtime_session_pauses_when_active_node_hits_breakpoint()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "first" });
        document.AddNode(new NodeModel { Id = "second" });
        var scheduler = new ManualRefreshScheduler();
        using var session = new GraphRuntimeSession(
            document,
            new ActiveNodeStateProvider("second"),
            scheduler);
        GraphRuntimeBreakpointHitEventArgs? breakpointHit = null;
        GraphRuntimeDiagnostic? breakpointDiagnostic = null;
        var breakpointChangeCount = 0;
        session.BreakpointHit += (_, args) => breakpointHit = args;
        session.BreakpointsChanged += (_, _) => breakpointChangeCount++;
        session.DiagnosticEmitted += (_, e) => breakpointDiagnostic = e.Diagnostic;

        session.AddBreakpoint("missing").ShouldBeFalse();
        session.AddBreakpoint("second").ShouldBeTrue();
        session.AddBreakpoint("second").ShouldBeFalse();
        session.BreakpointNodeIds.ShouldBe(["second"]);
        breakpointChangeCount.ShouldBe(1);

        await session.StartAsync(TestContext.Current.CancellationToken);

        session.Status.ShouldBe(RuntimeStatus.Paused);
        session.TickCount.ShouldBe(1);
        scheduler.IsRunning.ShouldBeFalse();
        session.CurrentSnapshot.ActiveNodeId.ShouldBe("second");
        session.CurrentSnapshot.Message.ShouldBe("Breakpoint hit at node 'second'.");
        session.CurrentSnapshot.Nodes.ShouldAllBe(node => node.Status == RuntimeStatus.Paused);
        session.CurrentSnapshot.CallStack.Single().NodeId.ShouldBe("second");
        session.CurrentSnapshot.Variables.Single().NodeId.ShouldBe("second");
        breakpointHit.ShouldNotBeNull();
        breakpointHit.NodeId.ShouldBe("second");
        breakpointHit.Snapshot.ShouldBeSameAs(session.CurrentSnapshot);
        breakpointDiagnostic.ShouldNotBeNull();
        breakpointDiagnostic.Code.ShouldBe(GraphRuntimeDiagnosticCodes.BreakpointHit);
        breakpointDiagnostic.Severity.ShouldBe(GraphRuntimeDiagnosticSeverity.Warning);
        breakpointDiagnostic.NodeId.ShouldBe("second");

        session.RemoveBreakpoint("second").ShouldBeTrue();
        breakpointChangeCount.ShouldBe(2);
        await session.StartAsync(TestContext.Current.CancellationToken);

        session.Status.ShouldBe(RuntimeStatus.Running);
        scheduler.IsRunning.ShouldBeTrue();
        session.AddBreakpoint("first").ShouldBeTrue();
        session.ClearBreakpoints();
        session.BreakpointNodeIds.ShouldBeEmpty();
        breakpointChangeCount.ShouldBe(4);
    }

    [Fact]
    public async Task Runtime_session_faults_when_provider_throws()
    {
        var scheduler = new ManualRefreshScheduler();
        var expected = new InvalidOperationException("provider failed");
        using var session = new GraphRuntimeSession(
            new GraphDocument(),
            new ThrowingStateProvider(expected),
            scheduler);
        GraphRuntimeDiagnostic? diagnostic = null;
        session.DiagnosticEmitted += (_, e) => diagnostic = e.Diagnostic;

        await session.StartAsync(TestContext.Current.CancellationToken);

        session.Status.ShouldBe(RuntimeStatus.Faulted);
        session.LastError.ShouldBeSameAs(expected);
        session.CurrentSnapshot.Message.ShouldBe(expected.Message);
        scheduler.IsRunning.ShouldBeFalse();
        diagnostic.ShouldNotBeNull();
        diagnostic.Code.ShouldBe(GraphRuntimeDiagnosticCodes.Faulted);
        diagnostic.Severity.ShouldBe(GraphRuntimeDiagnosticSeverity.Error);
        diagnostic.Exception.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task Runtime_fault_preserves_latest_variables()
    {
        var scheduler = new ManualRefreshScheduler();
        var provider = new FailingAfterFirstStateProvider();
        using var session = new GraphRuntimeSession(new GraphDocument(), provider, scheduler);
        await session.StartAsync(TestContext.Current.CancellationToken);

        scheduler.RaiseTick();

        session.Status.ShouldBe(RuntimeStatus.Faulted);
        session.CurrentSnapshot.Variables.Single().Name.ShouldBe("有效值");
        session.CurrentSnapshot.Variables.Single().Value.ShouldBe(42);
    }

    [Fact]
    public async Task Runtime_diagnostics_keep_bounded_session_history_and_can_be_cleared()
    {
        using var session = new GraphRuntimeSession(
            new GraphDocument(),
            new CountingStateProvider(),
            new ManualRefreshScheduler(),
            diagnosticCapacity: 2);
        var changedCount = 0;
        session.DiagnosticsChanged += (_, _) => changedCount++;

        await session.StepAsync(TestContext.Current.CancellationToken);
        await session.StepAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);

        session.DiagnosticCapacity.ShouldBe(2);
        session.RecentDiagnostics.Select(diagnostic => diagnostic.Code).ShouldBe([
            GraphRuntimeDiagnosticCodes.Stepped,
            GraphRuntimeDiagnosticCodes.Stopped
        ]);
        changedCount.ShouldBe(3);

        session.ClearDiagnostics();

        session.RecentDiagnostics.ShouldBeEmpty();
        changedCount.ShouldBe(4);
        session.ClearDiagnostics();
        changedCount.ShouldBe(4);
    }

    [Fact]
    public void Runtime_session_rejects_unsafe_diagnostic_capacity()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new GraphRuntimeSession(
            new GraphDocument(),
            new CountingStateProvider(),
            new ManualRefreshScheduler(),
            diagnosticCapacity: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new GraphRuntimeSession(
            new GraphDocument(),
            new CountingStateProvider(),
            diagnosticCapacity: GraphRuntimeSession.MaximumDiagnosticCapacity + 1));
    }

    [Fact]
    public async Task Runtime_watch_tracks_snapshot_variables_and_survives_stop()
    {
        using var session = new GraphRuntimeSession(
            new GraphDocument(),
            new CountingStateProvider(),
            new ManualRefreshScheduler());
        var watchesChanged = 0;
        session.WatchesChanged += (_, _) => watchesChanged++;

        (await session.AddWatchAsync(" 刷新次数 ", TestContext.Current.CancellationToken))
            .ShouldBeTrue();
        (await session.AddWatchAsync("刷新次数", TestContext.Current.CancellationToken))
            .ShouldBeFalse();
        (await session.AddWatchAsync(" ", TestContext.Current.CancellationToken))
            .ShouldBeFalse();
        session.WatchExpressions.ShouldBe(["刷新次数"]);
        var missingVariable = session.CurrentSnapshot.WatchResults.Single();
        missingVariable.IsSuccess.ShouldBeFalse();
        missingVariable.ErrorCode.ShouldBe(GraphRuntimeWatchErrorCodes.VariableNotFound);
        missingVariable.Error.ShouldBe("The variable was not found in the current snapshot.");

        await session.StepAsync(TestContext.Current.CancellationToken);

        var result = session.CurrentSnapshot.WatchResults.Single();
        result.Expression.ShouldBe("刷新次数");
        result.Value.ShouldBe(1);
        result.TypeName.ShouldBe(nameof(Int32));
        result.IsSuccess.ShouldBeTrue();

        await session.StopAsync(TestContext.Current.CancellationToken);

        session.WatchExpressions.ShouldBe(["刷新次数"]);
        session.CurrentSnapshot.WatchResults.ShouldBeEmpty();
        session.RemoveWatch("刷新次数").ShouldBeTrue();
        session.RemoveWatch("刷新次数").ShouldBeFalse();
        watchesChanged.ShouldBe(2);
    }

    [Fact]
    public async Task Runtime_watch_uses_provider_evaluator_and_isolates_failures()
    {
        using var session = new GraphRuntimeSession(
            new GraphDocument(),
            new WatchStateProvider(),
            new ManualRefreshScheduler());

        (await session.AddWatchAsync("状态", TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await session.AddWatchAsync("失败", TestContext.Current.CancellationToken)).ShouldBeTrue();
        await session.StepAsync(TestContext.Current.CancellationToken);

        var success = session.CurrentSnapshot.WatchResults[0];
        success.Value.ShouldBe("状态:1");
        success.TypeName.ShouldBe(nameof(String));
        success.IsSuccess.ShouldBeTrue();
        var failure = session.CurrentSnapshot.WatchResults[1];
        failure.Expression.ShouldBe("失败");
        failure.Error.ShouldBe("无法求值");
        failure.IsSuccess.ShouldBeFalse();
        session.Status.ShouldBe(RuntimeStatus.Paused);
        session.LastError.ShouldBeNull();

        session.ClearWatches();
        session.WatchExpressions.ShouldBeEmpty();
        session.CurrentSnapshot.WatchResults.ShouldBeEmpty();
    }

    [Fact]
    public async Task Runtime_watch_is_scoped_to_each_session_and_rolls_back_cancellation()
    {
        using var first = new GraphRuntimeSession(
            new GraphDocument(),
            new CountingStateProvider(),
            new ManualRefreshScheduler());
        using var second = new GraphRuntimeSession(
            new GraphDocument(),
            new CountingStateProvider(),
            new ManualRefreshScheduler());

        (await first.AddWatchAsync("刷新次数", TestContext.Current.CancellationToken)).ShouldBeTrue();

        first.WatchExpressions.ShouldBe(["刷新次数"]);
        second.WatchExpressions.ShouldBeEmpty();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await second.AddWatchAsync("取消项", cancellation.Token));
        second.WatchExpressions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Runtime_start_cancellation_restores_previous_state()
    {
        var provider = new CancellableStateProvider();
        using var session = new GraphRuntimeSession(
            new GraphDocument(),
            provider,
            new ManualRefreshScheduler());
        var previousSnapshot = session.CurrentSnapshot;
        using var cancellation = new CancellationTokenSource();

        var startTask = session.StartAsync(cancellation.Token).AsTask();
        await provider.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(startTask);
        session.Status.ShouldBe(RuntimeStatus.Stopped);
        session.TickCount.ShouldBe(0);
        session.CurrentSnapshot.ShouldBeSameAs(previousSnapshot);
        session.LastError.ShouldBeNull();
    }

    [Fact]
    public async Task Runtime_scheduler_skips_overlapping_async_refreshes()
    {
        var provider = new ControlledAsyncStateProvider();
        var scheduler = new ManualRefreshScheduler();
        using var session = new GraphRuntimeSession(new GraphDocument(), provider, scheduler);
        var secondSnapshotPublished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.SnapshotChanged += (_, snapshot) =>
        {
            if (snapshot.TickCount == 2)
            {
                secondSnapshotPublished.TrySetResult(true);
            }
        };
        await session.StartAsync(TestContext.Current.CancellationToken);

        scheduler.RaiseTick();
        await provider.RefreshBlocked.Task.WaitAsync(TestContext.Current.CancellationToken);
        scheduler.RaiseTick();

        provider.RefreshCount.ShouldBe(2);
        provider.MaximumConcurrency.ShouldBe(1);

        provider.ReleaseRefresh.TrySetResult(true);
        await secondSnapshotPublished.Task.WaitAsync(TestContext.Current.CancellationToken);
        session.TickCount.ShouldBe(2);
    }

    [Fact]
    public async Task Runtime_stop_cancels_active_step_before_transitioning()
    {
        var provider = new CancellableStateProvider();
        using var session = new GraphRuntimeSession(
            new GraphDocument(),
            provider,
            new ManualRefreshScheduler());
        var stepTask = session.StepAsync(TestContext.Current.CancellationToken).AsTask();
        await provider.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

        await session.StopAsync(TestContext.Current.CancellationToken);
        await stepTask.WaitAsync(TestContext.Current.CancellationToken);

        await provider.CancellationObserved.Task.WaitAsync(TestContext.Current.CancellationToken);
        session.Status.ShouldBe(RuntimeStatus.Stopped);
        session.TickCount.ShouldBe(0);
        session.LastError.ShouldBeNull();
    }

    [Fact]
    public async Task Runtime_dispose_async_cancels_and_waits_for_provider()
    {
        var provider = new CancellableStateProvider();
        var session = new GraphRuntimeSession(
            new GraphDocument(),
            provider,
            new ManualRefreshScheduler());
        var startTask = session.StartAsync(TestContext.Current.CancellationToken).AsTask();
        await provider.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

        await session.DisposeAsync();

        await startTask.WaitAsync(TestContext.Current.CancellationToken);
        provider.CancellationObserved.Task.IsCompletedSuccessfully.ShouldBeTrue();
        Should.Throw<ObjectDisposedException>(() => session.AddBreakpoint("node"));
        await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await session.AddWatchAsync("node", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Periodic_scheduler_rejects_unsafe_intervals()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new PeriodicGraphRefreshScheduler(TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new PeriodicGraphRefreshScheduler(TimeSpan.FromHours(2)));
    }

    private sealed class CountingStateProvider : IGraphRuntimeStateProvider
    {
        public int RefreshCount { get; private set; }

        public ValueTask<GraphRuntimeSnapshot> GetSnapshotAsync(
            GraphDocument document,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshCount++;
            return ValueTask.FromResult(new GraphRuntimeSnapshot
            {
                Status = RuntimeStatus.Stopped,
                Message = $"refresh-{RefreshCount}",
                Nodes = document.Nodes
                    .Select(node => new GraphRuntimeNodeState(node.Id, RuntimeStatus.Running))
                    .ToArray(),
                Edges = document.Edges
                    .Select(edge => new GraphRuntimeEdgeState(edge.Id, IsActive: true))
                    .ToArray(),
                Variables = [new GraphRuntimeVariable("刷新次数", RefreshCount, nameof(Int32), "测试")]
            });
        }
    }

    private sealed class ThrowingStateProvider(Exception exception) : IGraphRuntimeStateProvider
    {
        public ValueTask<GraphRuntimeSnapshot> GetSnapshotAsync(
            GraphDocument document,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<GraphRuntimeSnapshot>(exception);
    }

    private sealed class WatchStateProvider : IGraphRuntimeStateProvider, IGraphRuntimeWatchEvaluator
    {
        private int _refreshCount;

        public ValueTask<GraphRuntimeSnapshot> GetSnapshotAsync(
            GraphDocument document,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _refreshCount++;
            return ValueTask.FromResult(new GraphRuntimeSnapshot());
        }

        public ValueTask<GraphRuntimeWatchResult> EvaluateWatchAsync(
            GraphDocument document,
            GraphRuntimeSnapshot snapshot,
            string expression,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (expression == "失败")
            {
                throw new InvalidOperationException("无法求值");
            }

            return ValueTask.FromResult(new GraphRuntimeWatchResult(
                expression,
                $"{expression}:{_refreshCount}",
                nameof(String)));
        }
    }

    private sealed class ResettableStateProvider : IGraphRuntimeResettableStateProvider
    {
        public int ResetCount { get; private set; }

        public ValueTask<GraphRuntimeSnapshot> GetSnapshotAsync(
            GraphDocument document,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GraphRuntimeSnapshot
            {
                Variables = [new GraphRuntimeVariable("待清理", true)]
            });

        public ValueTask ResetAsync(
            GraphDocument document,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResetCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ActiveNodeStateProvider(string activeNodeId) : IGraphRuntimeStateProvider
    {
        public ValueTask<GraphRuntimeSnapshot> GetSnapshotAsync(
            GraphDocument document,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GraphRuntimeSnapshot
            {
                ActiveNodeId = activeNodeId,
                Nodes = document.Nodes
                    .Select(node => new GraphRuntimeNodeState(node.Id, RuntimeStatus.Running))
                    .ToArray(),
                CallStack = [new GraphRuntimeStackFrame(activeNodeId, $"节点 {activeNodeId}")],
                Variables = [new GraphRuntimeVariable("活动节点", activeNodeId, NodeId: activeNodeId)]
            });
    }

    private sealed class FailingAfterFirstStateProvider : IGraphRuntimeStateProvider
    {
        private int _refreshCount;

        public ValueTask<GraphRuntimeSnapshot> GetSnapshotAsync(
            GraphDocument document,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref _refreshCount) > 1)
            {
                throw new InvalidOperationException("刷新失败");
            }

            return ValueTask.FromResult(new GraphRuntimeSnapshot
            {
                Variables = [new GraphRuntimeVariable("有效值", 42)]
            });
        }
    }

    private sealed class CancellableStateProvider : IGraphRuntimeStateProvider
    {
        public TaskCompletionSource<bool> Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> CancellationObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<GraphRuntimeSnapshot> GetSnapshotAsync(
            GraphDocument document,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new GraphRuntimeSnapshot();
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    CancellationObserved.TrySetResult(true);
                }
            }
        }
    }

    private sealed class ControlledAsyncStateProvider : IGraphRuntimeStateProvider
    {
        private int _concurrency;
        private int _maximumConcurrency;
        private int _refreshCount;

        public TaskCompletionSource<bool> RefreshBlocked { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseRefresh { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int RefreshCount => Volatile.Read(ref _refreshCount);

        public int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);

        public async ValueTask<GraphRuntimeSnapshot> GetSnapshotAsync(
            GraphDocument document,
            CancellationToken cancellationToken = default)
        {
            var concurrency = Interlocked.Increment(ref _concurrency);
            UpdateMaximumConcurrency(concurrency);
            var refreshCount = Interlocked.Increment(ref _refreshCount);
            try
            {
                if (refreshCount > 1)
                {
                    RefreshBlocked.TrySetResult(true);
                    await ReleaseRefresh.Task.WaitAsync(cancellationToken);
                }

                return new GraphRuntimeSnapshot { Message = $"异步刷新 {refreshCount}" };
            }
            finally
            {
                Interlocked.Decrement(ref _concurrency);
            }
        }

        private void UpdateMaximumConcurrency(int concurrency)
        {
            var current = Volatile.Read(ref _maximumConcurrency);
            while (concurrency > current)
            {
                var observed = Interlocked.CompareExchange(
                    ref _maximumConcurrency,
                    concurrency,
                    current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }

    private sealed class ManualRefreshScheduler : IGraphRefreshScheduler
    {
        public bool IsRunning { get; private set; }

        public event EventHandler? Tick;

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void RaiseTick()
        {
            if (IsRunning)
            {
                Tick?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
