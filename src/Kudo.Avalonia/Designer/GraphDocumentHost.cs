using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;
using Kudo;
using Kudo.Avalonia;
using Kudo.Core.Documents;
using Kudo.Core.Runtime;
using System.Windows.Input;

namespace Kudo.Avalonia.Designer;

public sealed class GraphDocumentHost : GraphDesignerPanel
{
    public static readonly StyledProperty<GraphDocumentSession?> SessionProperty =
        AvaloniaProperty.Register<GraphDocumentHost, GraphDocumentSession?>(nameof(Session));

    public static readonly StyledProperty<IGraphRuntimeSession?> RuntimeSessionProperty =
        AvaloniaProperty.Register<GraphDocumentHost, IGraphRuntimeSession?>(nameof(RuntimeSession));

    public static readonly StyledProperty<IGraphEditCoordinator?> EditCoordinatorProperty =
        AvaloniaProperty.Register<GraphDocumentHost, IGraphEditCoordinator?>(nameof(EditCoordinator));

    public static readonly StyledProperty<ICommand?> NodeDropRequestedCommandProperty =
        GraphDocumentView.NodeDropRequestedCommandProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<IGraphNodeCatalog?> NodeCatalogProperty =
        GraphDocumentView.NodeCatalogProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<IGraphNodeFactory?> NodeFactoryProperty =
        GraphDocumentView.NodeFactoryProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<IGraphNodeDropHandler?> NodeDropHandlerProperty =
        GraphDocumentView.NodeDropHandlerProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<IGraphNodeCreationConfiguratorResolver?>
        NodeCreationConfiguratorsProperty =
            GraphDocumentView.NodeCreationConfiguratorsProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<bool> CreateDroppedNodesAutomaticallyProperty =
        GraphDocumentView.CreateDroppedNodesAutomaticallyProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<bool> ImportDroppedNodeAssetsProperty =
        GraphDocumentView.ImportDroppedNodeAssetsProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<bool> IsInteractionHintVisibleProperty =
        GraphDocumentView.IsInteractionHintVisibleProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        GraphCanvas.IsReadOnlyProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<string?> SelectedNodeIdProperty =
        GraphCanvas.SelectedNodeIdProperty.AddOwner<GraphDocumentHost>();

    public static readonly DirectProperty<GraphDocumentHost, IReadOnlyList<string>>
        SelectedNodeIdsProperty =
            GraphCanvas.SelectedNodeIdsProperty.AddOwner<GraphDocumentHost>(
                host => host.SelectedNodeIds);

    public static readonly DirectProperty<GraphDocumentHost, IReadOnlyList<string>>
        SelectedGroupIdsProperty =
            GraphCanvas.SelectedGroupIdsProperty.AddOwner<GraphDocumentHost>(
                host => host.SelectedGroupIds);

    public static readonly StyledProperty<string?> SelectedEdgeIdProperty =
        GraphCanvas.SelectedEdgeIdProperty.AddOwner<GraphDocumentHost>();

    public static readonly DirectProperty<GraphDocumentHost, IReadOnlyList<string>>
        SelectedEdgeIdsProperty =
            GraphCanvas.SelectedEdgeIdsProperty.AddOwner<GraphDocumentHost>(
                host => host.SelectedEdgeIds);

    public static readonly StyledProperty<string?> SelectedAnnotationIdProperty =
        GraphCanvas.SelectedAnnotationIdProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<ICommand?> SelectionChangedCommandProperty =
        GraphCanvas.SelectionChangedCommandProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<ICommand?> GraphEditCommittedCommandProperty =
        GraphCanvas.GraphEditCommittedCommandProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<ICommand?> GraphEditRejectedCommandProperty =
        GraphCanvas.GraphEditRejectedCommandProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<ICommand?> ConnectionValidationChangedCommandProperty =
        GraphCanvas.ConnectionValidationChangedCommandProperty.AddOwner<GraphDocumentHost>();

    public static readonly StyledProperty<IConnectionValidationRule?> ConnectionValidationRuleProperty =
        GraphCanvas.ConnectionValidationRuleProperty.AddOwner<GraphDocumentHost>();

    public static readonly DirectProperty<GraphDocumentHost, GraphDocumentView?> ViewProperty =
        AvaloniaProperty.RegisterDirect<GraphDocumentHost, GraphDocumentView?>(
            nameof(View),
            host => host.View);

