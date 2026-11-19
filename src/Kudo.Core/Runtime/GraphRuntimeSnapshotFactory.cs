// 运行时快照工厂：集中处理状态迁移、规范化和断点快照，保证各命令使用同一语义。
namespace Kudo.Core.Runtime;

internal static class GraphRuntimeSnapshotFactory
{
    public static GraphRuntimeSnapshot CreateTransition(
        RuntimeStatus status,
        GraphRuntimeSnapshot current) =>
        new()
        {
            Status = status,
            TickCount = current.TickCount,
            ActiveNodeId = status == RuntimeStatus.Stopping ? null : current.ActiveNodeId,
            Timestamp = current.Timestamp,
            Message = status switch
            {
                RuntimeStatus.Starting => "Runtime starting.",
                RuntimeStatus.Paused => "Runtime paused.",
                RuntimeStatus.Stopping => "Runtime stopping.",
                _ => current.Message
            },
            Nodes = status is RuntimeStatus.Paused or RuntimeStatus.Stopping
                ? current.Nodes.Select(node => node.Status == RuntimeStatus.Faulted
                    ? node
                    : node with { Status = status }).ToArray()
                : current.Nodes,
            Edges = status == RuntimeStatus.Stopping
                ? current.Edges.Select(edge => edge with { IsActive = false }).ToArray()
                : current.Edges,
            Signals = status == RuntimeStatus.Stopping ? [] : current.Signals,
            CallStack = status == RuntimeStatus.Stopping ? [] : current.CallStack,
            Variables = status == RuntimeStatus.Stopping ? [] : current.Variables,
            WatchResults = status == RuntimeStatus.Stopping ? [] : current.WatchResults
        };

    public static GraphRuntimeSnapshot Normalize(
        GraphRuntimeSnapshot? snapshot,
        RuntimeStatus status,
        long tickCount) =>
        new()
        {
            Status = status,
            TickCount = tickCount,
            ActiveNodeId = snapshot?.ActiveNodeId,
            Timestamp = snapshot?.Timestamp ?? DateTimeOffset.UtcNow,
            Message = snapshot?.Message,
            Nodes = status == RuntimeStatus.Paused
                ? snapshot?.Nodes
                    .Select(node => node.Status == RuntimeStatus.Faulted
                        ? node
                        : node with { Status = RuntimeStatus.Paused })
                    .ToArray() ?? []
                : snapshot?.Nodes ?? [],
            Edges = snapshot?.Edges ?? [],
            Signals = snapshot?.Signals ?? [],
            CallStack = snapshot?.CallStack ?? [],
            Variables = snapshot?.Variables ?? [],
            WatchResults = snapshot?.WatchResults ?? []
        };

    public static GraphRuntimeSnapshot CreateBreakpoint(
        GraphRuntimeSnapshot current,
        string nodeId) =>
        new()
        {
            Status = RuntimeStatus.Paused,
            TickCount = current.TickCount,
            ActiveNodeId = nodeId,
            Timestamp = current.Timestamp,
            Message = $"Breakpoint hit at node '{nodeId}'.",
            Nodes = current.Nodes
                .Select(node => node with
                {
                    Status = node.Status == RuntimeStatus.Faulted
                        ? RuntimeStatus.Faulted
                        : RuntimeStatus.Paused,
                    Message = node.NodeId == nodeId ? "Breakpoint hit." : node.Message
                })
                .ToArray(),
            Edges = current.Edges,
            Signals = current.Signals,
            CallStack = current.CallStack,
            Variables = current.Variables,
            WatchResults = current.WatchResults
        };

    public static GraphRuntimeSnapshot Copy(
        GraphRuntimeSnapshot snapshot,
        IReadOnlyList<GraphRuntimeWatchResult> watchResults) =>
        new()
        {
            Status = snapshot.Status,
            TickCount = snapshot.TickCount,
            ActiveNodeId = snapshot.ActiveNodeId,
            Timestamp = snapshot.Timestamp,
            Message = snapshot.Message,
            Nodes = snapshot.Nodes,
            Edges = snapshot.Edges,
            Signals = snapshot.Signals,
            CallStack = snapshot.CallStack,
            Variables = snapshot.Variables,
            WatchResults = watchResults
        };
}
