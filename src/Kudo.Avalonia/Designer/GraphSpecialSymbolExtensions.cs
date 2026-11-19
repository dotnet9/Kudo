using Kudo;
using Kudo.Avalonia.Export;
using Kudo.Core.Documents;

namespace Kudo.Avalonia.Designer;

/// <summary>
/// 业务节点的精确几何扩展。实现不得把视觉 token 写入 SDK；颜色、线宽和间距由业务 Renderer/资源字典决定。
/// </summary>
public interface IGraphNodeGeometryProvider
{
    GraphRect GetBounds(NodeModel node);

    bool Contains(NodeModel node, GraphPoint point);
}

public sealed class DefaultGraphNodeGeometryProvider : IGraphNodeGeometryProvider
{
    public static DefaultGraphNodeGeometryProvider Instance { get; } = new();

    private DefaultGraphNodeGeometryProvider()
    {
    }

    public GraphRect GetBounds(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return GraphNodeGeometry.GetCanvasBounds(node);
    }

    public bool Contains(NodeModel node, GraphPoint point)
    {
        ArgumentNullException.ThrowIfNull(node);
        return GraphNodeGeometry.Contains(node, point);
    }
}

public sealed record GraphNodeCreationConfigurationContext(
    GraphDocumentSession Session,
    GraphNodeDefinition Definition,
    GraphPoint Position,
    bool IsDragDrop,
    GraphNodeCreationOptions? ExistingOptions = null);

public sealed record GraphNodeCreationConfigurationResult(
    bool IsCancelled,
    GraphNodeCreationOptions? Options = null)
{
    public static GraphNodeCreationConfigurationResult Continue(
        GraphNodeCreationOptions? options = null) => new(false, options);

    public static GraphNodeCreationConfigurationResult Cancel { get; } = new(true);
}

/// <summary>
/// 节点拖入/创建前的业务配置器。配置器可以弹出业务表单，返回 ExtensionState、尺寸和其他创建选项。
/// </summary>
public interface IGraphNodeCreationConfigurator
{
    string NodeType { get; }

