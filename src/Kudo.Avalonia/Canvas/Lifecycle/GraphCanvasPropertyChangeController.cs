using Avalonia;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasPropertyChangeController(GraphCanvasPropertyChangeContext context)
{
    public void Handle(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == GraphCanvas.SelectedNodeIdProperty &&
            !context.GetIsUpdatingNodeSelection())
        {
            context.SynchronizeSelectedNodeSelection();
        }

        if (change.Property == GraphCanvas.SelectedEdgeIdProperty &&
            !context.GetIsUpdatingEdgeSelection())
        {
            context.SynchronizeSelectedEdgeSelection();
        }

        if (change.Property == GraphCanvas.SelectedAnnotationIdProperty &&
            !context.GetSuppressSelectionChanged())
        {
            context.SynchronizeSelectedAnnotationSelection();
        }

        if (ShouldInvalidateVisual(change.Property))
        {
            context.InvalidateVisual();
        }

        if (change.Property == GraphCanvas.DocumentProperty)
        {
            HandleDocumentChanged();
        }

        if (context.IsPaletteProperty(change.Property))
        {
            context.InvalidateOverviewDrawing();
        }

        if (ShouldUpdateEditorLayout(change.Property))
        {
            context.UpdateEditorLayouts();
        }

        if (change.Property == GraphCanvas.ViewportProperty &&
            !context.GetSuppressViewportChangedEvent())
        {
            context.RaiseViewportChanged(
                change.GetOldValue<GraphViewport>(),
                change.GetNewValue<GraphViewport>());
        }

        if (change.Property == GraphCanvas.ViewportOptionsProperty)
        {
            context.ApplyViewportOptionsChange(
                change.GetOldValue<GraphViewportOptions>(),
                change.GetNewValue<GraphViewportOptions>());
        }

        if (change.Property == GraphCanvas.IsNodeTitleEditingEnabledProperty &&
            !context.GetIsNodeTitleEditingEnabled())
        {
            context.CancelNodeTitleEdit();
        }

        if (change.Property == GraphCanvas.IsAnnotationTextEditingEnabledProperty &&
            !context.GetIsAnnotationTextEditingEnabled())
        {
            context.CancelAnnotationTextEdit();
        }

        if (change.Property == GraphCanvas.IsEdgeLabelEditingEnabledProperty &&
            !context.GetIsEdgeLabelEditingEnabled())
        {
            context.CancelEdgeLabelEdit();
        }

        if (change.Property == GraphCanvas.IsEdgeCuttingEnabledProperty &&
            !context.GetIsEdgeCuttingEnabled() &&
            context.GetActiveInteraction() is EdgeCuttingInteractionSession)
        {
            context.CancelActivePointerInteraction();
        }

        if (change.Property == GraphCanvas.IsNodePushingEnabledProperty &&
            !context.GetIsNodePushingEnabled() &&
            context.GetActiveInteraction() is NodePushingInteractionSession)
        {
            context.CancelActivePointerInteraction();
        }

        if (change.Property == GraphCanvas.SelectedNodeIdProperty ||
            change.Property == GraphCanvas.SelectedEdgeIdProperty ||
            change.Property == GraphCanvas.SelectedAnnotationIdProperty)
        {
            context.RefreshNodeControlStates();
            if (!context.GetSuppressSelectionChanged())
            {
                context.RaiseSelectionChanged();
            }
        }

        if (change.Property == GraphCanvas.LastConnectionValidationProperty &&
            context.GetLastConnectionValidation() is { } validation)
        {
            context.RaiseConnectionValidationChanged(validation);
        }

        if (change.Property == GraphCanvas.GridSettingsProperty)
        {
            context.ClearAlignmentGuides();
        }

        if (change.Property == GraphCanvas.IsAutoPanEnabledProperty ||
            change.Property == GraphCanvas.AutoPanEdgeDistanceProperty ||
            change.Property == GraphCanvas.AutoPanSpeedProperty)
        {
            context.UpdateAutoPanState();
        }

        if (change.Property == GraphCanvas.IsReadOnlyProperty)
        {
            HandleReadOnlyChanged();
        }

        if (change.Property == GraphCanvas.RuntimeSnapshotProperty)
        {
            context.UpdateRuntimeSnapshot();
            context.RefreshNodeControlStates();
        }

        if (change.Property == GraphCanvas.BreakpointNodeIdsProperty)
        {
            context.RefreshNodeControlStates();
        }
    }

    private void HandleDocumentChanged()
    {
        context.CancelActivePointerInteraction();
        context.CancelManualConnectionDraft();
        var selectionChanged = context.UpdateSelectionSnapshots();
        context.EdgeObserver.Synchronize();
        context.CancelNodeTitleEdit();
        context.CancelAnnotationTextEdit();
        context.CancelEdgeLabelEdit();
        context.ClearEdgeRoutingCache();
        context.InvalidateDocumentLookup();
        context.InvalidateElementSpatialIndex();
        context.SynchronizeNodeControlHosts();
        context.UpdateRuntimeAnimationState();
        if (selectionChanged)
        {
            context.RaiseSelectionChanged();
        }
    }

    private void HandleReadOnlyChanged()
    {
        if (context.GetIsReadOnly())
        {
            context.CancelNodeTitleEdit();
            context.CancelAnnotationTextEdit();
            context.CancelEdgeLabelEdit();
            context.CancelActivePointerInteraction();
            context.CancelManualConnectionDraft();
            context.ClearNodePlacementPreview();
        }

        context.UpdatePointerCursor();
    }

    private bool ShouldInvalidateVisual(AvaloniaProperty property) =>
        property == GraphCanvas.DocumentProperty ||
        property == GraphCanvas.ViewportProperty ||
        property == GraphCanvas.ViewportOptionsProperty ||
        property == GraphCanvas.IsLevelOfDetailEnabledProperty ||
        property == GraphCanvas.ShowPortsProperty ||
        property == GraphCanvas.LodPortsZoomThresholdProperty ||
        property == GraphCanvas.LodLabelsZoomThresholdProperty ||
        property == GraphCanvas.LodSimplifiedZoomThresholdProperty ||
        property == GraphCanvas.PortLabelVisibilityProperty ||
        property == GraphCanvas.SymbolNameVisibilityProperty ||
        property == GraphCanvas.SymbolDescriptionVisibilityProperty ||
        property == GraphCanvas.GridSettingsProperty ||
        property == GraphCanvas.RuntimeSnapshotProperty ||
        property == GraphCanvas.BreakpointNodeIdsProperty ||
        property == GraphCanvas.SelectedNodeIdProperty ||
        property == GraphCanvas.SelectedEdgeIdProperty ||
        property == GraphCanvas.SelectedAnnotationIdProperty ||
        property == GraphCanvas.LastConnectionValidationProperty ||
        context.IsPaletteProperty(property);

    private static bool ShouldUpdateEditorLayout(AvaloniaProperty property) =>
        property == GraphCanvas.DocumentProperty ||
        property == GraphCanvas.IsReadOnlyProperty ||
        property == GraphCanvas.ViewportProperty ||
        property == GraphCanvas.IsLevelOfDetailEnabledProperty ||
        property == GraphCanvas.LodLabelsZoomThresholdProperty ||
        property == GraphCanvas.SymbolNameVisibilityProperty ||
        property == GraphCanvas.SymbolDescriptionVisibilityProperty;
}

