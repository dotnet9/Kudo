namespace Kudo.Avalonia;

public static class GraphEditRequestFactory
{
    public static IReadOnlyList<GraphEditRequest> Create(
        GraphDocument document,
        IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(command);
        var requests = new List<GraphEditRequest>();
        GraphEditRequestCommandMapper.Add(document, command, requests);
        return requests;
    }

    public static IReadOnlyList<GraphEditRequest> CreateUndo(
        GraphDocument document,
        IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(command);
        var requests = new List<GraphEditRequest>();
        GraphEditUndoRequestCommandMapper.Add(document, command, requests);
        return requests;
    }
}
