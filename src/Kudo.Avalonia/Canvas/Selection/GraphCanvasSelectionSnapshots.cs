namespace Kudo.Avalonia;

internal sealed class GraphCanvasSelectionSnapshots(
    GraphCanvasSelectionContext context,
    GraphCanvasSelectionQueries queries)
{
    public void RaiseSelectionChanged()
    {
        UpdateSelectionSnapshots();
        context.NotifySelectionChanged(new GraphSelectionChangedEventArgs(
            context.SelectedNodeIdsSnapshot(),
            context.SelectedGroupIdsSnapshot(),
            context.SelectedNodeId(),
            context.SelectedEdgeIdsSnapshot(),
            context.SelectedEdgeId(),
            context.SelectedAnnotationId()));
    }

    public bool UpdateSelectionSnapshots()
    {
        var selectedNodeIds = queries.GetDocumentOrderedNodeIds(context.SelectedNodeIds());
        var selectedGroupIds = queries.GetDocumentOrderedGroupIds();
        var nodeSelectionChanged =
            !context.SelectedNodeIdsSnapshot().SequenceEqual(selectedNodeIds, StringComparer.Ordinal);
        var groupSelectionChanged =
            !context.SelectedGroupIdsSnapshot().SequenceEqual(selectedGroupIds, StringComparer.Ordinal);
        var selectedEdgeIds = queries.GetDocumentOrderedEdgeIds(context.SelectedEdgeIds());
        var edgeSelectionChanged =
            !context.SelectedEdgeIdsSnapshot().SequenceEqual(selectedEdgeIds, StringComparer.Ordinal);
        if (!nodeSelectionChanged && !groupSelectionChanged && !edgeSelectionChanged)
        {
            return false;
        }

        if (nodeSelectionChanged)
        {
            context.SetSelectedNodeIdsSnapshot(Array.AsReadOnly(selectedNodeIds.ToArray()));
        }

        if (groupSelectionChanged)
        {
            context.SetSelectedGroupIdsSnapshot(Array.AsReadOnly(selectedGroupIds.ToArray()));
        }

        if (edgeSelectionChanged)
        {
            context.SetSelectedEdgeIdsSnapshot(Array.AsReadOnly(selectedEdgeIds.ToArray()));
        }

        return true;
    }
}
