// 画布渲染状态：统一保存运行态投影和总览位图缓存，不持有 Document 的编辑权限。
using Avalonia;
using Avalonia.Media.Imaging;
using Kudo.Core.Runtime;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasRenderState
{
    public IReadOnlyDictionary<string, GraphRuntimeNodeState> RuntimeNodeStates { get; private set; } =
        new Dictionary<string, GraphRuntimeNodeState>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, GraphRuntimeEdgeState> RuntimeEdgeStates { get; private set; } =
        new Dictionary<string, GraphRuntimeEdgeState>(StringComparer.Ordinal);

    public double RuntimeFlowProgress { get; set; }

    public bool IsAttachedToVisualTree { get; set; }

    public RenderTargetBitmap? OverviewBitmap { get; set; }

    public GraphDocument? OverviewDocument { get; set; }

    public Rect OverviewGraphBounds { get; set; }

    public double OverviewScale { get; set; }

    public bool IsOverviewDirty { get; set; } = true;

    /// <summary>
    /// 将运行快照投影为按 ID 查询的只读状态，避免渲染热路径重复扫描集合。
    /// </summary>
    public void UpdateRuntimeSnapshot(GraphRuntimeSnapshot? snapshot)
    {
        RuntimeNodeStates = CreateRuntimeStateMap(snapshot?.Nodes ?? [], state => state.NodeId);
        RuntimeEdgeStates = CreateRuntimeStateMap(snapshot?.Edges ?? [], state => state.EdgeId);
    }

    /// <summary>
    /// 丢弃总览缓存；位图由下一次渲染按当前文档和主题重新生成。
    /// </summary>
    public void InvalidateOverview()
    {
        // Avalonia 合成器可能仍在引用当前帧位图，因此这里只释放托管引用。
        OverviewBitmap = null;
        OverviewDocument = null;
        OverviewGraphBounds = default;
        OverviewScale = 0;
        IsOverviewDirty = true;
    }

    private static IReadOnlyDictionary<string, TState> CreateRuntimeStateMap<TState>(
        IEnumerable<TState> states,
        Func<TState, string> idSelector) =>
        states
            .Where(state => !string.IsNullOrWhiteSpace(idSelector(state)))
            .GroupBy(idSelector, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
}
