// 领域扩展机制：显式组合 Catalog、控件、Renderer、属性、校验、输入和编辑策略。
using Avalonia.Controls;
using Kudo.Avalonia;

namespace Kudo.Avalonia.Designer;

public interface IGraphExtensionModule
{
    string Id { get; }

    void Configure(GraphExtensionBuilder builder);
}

public sealed class GraphExtensionBuilder
{
    internal List<GraphNodeDefinition> NodeDefinitions { get; } = [];
    internal List<NodeControlContribution> NodeControls { get; } = [];
    internal List<NodeRendererContribution> NodeRenderers { get; } = [];
    internal List<PortRendererContribution> PortRenderers { get; } = [];
    internal List<EdgeRendererContribution> EdgeRenderers { get; } = [];
    internal List<GroupRendererContribution> GroupRenderers { get; } = [];
    internal List<IGraphBackgroundRenderer> BackgroundRenderers { get; } = [];
    internal List<IGraphPropertyEditorProvider> PropertyEditors { get; } = [];
    internal List<StateProjectionContribution> StateProjections { get; } = [];
    internal List<GeometryContribution> GeometryProviders { get; } = [];
    internal List<CreationConfiguratorContribution> CreationConfigurators { get; } = [];
    internal List<ExportRendererContribution> ExportRenderers { get; } = [];
    internal List<IGraphValidationRule> ValidationRules { get; } = [];
    internal List<IGraphHistoryDescriptionProvider> HistoryDescriptionProviders { get; } = [];
    internal List<IGraphEditPolicy> EditPolicies { get; } = [];
    internal List<InputBehaviorContribution> InputBehaviors { get; } = [];
    internal List<InteractionSessionStarterContribution> InteractionSessionStarters { get; } = [];
    internal Dictionary<string, Func<GraphDocument, IGraphCommand>> CommandFactories { get; } =
        new(StringComparer.Ordinal);

    public GraphExtensionBuilder AddNodeDefinition(GraphNodeDefinition definition)
    {
        NodeDefinitions.Add(definition ?? throw new ArgumentNullException(nameof(definition)));
        return this;
    }

    public GraphExtensionBuilder AddNodeControl(
        string nodeType,
        Func<GraphNodeControlContext, Control> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        ArgumentNullException.ThrowIfNull(factory);
        NodeControls.Add(new NodeControlContribution(nodeType, factory));
        return this;
    }

    public GraphExtensionBuilder AddNodeRenderer(string nodeType, IGraphNodeRenderer renderer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        NodeRenderers.Add(new NodeRendererContribution(
            nodeType,
            renderer ?? throw new ArgumentNullException(nameof(renderer))));
        return this;
    }

    public GraphExtensionBuilder AddPortRenderer(string nodeType, IGraphPortRenderer renderer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        PortRenderers.Add(new PortRendererContribution(
            nodeType,
            renderer ?? throw new ArgumentNullException(nameof(renderer))));
        return this;
    }

    public GraphExtensionBuilder AddEdgeRenderer(
        Func<EdgeModel, bool> predicate,
        IGraphEdgeRenderer renderer)
    {
        EdgeRenderers.Add(new EdgeRendererContribution(
            predicate ?? throw new ArgumentNullException(nameof(predicate)),
            renderer ?? throw new ArgumentNullException(nameof(renderer))));
        return this;
    }

    public GraphExtensionBuilder AddGroupRenderer(
        Func<GroupModel, bool> predicate,
        IGraphGroupRenderer renderer)
    {
        GroupRenderers.Add(new GroupRendererContribution(
            predicate ?? throw new ArgumentNullException(nameof(predicate)),
            renderer ?? throw new ArgumentNullException(nameof(renderer))));
        return this;
    }

    public GraphExtensionBuilder AddBackgroundRenderer(IGraphBackgroundRenderer renderer)
    {
        BackgroundRenderers.Add(renderer ?? throw new ArgumentNullException(nameof(renderer)));
        return this;
    }

    public GraphExtensionBuilder AddPropertyEditor(IGraphPropertyEditorProvider provider)
    {
        PropertyEditors.Add(provider ?? throw new ArgumentNullException(nameof(provider)));
        return this;
    }

    public GraphExtensionBuilder AddPropertyEditor(
        string nodeType,
        IGraphPropertyEditorProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        ArgumentNullException.ThrowIfNull(provider);
        PropertyEditors.Add(new NodeTypePropertyEditorProvider(nodeType, provider));
        return this;
    }

