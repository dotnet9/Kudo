using Kudo;
using Kudo.Core.Documents;

namespace Kudo.Avalonia.Designer;

public interface IGraphNodeDropHandler
{
    ValueTask<GraphNodeDropResult> HandleAsync(
        GraphNodeDropContext context,
        CancellationToken cancellationToken = default);
}

public sealed record GraphNodeDropContext(
    GraphDocumentSession Session,
    GraphNodeDefinition Definition,
    GraphPoint Position,
    bool IsDragDrop,
    GraphNodeCreationOptions? Options = null);

public enum GraphNodeDropResultKind
{
    Create,
    Cancel,
    Handled
}

public sealed record GraphNodeDropResult(GraphNodeDropResultKind Kind)
{
    public GraphNodeDefinition? Definition { get; init; }

    public GraphPoint? Position { get; init; }

    public GraphNodeCreationOptions? Options { get; init; }

    public static GraphNodeDropResult Create(
        GraphNodeDefinition? definition = null,
        GraphPoint? position = null,
        GraphNodeCreationOptions? options = null) =>
        new(GraphNodeDropResultKind.Create)
        {
            Definition = definition,
            Position = position,
            Options = options
        };

    public static GraphNodeDropResult Cancel() => new(GraphNodeDropResultKind.Cancel);

    public static GraphNodeDropResult Handled() => new(GraphNodeDropResultKind.Handled);
}

public sealed record GraphNodeCreationOptions
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public GraphSize? Size { get; init; }

    public GraphNodeAppearance? Appearance { get; init; }

    public GraphExtensionState? ExtensionState { get; init; }

    public IReadOnlyDictionary<string, object?>? Properties { get; init; }

    public IReadOnlyList<PortModel>? Ports { get; init; }
}
