using Kudo.Core.Runtime;

namespace Kudo.Demo;

public sealed class DemoGraphRuntimeStateProvider : IGraphRuntimeResettableStateProvider
{
    private readonly IDemoGraphTextProvider? _textProvider;
    private long _refreshVersion;

    public DemoGraphRuntimeStateProvider(IDemoGraphTextProvider? textProvider = null) =>
        _textProvider = textProvider;

    public ValueTask<GraphRuntimeSnapshot> GetSnapshotAsync(
        GraphDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var timestamp = DateTimeOffset.UtcNow;
        var refreshVersion = Interlocked.Increment(ref _refreshVersion);
        var activeNodeId = document.Nodes.Count == 0
            ? null
            : document.Nodes[(int)((refreshVersion - 1) % document.Nodes.Count)].Id;
        var activeNode = activeNodeId is null ? null : document.FindNode(activeNodeId);
        return ValueTask.FromResult(new GraphRuntimeSnapshot
        {
            Status = RuntimeStatus.Running,
            ActiveNodeId = activeNodeId,
            Timestamp = timestamp,
            Message = F(DemoGraphTextKey.RefreshMessage, refreshVersion),
            Nodes = document.Nodes
                .Select(node => new GraphRuntimeNodeState(
                    node.Id,
                    RuntimeStatus.Running,
                    F(DemoGraphTextKey.NodeRefresh, refreshVersion)))
                .ToArray(),
            Edges = document.Edges
                .Select((edge, index) => new GraphRuntimeEdgeState(
                    edge.Id,
                    IsActive: true,
                    Value: (refreshVersion * 10 + index) % 100))
                .ToArray(),
            Signals = document.Edges
                .Select((edge, index) => new RuntimeSignal(
                    edge.Id,
                    (refreshVersion * 10 + index) % 100,
                    timestamp))
                .ToArray(),
            CallStack = activeNode is null
                ? []
                : [new GraphRuntimeStackFrame(
                    activeNode.Id,
                    string.IsNullOrWhiteSpace(activeNode.Name) ? activeNode.Id : activeNode.Name,
                    L(DemoGraphTextKey.RootCanvas))],
            Variables = CreateVariables(document, activeNode, refreshVersion)
        });
    }

    public ValueTask ResetAsync(
        GraphDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _refreshVersion, 0);
        return ValueTask.CompletedTask;
    }

    private GraphRuntimeVariable[] CreateVariables(
        GraphDocument document,
        NodeModel? activeNode,
        long refreshVersion)
    {
        var variables = new List<GraphRuntimeVariable>
        {
            new("Tick", refreshVersion, nameof(Int64), "Runtime"),
            new(L(DemoGraphTextKey.VariableNodeCount), document.Nodes.Count, nameof(Int32), "Document"),
            new(L(DemoGraphTextKey.VariableEdgeCount), document.Edges.Count, nameof(Int32), "Document")
        };
        if (activeNode is not null)
        {
            variables.Add(new GraphRuntimeVariable(
                L(DemoGraphTextKey.VariableCurrentNode),
                    string.IsNullOrWhiteSpace(activeNode.Name) ? activeNode.Id : activeNode.Name,
                nameof(String),
                L(DemoGraphTextKey.VariableCurrentFrame),
                activeNode.Id));
        }

        return [.. variables];
    }

    private string L(DemoGraphTextKey key) => DemoGraphText.Get(_textProvider, key);

    private string F(DemoGraphTextKey key, params object?[] arguments) =>
        DemoGraphText.Format(_textProvider, key, arguments);
}
