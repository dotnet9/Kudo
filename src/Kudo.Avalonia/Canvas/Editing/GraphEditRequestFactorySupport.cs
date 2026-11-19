namespace Kudo.Avalonia;

internal static class GraphEditRequestFactorySupport
{
    public static GraphEditRequest Request(
        GraphEditOperation operation,
        GraphDocument document,
        IGraphCommand command,
        IReadOnlyList<NodeModel>? nodes = null,
        EdgeModel? edge = null,
        GroupModel? group = null,
        AnnotationModel? annotation = null,
        PortReference? source = null,
        PortReference? target = null) =>
        new(
            operation,
            document,
            nodes,
            edge,
            group,
            annotation,
            source,
            target,
            command);

    public static IReadOnlyList<NodeModel> FindNodes(
        GraphDocument document,
        IEnumerable<string>? nodeIds)
    {
        var ids = (nodeIds ?? []).ToHashSet(StringComparer.Ordinal);
        return document.Nodes.Where(node => ids.Contains(node.Id)).ToArray();
    }

    public static EdgeModel? FindEdge(GraphDocument document, string edgeId) =>
        document.Edges.FirstOrDefault(edge => edge.Id == edgeId);
}
