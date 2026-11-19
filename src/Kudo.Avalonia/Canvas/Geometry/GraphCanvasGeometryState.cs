// 画布几何状态：统一拥有路径缓存、空间索引、障碍修订和文档边界缓存。
using Avalonia;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasGeometryState
{
    public GraphEdgePathCache EdgePathCache { get; } = new();

    public GraphEdgeSpatialIndex EdgeSpatialIndex { get; } = new();

    public GraphElementSpatialIndex ElementSpatialIndex { get; } = new();

    public GraphDocumentLookup DocumentLookup { get; } = new();

    public HashSet<string> ChangedNodeIds { get; } = new(StringComparer.Ordinal);

    public HashSet<string> AffectedEdgeEndpointNodeIds { get; } = new(StringComparer.Ordinal);

    public List<EdgeModel> AffectedEdges { get; } = [];

    public GraphEdgeObstacleSnapshot? DefaultObstacleSnapshot { get; set; }

    public long DefaultObstacleSnapshotRevision { get; set; } = -1;

    public long SmartObstacleRevision { get; set; }

    public Rect? DocumentBounds { get; set; }

    public bool IsDocumentBoundsDirty { get; set; } = true;

    /// <summary>
    /// 使依赖文档结构的缓存全部失效，用于文档切换或无法精确描述的外部修改。
    /// </summary>
    public void InvalidateAll()
    {
        EdgePathCache.Clear();
        DocumentLookup.Invalidate();
        ElementSpatialIndex.Invalidate();
        EdgeSpatialIndex.Invalidate();
        DefaultObstacleSnapshot = null;
        DefaultObstacleSnapshotRevision = -1;
        SmartObstacleRevision++;
        DocumentBounds = null;
        IsDocumentBoundsDirty = true;
    }
}
