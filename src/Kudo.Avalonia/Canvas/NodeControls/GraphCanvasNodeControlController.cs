using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Kudo;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasNodeControlController
{
    private readonly GraphCanvasNodeControlContext _context;
    private readonly GraphCanvasNodeControlLayout _layout;
    private readonly Dictionary<string, GraphNodeControlHost> _hosts = new(StringComparer.Ordinal);

    public GraphCanvasNodeControlController(GraphCanvasNodeControlContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _layout = new GraphCanvasNodeControlLayout(_context);
        _context.NodeControls.RegistrationsChanged += HandleRegistrationsChanged;
    }

    public Rect GetScreenRect(NodeModel node) => _layout.GetScreenRect(node);

    public void Synchronize()
    {
        var nodes = _context.Document()?.Nodes ?? [];
        var eligibleNodes = new Dictionary<string, (NodeModel Node, int Index)>(StringComparer.Ordinal);
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (_context.NodeControls.IsRegistered(node.Type))
            {
                eligibleNodes[node.Id] = (node, index);
            }
        }

        foreach (var nodeId in _hosts.Keys.Where(nodeId =>
                     !eligibleNodes.TryGetValue(nodeId, out var eligible) ||
                     !ReferenceEquals(_hosts[nodeId].Context.Node, eligible.Node) ||
                     !string.Equals(
                         _hosts[nodeId].RegisteredNodeType,
                         eligible.Node.Type,
                         StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            RemoveHost(nodeId);
        }

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (!eligibleNodes.TryGetValue(node.Id, out var eligible) ||
                !ReferenceEquals(eligible.Node, node))
            {
                continue;
            }

            if (_hosts.TryGetValue(node.Id, out var host))
            {
                host.ZIndex = index;
            }
            else
            {
                AddHost(node, index);
            }
        }

        RefreshStates();
        UpdateLayout();
    }

    public void Synchronize(IReadOnlyList<string> nodeTypes)
    {
        if (nodeTypes.Count == 0)
        {
            return;
        }

        var affectedTypes = nodeTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var nodeId in _hosts
                     .Where(pair => affectedTypes.Contains(pair.Value.RegisteredNodeType))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            RemoveHost(nodeId);
        }

        var nodes = _context.Document()?.Nodes ?? [];
        var eligibleNodes = new Dictionary<string, (NodeModel Node, int Index)>(StringComparer.Ordinal);
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (!affectedTypes.Contains(node.Type) || !_context.NodeControls.IsRegistered(node.Type))
            {
                continue;
            }

            eligibleNodes[node.Id] = (node, index);
        }

        var addedNodeIds = new List<string>(eligibleNodes.Count);
        foreach (var eligible in eligibleNodes.Values.OrderBy(item => item.Index))
        {
            if (_hosts.ContainsKey(eligible.Node.Id) ||
                !AddHost(eligible.Node, eligible.Index))
            {
                continue;
            }

            addedNodeIds.Add(eligible.Node.Id);
        }

        UpdateLayout(addedNodeIds);
    }

    public void UpdateLayout() => _layout.Update(_hosts);

    public void UpdateLayout(IEnumerable<string> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        _layout.Update(_hosts, nodeIds);
    }

    public void UpdateLayout(string nodeId) => _layout.Update(_hosts, nodeId);

    public void RefreshStates()
    {
        foreach (var host in _hosts.Values)
        {
            host.Context.RefreshState();
        }
    }

    public GraphNodeControlHost? FindHost(object? source)
    {
        var visual = source as Visual;
        while (visual is not null && !ReferenceEquals(visual, _context.Owner))
        {
            if (visual is GraphNodeControlHost host)
            {
                return host;
            }

            visual = visual.GetVisualParent();
        }

        return null;
    }

    private void HandleRegistrationsChanged(
        object? sender,
        GraphNodeControlRegistryChangedEventArgs e)
    {
        Synchronize(e.NodeTypes);
        _context.InvalidateVisual();
    }

    private bool AddHost(NodeModel node, int index)
    {
        if (!_context.NodeControls.TryCreate(_context.Owner, node, out var controlContext, out var control) ||
            controlContext is null ||
            control is null)
        {
            return false;
        }

        var host = new GraphNodeControlHost(node.Type, controlContext, control)
        {
            IsVisible = false,
            ZIndex = index
        };
        host.AddHandler(
            InputElement.PointerPressedEvent,
            HandlePointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _hosts.Add(node.Id, host);
        _context.NodeControlLayer.Children.Add(host);
        return true;
    }

    private void RemoveHost(string nodeId)
    {
        var host = _hosts[nodeId];
        _layout.Remove(host);
        _context.NodeControlLayer.Children.Remove(host);
        _hosts.Remove(nodeId);
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not GraphNodeControlHost host ||
            !e.GetCurrentPoint(_context.Owner).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _context.ToggleNodeSelection(host.Context.Node.Id);
        }
        else if (!_context.IsNodeSelected(host.Context.Node.Id))
        {
            _context.SetSelectedNodes([host.Context.Node.Id], host.Context.Node.Id);
        }
    }

}

internal sealed class GraphCanvasNodeControlContext
{
    public required GraphCanvas Owner { get; init; }

    public required Canvas NodeControlLayer { get; init; }

    public required GraphNodeControlRegistry NodeControls { get; init; }

    public required Func<GraphDocument?> Document { get; init; }

    public required Func<GraphViewport> Viewport { get; init; }

    public required Func<bool> IsViewportCullingEnabled { get; init; }

    public required Func<GraphRect?> GetViewportCullingGraphBounds { get; init; }

    public required GraphElementSpatialIndex ElementSpatialIndex { get; init; }

    public required Func<string, bool> IsNodeVisible { get; init; }

    public required Func<NodeModel, Rect> GetNodeScreenRect { get; init; }

    public required Func<NodeModel, Rect> GetNodeGraphRect { get; init; }

    public required Func<string, bool> IsNodeSelected { get; init; }

    public required Func<string, bool> ToggleNodeSelection { get; init; }

    public required Action<IEnumerable<string>, string?> SetSelectedNodes { get; init; }

    public required Action InvalidateVisual { get; init; }
}
