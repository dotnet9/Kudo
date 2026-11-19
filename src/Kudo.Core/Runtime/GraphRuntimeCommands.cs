using System.ComponentModel;

namespace Kudo.Core.Runtime;

public enum GraphRuntimeCommand
{
    [Description("启动运行时")]
    Start,

    [Description("暂停运行时")]
    Pause,

    [Description("单步执行")]
    Step,

    [Description("停止运行时")]
    Stop,

    [Description("重置运行时")]
    Reset
}

public enum GraphRuntimeCommandOutcome
{
    [Description("执行完成")]
    Completed,

    [Description("命中断点")]
    BreakpointHit,

    [Description("运行时故障")]
    Faulted,

    [Description("命令失败")]
    Failed
}

public sealed record GraphRuntimeCommandResult(
    GraphRuntimeCommand Command,
    GraphRuntimeCommandOutcome Outcome,
    RuntimeStatus Status,
    string? ActiveNodeId = null,
    string? Message = null,
    Exception? Exception = null)
{
    public bool IsSuccess => Outcome is GraphRuntimeCommandOutcome.Completed or
        GraphRuntimeCommandOutcome.BreakpointHit;
}

public readonly record struct GraphRuntimeCommandAvailability(
    bool CanStart,
    bool CanPause,
    bool CanStep,
    bool CanStop,
    bool CanReset)
{
    public static GraphRuntimeCommandAvailability FromStatus(RuntimeStatus status) => new(
        CanStart: status is RuntimeStatus.Stopped or RuntimeStatus.Paused or RuntimeStatus.Faulted,
        CanPause: status == RuntimeStatus.Running,
        CanStep: status is not (RuntimeStatus.Starting or RuntimeStatus.Running or RuntimeStatus.Stopping),
        CanStop: status is not (RuntimeStatus.Stopped or RuntimeStatus.Stopping),
        CanReset: status is not (RuntimeStatus.Starting or RuntimeStatus.Stopping));

    public bool CanExecute(GraphRuntimeCommand command) => command switch
    {
        GraphRuntimeCommand.Start => CanStart,
        GraphRuntimeCommand.Pause => CanPause,
        GraphRuntimeCommand.Step => CanStep,
        GraphRuntimeCommand.Stop => CanStop,
        GraphRuntimeCommand.Reset => CanReset,
        _ => false
    };
}

public static class GraphRuntimeCommandExecutor
{
    public static async ValueTask<GraphRuntimeCommandResult> ExecuteAsync(
        IGraphRuntimeSession session,
        GraphRuntimeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        try
        {
            await ExecuteCoreAsync(session, command, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new GraphRuntimeCommandResult(
                command,
                GraphRuntimeCommandOutcome.Failed,
                session.Status,
                session.CurrentSnapshot.ActiveNodeId,
                exception.Message,
                exception);
        }

        var snapshot = session.CurrentSnapshot;
        if (session.Status == RuntimeStatus.Faulted)
        {
            return new GraphRuntimeCommandResult(
                command,
                GraphRuntimeCommandOutcome.Faulted,
                session.Status,
                snapshot.ActiveNodeId,
                snapshot.Message);
        }

        if (session.Status == RuntimeStatus.Paused &&
            snapshot.ActiveNodeId is { } activeNodeId &&
            session.BreakpointNodeIds.Contains(activeNodeId, StringComparer.Ordinal))
        {
            return new GraphRuntimeCommandResult(
                command,
                GraphRuntimeCommandOutcome.BreakpointHit,
                session.Status,
                activeNodeId,
                snapshot.Message);
        }

        return new GraphRuntimeCommandResult(
            command,
            GraphRuntimeCommandOutcome.Completed,
            session.Status,
            snapshot.ActiveNodeId,
            snapshot.Message);
    }

    private static ValueTask ExecuteCoreAsync(
        IGraphRuntimeSession session,
        GraphRuntimeCommand command,
        CancellationToken cancellationToken) => command switch
        {
            GraphRuntimeCommand.Start => session.StartAsync(cancellationToken),
            GraphRuntimeCommand.Pause => session.PauseAsync(cancellationToken),
            GraphRuntimeCommand.Step => session.StepAsync(cancellationToken),
            GraphRuntimeCommand.Stop => session.StopAsync(cancellationToken),
            GraphRuntimeCommand.Reset => session.ResetAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
        };
}
