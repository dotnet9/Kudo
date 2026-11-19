using Kudo.Core.Algorithms;
using Kudo.Avalonia.Designer;

namespace Kudo.Avalonia;

internal sealed class GraphElementSpatialIndex
{
    private IGraphNodeGeometryProvider _geometryProvider =
        DefaultGraphNodeGeometryProvider.Instance;
    private readonly List<IndexedNode> _nodeCandidates = [];
    private readonly List<IndexedGroup> _groupCandidates = [];
    private readonly List<NodeModel> _nodeResults = [];
    private readonly List<GroupModel> _groupResults = [];
    private readonly Dictionary<NodeModel, IndexedNode> _indexedNodes =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<GroupModel, IndexedGroup> _indexedGroups =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _changedNodeIds = new(StringComparer.Ordinal);
    private readonly List<NodeIndexUpdate> _nodeUpdates = [];
    private readonly List<GroupIndexUpdate> _groupUpdates = [];
    private GraphSpatialIndex<IndexedNode>? _nodes;
    private GraphSpatialIndex<IndexedGroup>? _groups;
    private bool _isDirty = true;

    public void Invalidate() => _isDirty = true;

    public IGraphNodeGeometryProvider GeometryProvider
    {
        get => _geometryProvider;
        set
        {
            _geometryProvider = value ?? throw new ArgumentNullException(nameof(value));
            Invalidate();
        }
    }

    public void RebuildIfNeeded(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_isDirty)
        {
            return;
        }

