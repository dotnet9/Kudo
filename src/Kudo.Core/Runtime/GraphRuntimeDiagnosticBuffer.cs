// 诊断缓冲区：以固定容量保存最近记录，避免长时间运行导致内存持续增长。
namespace Kudo.Core.Runtime;

internal sealed class GraphRuntimeDiagnosticBuffer(int capacity)
{
    private readonly Queue<GraphRuntimeDiagnostic> _items = [];

    public int Count => _items.Count;

    public IReadOnlyList<GraphRuntimeDiagnostic> Snapshot() => _items.ToArray();

    public void Add(GraphRuntimeDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _items.Enqueue(diagnostic);
        while (_items.Count > capacity)
        {
            _items.Dequeue();
        }
    }

    public void Clear() => _items.Clear();
}
