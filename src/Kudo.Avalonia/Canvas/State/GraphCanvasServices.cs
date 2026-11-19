namespace Kudo.Avalonia;

internal sealed class GraphCanvasServices
{
    public required GraphCanvasViewportController ViewportController { get; init; }

    public required GraphCanvasSelectionController SelectionController { get; init; }

    public required GraphCanvasEditController EditController { get; init; }

    public required GraphCanvasGroupController GroupController { get; init; }

    public required GraphCanvasAnnotationController AnnotationController { get; init; }

    public required GraphCanvasAnnotationRenderer AnnotationRenderer { get; init; }

    public required GraphCanvasNodeControlController NodeControlController { get; init; }

    public required GraphCanvasPlacementController PlacementController { get; init; }

    public required GraphCanvasRuntimeController RuntimeController { get; init; }

    public required GraphCanvasNodeTitleEditor NodeTitleEditingController { get; init; }

    public required GraphCanvasEdgeLabelEditor EdgeLabelEditingController { get; init; }

    public required GraphCanvasAnnotationTextEditor AnnotationTextEditingController { get; init; }

    public required GraphCanvasKeyboardController KeyboardController { get; init; }

    public required GraphCanvasInteractionCoordinator InteractionCoordinator { get; init; }

    public required GraphCanvasAutoPanController AutoPanController { get; init; }

    public required GraphCanvasNodePushController NodePushController { get; init; }

    public required GraphCanvasEdgeCutController EdgeCutController { get; init; }

    public required GraphCanvasEdgeObserver EdgeObserver { get; init; }

    public required GraphCanvasGraphChangeController GraphChangeController { get; init; }

    public required GraphCanvasPropertyChangeController PropertyChangeController { get; init; }

    public required GraphCanvasHitTester HitTester { get; init; }

    public required GraphCanvasRenderer Renderer { get; init; }
}