        var firstGroupCollapsedByNodeId = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var group in document.Groups)
        {
            foreach (var nodeId in group.NodeIds ?? [])
            {
                firstGroupCollapsedByNodeId.TryAdd(nodeId, group.IsCollapsed);
            }
        }

        var nodes = document.Nodes
            .Select((node, index) => new IndexedNode(node, index, GetBounds(node)))
            .Where(item => !firstGroupCollapsedByNodeId.GetValueOrDefault(item.Node.Id))
            .ToArray();
        var groups = document.Groups
            .Select((group, index) => new
            {
                Group = group,
                Index = index,
                Bounds = GraphGroupBoundsCalculator.Calculate(document, group)
            })
            .Where(item => item.Bounds is not null)
            .Select(item => new IndexedGroup(item.Group, item.Index, item.Bounds!.Value))
            .ToArray();

        _nodes = Build(nodes, item => item.Bounds);
        _groups = Build(groups, item => item.Bounds);
        _indexedNodes.Clear();
        foreach (var node in nodes)
        {
            _indexedNodes.Add(node.Node, node);
        }

        _indexedGroups.Clear();
        foreach (var group in groups)
        {
            _indexedGroups.Add(group.Group, group);
        }

        _isDirty = false;
    }

    public bool TryUpdateNodeGeometry(GraphDocument document, IEnumerable<string> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(nodeIds);
        if (_isDirty)
        {
            return false;
        }

        _changedNodeIds.Clear();
        _changedNodeIds.UnionWith(nodeIds);
        if (_changedNodeIds.Count == 0)
        {
            return true;
        }

        if (_changedNodeIds.Count > Math.Max(32, document.Nodes.Count / 4))
        {
            Invalidate();
            return false;
        }

        _nodeUpdates.Clear();
        for (var index = 0; index < document.Nodes.Count; index++)
        {
            var node = document.Nodes[index];
            if (!_changedNodeIds.Contains(node.Id))
            {
                continue;
            }

            _indexedNodes.TryGetValue(node, out var previous);
            var current = IsNodeHidden(document, node.Id)
                ? null
                : new IndexedNode(node, index, GetBounds(node));
            if (!CanReplace(_nodes, previous, current, item => item.Index, item => item.Bounds))
            {
                Invalidate();
                return false;
            }

            _nodeUpdates.Add(new NodeIndexUpdate(previous, current));
        }

        _groupUpdates.Clear();
        for (var index = 0; index < document.Groups.Count; index++)
        {
            var group = document.Groups[index];
            if (!(group.NodeIds ?? []).Any(_changedNodeIds.Contains))
            {
                continue;
            }

            _indexedGroups.TryGetValue(group, out var previous);
            var bounds = GraphGroupBoundsCalculator.Calculate(document, group);
            var current = bounds is { } value
                ? new IndexedGroup(group, index, value)
                : null;
            if (!CanReplace(_groups, previous, current, item => item.Index, item => item.Bounds))
            {
                Invalidate();
                return false;
            }

            _groupUpdates.Add(new GroupIndexUpdate(previous, current));
        }

        foreach (var update in _nodeUpdates)
        {
            if (update.Previous is { } previous)
            {
                _nodes!.Remove(previous, previous.Bounds);
                _indexedNodes.Remove(previous.Node);
            }

            if (update.Current is { } current)
            {
                _nodes!.Insert(current, current.Bounds);
                _indexedNodes[current.Node] = current;
            }
        }

        foreach (var update in _groupUpdates)
        {
            if (update.Previous is { } previous)
            {
                _groups!.Remove(previous, previous.Bounds);
                _indexedGroups.Remove(previous.Group);
            }

            if (update.Current is { } current)
            {
                _groups!.Insert(current, current.Bounds);
                _indexedGroups[current.Group] = current;
            }
        }

        return true;
    }

    public NodeModel? HitTestNode(GraphPoint point)
    {
        _nodeCandidates.Clear();
        _nodes?.Query(new GraphRect(point.X, point.Y, 0, 0), _nodeCandidates);
        IndexedNode? best = null;
        foreach (var candidate in _nodeCandidates)
        {
            if (_geometryProvider.Contains(candidate.Node, point) &&
                (best is null || candidate.Index >= best.Index))
            {
                best = candidate;
            }
        }

        return best?.Node;
    }

    public GroupModel? HitTestGroup(GraphPoint point)
    {
        _groupCandidates.Clear();
        _groups?.Query(new GraphRect(point.X, point.Y, 0, 0), _groupCandidates);
        IndexedGroup? best = null;
        foreach (var candidate in _groupCandidates)
        {
            if (best is null || candidate.Index >= best.Index)
            {
                best = candidate;
            }
        }

        return best?.Group;
    }

    public void AddIntersectingNodeIds(GraphRect bounds, ISet<string> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        _nodeCandidates.Clear();
        _nodes?.Query(bounds, _nodeCandidates);
        foreach (var candidate in _nodeCandidates)
        {
            nodeIds.Add(candidate.Node.Id);
        }

        _groupCandidates.Clear();
        _groups?.Query(bounds, _groupCandidates);
        foreach (var candidate in _groupCandidates)
        {
            if (candidate.Group.IsCollapsed)
            {
                nodeIds.UnionWith(candidate.Group.NodeIds ?? []);
            }
        }
    }

    public IReadOnlyList<NodeModel> QueryNodes(GraphRect bounds)
    {
        _nodeCandidates.Clear();
        _nodes?.Query(bounds, _nodeCandidates);
        _nodeCandidates.Sort((first, second) => first.Index.CompareTo(second.Index));
        _nodeResults.Clear();
        foreach (var candidate in _nodeCandidates)
        {
            _nodeResults.Add(candidate.Node);
        }

        return _nodeResults;
    }

    public IReadOnlyList<GroupModel> QueryGroups(GraphRect bounds)
    {
        _groupCandidates.Clear();
        _groups?.Query(bounds, _groupCandidates);
        _groupCandidates.Sort((first, second) => first.Index.CompareTo(second.Index));
        _groupResults.Clear();
        foreach (var candidate in _groupCandidates)
        {
            _groupResults.Add(candidate.Group);
        }

        return _groupResults;
    }

    private static GraphSpatialIndex<T>? Build<T>(
        IReadOnlyList<T> items,
        Func<T, GraphRect> boundsSelector)
    {
        if (items.Count == 0)
        {
            return null;
        }

        var bounds = boundsSelector(items[0]);
        for (var index = 1; index < items.Count; index++)
        {
            bounds = Union(bounds, boundsSelector(items[index]));
        }

        var result = new GraphSpatialIndex<T>(EnsureArea(bounds));
        foreach (var item in items)
        {
            result.Insert(item, boundsSelector(item));
        }

        return result;
    }

    private static bool CanReplace<T>(
        GraphSpatialIndex<T>? index,
        T? previous,
        T? current,
        Func<T, int> indexSelector,
        Func<T, GraphRect> boundsSelector)
        where T : class
    {
        if (previous is null || current is null)
        {
            return previous is null && current is null;
        }

        return index is not null &&
               indexSelector(previous) == indexSelector(current) &&
               index.Contains(boundsSelector(current));
    }

    private static bool IsNodeHidden(GraphDocument document, string nodeId)
    {
        foreach (var group in document.Groups)
        {
            if ((group.NodeIds ?? []).Contains(nodeId, StringComparer.Ordinal))
            {
                return group.IsCollapsed;
            }
        }

        return false;
    }

    private GraphRect GetBounds(NodeModel node) => _geometryProvider.GetBounds(node);

    private static GraphRect Union(GraphRect first, GraphRect second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        var right = Math.Max(first.X + first.Width, second.X + second.Width);
        var bottom = Math.Max(first.Y + first.Height, second.Y + second.Height);
        return new GraphRect(left, top, right - left, bottom - top);
    }

    private static GraphRect EnsureArea(GraphRect bounds) => new(
        bounds.Width > 0 ? bounds.X : bounds.X - 0.5,
        bounds.Height > 0 ? bounds.Y : bounds.Y - 0.5,
        bounds.Width > 0 ? bounds.Width : 1,
        bounds.Height > 0 ? bounds.Height : 1);

    private sealed record IndexedNode(NodeModel Node, int Index, GraphRect Bounds);

    private sealed record IndexedGroup(GroupModel Group, int Index, GraphRect Bounds);

    private readonly record struct NodeIndexUpdate(IndexedNode? Previous, IndexedNode? Current);

    private readonly record struct GroupIndexUpdate(IndexedGroup? Previous, IndexedGroup? Current);
}
