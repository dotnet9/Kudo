// 节点控件注册表：支持宿主常驻注册和扩展模块可释放的临时注册。
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Kudo;
using Kudo.Core.Runtime;

namespace Kudo.Avalonia;

public sealed class GraphNodeControlContext : INotifyPropertyChanged
{
    private bool _isControlVisible;

    internal GraphNodeControlContext(GraphCanvas canvas, NodeModel node)
    {
        Canvas = canvas;
        Node = node;
        RefreshState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GraphCanvas Canvas { get; }

    public NodeModel Node { get; }

    public bool IsSelected { get; private set; }

    public bool HasBreakpoint { get; private set; }

    public GraphRuntimeNodeState? RuntimeState { get; private set; }

    public bool IsControlVisible
    {
        get => _isControlVisible;
        private set
        {
            if (_isControlVisible == value)
            {
                return;
            }

            _isControlVisible = value;
            OnPropertyChanged();
        }
    }

    internal void RefreshState()
    {
        IsSelected = Canvas.IsNodeSelected(Node.Id);
        HasBreakpoint = Canvas.HasBreakpoint(Node.Id);
        RuntimeState = Canvas.GetRuntimeNodeState(Node.Id);
        OnPropertyChanged(string.Empty);
    }

    internal void SetControlVisible(bool isVisible) => IsControlVisible = isVisible;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class GraphNodeControlRegistry
{
    private readonly Dictionary<string, Func<GraphNodeControlContext, Control>> _factories =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ScopedRegistration>> _scopedFactories =
        new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? Changed;

    internal event EventHandler<GraphNodeControlRegistryChangedEventArgs>? RegistrationsChanged;

    public IEnumerable<string> RegisteredNodeTypes => _factories.Keys
        .Concat(_scopedFactories.Keys)
        .Distinct(StringComparer.OrdinalIgnoreCase);

    public void Register(string nodeType, Func<GraphNodeControlContext, Control> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        ArgumentNullException.ThrowIfNull(factory);
        _factories[nodeType] = factory;
        RaiseChanged([nodeType]);
    }

    /// <summary>
    /// 注册可释放的节点控件工厂；后注册项优先，释放后自动恢复之前的工厂。
    /// </summary>
    public IDisposable RegisterScoped(
        string nodeType,
        Func<GraphNodeControlContext, Control> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        ArgumentNullException.ThrowIfNull(factory);
        var registration = new ScopedRegistration(this, nodeType, factory);
        if (!_scopedFactories.TryGetValue(nodeType, out var registrations))
        {
            registrations = [];
            _scopedFactories.Add(nodeType, registrations);
        }

        registrations.Add(registration);
        RaiseChanged([nodeType]);
        return registration;
    }

    public bool Unregister(string nodeType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        var changed = _factories.Remove(nodeType);
        if (_scopedFactories.Remove(nodeType, out var scoped))
        {
            changed = true;
            foreach (var registration in scoped)
            {
                registration.Detach();
            }
        }

        if (!changed)
        {
            return false;
        }

        RaiseChanged([nodeType]);
        return true;
    }

    public bool IsRegistered(string? nodeType) =>
        !string.IsNullOrWhiteSpace(nodeType) &&
        (_factories.ContainsKey(nodeType) || _scopedFactories.ContainsKey(nodeType));

    public void Clear()
    {
        if (_factories.Count == 0 && _scopedFactories.Count == 0)
        {
            return;
        }

        var nodeTypes = RegisteredNodeTypes.ToArray();
        _factories.Clear();
        foreach (var registration in _scopedFactories.Values.SelectMany(value => value))
        {
            registration.Detach();
        }

        _scopedFactories.Clear();
        RaiseChanged(nodeTypes);
    }

    internal bool TryCreate(
        GraphCanvas canvas,
        NodeModel node,
        out GraphNodeControlContext? context,
        out Control? control)
    {
        Func<GraphNodeControlContext, Control>? factory = null;
        if (_scopedFactories.TryGetValue(node.Type, out var registrations) && registrations.Count > 0)
        {
            factory = registrations[^1].Factory;
        }
        else
        {
            _factories.TryGetValue(node.Type, out factory);
        }

        if (factory is null)
        {
            context = null;
            control = null;
            return false;
        }

        context = new GraphNodeControlContext(canvas, node);
        control = factory(context) ??
            throw new InvalidOperationException(
                $"The control factory for node type '{node.Type}' returned null.");
        return true;
    }

    private void RaiseChanged(IReadOnlyList<string> nodeTypes)
    {
        RegistrationsChanged?.Invoke(
            this,
            new GraphNodeControlRegistryChangedEventArgs(nodeTypes));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveScoped(ScopedRegistration registration)
    {
        if (!_scopedFactories.TryGetValue(registration.NodeType, out var registrations) ||
            !registrations.Remove(registration))
        {
            return;
        }

        if (registrations.Count == 0)
        {
            _scopedFactories.Remove(registration.NodeType);
        }

        RaiseChanged([registration.NodeType]);
    }

    private sealed class ScopedRegistration(
        GraphNodeControlRegistry owner,
        string nodeType,
        Func<GraphNodeControlContext, Control> factory) : IDisposable
    {
        private GraphNodeControlRegistry? _owner = owner;

        public string NodeType { get; } = nodeType;

        public Func<GraphNodeControlContext, Control> Factory { get; } = factory;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.RemoveScoped(this);

        public void Detach() => _owner = null;
    }
}

internal sealed class GraphNodeControlRegistryChangedEventArgs(
    IReadOnlyList<string> nodeTypes) : EventArgs
{
    public IReadOnlyList<string> NodeTypes { get; } = nodeTypes;
}
