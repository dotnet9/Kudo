using Kudo;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasGraphChangeController(GraphCanvasGraphChangeContext context)
{
    public void RefreshGraph()
    {
        context.CancelManualConnectionDraft();
        context.EdgeObserver.Synchronize();
        context.ClearEdgeRoutingCache();
        context.InvalidateDocumentLookup();
        context.InvalidateElementSpatialIndex();
        context.InvalidateOverviewDrawing();
        var selectionChanged = context.UpdateSelectionSnapshots();
        context.SynchronizeNodeControlHosts();
        SynchronizeVisualState();
        context.RaiseGraphChanged();
        if (selectionChanged)
        {
            context.NotifySelectionChanged();
        }
    }

    public void ApplyGraphChanges(GraphChangeSet changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.RequiresFullRefresh)
        {
            RefreshGraph();
            return;
        }

        var structureChanged = changes.Kind.HasFlag(GraphChangeKind.Structure);
        var geometryChanged = changes.Kind.HasFlag(GraphChangeKind.Geometry) ||
                              changes.Kind.HasFlag(GraphChangeKind.Routing);
        var groupChanged = changes.Groups.RequiresReset ||
                           changes.Groups.AddedIds.Count > 0 ||
                           changes.Groups.RemovedIds.Count > 0 ||
                           changes.Groups.ModifiedIds.Count > 0;
        if (structureChanged || groupChanged)
        {
            context.InvalidateDocumentLookup();
            context.InvalidateElementSpatialIndex();
        }

        var visualPropertiesChanged = changes.Kind.HasFlag(GraphChangeKind.Appearance) ||
                                      changes.Kind.HasFlag(GraphChangeKind.Properties);
        if (structureChanged || visualPropertiesChanged)
        {
            context.InvalidateOverviewDrawing();
        }

        if (structureChanged)
        {
            context.CancelManualConnectionDraft();
            context.EdgeObserver.Synchronize();
        }

        var canApplyNodeGeometry =
            geometryChanged &&
            !structureChanged &&
            !changes.Kind.HasFlag(GraphChangeKind.Routing) &&
            changes.Groups.IsEmpty &&
            !changes.Nodes.RequiresReset &&
            changes.Nodes.ModifiedIds.Count > 0;
        if (canApplyNodeGeometry)
        {
            context.InvalidateNodeGeometry(changes.Nodes.ModifiedIds);
        }
        else if (geometryChanged || structureChanged)
        {
            context.ClearEdgeRoutingCache();
        }

        var selectionChanged = structureChanged && context.UpdateSelectionSnapshots();
        if (structureChanged ||
            changes.Nodes.RequiresReset ||
            changes.Nodes.AddedIds.Count > 0 ||
            changes.Nodes.RemovedIds.Count > 0 ||
            changes.Groups.RequiresReset ||
            changes.Groups.AddedIds.Count > 0 ||
            changes.Groups.RemovedIds.Count > 0)
        {
            context.SynchronizeNodeControlHosts();
        }

        SynchronizeVisualState();
        context.RaiseGraphChanged();
        if (selectionChanged)
        {
            context.NotifySelectionChanged();
        }
    }

    private void SynchronizeVisualState()
    {
        context.RefreshNodeControlStates();
        context.UpdateNodeControlHostLayout();
        context.UpdateNodeTitleEditorLayout();
        context.UpdateAnnotationTextEditorLayout();
        context.UpdateEdgeLabelEditorLayout();
        context.UpdateRuntimeAnimationState();
        context.InvalidateVisual();
    }
}

internal sealed class GraphCanvasGraphChangeContext
{
    public required GraphCanvasEdgeObserver EdgeObserver { get; init; }

    public required Action CancelManualConnectionDraft { get; init; }

    public required Action ClearEdgeRoutingCache { get; init; }

    public required Action InvalidateDocumentLookup { get; init; }

    public required Action InvalidateElementSpatialIndex { get; init; }

    public required Action InvalidateOverviewDrawing { get; init; }

    public required Func<bool> UpdateSelectionSnapshots { get; init; }

    public required Action SynchronizeNodeControlHosts { get; init; }

    public required Action RefreshNodeControlStates { get; init; }

    public required Action UpdateNodeControlHostLayout { get; init; }

    public required Action UpdateNodeTitleEditorLayout { get; init; }

    public required Action UpdateAnnotationTextEditorLayout { get; init; }

    public required Action UpdateEdgeLabelEditorLayout { get; init; }

    public required Action UpdateRuntimeAnimationState { get; init; }

    public required Action InvalidateVisual { get; init; }

    public required Action RaiseGraphChanged { get; init; }

    public required Action NotifySelectionChanged { get; init; }

    public required Action<IEnumerable<string>> InvalidateNodeGeometry { get; init; }
}
