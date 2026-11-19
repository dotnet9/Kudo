using System.ComponentModel;
using Kudo;

namespace Kudo.Core.Runtime;

public enum RuntimeStatus
{
    [Description("已停止")]
    Stopped,

    [Description("正在启动")]
    Starting,

    [Description("运行中")]
    Running,

    [Description("已暂停")]
    Paused,

    [Description("正在停止")]
    Stopping,

    [Description("运行故障")]
    Faulted
}

public enum GraphRuntimeDiagnosticSeverity
{
    [Description("跟踪")]
    Trace,

    [Description("信息")]
    Information,

    [Description("警告")]
    Warning,

    [Description("错误")]
    Error
}

public static class GraphRuntimeDiagnosticCodes
{
    public const string SnapshotRefreshed = "Runtime.SnapshotRefreshed";

    public const string Paused = "Runtime.Paused";

    public const string Stepped = "Runtime.Stepped";

    public const string Stopped = "Runtime.Stopped";

    public const string Reset = "Runtime.Reset";

    public const string BreakpointHit = "Runtime.BreakpointHit";

    public const string Faulted = "Runtime.Faulted";
}

public sealed record GraphRuntimeDiagnostic(
    string Code,
    GraphRuntimeDiagnosticSeverity Severity,
    RuntimeStatus Status,
    long TickCount,
    string Message,
    DateTimeOffset Timestamp,
    string? NodeId = null,
    Exception? Exception = null);

public sealed record RuntimeSignal(
    string EdgeId,
    object? Value,
    DateTimeOffset Timestamp);

public sealed record GraphRuntimeNodeState(
    string NodeId,
    RuntimeStatus Status,
    string? Message = null);

public sealed record GraphRuntimeEdgeState(
    string EdgeId,
    bool IsActive,
    object? Value = null);

public sealed record GraphRuntimeStackFrame(
    string NodeId,
    string DisplayName,
    string? Detail = null);

public sealed record GraphRuntimeVariable(
    string Name,
    object? Value,
    string? TypeName = null,
    string? Scope = null,
    string? NodeId = null);

public sealed record GraphRuntimeWatchResult(
    string Expression,
    object? Value,
    string? TypeName = null,
    string? Error = null)
{
    public string? ErrorCode { get; init; }

    public bool IsSuccess => string.IsNullOrWhiteSpace(Error);
}

public static class GraphRuntimeWatchErrorCodes
{
    public const string VariableNotFound = "VariableNotFound";
}

public sealed class GraphRuntimeSnapshot
{
    public RuntimeStatus Status { get; init; } = RuntimeStatus.Stopped;

    public long TickCount { get; init; }

    public string? ActiveNodeId { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public string? Message { get; init; }

    public IReadOnlyList<GraphRuntimeNodeState> Nodes { get; init; } = [];

    public IReadOnlyList<GraphRuntimeEdgeState> Edges { get; init; } = [];

    public IReadOnlyList<RuntimeSignal> Signals { get; init; } = [];

    public IReadOnlyList<GraphRuntimeStackFrame> CallStack { get; init; } = [];

    public IReadOnlyList<GraphRuntimeVariable> Variables { get; init; } = [];

    public IReadOnlyList<GraphRuntimeWatchResult> WatchResults { get; init; } = [];
}

public sealed class GraphRuntimeBreakpointHitEventArgs(
    string nodeId,
    GraphRuntimeSnapshot snapshot) : EventArgs
{
    public string NodeId { get; } = nodeId;

    public GraphRuntimeSnapshot Snapshot { get; } = snapshot;
}

public sealed class GraphRuntimeDiagnosticEventArgs(GraphRuntimeDiagnostic diagnostic) : EventArgs
{
    public GraphRuntimeDiagnostic Diagnostic { get; } = diagnostic;
}

public interface IGraphRuntimeValueProvider
{
    ValueTask<object?> GetValueAsync(PortReference port, CancellationToken cancellationToken = default);
}

public interface IGraphRuntimeStateProvider
{
    ValueTask<GraphRuntimeSnapshot> GetSnapshotAsync(
        GraphDocument document,
        CancellationToken cancellationToken = default);
}

public interface IGraphRuntimeResettableStateProvider : IGraphRuntimeStateProvider
{
    ValueTask ResetAsync(
        GraphDocument document,
        CancellationToken cancellationToken = default);
}

public interface IGraphRuntimeWatchEvaluator
{
    ValueTask<GraphRuntimeWatchResult> EvaluateWatchAsync(
        GraphDocument document,
        GraphRuntimeSnapshot snapshot,
        string expression,
        CancellationToken cancellationToken = default);
}

public interface IGraphRefreshScheduler
{
    event EventHandler? Tick;

    void Start();

    void Stop();
}

public interface IGraphRuntimeSession : IAsyncDisposable
{
    GraphDocument Document { get; }

    RuntimeStatus Status { get; }

    long TickCount { get; }

    int DiagnosticCapacity { get; }

    IReadOnlyCollection<string> BreakpointNodeIds { get; }

    IReadOnlyList<string> WatchExpressions { get; }

    IReadOnlyList<GraphRuntimeDiagnostic> RecentDiagnostics { get; }

    GraphRuntimeSnapshot CurrentSnapshot { get; }

    event EventHandler<GraphRuntimeSnapshot>? SnapshotChanged;

    event EventHandler<GraphRuntimeBreakpointHitEventArgs>? BreakpointHit;

    event EventHandler? BreakpointsChanged;

    event EventHandler? WatchesChanged;

    event EventHandler<GraphRuntimeDiagnosticEventArgs>? DiagnosticEmitted;

    event EventHandler? DiagnosticsChanged;

    bool AddBreakpoint(string nodeId);

    bool RemoveBreakpoint(string nodeId);

    void ClearBreakpoints();

    ValueTask<bool> AddWatchAsync(
        string expression,
        CancellationToken cancellationToken = default);

    bool RemoveWatch(string expression);

    void ClearWatches();

    void ClearDiagnostics();

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask PauseAsync(CancellationToken cancellationToken = default);

    ValueTask StepAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);

    ValueTask ResetAsync(CancellationToken cancellationToken = default);

    ValueTask RefreshAsync(CancellationToken cancellationToken = default);
}
