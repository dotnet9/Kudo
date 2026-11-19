using Kudo;

namespace Kudo.Avalonia.Designer;

public sealed class GraphNodeDefinitionFactory : IGraphNodeFactory
{
    public static GraphNodeDefinitionFactory Instance { get; } = new();

    public NodeModel CreateNode(GraphNodeDefinition definition, string nodeId, GraphPoint position)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Kind == GraphNodeDefinitionKind.Annotation)
        {
            throw new InvalidOperationException("Annotation definitions create AnnotationModel instances through GraphDocumentView.");
        }

        var node = new NodeModel
        {
            Id = nodeId,
            Type = definition.NodeType,
            DefinitionId = definition.Id,
            Name = definition.Name,
            Description = string.Empty,
            Position = position,
            Size = definition.GetEffectiveDefaultSize(),
            Appearance = definition.DefaultAppearance.Normalize(),
            Properties = GraphNodeDefinitionValue.MaterializeDictionary(definition.DefaultProperties)
        };
        node.Ports.AddRange(definition.DefaultPorts.Select(GraphNodeDefinitionValue.ClonePort));
        return node;
    }
}

public sealed class ProjectedGraphNodeFactory : IGraphNodeFactory
{
    private readonly IGraphNodeFactory _inner;
    private readonly IGraphNodeStateProjectionResolver _projections;

    public ProjectedGraphNodeFactory(
        IGraphNodeStateProjectionResolver projections,
        IGraphNodeFactory? inner = null)
    {
        _projections = projections ?? throw new ArgumentNullException(nameof(projections));
        _inner = inner ?? GraphNodeDefinitionFactory.Instance;
    }

    public NodeModel CreateNode(
        GraphNodeDefinition definition,
        string nodeId,
        GraphPoint position)
    {
        var node = _inner.CreateNode(definition, nodeId, position);
        return ApplyProjection(node);
    }

    public NodeModel ApplyProjection(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.ExtensionState is not { } state ||
            !_projections.TryProject(node, state, out var projection))
        {
            return node;
        }

        node.Size = projection!.Size;
        node.Ports = projection.Ports.Select(GraphNodeDefinitionValue.ClonePort).ToList();
        return node;
    }
}
