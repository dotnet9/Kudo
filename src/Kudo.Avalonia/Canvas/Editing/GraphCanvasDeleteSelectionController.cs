using Kudo;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasDeleteSelectionController(
    GraphCanvasEditContext context,
    GraphCanvasEdgeEditController edgeEditController,
    GraphCanvasNodeEditController nodeEditController)
{
    public bool CanDeleteSelection() =>
        CreateDeleteSelectionRequest() is { } request && context.CanAuthorizeEdit(request);

    public bool TryAuthorizeDeleteSelection() =>
        CreateDeleteSelectionRequest() is { } request && context.TryAuthorizeEdit(request);

    public bool DeleteSelection()
    {
        var request = CreateDeleteSelectionRequest();
        return request?.Operation switch
        {
            GraphEditOperation.DeleteEdges => edgeEditController.DeleteSelectedEdges(),
            GraphEditOperation.DeleteAnnotation => context.DeleteSelectedAnnotation(),
            GraphEditOperation.DeleteNodes => nodeEditController.DeleteSelectedNode(),
            _ => false
        };
    }

    private GraphEditRequest? CreateDeleteSelectionRequest()
    {
        var document = context.Document();
        if (document is null)
        {
            return null;
        }

        var edges = document.Edges
            .Where(edge => context.SelectedEdgeIds().Contains(edge.Id))
            .ToArray();
        if (edges.Length > 0)
        {
            return new GraphEditRequest(
                GraphEditOperation.DeleteEdges,
                document,
                Edge: edges.FirstOrDefault(edge => edge.Id == context.SelectedEdgeId()) ?? edges[0],
                Edges: edges);
        }

        if (document.FindAnnotation(context.SelectedAnnotationId() ?? string.Empty) is { } annotation)
        {
            return new GraphEditRequest(
                GraphEditOperation.DeleteAnnotation,
                document,
                Annotation: annotation);
        }

        var nodes = document.Nodes
            .Where(node => context.SelectedNodeIds().Contains(node.Id) && !node.IsLocked)
            .ToArray();
        return nodes.Length == 0
            ? null
            : new GraphEditRequest(
                GraphEditOperation.DeleteNodes,
                document,
                Nodes: nodes);
    }
}