    public GraphExtensionBuilder AddStateProjection(
        string nodeType,
        IGraphNodeStateProjectionProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        StateProjections.Add(new StateProjectionContribution(
            nodeType,
            provider ?? throw new ArgumentNullException(nameof(provider))));
        return this;
    }

    public GraphExtensionBuilder AddGeometryProvider(
        string nodeType,
        IGraphNodeGeometryProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        GeometryProviders.Add(new GeometryContribution(
            nodeType,
            provider ?? throw new ArgumentNullException(nameof(provider))));
        return this;
    }

    public GraphExtensionBuilder AddCreationConfigurator(
        string nodeType,
        IGraphNodeCreationConfigurator configurator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        ArgumentNullException.ThrowIfNull(configurator);
        if (!string.Equals(nodeType, configurator.NodeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Configurator node type '{configurator.NodeType}' does not match registration '{nodeType}'.",
                nameof(configurator));
        }

        CreationConfigurators.Add(new CreationConfiguratorContribution(nodeType, configurator));
        return this;
    }

    public GraphExtensionBuilder AddExportRenderer(
        string nodeType,
        IGraphNodeExportRenderer renderer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeType);
        ArgumentNullException.ThrowIfNull(renderer);
        if (!string.Equals(nodeType, renderer.NodeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Export renderer node type '{renderer.NodeType}' does not match registration '{nodeType}'.",
                nameof(renderer));
        }

        ExportRenderers.Add(new ExportRendererContribution(nodeType, renderer));
        return this;
    }

    public GraphExtensionBuilder AddValidationRule(IGraphValidationRule rule)
    {
        ValidationRules.Add(rule ?? throw new ArgumentNullException(nameof(rule)));
        return this;
    }

    public GraphExtensionBuilder AddHistoryDescriptionProvider(
        IGraphHistoryDescriptionProvider provider)
    {
        HistoryDescriptionProviders.Add(provider ?? throw new ArgumentNullException(nameof(provider)));
        return this;
    }

    public GraphExtensionBuilder AddEditPolicy(IGraphEditPolicy policy)
    {
        EditPolicies.Add(policy ?? throw new ArgumentNullException(nameof(policy)));
        return this;
    }

    public GraphExtensionBuilder AddInputBehavior(IGraphInputBehavior behavior, int priority = 0)
    {
        InputBehaviors.Add(new InputBehaviorContribution(
            behavior ?? throw new ArgumentNullException(nameof(behavior)),
            priority));
        return this;
    }

    public GraphExtensionBuilder AddInteractionSessionStarter(
        IGraphInteractionSessionStarter starter,
        int priority = 0)
    {
        InteractionSessionStarters.Add(new InteractionSessionStarterContribution(
            starter ?? throw new ArgumentNullException(nameof(starter)),
            priority));
        return this;
    }

    public GraphExtensionBuilder AddCommandFactory(
        string key,
        Func<GraphDocument, IGraphCommand> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        if (!CommandFactories.TryAdd(key, factory))
        {
            throw new ArgumentException($"Command factory key '{key}' is duplicated.", nameof(key));
        }

        return this;
    }
}

/// <summary>
/// 保存活动领域模块。注册顺序稳定，后注册的 Renderer 和属性 Provider 具有更高匹配优先级。
/// </summary>
public sealed class GraphExtensionRegistry
{
    private readonly List<ModuleRegistration> _modules = [];
    private int _version;

    public GraphExtensionRegistry() => SpecialSymbols = new GraphSpecialSymbolRegistry(this);

    public event EventHandler? Changed;

    public IReadOnlyList<IGraphExtensionModule> Modules => _modules
        .Select(item => item.Module)
        .ToArray();

    public GraphSpecialSymbolRegistry SpecialSymbols { get; }

    internal int Version => _version;

