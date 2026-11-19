// 模型变更描述：以结构化集合表达一次写入影响的元素和缓存范围。
using System.ComponentModel;

namespace Kudo;

[Flags]
public enum GraphChangeKind
{
    [Description("无变更")]
    None = 0,

    [Description("文档元数据")]
    Document = 1 << 0,

    [Description("结构变更")]
    Structure = 1 << 1,

    [Description("几何变更")]
    Geometry = 1 << 2,

    [Description("路由变更")]
    Routing = 1 << 3,

    [Description("外观变更")]
    Appearance = 1 << 4,

    [Description("扩展属性变更")]
    Properties = 1 << 5,

    [Description("画布设置变更")]
    Settings = 1 << 6,

    [Description("需要全量同步")]
    FullRefresh = 1 << 30
}

/// <summary>
/// 描述同一种元素在一次编辑中的新增、删除和修改集合。
/// </summary>
public sealed record GraphElementChangeSet(
    IReadOnlyList<string> AddedIds,
    IReadOnlyList<string> RemovedIds,
    IReadOnlyList<string> ModifiedIds,
    bool RequiresReset = false)
{
    public static GraphElementChangeSet Empty { get; } = new([], [], []);

    public static GraphElementChangeSet Reset { get; } = new([], [], [], true);

    public bool IsEmpty =>
        !RequiresReset && AddedIds.Count == 0 && RemovedIds.Count == 0 && ModifiedIds.Count == 0;

    public IReadOnlyList<string> AffectedIds => AddedIds
        .Concat(RemovedIds)
        .Concat(ModifiedIds)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    internal static GraphElementChangeSet Added(string id, bool reverse) =>
        reverse ? Removed(id, false) : new([id], [], []);

    internal static GraphElementChangeSet Removed(string id, bool reverse) =>
        reverse ? Added(id, false) : new([], [id], []);

    internal static GraphElementChangeSet Modified(string id) =>
        new([], [], Normalize([id]));

    internal static GraphElementChangeSet Modified(IEnumerable<string> ids) =>
        new([], [], Normalize(ids));

    internal GraphElementChangeSet Merge(GraphElementChangeSet other)
    {
        if (RequiresReset || other.RequiresReset)
        {
            return Reset;
        }

        return new(
            Normalize(AddedIds.Concat(other.AddedIds)),
            Normalize(RemovedIds.Concat(other.RemovedIds)),
            Normalize(ModifiedIds.Concat(other.ModifiedIds)));
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> ids) => ids
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}

/// <summary>
/// 一次文档写入产生的聚合变更，可由 Canvas、会话和缓存层共同消费。
/// </summary>
public sealed record GraphChangeSet(
    GraphChangeKind Kind,
    GraphElementChangeSet Nodes,
    GraphElementChangeSet Edges,
    GraphElementChangeSet Groups,
    GraphElementChangeSet Annotations)
{
    public static GraphChangeSet Empty { get; } = new(
        GraphChangeKind.None,
        GraphElementChangeSet.Empty,
        GraphElementChangeSet.Empty,
        GraphElementChangeSet.Empty,
        GraphElementChangeSet.Empty);

    public static GraphChangeSet FullRefresh { get; } = new(
        GraphChangeKind.FullRefresh,
        GraphElementChangeSet.Reset,
        GraphElementChangeSet.Reset,
        GraphElementChangeSet.Reset,
        GraphElementChangeSet.Reset);

    public bool IsEmpty => Kind == GraphChangeKind.None &&
                           Nodes.IsEmpty &&
                           Edges.IsEmpty &&
                           Groups.IsEmpty &&
                           Annotations.IsEmpty;

    public bool RequiresFullRefresh => Kind.HasFlag(GraphChangeKind.FullRefresh);

    /// <summary>
    /// 根据内置命令生成变更范围；未知扩展命令返回全量刷新范围。
    /// </summary>
    public static GraphChangeSet ForCommand(IGraphCommand command, bool undo = false) =>
        GraphCommandChangeSetFactory.Create(command, undo);

    public GraphChangeSet Merge(GraphChangeSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (RequiresFullRefresh || other.RequiresFullRefresh)
        {
            return FullRefresh;
        }

        return new(
            Kind | other.Kind,
            Nodes.Merge(other.Nodes),
            Edges.Merge(other.Edges),
            Groups.Merge(other.Groups),
            Annotations.Merge(other.Annotations));
    }
}