internal sealed class GraphCanvasPropertyChangeContext
{
    public required Func<AvaloniaProperty, bool> IsPaletteProperty { get; init; }

    public required Func<bool> GetIsUpdatingNodeSelection { get; init; }

    public required Func<bool> GetIsUpdatingEdgeSelection { get; init; }

    public required Action SynchronizeSelectedNodeSelection { get; init; }

    public required Action SynchronizeSelectedEdgeSelection { get; init; }

    public required Action SynchronizeSelectedAnnotationSelection { get; init; }

    public required Action InvalidateVisual { get; init; }

    public required Func<bool> UpdateSelectionSnapshots { get; init; }

    public required GraphCanvasEdgeObserver EdgeObserver { get; init; }

    public required Action CancelNodeTitleEdit { get; init; }

    public required Action CancelAnnotationTextEdit { get; init; }

    public required Action CancelEdgeLabelEdit { get; init; }

    public required Action ClearEdgeRoutingCache { get; init; }

    public required Action InvalidateDocumentLookup { get; init; }

    public required Action InvalidateElementSpatialIndex { get; init; }

    public required Action SynchronizeNodeControlHosts { get; init; }

    public required Action UpdateRuntimeAnimationState { get; init; }

    public required Action InvalidateOverviewDrawing { get; init; }

    public required Action UpdateEditorLayouts { get; init; }

    public required Func<bool> GetSuppressViewportChangedEvent { get; init; }

    public required Action<GraphViewport, GraphViewport> RaiseViewportChanged { get; init; }

    public required Action<GraphViewportOptions, GraphViewportOptions> ApplyViewportOptionsChange { get; init; }

    public required Func<bool> GetIsNodeTitleEditingEnabled { get; init; }

    public required Func<bool> GetIsAnnotationTextEditingEnabled { get; init; }

    public required Func<bool> GetIsEdgeLabelEditingEnabled { get; init; }

    public required Func<bool> GetIsEdgeCuttingEnabled { get; init; }

    public required Func<bool> GetIsNodePushingEnabled { get; init; }

    public required Func<IGraphInteractionSession?> GetActiveInteraction { get; init; }

    public required Action CancelActivePointerInteraction { get; init; }

    public required Action CancelManualConnectionDraft { get; init; }

    public required Action RefreshNodeControlStates { get; init; }

    public required Func<bool> GetSuppressSelectionChanged { get; init; }

    public required Action RaiseSelectionChanged { get; init; }

    public required Func<ConnectionValidationResult?> GetLastConnectionValidation { get; init; }

    public required Action<ConnectionValidationResult> RaiseConnectionValidationChanged { get; init; }

    public required Action ClearAlignmentGuides { get; init; }

    public required Action UpdateAutoPanState { get; init; }

    public required Func<bool> GetIsReadOnly { get; init; }

    public required Action ClearNodePlacementPreview { get; init; }

    public required Action UpdatePointerCursor { get; init; }

    public required Action UpdateRuntimeSnapshot { get; init; }
}
