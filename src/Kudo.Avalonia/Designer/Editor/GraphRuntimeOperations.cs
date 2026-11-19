using Kudo.Core.Runtime;

namespace Kudo.Avalonia.Designer;

/// <summary>
/// 提供可选运行时会话的生命周期、断点、监视和诊断操作。
/// </summary>
public sealed class GraphRuntimeOperations
{
    internal GraphRuntimeOperations(IGraphRuntimeSession? session) => Session = session;

    public IGraphRuntimeSession? Session { get; }

    public bool IsAvailable => Session is not null;

    public RuntimeStatus Status => Session?.Status ?? RuntimeStatus.Stopped;

    public GraphRuntimeCommandAvailability Availability => Session is null
        ? default
        : GraphRuntimeCommandAvailability.FromStatus(Session.Status);

    public long TickCount => Session?.TickCount ?? 0;

    public IReadOnlyCollection<string> BreakpointNodeIds =>
        Session?.BreakpointNodeIds ?? Array.Empty<string>();

    public IReadOnlyList<string> WatchExpressions =>
        Session?.WatchExpressions ?? Array.Empty<string>();

    public IReadOnlyList<GraphRuntimeDiagnostic> RecentDiagnostics =>
        Session?.RecentDiagnostics ?? Array.Empty<GraphRuntimeDiagnostic>();

    public GraphRuntimeSnapshot? CurrentSnapshot => Session?.CurrentSnapshot;

    public bool AddBreakpoint(string nodeId) => Session?.AddBreakpoint(nodeId) ?? false;

    public bool RemoveBreakpoint(string nodeId) => Session?.RemoveBreakpoint(nodeId) ?? false;

    public bool ToggleBreakpoint(string nodeId) =>
        BreakpointNodeIds.Contains(nodeId, StringComparer.Ordinal)
            ? RemoveBreakpoint(nodeId)
            : AddBreakpoint(nodeId);

    public bool ClearBreakpoints()
    {
        if (Session is null || Session.BreakpointNodeIds.Count == 0)
        {
            return false;
        }

        Session.ClearBreakpoints();
        return true;
    }

    public bool CanExecute(GraphRuntimeCommand command) =>
        Session is not null && Availability.CanExecute(command);

    public ValueTask<GraphRuntimeCommandResult?> ExecuteAsync(
        GraphRuntimeCommand command,
        CancellationToken cancellationToken = default) =>
        Session is null
            ? ValueTask.FromResult<GraphRuntimeCommandResult?>(null)
            : ExecuteAvailableAsync(Session, command, cancellationToken);

    public ValueTask<GraphRuntimeCommandResult?> StartAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(GraphRuntimeCommand.Start, cancellationToken);

    public ValueTask<GraphRuntimeCommandResult?> PauseAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(GraphRuntimeCommand.Pause, cancellationToken);

    public ValueTask<GraphRuntimeCommandResult?> StepAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(GraphRuntimeCommand.Step, cancellationToken);

    public ValueTask<GraphRuntimeCommandResult?> StopAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(GraphRuntimeCommand.Stop, cancellationToken);

    public ValueTask<GraphRuntimeCommandResult?> ResetAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(GraphRuntimeCommand.Reset, cancellationToken);

    public async ValueTask<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Session is null)
        {
            return false;
        }

        await Session.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async ValueTask<bool> AddWatchAsync(
        string expression,
        CancellationToken cancellationToken = default) =>
        Session is not null && await Session.AddWatchAsync(expression, cancellationToken)
            .ConfigureAwait(false);

    public bool RemoveWatch(string expression) => Session?.RemoveWatch(expression) ?? false;

    public bool ClearWatches()
    {
        if (Session is null || Session.WatchExpressions.Count == 0)
        {
            return false;
        }

        Session.ClearWatches();
        return true;
    }

    public bool ClearDiagnostics()
    {
        if (Session is null || Session.RecentDiagnostics.Count == 0)
        {
            return false;
        }

        Session.ClearDiagnostics();
        return true;
    }

    private static async ValueTask<GraphRuntimeCommandResult?> ExecuteAvailableAsync(
        IGraphRuntimeSession session,
        GraphRuntimeCommand command,
        CancellationToken cancellationToken)
    {
        if (!GraphRuntimeCommandAvailability.FromStatus(session.Status).CanExecute(command))
        {
            return null;
        }

        return await GraphRuntimeCommandExecutor.ExecuteAsync(session, command, cancellationToken)
            .ConfigureAwait(false);
    }
}