    public IDisposable Register(IGraphExtensionModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(module.Id);
        if (_modules.Any(item => string.Equals(item.Module.Id, module.Id, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"Extension module ID '{module.Id}' is already registered.", nameof(module));
        }

        var builder = new GraphExtensionBuilder();
        module.Configure(builder);
        ValidateContribution(module.Id, builder);
        var registration = new ModuleRegistration(this, module, builder);
        _modules.Add(registration);
        RaiseChanged();
        return registration;
    }

    /// <summary>
    /// 在保持基础目录分组与图元顺序的前提下，按模块注册顺序追加扩展提供的图元定义。
    /// 只注册行为贡献的模块不会改变目录内容。
    /// </summary>
    public GraphNodeCatalog CreateCatalog(IGraphNodeCatalog? baseCatalog = null)
    {
        var definitions = baseCatalog?.Groups.SelectMany(group => group.Definitions).ToList() ?? [];
        definitions.AddRange(_modules.SelectMany(module => module.Builder.NodeDefinitions));
        return new GraphNodeCatalog(definitions);
    }

    public GraphValidator CreateValidator(IEnumerable<IGraphValidationRule>? baseRules = null) =>
        new((baseRules ?? []).Concat(_modules.SelectMany(module => module.Builder.ValidationRules)));

    public GraphHistoryDescriptionRegistry CreateHistoryDescriptionRegistry(
        IEnumerable<IGraphHistoryDescriptionProvider>? baseProviders = null)
    {
        var registry = new GraphHistoryDescriptionRegistry();
        if (baseProviders is not null)
        {
            foreach (var provider in baseProviders)
            {
                registry.Register(provider);
            }
        }

        foreach (var provider in _modules.SelectMany(module => module.Builder.HistoryDescriptionProviders))
        {
            registry.Register(provider);
        }

        return registry;
    }

    public bool TryCreateCommand(string key, GraphDocument document, out IGraphCommand? command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(document);
        for (var index = _modules.Count - 1; index >= 0; index--)
        {
            if (_modules[index].Builder.CommandFactories.TryGetValue(key, out var factory))
            {
                command = factory(document) ?? throw new InvalidOperationException(
                    $"Command factory '{key}' returned null.");
                return true;
            }
        }

        command = null;
        return false;
    }

    public GraphExtensionApplication ApplyTo(
        GraphCanvas canvas,
        GraphPropertiesView? propertiesView = null) =>
        new(this, canvas ?? throw new ArgumentNullException(nameof(canvas)), propertiesView);

    internal IEnumerable<GraphExtensionBuilder> Builders => _modules.Select(item => item.Builder);

    private void Remove(ModuleRegistration registration)
    {
        if (!_modules.Remove(registration))
        {
            return;
        }

        RaiseChanged();
    }

    private void RaiseChanged()
    {
        unchecked
        {
            _version++;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ValidateContribution(string moduleId, GraphExtensionBuilder builder)
    {
        var duplicateLocalType = builder.NodeDefinitions
            .GroupBy(item => item.NodeType, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateLocalType is not null)
        {
            throw new InvalidOperationException(
                $"Extension module '{moduleId}' contains duplicate node type '{duplicateLocalType}'.");
        }

        var activeTypes = _modules
            .SelectMany(item => item.Builder.NodeDefinitions)
            .Select(item => item.NodeType)
            .ToHashSet(StringComparer.Ordinal);
        var duplicateActiveType = builder.NodeDefinitions
            .Select(item => item.NodeType)
            .FirstOrDefault(activeTypes.Contains);
        if (duplicateActiveType is not null)
        {
            throw new InvalidOperationException(
                $"Node type '{duplicateActiveType}' is already provided by another extension module.");
        }
    }

    private sealed class ModuleRegistration(
        GraphExtensionRegistry owner,
        IGraphExtensionModule module,
        GraphExtensionBuilder builder) : IDisposable
    {
        private GraphExtensionRegistry? _owner = owner;

        public IGraphExtensionModule Module { get; } = module;

        public GraphExtensionBuilder Builder { get; } = builder;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Remove(this);
    }
}

/// <summary>
/// 将 Registry 的当前组合投影到指定 Canvas 和属性面板；释放后恢复宿主原始配置。
/// </summary>
public sealed class GraphExtensionApplication : IDisposable
{
    private readonly GraphExtensionRegistry _registry;
    private readonly GraphCanvas _canvas;
    private readonly GraphPropertiesView? _propertiesView;
    private readonly IGraphBackgroundRenderer _baseBackgroundRenderer;
    private readonly IGraphNodeRenderer _baseNodeRenderer;
    private readonly IGraphPortRenderer _basePortRenderer;
    private readonly IGraphEdgeRenderer _baseEdgeRenderer;
    private readonly IGraphGroupRenderer _baseGroupRenderer;
    private readonly IGraphNodeGeometryProvider _baseNodeGeometryProvider;
    private readonly IGraphEditPolicy _baseEditPolicy;
    private readonly IGraphBackgroundRenderer _compositeBackgroundRenderer;
    private readonly IGraphNodeRenderer _compositeNodeRenderer;
    private readonly IGraphPortRenderer _compositePortRenderer;
    private readonly IGraphEdgeRenderer _compositeEdgeRenderer;
    private readonly IGraphGroupRenderer _compositeGroupRenderer;
    private readonly IGraphEditPolicy _compositeEditPolicy;
    private readonly List<IDisposable> _scopedRegistrations = [];
    private bool _isDisposed;

    internal GraphExtensionApplication(
        GraphExtensionRegistry registry,
        GraphCanvas canvas,
        GraphPropertiesView? propertiesView)
    {
        _registry = registry;
        _canvas = canvas;
        _propertiesView = propertiesView;
        _baseBackgroundRenderer = canvas.BackgroundRenderer;
        _baseNodeRenderer = canvas.NodeRenderer;
        _basePortRenderer = canvas.PortRenderer;
        _baseEdgeRenderer = canvas.EdgeRenderer;
        _baseGroupRenderer = canvas.GroupRenderer;
        _baseNodeGeometryProvider = canvas.NodeGeometryProvider;
        _baseEditPolicy = canvas.EditPolicy;
        _compositeBackgroundRenderer = new ExtensionBackgroundRenderer(registry, _baseBackgroundRenderer);
        _compositeNodeRenderer = new ExtensionNodeRenderer(registry, _baseNodeRenderer);
        _compositePortRenderer = new ExtensionPortRenderer(registry, _basePortRenderer);
        _compositeEdgeRenderer = new ExtensionEdgeRenderer(registry, _baseEdgeRenderer);
        _compositeGroupRenderer = new ExtensionGroupRenderer(registry, _baseGroupRenderer);
        _compositeEditPolicy = new ExtensionEditPolicy(registry, _baseEditPolicy);
        canvas.BackgroundRenderer = _compositeBackgroundRenderer;
        canvas.NodeRenderer = _compositeNodeRenderer;
        canvas.PortRenderer = _compositePortRenderer;
        canvas.EdgeRenderer = _compositeEdgeRenderer;
        canvas.GroupRenderer = _compositeGroupRenderer;
        canvas.NodeGeometryProvider = registry.SpecialSymbols;
        canvas.EditPolicy = _compositeEditPolicy;
        registry.Changed += HandleRegistryChanged;
        RefreshScopedRegistrations();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _registry.Changed -= HandleRegistryChanged;
        ClearScopedRegistrations();
        Restore(
            _compositeBackgroundRenderer,
            _baseBackgroundRenderer,
            () => _canvas.BackgroundRenderer,
            value => _canvas.BackgroundRenderer = value);
        Restore(
            _compositeNodeRenderer,
            _baseNodeRenderer,
            () => _canvas.NodeRenderer,
            value => _canvas.NodeRenderer = value);
        Restore(
            _compositePortRenderer,
            _basePortRenderer,
            () => _canvas.PortRenderer,
            value => _canvas.PortRenderer = value);
        Restore(
            _compositeEdgeRenderer,
            _baseEdgeRenderer,
            () => _canvas.EdgeRenderer,
            value => _canvas.EdgeRenderer = value);
        Restore(
            _compositeGroupRenderer,
            _baseGroupRenderer,
            () => _canvas.GroupRenderer,
            value => _canvas.GroupRenderer = value);
        Restore(
            _registry.SpecialSymbols,
            _baseNodeGeometryProvider,
            () => _canvas.NodeGeometryProvider,
            value => _canvas.NodeGeometryProvider = value);
        Restore(
            _compositeEditPolicy,
            _baseEditPolicy,
            () => _canvas.EditPolicy,
            value => _canvas.EditPolicy = value);
    }

    private void HandleRegistryChanged(object? sender, EventArgs e)
    {
        RefreshScopedRegistrations();
        _canvas.RefreshNodeGeometryProvider();
        _propertiesView?.Refresh(_propertiesView.EditorContext);
    }

    private void RefreshScopedRegistrations()
    {
        ClearScopedRegistrations();
        foreach (var builder in _registry.Builders)
        {
            _scopedRegistrations.AddRange(builder.NodeControls.Select(item =>
                _canvas.NodeControls.RegisterScoped(item.NodeType, item.Factory)));
            _scopedRegistrations.AddRange(builder.InputBehaviors.Select(item =>
                _canvas.InputBehaviors.Register(item.Behavior, item.Priority)));
            _scopedRegistrations.AddRange(builder.InteractionSessionStarters.Select(item =>
                _canvas.InteractionSessionStarters.Register(item.Starter, item.Priority)));
            if (_propertiesView is not null)
            {
                _scopedRegistrations.AddRange(builder.PropertyEditors.Select(
                    _propertiesView.PropertyEditorProviders.Register));
            }
        }
    }

    private void ClearScopedRegistrations()
    {
        for (var index = _scopedRegistrations.Count - 1; index >= 0; index--)
        {
            _scopedRegistrations[index].Dispose();
        }

        _scopedRegistrations.Clear();
    }

    private static void Restore<T>(
        T expected,
        T original,
        Func<T> getCurrent,
        Action<T> restore) where T : class
    {
        // 仅恢复本 Application 安装的对象，避免覆盖宿主在应用期间主动替换的新配置。
        if (ReferenceEquals(getCurrent(), expected))
        {
            restore(original);
        }
    }
}

internal sealed record NodeControlContribution(
    string NodeType,
    Func<GraphNodeControlContext, Control> Factory);

internal sealed record NodeRendererContribution(string NodeType, IGraphNodeRenderer Renderer);

internal sealed record PortRendererContribution(string NodeType, IGraphPortRenderer Renderer);

internal sealed record EdgeRendererContribution(Func<EdgeModel, bool> Predicate, IGraphEdgeRenderer Renderer);

internal sealed record GroupRendererContribution(Func<GroupModel, bool> Predicate, IGraphGroupRenderer Renderer);

internal sealed record InputBehaviorContribution(IGraphInputBehavior Behavior, int Priority);

internal sealed record InteractionSessionStarterContribution(
    IGraphInteractionSessionStarter Starter,
    int Priority);

internal static class GraphExtensionResolver
{
    public static T? ResolveLast<T>(
        IEnumerable<GraphExtensionBuilder> builders,
        Func<GraphExtensionBuilder, IEnumerable<T>> select) where T : class =>
        builders.SelectMany(select).LastOrDefault();
}

internal sealed class ExtensionBackgroundRenderer(
    GraphExtensionRegistry registry,
    IGraphBackgroundRenderer fallback) : IGraphBackgroundRenderer
{
    private int _version = -1;
    private IGraphBackgroundRenderer? _activeRenderer;

    public void Render(in GraphBackgroundRenderContext context) =>
        GetRenderer().Render(context);

    private IGraphBackgroundRenderer GetRenderer()
    {
        if (_version == registry.Version && _activeRenderer is not null)
        {
            return _activeRenderer;
        }

        _activeRenderer = GraphExtensionResolver.ResolveLast(
            registry.Builders,
            builder => builder.BackgroundRenderers) ?? fallback;
        _version = registry.Version;
        return _activeRenderer;
    }
}

internal sealed class ExtensionNodeRenderer(
    GraphExtensionRegistry registry,
    IGraphNodeRenderer fallback) :
    IGraphNodeRenderer,
    IGraphNodeRendererDefaultCompatibility
{
    private int _version = -1;
    private IReadOnlyDictionary<string, IGraphNodeRenderer> _renderers =
        new Dictionary<string, IGraphNodeRenderer>(StringComparer.OrdinalIgnoreCase);

    public void Render(in GraphNodeRenderContext context)
    {
        var renderers = GetRenderers();
        var renderer = renderers.TryGetValue(context.Node.Type, out var matchedRenderer)
            ? matchedRenderer
            : fallback;
        renderer.Render(context);
    }

    public bool UsesDefaultRendering(NodeModel node) =>
        ReferenceEquals(fallback, DefaultGraphNodeRenderer.Instance) &&
        !GetRenderers().ContainsKey(node.Type);

    private IReadOnlyDictionary<string, IGraphNodeRenderer> GetRenderers()
    {
        if (_version == registry.Version)
        {
            return _renderers;
        }

        _renderers = registry.Builders
            .SelectMany(builder => builder.NodeRenderers)
            .GroupBy(item => item.NodeType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Renderer,
                StringComparer.OrdinalIgnoreCase);
        _version = registry.Version;
        return _renderers;
    }
}

internal sealed class ExtensionPortRenderer(
    GraphExtensionRegistry registry,
    IGraphPortRenderer fallback) :
    IGraphPortRenderer,
    IGraphPortRendererDefaultCompatibility
{
    private int _version = -1;
    private IReadOnlyDictionary<string, IGraphPortRenderer> _renderers =
        new Dictionary<string, IGraphPortRenderer>(StringComparer.OrdinalIgnoreCase);

    public void Render(in GraphPortRenderContext context)
    {
        var renderers = GetRenderers();
        var renderer = renderers.TryGetValue(context.Node.Type, out var matchedRenderer)
            ? matchedRenderer
            : fallback;
        renderer.Render(context);
    }

    public bool UsesDefaultRendering(NodeModel node, PortModel port) =>
        ReferenceEquals(fallback, DefaultGraphPortRenderer.Instance) &&
        !GetRenderers().ContainsKey(node.Type);

    private IReadOnlyDictionary<string, IGraphPortRenderer> GetRenderers()
    {
        if (_version == registry.Version)
        {
            return _renderers;
        }

        _renderers = registry.Builders
            .SelectMany(builder => builder.PortRenderers)
            .GroupBy(item => item.NodeType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Renderer,
                StringComparer.OrdinalIgnoreCase);
        _version = registry.Version;
        return _renderers;
    }
}

internal sealed class ExtensionEdgeRenderer(
    GraphExtensionRegistry registry,
    IGraphEdgeRenderer fallback) :
    IGraphEdgeRenderer,
    IGraphEdgeRendererDefaultCompatibility
{
    private int _version = -1;
    private IReadOnlyList<EdgeRendererContribution> _renderers = [];

    public void Render(in GraphEdgeRenderContext context)
    {
        var edge = context.Edge;
        var renderer = GetRenderers()
            .LastOrDefault(item => item.Predicate(edge))
            ?.Renderer ?? fallback;
        renderer.Render(context);
    }

    public bool UsesDefaultRendering(EdgeModel edge)
    {
        var renderers = GetRenderers();
        return ReferenceEquals(fallback, DefaultGraphEdgeRenderer.Instance) &&
               (renderers.Count == 0 || renderers.All(item => !item.Predicate(edge)));
    }

    private IReadOnlyList<EdgeRendererContribution> GetRenderers()
    {
        if (_version == registry.Version)
        {
            return _renderers;
        }

        _renderers = registry.Builders
            .SelectMany(builder => builder.EdgeRenderers)
            .ToArray();
        _version = registry.Version;
        return _renderers;
    }
}

internal sealed class ExtensionGroupRenderer(
    GraphExtensionRegistry registry,
    IGraphGroupRenderer fallback) :
    IGraphGroupRenderer,
    IGraphGroupRendererDefaultCompatibility
{
    private int _version = -1;
    private IReadOnlyList<GroupRendererContribution> _renderers = [];

    public void Render(in GraphGroupRenderContext context)
    {
        var group = context.Group;
        var renderer = GetRenderers()
            .LastOrDefault(item => item.Predicate(group))
            ?.Renderer ?? fallback;
        renderer.Render(context);
    }

    public bool UsesDefaultRendering(GroupModel group)
    {
        var renderers = GetRenderers();
        return ReferenceEquals(fallback, DefaultGraphGroupRenderer.Instance) &&
               (renderers.Count == 0 || renderers.All(item => !item.Predicate(group)));
    }

    private IReadOnlyList<GroupRendererContribution> GetRenderers()
    {
        if (_version == registry.Version)
        {
            return _renderers;
        }

        _renderers = registry.Builders
            .SelectMany(builder => builder.GroupRenderers)
            .ToArray();
        _version = registry.Version;
        return _renderers;
    }
}

internal sealed class ExtensionEditPolicy(
    GraphExtensionRegistry registry,
    IGraphEditPolicy fallback) : IGraphEditPolicy
{
    public GraphEditDecision Evaluate(GraphEditRequest request)
    {
        var decision = fallback.Evaluate(request);
        if (!decision.IsAllowed)
        {
            return decision;
        }

        foreach (var policy in registry.Builders.SelectMany(builder => builder.EditPolicies))
        {
            decision = policy.Evaluate(request);
            if (!decision.IsAllowed)
            {
                return decision;
            }
        }

        return GraphEditDecision.Allow;
    }
}