    ValueTask<GraphNodeCreationConfigurationResult> ConfigureAsync(
        GraphNodeCreationConfigurationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 自定义导出节点 Renderer。Kudo 负责画布、变换、端口、外部名称/描述和导出生命周期，
/// 业务完全负责节点主体，不会叠加默认背景、边框或嵌入内容。
/// </summary>
public interface IGraphNodeExportRenderer
{
    string NodeType { get; }

    void Render(in GraphNodeExportRenderContext context);
}

public readonly record struct GraphNodeExportRenderContext(
    SkiaSharp.SKCanvas Canvas,
    GraphDocument Document,
    NodeModel Node,
    GraphExportLayout Layout,
    GraphExportOptions Options,
    GraphExportTheme Theme,
    GraphRect Bounds,
    CancellationToken CancellationToken);

public interface IGraphNodeExportRendererResolver
{
    IGraphNodeExportRenderer? ResolveExportRenderer(NodeModel node);
}

public interface IGraphNodeCreationConfiguratorResolver
{
    ValueTask<GraphNodeCreationConfigurationResult> ConfigureCreationAsync(
        GraphNodeCreationConfigurationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 业务特殊图元资源的可选集合。它只存储注册结果，不拥有任何视觉 token。
/// </summary>
public sealed class GraphSpecialSymbolRegistry :
    IGraphNodeStateProjectionResolver,
    IGraphNodeGeometryProvider,
    IGraphNodeExportRendererResolver,
    IGraphNodeCreationConfiguratorResolver
{
    private readonly GraphExtensionRegistry _registry;
    private int _version = -1;
    private IReadOnlyDictionary<string, IGraphNodeStateProjectionProvider> _projections =
        new Dictionary<string, IGraphNodeStateProjectionProvider>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, IGraphNodeGeometryProvider> _geometry =
        new Dictionary<string, IGraphNodeGeometryProvider>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, IGraphNodeCreationConfigurator> _configurators =
        new Dictionary<string, IGraphNodeCreationConfigurator>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, IGraphNodeExportRenderer> _exportRenderers =
        new Dictionary<string, IGraphNodeExportRenderer>(StringComparer.OrdinalIgnoreCase);

    internal GraphSpecialSymbolRegistry(GraphExtensionRegistry registry) =>
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public bool TryGetProjectionProvider(
        string nodeType,
        out IGraphNodeStateProjectionProvider? provider)
    {
        EnsureFresh();
        return _projections.TryGetValue(nodeType, out provider);
    }

    public bool TryGetGeometryProvider(
        string nodeType,
        out IGraphNodeGeometryProvider? provider)
    {
        EnsureFresh();
        return _geometry.TryGetValue(nodeType, out provider);
    }

    public bool TryGetCreationConfigurator(
        string nodeType,
        out IGraphNodeCreationConfigurator? configurator)
    {
        EnsureFresh();
        return _configurators.TryGetValue(nodeType, out configurator);
    }

    public bool TryGetExportRenderer(
        string nodeType,
        out IGraphNodeExportRenderer? renderer)
    {
        EnsureFresh();
        return _exportRenderers.TryGetValue(nodeType, out renderer);
    }

    public GraphRect GetBounds(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        EnsureFresh();
        return _geometry.TryGetValue(node.Type, out var provider)
            ? provider.GetBounds(node)
            : DefaultGraphNodeGeometryProvider.Instance.GetBounds(node);
    }

    public bool Contains(NodeModel node, GraphPoint point)
    {
        ArgumentNullException.ThrowIfNull(node);
        EnsureFresh();
        return _geometry.TryGetValue(node.Type, out var provider)
            ? provider.Contains(node, point)
            : DefaultGraphNodeGeometryProvider.Instance.Contains(node, point);
    }

    public bool TryProject(
        NodeModel node,
        GraphExtensionState state,
        out GraphNodeProjection? projection)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        EnsureFresh();
        if (!_projections.TryGetValue(node.Type, out var provider))
        {
            projection = null;
            return false;
        }

        projection = provider.Project(node, state) ??
            throw new InvalidOperationException(
                $"Projection provider for node type '{node.Type}' returned null.");
        GraphNodeProjectionValidator.Validate(projection);
        return true;
    }

    public async ValueTask<GraphNodeCreationConfigurationResult> ConfigureCreationAsync(
        GraphNodeCreationConfigurationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsureFresh();
        if (!_configurators.TryGetValue(context.Definition.NodeType, out var configurator))
        {
            return GraphNodeCreationConfigurationResult.Continue(context.ExistingOptions);
        }

        var result = await configurator.ConfigureAsync(context, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            throw new InvalidOperationException(
                $"Creation configurator for node type '{context.Definition.NodeType}' returned null.");
        }

        return result;
    }

    public IGraphNodeExportRenderer? ResolveExportRenderer(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        EnsureFresh();
        return _exportRenderers.GetValueOrDefault(node.Type);
    }

    private void EnsureFresh()
    {
        if (_version == _registry.Version)
        {
            return;
        }

        var builders = _registry.Builders.ToArray();
        _projections = LastByNodeType(
            builders.SelectMany(builder => builder.StateProjections),
            item => item.NodeType,
            item => item.Provider);
        _geometry = LastByNodeType(
            builders.SelectMany(builder => builder.GeometryProviders),
            item => item.NodeType,
            item => item.Provider);
        _configurators = LastByNodeType(
            builders.SelectMany(builder => builder.CreationConfigurators),
            item => item.NodeType,
            item => item.Configurator);
        _exportRenderers = LastByNodeType(
            builders.SelectMany(builder => builder.ExportRenderers),
            item => item.NodeType,
            item => item.Renderer);
        _version = _registry.Version;
    }

    private static IReadOnlyDictionary<string, TValue> LastByNodeType<TContribution, TValue>(
        IEnumerable<TContribution> contributions,
        Func<TContribution, string> nodeType,
        Func<TContribution, TValue> value)
        where TValue : class => contributions
        .GroupBy(nodeType, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => value(group.Last()), StringComparer.OrdinalIgnoreCase);
}

internal sealed record StateProjectionContribution(
    string NodeType,
    IGraphNodeStateProjectionProvider Provider);

internal sealed record GeometryContribution(
    string NodeType,
    IGraphNodeGeometryProvider Provider);

internal sealed record CreationConfiguratorContribution(
    string NodeType,
    IGraphNodeCreationConfigurator Configurator);

internal sealed record ExportRendererContribution(
    string NodeType,
    IGraphNodeExportRenderer Renderer);

internal sealed class NodeTypePropertyEditorProvider(
    string nodeType,
    IGraphPropertyEditorProvider inner) : IGraphPropertyEditorProvider
{
    public bool TryCreate(
        GraphPropertyEditorContext editorContext,
        GraphPropertyContext propertyContext,
        out GraphPropertyEditorContent? content)
    {
        if (propertyContext.Node is not { } node ||
            !string.Equals(node.Type, nodeType, StringComparison.OrdinalIgnoreCase))
        {
            content = null;
            return false;
        }

        return inner.TryCreate(editorContext, propertyContext, out content);
    }
}
