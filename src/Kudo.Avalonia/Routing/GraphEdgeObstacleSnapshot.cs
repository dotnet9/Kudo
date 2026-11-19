using System.Collections;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphEdgeObstacleSnapshot
{
    private readonly GraphRect[] _obstacles;
    private readonly IReadOnlyDictionary<string, int> _nodeIndices;
    private readonly IReadOnlyDictionary<string, int> _groupIndices;

    public GraphEdgeObstacleSnapshot(
        GraphDocument document,
        GraphRect[] obstacles,
        IReadOnlyDictionary<string, int> nodeIndices,
        IReadOnlyDictionary<string, int> groupIndices)
    {
        Document = document;
        _obstacles = obstacles;
        _nodeIndices = nodeIndices;
        _groupIndices = groupIndices;
    }

    public GraphDocument Document { get; }

    public IReadOnlyList<GraphRect> CreateView(GraphEdgeObstacleRequest request) =>
        new GraphEdgeObstacleList(
            _obstacles,
            FindIndex(_nodeIndices, request.SourceNode.Id),
            FindIndex(_nodeIndices, request.TargetNode.Id),
            FindIndex(_groupIndices, request.SourceCollapsedGroup?.Id),
            FindIndex(_groupIndices, request.TargetCollapsedGroup?.Id));

    public bool TryGetNodeBounds(string nodeId, out GraphRect bounds) =>
        TryGetBounds(_nodeIndices, nodeId, out bounds);

    public bool TryGetGroupBounds(string groupId, out GraphRect bounds) =>
        TryGetBounds(_groupIndices, groupId, out bounds);

    private static int FindIndex(IReadOnlyDictionary<string, int> indices, string? id) =>
        !string.IsNullOrWhiteSpace(id) && indices.TryGetValue(id, out var index)
            ? index
            : -1;

    private bool TryGetBounds(
        IReadOnlyDictionary<string, int> indices,
        string id,
        out GraphRect bounds)
    {
        if (indices.TryGetValue(id, out var index) &&
            index >= 0 &&
            index < _obstacles.Length)
        {
            bounds = _obstacles[index];
            return true;
        }

        bounds = default;
        return false;
    }
}

internal sealed class GraphEdgeObstacleList : IReadOnlyList<GraphRect>
{
    private readonly GraphRect[] _obstacles;
    private readonly int _excluded0;
    private readonly int _excluded1;
    private readonly int _excluded2;
    private readonly int _excluded3;
    private readonly int _excludedCount;

    public GraphEdgeObstacleList(
        GraphRect[] obstacles,
        int excluded0,
        int excluded1,
        int excluded2,
        int excluded3)
    {
        _obstacles = obstacles;
        Span<int> exclusions = stackalloc int[4];
        var count = 0;
        AddExclusion(exclusions, ref count, excluded0);
        AddExclusion(exclusions, ref count, excluded1);
        AddExclusion(exclusions, ref count, excluded2);
        AddExclusion(exclusions, ref count, excluded3);
        exclusions[..count].Sort();
        _excluded0 = count > 0 ? exclusions[0] : -1;
        _excluded1 = count > 1 ? exclusions[1] : -1;
        _excluded2 = count > 2 ? exclusions[2] : -1;
        _excluded3 = count > 3 ? exclusions[3] : -1;
        _excludedCount = count;
    }

    public int Count => _obstacles.Length - _excludedCount;

    public GraphRect this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            if (index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var physicalIndex = index;
            for (var exclusionIndex = 0; exclusionIndex < _excludedCount; exclusionIndex++)
            {
                if (physicalIndex >= GetExclusion(exclusionIndex))
                {
                    physicalIndex++;
                }
            }

            return _obstacles[physicalIndex];
        }
    }

    public IEnumerator<GraphRect> GetEnumerator()
    {
        for (var index = 0; index < _obstacles.Length; index++)
        {
            if (!IsExcluded(index))
            {
                yield return _obstacles[index];
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private bool IsExcluded(int index) =>
        index == _excluded0 ||
        index == _excluded1 ||
        index == _excluded2 ||
        index == _excluded3;

    private int GetExclusion(int index) => index switch
    {
        0 => _excluded0,
        1 => _excluded1,
        2 => _excluded2,
        3 => _excluded3,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    private static void AddExclusion(Span<int> exclusions, ref int count, int index)
    {
        if (index < 0)
        {
            return;
        }

        for (var existing = 0; existing < count; existing++)
        {
            if (exclusions[existing] == index)
            {
                return;
            }
        }

        exclusions[count++] = index;
    }
}
