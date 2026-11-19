using Avalonia.Controls;
using Avalonia.Input;

namespace Kudo.Avalonia;

public partial class GraphCanvas : Decorator, IGraphViewportHost
{
    public GraphCanvas()
    {
        _edgeGraphPathProvider = GetEdgeGraphPath;
        var hitTester = new GraphCanvasHitTester(
            new GraphCanvasHitTesterContext
            {
                Document = () => Document,
                Viewport = () => Viewport,
                GridSettings = () => GridSettings,
                IsReadOnly = () => IsReadOnly,
                SelectedNodeId = () => SelectedNodeId,
                SelectedEdgeId = () => SelectedEdgeId,
                SelectedNodeIds = () => _selectedNodeIds,
                PortHitRadius = () => PortHitRadius,
                ShowPorts = () => ShowPorts &&
                    (!IsLevelOfDetailEnabled || Viewport.Zoom >= LodPortsZoomThreshold),
                IsEdgeRoutingCacheEnabled = () => IsEdgeRoutingCacheEnabled,
                ElementSpatialIndex = _elementSpatialIndex,
                EdgeSpatialIndex = _edgeSpatialIndex,
                EdgeGraphPathProvider = _edgeGraphPathProvider,
                ScreenToGraph = ScreenToGraph,
                GraphToScreen = GraphToScreen,
                GetPortScreenPosition = GetPortScreenPosition,
                GetEdgeGraphPath = GetEdgeGraphPath,
                GetEdgeScreenRoute = GetEdgeScreenRoute,
                IsNodeVisible = IsNodeVisible,
                GetNodeResizeHandlePositions = GetNodeResizeHandlePositions,
                GetSelectedNodesForRotation = GetSelectedNodesForRotation,
                GetSelectedNodeRotationBounds = GetSelectedNodeRotationBounds,
                GetNodeRotationHandlePosition = GetNodeRotationHandlePosition,
                GetSelectionRotationHandlePositions = bounds => GetSelectionRotationHandlePositions(bounds)
            },
            new GraphCanvasHitTestMetrics(
                EdgeHitTolerance,
                EdgeEndpointHitRadius,
                EdgeWaypointHitRadius,
                NodeResizeHandleHitRadius,
                NodeRotationHandleHitRadius));
        var annotationController = new GraphCanvasAnnotationController(new GraphCanvasAnnotationContext
        {
            Document = () => Document,
            IsReadOnly = () => IsReadOnly,
            Viewport = () => Viewport,
            SelectedNodeIds = () => _selectedNodeIds,
            SelectedNodeId = () => SelectedNodeId,
            SetSelectedNodeId = value => SelectedNodeId = value,
            SelectedEdgeIds = () => _selectedEdgeIds,
            SelectedEdgeId = () => SelectedEdgeId,
            SetSelectedEdgeId = value => SelectedEdgeId = value,
            SelectedAnnotationId = () => SelectedAnnotationId,
            SetSelectedAnnotationId = value => SetValue(SelectedAnnotationIdProperty, value),
            SetIsUpdatingNodeSelection = value => _isUpdatingNodeSelection = value,
            SetIsUpdatingEdgeSelection = value => _isUpdatingEdgeSelection = value,
            SetSuppressSelectionChanged = value => _suppressSelectionChanged = value,
            ScreenToGraph = ScreenToGraph,
            CanPerformEdit = CanPerformEdit,
            CommitGraphEdit = CommitGraphEdit,
            InvalidateVisual = InvalidateVisual,
            RaiseSelectionChanged = RaiseSelectionChanged,
            RefreshNodeControlStates = RefreshNodeControlStates
        });
        var nodeControlController = new GraphCanvasNodeControlController(new GraphCanvasNodeControlContext
        {
            Owner = this,
            NodeControlLayer = _nodeControlLayer,
            NodeControls = NodeControls,
            Document = () => Document,
            Viewport = () => Viewport,
            IsViewportCullingEnabled = () => IsViewportCullingEnabled,
            GetViewportCullingGraphBounds = GetViewportCullingGraphBounds,
            ElementSpatialIndex = _elementSpatialIndex,
            IsNodeVisible = IsNodeVisible,
            GetNodeScreenRect = ToScreenRect,
            GetNodeGraphRect = ToGraphRect,
            IsNodeSelected = IsNodeSelected,
            ToggleNodeSelection = ToggleNodeSelection,
            SetSelectedNodes = SetSelectedNodes,
            InvalidateVisual = InvalidateVisual
        });
        var placementController = new GraphCanvasPlacementController(new GraphCanvasPlacementContext
        {
            Document = () => Document,
            GridSettings = () => GridSettings,
            Viewport = () => Viewport,
            AlignmentGuides = () => _activeAlignmentGuides,
            SetAlignmentGuides = value => _activeAlignmentGuides = value,
            IsNodeVisible = IsNodeVisible,
            InvalidateVisual = InvalidateVisual
        });
        var runtimeController = new GraphCanvasRuntimeController(
            _renderState,
            new GraphCanvasRuntimeContext
            {
                Document = () => Document,
                RuntimeSnapshot = () => RuntimeSnapshot,
                ImageResolver = () => ImageResolver,
                InvalidateVisual = InvalidateVisual
            });
        var renderer = new GraphCanvasRenderer(
            _geometryState,
            _renderState,
            new GraphCanvasRendererContext
            {
                Bounds = () => Bounds,
                Viewport = () => Viewport,
                ViewportOptions = () => ViewportOptions,
                GridSettings = () => GridSettings,
                Document = () => Document,
                GetViewportCullingGraphBounds = GetViewportCullingGraphBounds,
                GetDocumentGraphBounds = GetDocumentGraphBounds,
                IsLevelOfDetailEnabled = () => IsLevelOfDetailEnabled,
                ShowPorts = () => ShowPorts,
                LodSimplifiedZoomThreshold = () => LodSimplifiedZoomThreshold,
                LodLabelsZoomThreshold = () => LodLabelsZoomThreshold,
                LodPortsZoomThreshold = () => LodPortsZoomThreshold,
                PortLabelVisibility = () => PortLabelVisibility,
                SymbolNameVisibility = () => SymbolNameVisibility,
                SymbolDescriptionVisibility = () => SymbolDescriptionVisibility,
                IsOverviewCacheBlockedByInteraction = IsOverviewCacheBlockedByInteraction,
                IsEdgeRoutingCacheEnabled = () => IsEdgeRoutingCacheEnabled,
                BackgroundRenderer = () => BackgroundRenderer,
                NodeRenderer = () => NodeRenderer,
                PortRenderer = () => PortRenderer,
                EdgeRenderer = () => EdgeRenderer,
                GroupRenderer = () => GroupRenderer,
                ImageResolver = () => ImageResolver,
                BackgroundImageResolver = () => BackgroundImageResolver,
                RuntimeSnapshot = () => RuntimeSnapshot,
                SelectedNodeIds = () => _selectedNodeIds,
                BreakpointNodeIds = () => BreakpointNodeIds,
                SelectedEdgeIds = () => _selectedEdgeIds,
                EditingNodeTitleId = () => EditingNodeTitleId,
                ReconnectingEdge = () => (_interactionSession as ReconnectEdgeInteractionSession)?.Edge,
                GetConnectionStartPort = GetConnectionStartPort,
                GetConnectionCandidatePort = GetConnectionCandidatePort,
                HoveredPort = () => _hoveredPort,
                HoveredNodeId = () => _hoveredNodeId,
                CreatePalette = CreateRenderPalette,
                RenderInteractionOverlays = RenderInteractionOverlays
            },
            new GraphCanvasRenderCallbacks
            {
                GetEdgeGraphPath = GetEdgeGraphPath,
                GetEdgeScreenRoute = GetEdgeScreenRoute,
                GraphToScreen = GraphToScreen,
                GetNodeScreenRect = ToScreenRect,
                GetNodeVisualScreenRect = GetNodeVisualScreenRect,
                GetGroupScreenRect = GetGroupScreenRect,
                GetCollapsedGroupProxyPorts = GetCollapsedGroupProxyPorts,
                IsNodeVisible = IsNodeVisible,
                IsNodeSelected = IsNodeSelected,
                IsGroupSelected = IsGroupSelected,
                HasBreakpoint = HasBreakpoint,
                IsEdgeCuttingCandidate = IsEdgeCuttingCandidate,
                GetPortScreenPosition = GetPortScreenPosition,
                RenderAnnotations = RenderAnnotations
            });
        var viewportController = new GraphCanvasViewportController(
            () => Viewport,
            () => ViewportOptions,
            () => Bounds.Size,
            SetViewportCore);
        var selectionController = new GraphCanvasSelectionController(new GraphCanvasSelectionContext
        {
            Document = () => Document,
            SelectedNodeIds = () => _selectedNodeIds,
            SelectedNodeId = () => SelectedNodeId,
            SetSelectedNodeId = value => SelectedNodeId = value,
            SelectedEdgeIds = () => _selectedEdgeIds,
            SelectedEdgeId = () => SelectedEdgeId,
            SetSelectedEdgeId = value => SelectedEdgeId = value,
            SelectedAnnotationId = () => SelectedAnnotationId,
            SetSelectedAnnotationId = value => SetValue(SelectedAnnotationIdProperty, value),
            SetIsUpdatingNodeSelection = value => _isUpdatingNodeSelection = value,
            SetIsUpdatingEdgeSelection = value => _isUpdatingEdgeSelection = value,
            SetSuppressSelectionChanged = value => _suppressSelectionChanged = value,
            BreakpointNodeIds = () => BreakpointNodeIds,
            SelectedNodeIdsSnapshot = () => _canvasState.SelectedNodeIdsSnapshot,
            SetSelectedNodeIdsSnapshot = value => SetAndRaise(
                SelectedNodeIdsProperty,
                ref _canvasState.SelectedNodeIdsSnapshot,
                value),
            SelectedEdgeIdsSnapshot = () => _canvasState.SelectedEdgeIdsSnapshot,
            SetSelectedEdgeIdsSnapshot = value => SetAndRaise(
                SelectedEdgeIdsProperty,
                ref _canvasState.SelectedEdgeIdsSnapshot,
                value),
            SelectedGroupIdsSnapshot = () => _canvasState.SelectedGroupIdsSnapshot,
            SetSelectedGroupIdsSnapshot = value => SetAndRaise(
                SelectedGroupIdsProperty,
                ref _canvasState.SelectedGroupIdsSnapshot,
                value),
            DocumentLookup = _documentLookup,
            ElementSpatialIndex = _elementSpatialIndex,
            CanPerformEdit = CanPerformEdit,
            GetViewportGraphBounds = GetViewportGraphBounds,
            GetNodeGraphRect = ToGraphRect,
            InvalidateVisual = InvalidateVisual,
            RefreshNodeControlStates = RefreshNodeControlStates,
            NotifySelectionChanged = NotifySelectionChanged
        });
        var editController = new GraphCanvasEditController(
            new GraphCanvasEditContext
            {
                Document = () => Document,
                IsReadOnly = () => IsReadOnly,
                IsPointerInteracting = () => IsPointerInteracting,
                SelectedNodeIds = () => _selectedNodeIds,
                SelectedEdgeIds = () => _selectedEdgeIds,
                SelectedEdgeId = () => SelectedEdgeId,
                SetSelectedEdgeId = value => SelectedEdgeId = value,
                SetSelectedEdges = (edgeIds, primaryEdgeId) => SetSelectedEdges(edgeIds, primaryEdgeId),
                SelectedAnnotationId = () => SelectedAnnotationId,
                SetLastConnectionValidation = value => LastConnectionValidation = value,
                ValidateConnection = ValidateConnection,
                CanPerformEdit = CanPerformEdit,
                CanAuthorizeEdit = CanAuthorizeEdit,
                TryAuthorizeEdit = TryAuthorizeEdit,
                SetSelectedNodes = SetSelectedNodes,
                ClearSelection = ClearSelection,
                CommitGraphEdit = CommitGraphEdit,
                ClampNodeSize = ClampNodeSize,
                GetEdgeScreenRoute = GetEdgeScreenRoute,
                ScreenToGraph = ScreenToGraph,
                FindWaypointInsertionIndex = FindWaypointInsertionIndex,
                DeleteSelectedAnnotation = DeleteSelectedAnnotation
            },
            new GraphCanvasEditMetrics(
                EdgeHitTolerance,
                EdgeLabelProgress));
        var groupController = new GraphCanvasGroupController(new GraphCanvasGroupContext
        {
            Document = () => Document,
            IsReadOnly = () => IsReadOnly,
            SelectedNodeIds = () => _selectedNodeIds,
            GetDocumentOrderedNodeIds = GetDocumentOrderedNodeIds,
            IsGroupSelected = IsGroupSelected,
            GetSelectedGroups = GetSelectedGroups,
            CanAuthorizeEdit = CanAuthorizeEdit,
            CanPerformEdit = CanPerformEdit,
            SetSelectedNodes = SetSelectedNodes,
            CommitGraphEdit = CommitGraphEdit
        });
        var nodeTitleEditingController = new GraphCanvasNodeTitleEditor(
            new GraphCanvasNodeTitleEditorContext
            {
                Owner = this,
                NodeControlLayer = _nodeControlLayer,
                Document = () => Document,
                IsEnabled = () => IsNodeTitleEditingEnabled,
                IsReadOnly = () => IsReadOnly,
                Viewport = () => Viewport,
                IsLevelOfDetailEnabled = () => IsLevelOfDetailEnabled,
                LodLabelsZoomThreshold = () => LodLabelsZoomThreshold,
                SymbolNameVisibility = () => SymbolNameVisibility,
                SelectedNodeIds = () => _selectedNodeIds,
                NodeRenderer = () => NodeRenderer,
                SymbolNameOffset = () => SymbolNameOffset,
                IsNodeVisible = IsNodeVisible,
                CanAuthorizeEdit = CanAuthorizeEdit,
                CanPerformEdit = CanPerformEdit,
                GetNodeScreenRect = ToScreenRect,
                GetNodeVisualScreenRect = GetNodeVisualScreenRect,
                InverseTransformNodeScreenPoint = InverseTransformNodeScreenPoint,
                CommitAnnotationTextEdit = CommitAnnotationTextEdit,
                CommitEdgeLabelEdit = CommitEdgeLabelEdit,
                CommitGraphEdit = CommitGraphEdit,
                InvalidateVisual = InvalidateVisual,
                FocusCanvas = () => Focus()
            });
        var edgeLabelEditingController = new GraphCanvasEdgeLabelEditor(
            new GraphCanvasEdgeLabelEditorContext
            {
                Owner = this,
                NodeControlLayer = _nodeControlLayer,
                Document = () => Document,
                IsEnabled = () => IsEdgeLabelEditingEnabled,
                IsReadOnly = () => IsReadOnly,
                Viewport = () => Viewport,
                IsLevelOfDetailEnabled = () => IsLevelOfDetailEnabled,
                LodLabelsZoomThreshold = () => LodLabelsZoomThreshold,
                EdgeRenderer = () => EdgeRenderer,
                GetEdgeLabelScreenPosition = GetEdgeLabelScreenPosition,
                CanPerformEdit = CanPerformEdit,
                CommitNodeTitleEdit = CommitNodeTitleEdit,
                CommitAnnotationTextEdit = CommitAnnotationTextEdit,
                ClearSelection = ClearSelection,
                SetSelectedEdgeId = value => SelectedEdgeId = value,
                CommitGraphEdit = CommitGraphEdit,
                InvalidateVisual = InvalidateVisual,
                FocusCanvas = () => Focus()
            });
        var annotationTextEditingController = new GraphCanvasAnnotationTextEditor(
            new GraphCanvasAnnotationTextEditorContext
            {
                Owner = this,
                NodeControlLayer = _nodeControlLayer,
                Document = () => Document,
                IsEnabled = () => IsAnnotationTextEditingEnabled,
                IsReadOnly = () => IsReadOnly,
                Viewport = () => Viewport,
                CanPerformEdit = CanPerformEdit,
                CommitNodeTitleEdit = CommitNodeTitleEdit,
                CommitEdgeLabelEdit = CommitEdgeLabelEdit,
                SetSelectedAnnotation = annotationId => SetSelectedAnnotation(annotationId),
                GetAnnotationScreenRect = GetAnnotationScreenRect,
                CommitGraphEdit = CommitGraphEdit,
                InvalidateVisual = InvalidateVisual,
                FocusCanvas = () => Focus()
            });
        var annotationRenderer = new GraphCanvasAnnotationRenderer(new GraphCanvasAnnotationRendererContext
        {
            Document = () => Document,
            Viewport = () => Viewport,
            SelectedAnnotationId = () => SelectedAnnotationId,
            CanvasBackgroundBrush = () => CanvasBackgroundBrush,
            AnnotationFillBrush = () => AnnotationFillBrush,
            AnnotationStrokeBrush = () => AnnotationStrokeBrush,
            SelectedAnnotationStrokeBrush = () => SelectedAnnotationStrokeBrush,
            AnnotationTextBrush = () => AnnotationTextBrush,
            IsEditingAnnotation = annotation => annotationTextEditingController.IsEditingAnnotation(annotation)
        });
        var interactionCoordinator = new GraphCanvasInteractionCoordinator(_canvasState, this);
        var autoPanController = new GraphCanvasAutoPanController(new GraphCanvasAutoPanContext
        {
            IsEnabled = () => IsAutoPanEnabled,
            EdgeDistance = () => AutoPanEdgeDistance,
            Speed = () => AutoPanSpeed,
            Bounds = () => Bounds,
            InteractionSession = () => interactionCoordinator.ActiveSession,
            PanBy = delta => PanBy(delta)
        });
        var nodePushController = new GraphCanvasNodePushController(new GraphCanvasNodePushContext
        {
            GetIsEnabled = () => IsNodePushingEnabled,
            GetIsReadOnly = () => IsReadOnly,
            GetIsPointerInteracting = () => IsPointerInteracting,
            GetDocument = () => Document,
            IsNodeVisible = IsNodeVisible,
            CanPerformEdit = CanPerformEdit,
            GetCommandName = () => L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.PushNodesCommand),
            CommitGraphEdit = CommitGraphEdit
        });
        var edgeCutController = new GraphCanvasEdgeCutController(new GraphCanvasEdgeCutContext
        {
            GetIsEnabled = () => IsEdgeCuttingEnabled,
            GetIsReadOnly = () => IsReadOnly,
            GetDocument = () => Document,
            GetIsEdgeRoutingCacheEnabled = () => IsEdgeRoutingCacheEnabled,
            EdgeSpatialIndex = _edgeSpatialIndex,
            GetEdgeGraphPath = GetEdgeGraphPath,
            CanPerformEdit = CanPerformEdit,
            GetCommandName = () => L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.CutEdgesCommand),
            GetSelectedEdgeId = () => SelectedEdgeId,
            GetSelectedEdgeIds = () => SelectedEdgeIds,
            SetSelectedEdges = (edgeIds, primaryEdgeId) => SetSelectedEdges(edgeIds, primaryEdgeId),
            CommitGraphEdit = CommitGraphEdit
        });
        var keyboardController = new GraphCanvasKeyboardController(
            this,
            () => interactionCoordinator.ActiveSession is PanInteractionSession,
            (key, modifiers) => InputGestures.KeyboardPan.Matches(key, modifiers),
            key => InputGestures.KeyboardPan.Key == key && key != Key.None,
            () => IsReadOnly,
            point => HitTestSelectedNodeRotationHandle(point) is not null,
            point => HitTestSelectedNodeResizeHandle(point)?.Handle ??
                     HitTestSelectedAnnotationResizeHandle(point)?.Handle,
            point => HitTestPort(point) is not null);
        var edgeObserver = new GraphCanvasEdgeObserver(
            () => Document,
            InvalidateEdgeGeometry,
            () => _documentLookup.Invalidate(),
            InvalidateOverviewDrawing,
            UpdateRuntimeAnimationState,
            InvalidateVisual,
            () => GraphChanged?.Invoke(this, EventArgs.Empty));
        var graphChangeController = new GraphCanvasGraphChangeController(
            new GraphCanvasGraphChangeContext
            {
                EdgeObserver = edgeObserver,
                CancelManualConnectionDraft = () => CancelManualConnectionDraft(),
                ClearEdgeRoutingCache = ClearEdgeRoutingCache,
                InvalidateDocumentLookup = () => _documentLookup.Invalidate(),
                InvalidateElementSpatialIndex = () => _elementSpatialIndex.Invalidate(),
                InvalidateOverviewDrawing = InvalidateOverviewDrawing,
                UpdateSelectionSnapshots = UpdateSelectionSnapshots,
                SynchronizeNodeControlHosts = SynchronizeNodeControlHosts,
                RefreshNodeControlStates = RefreshNodeControlStates,
                UpdateNodeControlHostLayout = UpdateNodeControlHostLayout,
                UpdateNodeTitleEditorLayout = UpdateNodeTitleEditorLayout,
                UpdateAnnotationTextEditorLayout = UpdateAnnotationTextEditorLayout,
                UpdateEdgeLabelEditorLayout = UpdateEdgeLabelEditorLayout,
                UpdateRuntimeAnimationState = UpdateRuntimeAnimationState,
                InvalidateVisual = InvalidateVisual,
                RaiseGraphChanged = () => GraphChanged?.Invoke(this, EventArgs.Empty),
                NotifySelectionChanged = NotifySelectionChanged,
                InvalidateNodeGeometry = InvalidateNodeGeometry
            });
        var propertyChangeController = new GraphCanvasPropertyChangeController(
            new GraphCanvasPropertyChangeContext
            {
                IsPaletteProperty = IsPaletteProperty,
                GetIsUpdatingNodeSelection = () => _isUpdatingNodeSelection,
                GetIsUpdatingEdgeSelection = () => _isUpdatingEdgeSelection,
                SynchronizeSelectedNodeSelection = () =>
                {
                    _suppressSelectionChanged = true;
                    _isUpdatingNodeSelection = true;
                    _isUpdatingEdgeSelection = true;
                    try
                    {
                        _selectedNodeIds.Clear();
                        if (!string.IsNullOrWhiteSpace(SelectedNodeId) &&
                            Document?.FindNode(SelectedNodeId) is not null)
                        {
                            _selectedNodeIds.Add(SelectedNodeId);
                        }
                        else if (!string.IsNullOrWhiteSpace(SelectedNodeId))
                        {
                            SetValue(SelectedNodeIdProperty, null);
                        }

                        _selectedEdgeIds.Clear();
                        SetValue(SelectedEdgeIdProperty, null);
                        SetValue(SelectedAnnotationIdProperty, null);
                    }
                    finally
                    {
                        _isUpdatingEdgeSelection = false;
                        _isUpdatingNodeSelection = false;
                        _suppressSelectionChanged = false;
                    }
                },
                SynchronizeSelectedEdgeSelection = () =>
                {
                    _suppressSelectionChanged = true;
                    _isUpdatingNodeSelection = true;
                    _isUpdatingEdgeSelection = true;
                    try
                    {
                        _selectedNodeIds.Clear();
                        SetValue(SelectedNodeIdProperty, null);
                        SetValue(SelectedAnnotationIdProperty, null);
                        _selectedEdgeIds.Clear();
                        if (!string.IsNullOrWhiteSpace(SelectedEdgeId) &&
                            Document?.Edges.Any(edge => edge.Id == SelectedEdgeId) == true)
                        {
                            _selectedEdgeIds.Add(SelectedEdgeId);
                        }
                        else if (!string.IsNullOrWhiteSpace(SelectedEdgeId))
                        {
                            SetValue(SelectedEdgeIdProperty, null);
                        }
                    }
                    finally
                    {
                        _isUpdatingEdgeSelection = false;
                        _isUpdatingNodeSelection = false;
                        _suppressSelectionChanged = false;
                    }
                },
                SynchronizeSelectedAnnotationSelection = () =>
                {
                    _suppressSelectionChanged = true;
                    _isUpdatingNodeSelection = true;
                    _isUpdatingEdgeSelection = true;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(SelectedAnnotationId) &&
                            Document?.FindAnnotation(SelectedAnnotationId) is null)
                        {
                            SetValue(SelectedAnnotationIdProperty, null);
                        }

                        _selectedNodeIds.Clear();
                        SetValue(SelectedNodeIdProperty, null);
                        _selectedEdgeIds.Clear();
                        SetValue(SelectedEdgeIdProperty, null);
                    }
                    finally
                    {
                        _isUpdatingEdgeSelection = false;
                        _isUpdatingNodeSelection = false;
                        _suppressSelectionChanged = false;
                    }
                },
                InvalidateVisual = InvalidateVisual,
                UpdateSelectionSnapshots = UpdateSelectionSnapshots,
                EdgeObserver = edgeObserver,
                CancelNodeTitleEdit = () => CancelNodeTitleEdit(),
                CancelAnnotationTextEdit = () => CancelAnnotationTextEdit(),
                CancelEdgeLabelEdit = () => CancelEdgeLabelEdit(),
                ClearEdgeRoutingCache = ClearEdgeRoutingCache,
                InvalidateDocumentLookup = () => _documentLookup.Invalidate(),
                InvalidateElementSpatialIndex = () => _elementSpatialIndex.Invalidate(),
                SynchronizeNodeControlHosts = SynchronizeNodeControlHosts,
                UpdateRuntimeAnimationState = UpdateRuntimeAnimationState,
                InvalidateOverviewDrawing = InvalidateOverviewDrawing,
                UpdateEditorLayouts = () =>
                {
                    UpdateNodeControlHostLayout();
                    UpdateNodeTitleEditorLayout();
                    UpdateAnnotationTextEditorLayout();
                    UpdateEdgeLabelEditorLayout();
                },
                GetSuppressViewportChangedEvent = () => _suppressViewportChangedEvent,
                RaiseViewportChanged = (previous, current) => RaiseViewportChanged(
                    previous,
                    current,
                    ViewportOptions,
                    ViewportOptions,
                    _pendingViewportChangeReason ?? GraphViewportChangeReason.External),
                ApplyViewportOptionsChange = ApplyViewportOptionsChange,
                GetIsNodeTitleEditingEnabled = () => IsNodeTitleEditingEnabled,
                GetIsAnnotationTextEditingEnabled = () => IsAnnotationTextEditingEnabled,
                GetIsEdgeLabelEditingEnabled = () => IsEdgeLabelEditingEnabled,
                GetIsEdgeCuttingEnabled = () => IsEdgeCuttingEnabled,
                GetIsNodePushingEnabled = () => IsNodePushingEnabled,
                GetActiveInteraction = () => _interactionSession,
                CancelActivePointerInteraction = () => CancelActivePointerInteraction(),
                CancelManualConnectionDraft = () => CancelManualConnectionDraft(),
                RefreshNodeControlStates = RefreshNodeControlStates,
                GetSuppressSelectionChanged = () => _suppressSelectionChanged,
                RaiseSelectionChanged = RaiseSelectionChanged,
                GetLastConnectionValidation = () => LastConnectionValidation,
                RaiseConnectionValidationChanged = validation =>
                {
                    ConnectionValidationChanged?.Invoke(this, validation);
                    ExecuteNotificationCommand(ConnectionValidationChangedCommand, validation);
                },
                ClearAlignmentGuides = () => _activeAlignmentGuides = [],
                UpdateAutoPanState = UpdateAutoPanState,
                GetIsReadOnly = () => IsReadOnly,
                ClearNodePlacementPreview = ClearNodePlacementPreview,
                UpdatePointerCursor = () => keyboardController.UpdatePointerCursor(),
                UpdateRuntimeSnapshot = () => UpdateRuntimeSnapshot(RuntimeSnapshot)
            });
        _services = new GraphCanvasServices
        {
            ViewportController = viewportController,
            SelectionController = selectionController,
            EditController = editController,
            GroupController = groupController,
            AnnotationController = annotationController,
            AnnotationRenderer = annotationRenderer,
            NodeControlController = nodeControlController,
            PlacementController = placementController,
            RuntimeController = runtimeController,
            NodeTitleEditingController = nodeTitleEditingController,
            EdgeLabelEditingController = edgeLabelEditingController,
            AnnotationTextEditingController = annotationTextEditingController,
            KeyboardController = keyboardController,
            InteractionCoordinator = interactionCoordinator,
            AutoPanController = autoPanController,
            NodePushController = nodePushController,
            EdgeCutController = edgeCutController,
            EdgeObserver = edgeObserver,
            GraphChangeController = graphChangeController,
            PropertyChangeController = propertyChangeController,
            HitTester = hitTester,
            Renderer = renderer
        };
        ClipToBounds = true;
        Focusable = true;
        Child = _nodeControlLayer;
        InitializeNodeTitleEditor();
        InitializeAnnotationTextEditor();
        InitializeEdgeLabelEditor();
    }
}
