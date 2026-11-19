using Kudo.Core.Algorithms;

namespace Kudo.Avalonia.Designer;

public sealed class GraphArrangementOperations
{
    private readonly GraphDocumentView _view;

    internal GraphArrangementOperations(GraphDocumentView view) => _view = view;

    public bool Align(NodeAlignment alignment, string? commandName = null) =>
        _view.Canvas.AlignSelectedNodes(alignment, commandName);

    public bool Distribute(NodeDistribution distribution, string? commandName = null) =>
        _view.Canvas.DistributeSelectedNodes(distribution, commandName);

    public bool ApplyLayout(GraphLayoutResult result, string? commandName = null) =>
        _view.Canvas.ApplyLayout(result, commandName);

    public bool ApplyLayout(
        IGraphLayoutAlgorithm algorithm,
        GraphLayoutRequest? request = null,
        string? commandName = null)
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        request ??= new GraphLayoutRequest(_view.Session.Document);
        if (!ReferenceEquals(request.Document, _view.Session.Document))
        {
            throw new ArgumentException(
                "Layout request must target the editor document.",
                nameof(request));
        }

        return ApplyLayout(algorithm.Layout(request), commandName ?? algorithm.Name);
    }

    public bool ApplyLayered(
        GraphLayoutRequest? request = null,
        string? commandName = null) =>
        ApplyLayout(new LayeredLayoutAlgorithm(), request, commandName);
}