    public static readonly DirectProperty<GraphDocumentHost, GraphPropertyEditorContext?>
        PropertyEditorContextProperty =
            AvaloniaProperty.RegisterDirect<GraphDocumentHost, GraphPropertyEditorContext?>(
                nameof(PropertyEditorContext),
                host => host.PropertyEditorContext);

    private readonly List<IDisposable> _viewBindings = [];
    private GraphDocumentView? _view;
    private GraphPropertyEditorContext? _propertyEditorContext;
    private IReadOnlyList<string> _selectedNodeIds = Array.Empty<string>();
    private IReadOnlyList<string> _selectedEdgeIds = Array.Empty<string>();
    private IReadOnlyList<string> _selectedGroupIds = Array.Empty<string>();
    private bool _isAttached;

    public GraphDocumentSession? Session
    {
        get => GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public IGraphRuntimeSession? RuntimeSession
    {
        get => GetValue(RuntimeSessionProperty);
        set => SetValue(RuntimeSessionProperty, value);
    }

    public ICommand? NodeDropRequestedCommand
    {
        get => GetValue(NodeDropRequestedCommandProperty);
        set => SetValue(NodeDropRequestedCommandProperty, value);
    }

    public IGraphNodeCatalog? NodeCatalog
    {
        get => GetValue(NodeCatalogProperty);
        set => SetValue(NodeCatalogProperty, value);
    }

    public IGraphNodeFactory? NodeFactory
    {
        get => GetValue(NodeFactoryProperty);
        set => SetValue(NodeFactoryProperty, value);
    }

    public IGraphNodeDropHandler? NodeDropHandler
    {
        get => GetValue(NodeDropHandlerProperty);
        set => SetValue(NodeDropHandlerProperty, value);
    }

    public IGraphEditCoordinator? EditCoordinator
    {
        get => GetValue(EditCoordinatorProperty);
        set => SetValue(EditCoordinatorProperty, value);
    }

    public IGraphNodeCreationConfiguratorResolver? NodeCreationConfigurators
    {
        get => GetValue(NodeCreationConfiguratorsProperty);
        set => SetValue(NodeCreationConfiguratorsProperty, value);
    }

    public bool CreateDroppedNodesAutomatically
    {
        get => GetValue(CreateDroppedNodesAutomaticallyProperty);
        set => SetValue(CreateDroppedNodesAutomaticallyProperty, value);
    }

    public bool ImportDroppedNodeAssets
    {
        get => GetValue(ImportDroppedNodeAssetsProperty);
        set => SetValue(ImportDroppedNodeAssetsProperty, value);
    }

    public bool IsInteractionHintVisible
    {
        get => GetValue(IsInteractionHintVisibleProperty);
        set => SetValue(IsInteractionHintVisibleProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public string? SelectedNodeId
    {
        get => GetValue(SelectedNodeIdProperty);
        set => SetValue(SelectedNodeIdProperty, value);
    }

    public IReadOnlyList<string> SelectedNodeIds => _selectedNodeIds;

    public IReadOnlyList<string> SelectedGroupIds => _selectedGroupIds;

    public string? SelectedEdgeId
    {
        get => GetValue(SelectedEdgeIdProperty);
        set => SetValue(SelectedEdgeIdProperty, value);
    }

    public IReadOnlyList<string> SelectedEdgeIds => _selectedEdgeIds;

    public string? SelectedAnnotationId
    {
        get => GetValue(SelectedAnnotationIdProperty);
        set => SetValue(SelectedAnnotationIdProperty, value);
    }

    public ICommand? SelectionChangedCommand
    {
        get => GetValue(SelectionChangedCommandProperty);
        set => SetValue(SelectionChangedCommandProperty, value);
    }

    public ICommand? GraphEditCommittedCommand
    {
        get => GetValue(GraphEditCommittedCommandProperty);
        set => SetValue(GraphEditCommittedCommandProperty, value);
    }

    public ICommand? GraphEditRejectedCommand
    {
        get => GetValue(GraphEditRejectedCommandProperty);
        set => SetValue(GraphEditRejectedCommandProperty, value);
    }

    public ICommand? ConnectionValidationChangedCommand
    {
        get => GetValue(ConnectionValidationChangedCommandProperty);
        set => SetValue(ConnectionValidationChangedCommandProperty, value);
    }

    public IConnectionValidationRule? ConnectionValidationRule
    {
        get => GetValue(ConnectionValidationRuleProperty);
        set => SetValue(ConnectionValidationRuleProperty, value);
    }

    public GraphDocumentView? View
    {
        get => _view;
        private set => SetAndRaise(ViewProperty, ref _view, value);
    }

    public GraphPropertyEditorContext? PropertyEditorContext
    {
        get => _propertyEditorContext;
        private set => SetAndRaise(
            PropertyEditorContextProperty,
            ref _propertyEditorContext,
            value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        RebuildView(resetSelection: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        DisposeView(clearSelectionSnapshot: true);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SessionProperty)
        {
            RebuildView(resetSelection: true);
        }
        else if (change.Property == RuntimeSessionProperty ||
                 change.Property == EditCoordinatorProperty)
        {
            RebuildView(resetSelection: false);
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            UpdatePropertyEditorContext();
        }
    }

    private void RebuildView(bool resetSelection)
    {
        DisposeView(clearSelectionSnapshot: false);
        if (resetSelection)
        {
            SetCurrentValue(SelectedNodeIdProperty, null);
            SetCurrentValue(SelectedEdgeIdProperty, null);
            SetCurrentValue(SelectedAnnotationIdProperty, null);
        }

        var session = Session;
        if (!_isAttached || session is null)
        {
            UpdateSelectedNodeIds(Array.Empty<string>());
            UpdateSelectedEdgeIds(Array.Empty<string>());
            UpdateSelectedGroupIds(Array.Empty<string>());
            return;
        }

        var runtimeSession = RuntimeSession;
        if (runtimeSession is not null &&
            !ReferenceEquals(runtimeSession.Document, session.Document))
        {
            runtimeSession = null;
        }

        var view = new GraphDocumentView(session, runtimeSession, EditCoordinator);
        BindViewProperty(view, PanelBrushProperty, nameof(PanelBrush));
        BindViewProperty(view, ElevatedBrushProperty, nameof(ElevatedBrush));
        BindViewProperty(view, DividerBrushProperty, nameof(DividerBrush));
        BindViewProperty(view, PrimaryTextBrushProperty, nameof(PrimaryTextBrush));
        BindViewProperty(view, SecondaryTextBrushProperty, nameof(SecondaryTextBrush));
        BindViewProperty(view, AccentBrushProperty, nameof(AccentBrush));
        BindViewProperty(view, SuccessBrushProperty, nameof(SuccessBrush));
        BindViewProperty(view, WarningBrushProperty, nameof(WarningBrush));
        BindViewProperty(view, ErrorBrushProperty, nameof(ErrorBrush));
        BindViewProperty(
            view,
            GraphDocumentView.NodeDropRequestedCommandProperty,
            nameof(NodeDropRequestedCommand));
        BindViewProperty(
            view,
            GraphDocumentView.NodeCatalogProperty,
            nameof(NodeCatalog));
        BindViewProperty(
            view,
            GraphDocumentView.NodeFactoryProperty,
            nameof(NodeFactory));
        BindViewProperty(
            view,
            GraphDocumentView.NodeDropHandlerProperty,
            nameof(NodeDropHandler));
        BindViewProperty(
            view,
            GraphDocumentView.NodeCreationConfiguratorsProperty,
            nameof(NodeCreationConfigurators));
        BindViewProperty(
            view,
            GraphDocumentView.CreateDroppedNodesAutomaticallyProperty,
            nameof(CreateDroppedNodesAutomatically));
        BindViewProperty(
            view,
            GraphDocumentView.ImportDroppedNodeAssetsProperty,
            nameof(ImportDroppedNodeAssets));
        BindViewProperty(
            view,
            GraphDocumentView.IsInteractionHintVisibleProperty,
            nameof(IsInteractionHintVisible));
        BindViewProperty(view.Canvas, GraphCanvas.IsReadOnlyProperty, nameof(IsReadOnly), BindingMode.TwoWay);
        BindViewProperty(
            view.Canvas,
            GraphCanvas.SelectedNodeIdProperty,
            nameof(SelectedNodeId),
            BindingMode.TwoWay);
        BindViewProperty(
            view.Canvas,
            GraphCanvas.SelectedEdgeIdProperty,
            nameof(SelectedEdgeId),
            BindingMode.TwoWay);
        BindViewProperty(
            view.Canvas,
            GraphCanvas.SelectedAnnotationIdProperty,
            nameof(SelectedAnnotationId),
            BindingMode.TwoWay);
        BindViewProperty(
            view.Canvas,
            GraphCanvas.SelectionChangedCommandProperty,
            nameof(SelectionChangedCommand));
        BindViewProperty(
            view.Canvas,
            GraphCanvas.GraphEditCommittedCommandProperty,
            nameof(GraphEditCommittedCommand));
        BindViewProperty(
            view.Canvas,
            GraphCanvas.GraphEditRejectedCommandProperty,
            nameof(GraphEditRejectedCommand));
        BindViewProperty(
            view.Canvas,
            GraphCanvas.ConnectionValidationChangedCommandProperty,
            nameof(ConnectionValidationChangedCommand));
        BindViewProperty(
            view.Canvas,
            GraphCanvas.ConnectionValidationRuleProperty,
            nameof(ConnectionValidationRule));

        view.Canvas.PropertyChanged += HandleCanvasPropertyChanged;
        view.StateChanged += HandleViewStateChanged;
        View = view;
        Content = view;
        UpdateSelectedNodeIds(view.Canvas.SelectedNodeIds);
        UpdateSelectedEdgeIds(view.Canvas.SelectedEdgeIds);
        UpdateSelectedGroupIds(view.Canvas.SelectedGroupIds);
        UpdatePropertyEditorContext();
    }

    private void BindViewProperty<T>(
        AvaloniaObject target,
        StyledProperty<T> property,
        string sourcePropertyName,
        BindingMode mode = BindingMode.OneWay) =>
        _viewBindings.Add(target.Bind(
            property,
            new Binding(sourcePropertyName)
            {
                Source = this,
                Mode = mode
            }));

    private void DisposeView(bool clearSelectionSnapshot)
    {
        Content = null;
        foreach (var binding in _viewBindings)
        {
            binding.Dispose();
        }

        _viewBindings.Clear();
        if (View is { } view)
        {
            view.Canvas.PropertyChanged -= HandleCanvasPropertyChanged;
            view.StateChanged -= HandleViewStateChanged;
            view.Dispose();
        }

        View = null;
        if (clearSelectionSnapshot)
        {
            UpdateSelectedNodeIds(Array.Empty<string>());
            UpdateSelectedEdgeIds(Array.Empty<string>());
            UpdateSelectedGroupIds(Array.Empty<string>());
        }

        PropertyEditorContext = null;
    }

    private void HandleViewStateChanged(object? sender, EventArgs e) =>
        UpdatePropertyEditorContext();

    private void HandleCanvasPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == GraphCanvas.SelectedNodeIdsProperty && sender is GraphCanvas canvas)
        {
            UpdateSelectedNodeIds(canvas.SelectedNodeIds);
        }
        else if (e.Property == GraphCanvas.SelectedEdgeIdsProperty && sender is GraphCanvas edgeCanvas)
        {
            UpdateSelectedEdgeIds(edgeCanvas.SelectedEdgeIds);
        }
        else if (e.Property == GraphCanvas.SelectedGroupIdsProperty && sender is GraphCanvas groupCanvas)
        {
            UpdateSelectedGroupIds(groupCanvas.SelectedGroupIds);
        }
    }

    private void UpdateSelectedNodeIds(IReadOnlyList<string> selectedNodeIds) =>
        SetAndRaise(SelectedNodeIdsProperty, ref _selectedNodeIds, selectedNodeIds);

    private void UpdateSelectedEdgeIds(IReadOnlyList<string> selectedEdgeIds) =>
        SetAndRaise(SelectedEdgeIdsProperty, ref _selectedEdgeIds, selectedEdgeIds);

    private void UpdateSelectedGroupIds(IReadOnlyList<string> selectedGroupIds) =>
        SetAndRaise(SelectedGroupIdsProperty, ref _selectedGroupIds, selectedGroupIds);

    private void UpdatePropertyEditorContext()
    {
        PropertyEditorContext = View?.PropertyEditorContext;
    }

}
