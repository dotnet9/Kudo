using Kudo.Core.Runtime;
using Shouldly;

namespace Kudo.Core.Tests.Runtime;

public class GraphRuntimeCommandTests
{
    [Fact]
    public void Command_availability_matches_runtime_lifecycle()
    {
        AssertAvailability(RuntimeStatus.Stopped, true, false, true, false, true);
        AssertAvailability(RuntimeStatus.Starting, false, false, false, true, false);
        AssertAvailability(RuntimeStatus.Running, false, true, false, true, true);
        AssertAvailability(RuntimeStatus.Paused, true, false, true, true, true);
        AssertAvailability(RuntimeStatus.Stopping, false, false, false, false, false);
        AssertAvailability(RuntimeStatus.Faulted, true, false, true, true, true);
    }

    [Fact]
    public async Task Executor_classifies_completed_breakpoint_fault_and_exception_outcomes()
    {
        var completed = CreateSession(new SnapshotProvider());
        using (completed)
        {
            var result = await GraphRuntimeCommandExecutor.ExecuteAsync(
                completed,
                GraphRuntimeCommand.Step,
                TestContext.Current.CancellationToken);

            result.Outcome.ShouldBe(GraphRuntimeCommandOutcome.Completed);
            result.Status.ShouldBe(RuntimeStatus.Paused);
            result.ActiveNodeId.ShouldBe("node");
            result.IsSuccess.ShouldBeTrue();
        }

        var breakpoint = CreateSession(new SnapshotProvider());
        using (breakpoint)
        {
            breakpoint.AddBreakpoint("node").ShouldBeTrue();
            var result = await GraphRuntimeCommandExecutor.ExecuteAsync(
                breakpoint,
                GraphRuntimeCommand.Step,
                TestContext.Current.CancellationToken);

            result.Outcome.ShouldBe(GraphRuntimeCommandOutcome.BreakpointHit);
            result.ActiveNodeId.ShouldBe("node");
            result.IsSuccess.ShouldBeTrue();
        }

        var faulted = CreateSession(new SnapshotProvider(shouldThrow: true));
        using (faulted)
        {
            var result = await GraphRuntimeCommandExecutor.ExecuteAsync(
                faulted,
                GraphRuntimeCommand.Step,
                TestContext.Current.CancellationToken);

            result.Outcome.ShouldBe(GraphRuntimeCommandOutcome.Faulted);
            result.Status.ShouldBe(RuntimeStatus.Faulted);
            result.IsSuccess.ShouldBeFalse();
        }

        var disposed = CreateSession(new SnapshotProvider());
        disposed.Dispose();
        var failed = await GraphRuntimeCommandExecutor.ExecuteAsync(
            disposed,
            GraphRuntimeCommand.Start,
            TestContext.Current.CancellationToken);
        failed.Outcome.ShouldBe(GraphRuntimeCommandOutcome.Failed);
        failed.Exception.ShouldBeOfType<ObjectDisposedException>();
        failed.IsSuccess.ShouldBeFalse();
    }

    private static GraphRuntimeSession CreateSession(IGraphRuntimeStateProvider provider)
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "node" });
        return new GraphRuntimeSession(document, provider, new ManualScheduler());
    }

    private static void AssertAvailability(
        RuntimeStatus status,
        bool canStart,
        bool canPause,
        bool canStep,
        bool canStop,
        bool canReset)
    {
        var availability = GraphRuntimeCommandAvailability.FromStatus(status);

        availability.CanStart.ShouldBe(canStart);
        availability.CanPause.ShouldBe(canPause);
        availability.CanStep.ShouldBe(canStep);
        availability.CanStop.ShouldBe(canStop);
        availability.CanReset.ShouldBe(canReset);
        availability.CanExecute(GraphRuntimeCommand.Start).ShouldBe(canStart);
        availability.CanExecute(GraphRuntimeCommand.Pause).ShouldBe(canPause);
        availability.CanExecute(GraphRuntimeCommand.Step).ShouldBe(canStep);
        availability.CanExecute(GraphRuntimeCommand.Stop).ShouldBe(canStop);
        availability.CanExecute(GraphRuntimeCommand.Reset).ShouldBe(canReset);
    }

    private sealed class SnapshotProvider(bool shouldThrow = false) : IGraphRuntimeStateProvider
    {
        public ValueTask<GraphRuntimeSnapshot> GetSnapshotAsync(
            GraphDocument document,
            CancellationToken cancellationToken = default)
        {
            if (shouldThrow)
            {
                throw new InvalidOperationException("Provider 故障");
            }

            return ValueTask.FromResult(new GraphRuntimeSnapshot { ActiveNodeId = "node" });
        }
    }

    private sealed class ManualScheduler : IGraphRefreshScheduler
    {
        public event EventHandler? Tick
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}
