namespace Kudo.Avalonia;

internal sealed class GraphSpatialIndex<T>
{
    private readonly Node _root;

    public GraphSpatialIndex(GraphRect bounds, int nodeCapacity = 8, int maximumDepth = 12)
    {
        if (nodeCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeCapacity));
        }

        if (maximumDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        }

        _root = new Node(Normalize(bounds), 0, nodeCapacity, maximumDepth);
    }

    public void Insert(T item, GraphRect bounds)
    {
        var normalizedBounds = Normalize(bounds);
        if (Intersects(_root.Bounds, normalizedBounds))
        {
            _root.Insert(new Entry(item, normalizedBounds));
        }
    }

    public bool Contains(GraphRect bounds) => Contains(_root.Bounds, Normalize(bounds));

    public bool Remove(T item, GraphRect bounds) =>
        _root.Remove(item, Normalize(bounds), EqualityComparer<T>.Default);

    public void Query(GraphRect bounds, List<T> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        _root.Query(Normalize(bounds), results);
    }

    private static GraphRect Normalize(GraphRect rect)
    {
        var left = Math.Min(rect.X, rect.X + rect.Width);
        var top = Math.Min(rect.Y, rect.Y + rect.Height);
        return new GraphRect(left, top, Math.Abs(rect.Width), Math.Abs(rect.Height));
    }

    private static bool Contains(GraphRect outer, GraphRect inner) =>
        inner.X >= outer.X &&
        inner.Y >= outer.Y &&
        inner.X + inner.Width <= outer.X + outer.Width &&
        inner.Y + inner.Height <= outer.Y + outer.Height;

    private static bool Intersects(GraphRect first, GraphRect second) =>
        first.X <= second.X + second.Width &&
        first.X + first.Width >= second.X &&
        first.Y <= second.Y + second.Height &&
        first.Y + first.Height >= second.Y;

    private sealed class Node(
        GraphRect bounds,
        int depth,
        int nodeCapacity,
        int maximumDepth)
    {
        private readonly List<Entry> _entries = [];
        private Node[]? _children;

        public GraphRect Bounds { get; } = bounds;

        public void Insert(Entry entry)
        {
            if (_children is not null && TryGetContainingChild(entry.Bounds, out var child))
            {
                child.Insert(entry);
                return;
            }

            _entries.Add(entry);
            if (_entries.Count > nodeCapacity && depth < maximumDepth && _children is null)
            {
                Subdivide();
            }
        }

        public void Query(GraphRect queryBounds, List<T> results)
        {
            if (!Intersects(Bounds, queryBounds))
            {
                return;
            }

            foreach (var entry in _entries)
            {
                if (Intersects(entry.Bounds, queryBounds))
                {
                    results.Add(entry.Item);
                }
            }

            if (_children is null)
            {
                return;
            }

            foreach (var child in _children)
            {
                child.Query(queryBounds, results);
            }
        }

        public bool Remove(T item, GraphRect itemBounds, IEqualityComparer<T> comparer)
        {
            if (!Intersects(Bounds, itemBounds))
            {
                return false;
            }

            for (var index = 0; index < _entries.Count; index++)
            {
                if (!comparer.Equals(_entries[index].Item, item))
                {
                    continue;
                }

                _entries.RemoveAt(index);
                return true;
            }

            if (_children is null || !TryGetContainingChild(itemBounds, out var child))
            {
                return false;
            }

            return child.Remove(item, itemBounds, comparer);
        }

        private void Subdivide()
        {
            var halfWidth = Bounds.Width / 2;
            var halfHeight = Bounds.Height / 2;
            if (halfWidth <= 0 || halfHeight <= 0)
            {
                return;
            }

            _children =
            [
                new Node(
                    new GraphRect(Bounds.X, Bounds.Y, halfWidth, halfHeight),
                    depth + 1,
                    nodeCapacity,
                    maximumDepth),
                new Node(
                    new GraphRect(Bounds.X + halfWidth, Bounds.Y, halfWidth, halfHeight),
                    depth + 1,
                    nodeCapacity,
                    maximumDepth),
                new Node(
                    new GraphRect(Bounds.X, Bounds.Y + halfHeight, halfWidth, halfHeight),
                    depth + 1,
                    nodeCapacity,
                    maximumDepth),
                new Node(
                    new GraphRect(Bounds.X + halfWidth, Bounds.Y + halfHeight, halfWidth, halfHeight),
                    depth + 1,
                    nodeCapacity,
                    maximumDepth)
            ];

            for (var index = _entries.Count - 1; index >= 0; index--)
            {
                var entry = _entries[index];
                if (!TryGetContainingChild(entry.Bounds, out var child))
                {
                    continue;
                }

                _entries.RemoveAt(index);
                child.Insert(entry);
            }
        }

        private bool TryGetContainingChild(GraphRect entryBounds, out Node child)
        {
            if (_children is not null)
            {
                foreach (var candidate in _children)
                {
                    if (Contains(candidate.Bounds, entryBounds))
                    {
                        child = candidate;
                        return true;
                    }
                }
            }

            child = null!;
            return false;
        }
    }

    private sealed record Entry(T Item, GraphRect Bounds);
}
