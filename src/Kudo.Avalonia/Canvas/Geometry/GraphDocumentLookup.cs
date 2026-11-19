namespace Kudo.Avalonia;

internal sealed class GraphDocumentLookup
{
    private readonly Dictionary<string, NodeModel> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EdgeModel> _edges = new(StringComparer.Ordinal);
    private readonly Dictionary<PortReference, PortModel> _ports = [];
    private readonly HashSet<PortReference> _outgoingConnectedPorts = [];
    private readonly HashSet<PortReference> _incomingConnectedPorts = [];
    private readonly Dictionary<string, GroupModel> _groupsByNodeId = new(StringComparer.Ordinal);
    private GraphDocument? _document;
    private bool _isDirty = true;

    public void Invalidate() => _isDirty = true;

    public void RebuildIfNeeded(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_isDirty && ReferenceEquals(_document, document))
        {
            return;
        }

        _nodes.Clear();
        _edges.Clear();
        _ports.Clear();
        _outgoingConnectedPorts.Clear();
        _incomingConnectedPorts.Clear();
        _groupsByNodeId.Clear();
        foreach (var node in document.Nodes)
        {
            if (!_nodes.TryAdd(node.Id, node))
            {
                continue;
            }

            foreach (var port in node.Ports)
            {
                _ports.TryAdd(new PortReference(node.Id, port.Id), port);
            }
        }

        foreach (var group in document.Groups)
        {
            foreach (var nodeId in group.NodeIds ?? [])
            {
                _groupsByNodeId.TryAdd(nodeId, group);
            }
        }

        foreach (var edge in document.Edges)
        {
            _edges.TryAdd(edge.Id, edge);
            _outgoingConnectedPorts.Add(edge.Source);
            _incomingConnectedPorts.Add(edge.Target);
        }

        _document = document;
        _isDirty = false;
    }

    public NodeModel? FindNode(string nodeId) => _nodes.GetValueOrDefault(nodeId);

    public PortModel? FindPort(PortReference reference) => _ports.GetValueOrDefault(reference);

    public EdgeModel? FindEdge(string edgeId) => _edges.GetValueOrDefault(edgeId);

    public bool HasOutgoingConnection(PortReference reference) => _outgoingConnectedPorts.Contains(reference);

    public bool HasIncomingConnection(PortReference reference) => _incomingConnectedPorts.Contains(reference);

    public GroupModel? FindGroupContainingNode(string nodeId) => _groupsByNodeId.GetValueOrDefault(nodeId);
}
