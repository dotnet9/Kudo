using Avalonia;
using Avalonia.Controls;
using Kudo;
using Kudo.Avalonia.Designer;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private const double MinimumLodZoom = 0.01;
    private const double MaximumLodZoom = 3.0;
    private const double DefaultConnectionSnapDistance = 28.0;
    private const double DefaultViewportCullingMargin = 160.0;
    private const double EdgeHitTolerance = 8.0;
    private const double EdgeEndpointRadius = 7.0;
    private const double EdgeEndpointHitRadius = 12.0;
    private const double EdgeWaypointRadius = 5.0;
    private const double EdgeWaypointHitRadius = 9.0;
    private const double NodeResizeHandleRadius = 4.0;
    private const double NodeResizeHandleHitRadius = 7.0;
    private const double NodeResizeHandleOffset = 8.0;
    private const double NodeRotationHandleRadius = 6.0;
    private const double NodeRotationHandleHitRadius = 10.0;
    private const double NodeRotationHandleOffset = 34.0;
    internal const double NodeRotationSnapStep = 15.0;
    private const double EdgeLabelProgress = 0.5;

    private readonly GraphCanvasState _canvasState = new();
    private readonly GraphCanvasServices _services;
    private GraphCanvasViewportController _viewportController => _services.ViewportController;
    private GraphCanvasSelectionController _selectionController => _services.SelectionController;
    private GraphCanvasEditController _editController => _services.EditController;
    private GraphCanvasGroupController _groupController => _services.GroupController;
    private GraphCanvasAnnotationController _annotationController => _services.AnnotationController;
    private GraphCanvasAnnotationRenderer _annotationRenderer => _services.AnnotationRenderer;
    private GraphCanvasNodeControlController _nodeControlController => _services.NodeControlController;
    private GraphCanvasPlacementController _placementController => _services.PlacementController;
    private GraphCanvasRuntimeController _runtimeController => _services.RuntimeController;
    private GraphCanvasNodeTitleEditor _nodeTitleEditingController => _services.NodeTitleEditingController;
    private GraphCanvasEdgeLabelEditor _edgeLabelEditingController => _services.EdgeLabelEditingController;
    private GraphCanvasAnnotationTextEditor _annotationTextEditingController =>
        _services.AnnotationTextEditingController;
    private GraphCanvasKeyboardController _keyboardController => _services.KeyboardController;
    private GraphCanvasInteractionCoordinator _interactionCoordinator => _services.InteractionCoordinator;
    private GraphCanvasAutoPanController _autoPanController => _services.AutoPanController;
    private GraphCanvasNodePushController _nodePushController => _services.NodePushController;
    private GraphCanvasEdgeCutController _edgeCutController => _services.EdgeCutController;
    private GraphCanvasEdgeObserver _edgeObserver => _services.EdgeObserver;
    private GraphCanvasGraphChangeController _graphChangeController => _services.GraphChangeController;
    private GraphCanvasPropertyChangeController _propertyChangeController => _services.PropertyChangeController;
    private GraphCanvasHitTester _hitTester => _services.HitTester;
    private GraphViewportChangeReason? _pendingViewportChangeReason;
    private bool _suppressViewportChangedEvent;
    private HashSet<string> _selectedNodeIds => _canvasState.SelectedNodeIds;
    private HashSet<string> _selectedEdgeIds => _canvasState.SelectedEdgeIds;
    private bool _isUpdatingNodeSelection
    {
        get => _canvasState.IsUpdatingNodeSelection;
        set => _canvasState.IsUpdatingNodeSelection = value;
    }

    private bool _suppressSelectionChanged
    {
        get => _canvasState.SuppressSelectionChanged;
        set => _canvasState.SuppressSelectionChanged = value;
    }

    private IReadOnlyList<GraphAlignmentGuide> _activeAlignmentGuides
    {
        get => _canvasState.ActiveAlignmentGuides;
        set => _canvasState.ActiveAlignmentGuides = value;
    }

    private string? _hoveredNodeId
    {
        get => _canvasState.HoveredNodeId;
        set => _canvasState.HoveredNodeId = value;
    }

    private PortHitResult? _hoveredPort
    {
        get => _canvasState.HoveredPort;
        set => _canvasState.HoveredPort = value;
    }

    private bool _isUpdatingEdgeSelection
    {
        get => _canvasState.IsUpdatingEdgeSelection;
        set => _canvasState.IsUpdatingEdgeSelection = value;
    }

    private ManualConnectionDraft? _manualConnectionDraft
    {
        get => _canvasState.ManualConnectionDraft;
        set => _canvasState.ManualConnectionDraft = value;
    }

    private GraphCanvasRenderState _renderState => _canvasState.Render;

    /// <summary>
    /// Gets the last valid pointer position in graph coordinates.
    /// </summary>
    public GraphPoint? LastPointerGraphPoint { get; private set; }

    /// <summary>
    /// Gets the monotonically increasing version of the last pointer press.
    /// </summary>
    public long LastPointerGraphPointVersion { get; private set; }
    private GraphCanvasRenderer _renderer => _services.Renderer;
    private readonly Canvas _nodeControlLayer = new()
    {
        ClipToBounds = true
    };
    private readonly GraphCanvasGeometryState _geometryState = new();
    private readonly Func<EdgeModel, GraphEdgePath?> _edgeGraphPathProvider;
    private GraphEdgePathCache _edgePathCache => _geometryState.EdgePathCache;
    private GraphEdgeSpatialIndex _edgeSpatialIndex => _geometryState.EdgeSpatialIndex;
    private GraphElementSpatialIndex _elementSpatialIndex => _geometryState.ElementSpatialIndex;
    internal GraphElementSpatialIndex ElementSpatialIndex => _elementSpatialIndex;
    private HashSet<string> _changedGeometryNodeIds => _geometryState.ChangedNodeIds;
    private HashSet<string> _affectedEdgeEndpointNodeIds => _geometryState.AffectedEdgeEndpointNodeIds;
    private List<EdgeModel> _affectedGeometryEdges => _geometryState.AffectedEdges;
    private GraphDocumentLookup _documentLookup => _geometryState.DocumentLookup;
    private GraphEdgeObstacleSnapshot? _defaultEdgeObstacleSnapshot
    {
        get => _geometryState.DefaultObstacleSnapshot;
        set => _geometryState.DefaultObstacleSnapshot = value;
    }

    private long _defaultEdgeObstacleSnapshotRevision
    {
        get => _geometryState.DefaultObstacleSnapshotRevision;
        set => _geometryState.DefaultObstacleSnapshotRevision = value;
    }

    private GraphEdgeRoutingService _edgeRoutingService = GraphEdgeRoutingService.Default;
    private GraphEdgeRoutingOptions _edgeRoutingOptions = GraphEdgeRoutingOptions.Default;
    private IGraphEdgeObstacleProvider _edgeObstacleProvider = GraphCanvasEdgeObstacleProvider.Default;
    private IGraphEditPolicy _editPolicy = AllowAllGraphEditPolicy.Instance;
    private IGraphBackgroundRenderer _backgroundRenderer = DefaultGraphBackgroundRenderer.Instance;
    private IGraphImageResolver? _imageResolver;
    private IGraphNodeBackgroundImageResolver? _backgroundImageResolver;
    private IGraphNodeRenderer _nodeRenderer = DefaultGraphNodeRenderer.Instance;
    private IGraphPortRenderer _portRenderer = DefaultGraphPortRenderer.Instance;
    private IGraphEdgeRenderer _edgeRenderer = DefaultGraphEdgeRenderer.Instance;
    private IGraphGroupRenderer _groupRenderer = DefaultGraphGroupRenderer.Instance;
    private IGraphNodeGeometryProvider _nodeGeometryProvider =
        DefaultGraphNodeGeometryProvider.Instance;
    private IGraphPortPositionProvider _portPositionProvider =
        DefaultGraphPortPositionProvider.Instance;
    private bool _isEdgeRoutingCacheEnabled = true;
    private long _smartEdgeObstacleRevision
    {
        get => _geometryState.SmartObstacleRevision;
        set => _geometryState.SmartObstacleRevision = value;
    }

    private GraphSize _minimumNodeSize = GraphNodeResizeCalculator.DefaultMinimumSize;
    private double _connectionSnapDistance = DefaultConnectionSnapDistance;
    private bool _isViewportCullingEnabled = true;
    private double _viewportCullingMargin = DefaultViewportCullingMargin;
    private Rect? _documentGraphBoundsCache
    {
        get => _geometryState.DocumentBounds;
        set => _geometryState.DocumentBounds = value;
    }

    private bool _isDocumentGraphBoundsDirty
    {
        get => _geometryState.IsDocumentBoundsDirty;
        set => _geometryState.IsDocumentBoundsDirty = value;
    }
}
