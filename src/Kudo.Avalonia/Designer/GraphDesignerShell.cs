using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Kudo;
using Kudo.Avalonia;
using Kudo.Core.Runtime;
using System.Windows.Input;

namespace Kudo.Avalonia.Designer;

public class GraphDesignerShell : UserControl, IGraphViewportHost
{
    public static readonly StyledProperty<IGraphNodeCatalog?> CatalogProperty =
        GraphNodePalette.CatalogProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<string?> ModelDirectoryProperty =
        GraphNodePalette.ModelDirectoryProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<ICommand?> NodeRequestedCommandProperty =
        GraphNodePalette.NodeRequestedCommandProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<bool> IsPaletteClickToAddEnabledProperty =
        GraphNodePalette.IsClickToAddEnabledProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<GraphDocument?> DocumentProperty =
        GraphCanvas.DocumentProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        GraphCanvas.IsReadOnlyProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<GraphViewport> ViewportProperty =
        GraphCanvas.ViewportProperty.AddOwner<GraphDesignerShell>(
            new StyledPropertyMetadata<GraphViewport>(
                GraphViewport.Default,
                BindingMode.TwoWay,
                static (_, value) => NormalizeViewport(value)));

    public static readonly StyledProperty<GraphViewportOptions> ViewportOptionsProperty =
        GraphCanvas.ViewportOptionsProperty.AddOwner<GraphDesignerShell>(
            new StyledPropertyMetadata<GraphViewportOptions>(
                GraphViewportOptions.Default,
                BindingMode.TwoWay,
                static (_, value) => (value ?? GraphViewportOptions.Default).Normalize()));

    public static readonly StyledProperty<GraphGridSettings> GridSettingsProperty =
        GraphCanvas.GridSettingsProperty.AddOwner<GraphDesignerShell>(
            new StyledPropertyMetadata<GraphGridSettings>(
                GraphGridSettings.Default,
                BindingMode.TwoWay,
                static (owner, value) => CoerceCanvasValue(
                    GraphCanvas.GridSettingsProperty,
                    owner,
                    value)));

    public static readonly StyledProperty<GraphRuntimeSnapshot?> RuntimeSnapshotProperty =
        GraphCanvas.RuntimeSnapshotProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<IReadOnlyCollection<string>> BreakpointNodeIdsProperty =
        GraphCanvas.BreakpointNodeIdsProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<string?> SelectedNodeIdProperty =
        GraphCanvas.SelectedNodeIdProperty.AddOwner<GraphDesignerShell>();

    public static readonly DirectProperty<GraphDesignerShell, IReadOnlyList<string>>
        SelectedNodeIdsProperty =
            GraphCanvas.SelectedNodeIdsProperty.AddOwner<GraphDesignerShell>(
                shell => shell.SelectedNodeIds);

    public static readonly DirectProperty<GraphDesignerShell, IReadOnlyList<string>>
        SelectedGroupIdsProperty =
            GraphCanvas.SelectedGroupIdsProperty.AddOwner<GraphDesignerShell>(
                shell => shell.SelectedGroupIds);

    public static readonly StyledProperty<string?> SelectedEdgeIdProperty =
        GraphCanvas.SelectedEdgeIdProperty.AddOwner<GraphDesignerShell>();

    public static readonly DirectProperty<GraphDesignerShell, IReadOnlyList<string>>
        SelectedEdgeIdsProperty =
            GraphCanvas.SelectedEdgeIdsProperty.AddOwner<GraphDesignerShell>(
                shell => shell.SelectedEdgeIds);

    public static readonly StyledProperty<string?> SelectedAnnotationIdProperty =
        GraphCanvas.SelectedAnnotationIdProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<ICommand?> SelectionChangedCommandProperty =
        GraphCanvas.SelectionChangedCommandProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<ICommand?> GraphEditCommittedCommandProperty =
        GraphCanvas.GraphEditCommittedCommandProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<ICommand?> GraphEditRejectedCommandProperty =
        GraphCanvas.GraphEditRejectedCommandProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<IGraphEditCoordinator> EditCoordinatorProperty =
        GraphCanvas.EditCoordinatorProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<ICommand?> ConnectionValidationChangedCommandProperty =
        GraphCanvas.ConnectionValidationChangedCommandProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<IConnectionValidationRule?> ConnectionValidationRuleProperty =
        GraphCanvas.ConnectionValidationRuleProperty.AddOwner<GraphDesignerShell>();

    public static readonly StyledProperty<bool> IsLevelOfDetailEnabledProperty =
        GraphCanvas.IsLevelOfDetailEnabledProperty.AddOwner<GraphDesignerShell>(
            new StyledPropertyMetadata<bool>(true, BindingMode.TwoWay));

    public static readonly StyledProperty<bool> ShowPortsProperty =
        GraphCanvas.ShowPortsProperty.AddOwner<GraphDesignerShell>(
            new StyledPropertyMetadata<bool>(true, BindingMode.TwoWay));

    public static readonly StyledProperty<bool> IsMinimapVisibleProperty =
        GraphDocumentView.IsMinimapVisibleProperty.AddOwner<GraphDesignerShell>(
            new StyledPropertyMetadata<bool>(true, BindingMode.TwoWay));

    public static readonly StyledProperty<double> LodPortsZoomThresholdProperty =
        GraphCanvas.LodPortsZoomThresholdProperty.AddOwner<GraphDesignerShell>(
            new StyledPropertyMetadata<double>(
                0.4,
                BindingMode.TwoWay,
                static (owner, value) => CoerceCanvasValue(
                    GraphCanvas.LodPortsZoomThresholdProperty,
                    owner,
                    value)));

    public static readonly StyledProperty<double> LodLabelsZoomThresholdProperty =
        GraphCanvas.LodLabelsZoomThresholdProperty.AddOwner<GraphDesignerShell>(
            new StyledPropertyMetadata<double>(
                0.3,
                BindingMode.TwoWay,
                static (owner, value) => CoerceCanvasValue(
                    GraphCanvas.LodLabelsZoomThresholdProperty,
                    owner,
                    value)));

    public static readonly StyledProperty<double> LodSimplifiedZoomThresholdProperty =
        GraphCanvas.LodSimplifiedZoomThresholdProperty.AddOwner<GraphDesignerShell>(
            new StyledPropertyMetadata<double>(
                0.4,
                BindingMode.TwoWay,
                static (owner, value) => CoerceCanvasValue(
                    GraphCanvas.LodSimplifiedZoomThresholdProperty,
                    owner,
                    value)));

    private readonly GraphCanvas _canvas = new();
    private readonly GraphMinimap _minimap;
    private readonly GraphNodePalette _nodePalette = new();
    private IReadOnlyList<string> _selectedNodeIds = Array.Empty<string>();
    private IReadOnlyList<string> _selectedEdgeIds = Array.Empty<string>();
    private IReadOnlyList<string> _selectedGroupIds = Array.Empty<string>();

    public GraphDesignerShell()
    {
        _minimap = new GraphMinimap
        {
            TargetCanvas = _canvas,
            Width = 220,
            Height = 150,
            Margin = new Thickness(16),
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Bottom
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("220,*")
        };

        var canvasHost = new Grid();
        canvasHost.Children.Add(_canvas);
        canvasHost.Children.Add(_minimap);

        Grid.SetColumn(_nodePalette, 0);
        Grid.SetColumn(canvasHost, 1);
        grid.Children.Add(_nodePalette);
        grid.Children.Add(canvasHost);

        _canvas.PropertyChanged += HandleCanvasPropertyChanged;
        _canvas.ViewportChanged += HandleCanvasViewportChanged;
        BindInnerControls();
        Content = grid;
    }

    public GraphCanvas Canvas => _canvas;

    public GraphMinimap Minimap => _minimap;

    public GraphNodePalette NodePalette => _nodePalette;

    public IGraphNodeCatalog? Catalog
    {
        get => GetValue(CatalogProperty);
        set => SetValue(CatalogProperty, value);
    }

    public string? ModelDirectory
    {
        get => GetValue(ModelDirectoryProperty);
        set => SetValue(ModelDirectoryProperty, value);
    }

    public ICommand? NodeRequestedCommand
    {
        get => GetValue(NodeRequestedCommandProperty);
        set => SetValue(NodeRequestedCommandProperty, value);
    }

    public bool IsPaletteClickToAddEnabled
    {
        get => GetValue(IsPaletteClickToAddEnabledProperty);
        set => SetValue(IsPaletteClickToAddEnabledProperty, value);
    }

    public GraphDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public GraphViewport Viewport
    {
        get => GetValue(ViewportProperty);
        set => SetValue(ViewportProperty, value);
    }

    public GraphViewportOptions ViewportOptions
    {
        get => GetValue(ViewportOptionsProperty);
        set => SetValue(ViewportOptionsProperty, value);
    }

    public Rect? VisibleGraphBounds => _canvas.VisibleGraphBounds;

    public GraphGridSettings GridSettings
    {
        get => GetValue(GridSettingsProperty);
        set => SetValue(GridSettingsProperty, value);
    }

    public GraphRuntimeSnapshot? RuntimeSnapshot
    {
        get => GetValue(RuntimeSnapshotProperty);
        set => SetValue(RuntimeSnapshotProperty, value);
    }

    public IReadOnlyCollection<string> BreakpointNodeIds
    {
        get => GetValue(BreakpointNodeIdsProperty);
        set => SetValue(BreakpointNodeIdsProperty, value);
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

    public bool IsLevelOfDetailEnabled
    {
        get => GetValue(IsLevelOfDetailEnabledProperty);
        set => SetValue(IsLevelOfDetailEnabledProperty, value);
    }

    public bool ShowPorts
    {
        get => GetValue(ShowPortsProperty);
        set => SetValue(ShowPortsProperty, value);
    }

    public bool IsMinimapVisible
    {
        get => GetValue(IsMinimapVisibleProperty);
        set => SetValue(IsMinimapVisibleProperty, value);
    }

    public double LodPortsZoomThreshold
    {
        get => GetValue(LodPortsZoomThresholdProperty);
        set => SetValue(LodPortsZoomThresholdProperty, value);
    }

    public double LodLabelsZoomThreshold
    {
        get => GetValue(LodLabelsZoomThresholdProperty);
        set => SetValue(LodLabelsZoomThresholdProperty, value);
    }

    public double LodSimplifiedZoomThreshold
    {
        get => GetValue(LodSimplifiedZoomThresholdProperty);
        set => SetValue(LodSimplifiedZoomThresholdProperty, value);
    }

    public IGraphEditCoordinator EditCoordinator
    {
        get => GetValue(EditCoordinatorProperty);
        set => SetValue(EditCoordinatorProperty, value);
    }

    public event EventHandler<GraphViewportChangedEventArgs>? ViewportChanged;

    public Point GraphToScreen(GraphPoint point) => _canvas.GraphToScreen(point);

    public GraphPoint ScreenToGraph(Point point) => _canvas.ScreenToGraph(point);

    public bool SetViewport(GraphViewport viewport) => _canvas.SetViewport(viewport);

    public bool SetZoom(double zoom) => _canvas.SetZoom(zoom);

    public bool ZoomAt(Point screenPoint, double targetZoom) => _canvas.ZoomAt(screenPoint, targetZoom);

    public bool ZoomBy(double factor) => _canvas.ZoomBy(factor);

    public bool ZoomBy(double factor, Point screenPoint) => _canvas.ZoomBy(factor, screenPoint);

    public bool PanBy(Vector screenDelta) => _canvas.PanBy(screenDelta);

    public bool CenterViewport(GraphPoint graphPoint) => _canvas.CenterViewport(graphPoint);

    public bool ResetViewport() => _canvas.ResetViewport();

    public bool FitToDocument(Size? viewportSize = null, double padding = 48) =>
        _canvas.FitToDocument(viewportSize, padding);

    public bool FitToSelection(Size? viewportSize = null, double padding = 64) =>
        _canvas.FitToSelection(viewportSize, padding);

    public event Action<GraphNodeDefinition>? NodeRequested
    {
        add => _nodePalette.NodeRequested += value;
        remove => _nodePalette.NodeRequested -= value;
    }

    private void BindInnerControls()
    {
        _nodePalette[!GraphNodePalette.CatalogProperty] = this[!CatalogProperty];
        _nodePalette[!GraphNodePalette.ModelDirectoryProperty] = this[!ModelDirectoryProperty];
        _nodePalette[!GraphNodePalette.NodeRequestedCommandProperty] =
            this[!NodeRequestedCommandProperty];
        _nodePalette[!GraphNodePalette.IsClickToAddEnabledProperty] =
            this[!IsPaletteClickToAddEnabledProperty];
        _canvas[!GraphCanvas.DocumentProperty] = this[!DocumentProperty];
        BindCanvasTwoWay(GraphCanvas.IsReadOnlyProperty, nameof(IsReadOnly));
        BindCanvasTwoWay(GraphCanvas.ViewportProperty, nameof(Viewport));
        BindCanvasTwoWay(GraphCanvas.ViewportOptionsProperty, nameof(ViewportOptions));
        BindCanvasTwoWay(GraphCanvas.GridSettingsProperty, nameof(GridSettings));
        _canvas[!GraphCanvas.RuntimeSnapshotProperty] = this[!RuntimeSnapshotProperty];
        _canvas[!GraphCanvas.BreakpointNodeIdsProperty] = this[!BreakpointNodeIdsProperty];
        BindCanvasTwoWay(GraphCanvas.SelectedNodeIdProperty, nameof(SelectedNodeId));
        BindCanvasTwoWay(GraphCanvas.SelectedEdgeIdProperty, nameof(SelectedEdgeId));
        BindCanvasTwoWay(GraphCanvas.SelectedAnnotationIdProperty, nameof(SelectedAnnotationId));
        _canvas[!GraphCanvas.SelectionChangedCommandProperty] = this[!SelectionChangedCommandProperty];
        _canvas[!GraphCanvas.GraphEditCommittedCommandProperty] =
            this[!GraphEditCommittedCommandProperty];
        _canvas[!GraphCanvas.GraphEditRejectedCommandProperty] =
            this[!GraphEditRejectedCommandProperty];
        _canvas[!GraphCanvas.EditCoordinatorProperty] = this[!EditCoordinatorProperty];
        _canvas[!GraphCanvas.ConnectionValidationChangedCommandProperty] =
            this[!ConnectionValidationChangedCommandProperty];
        _canvas[!GraphCanvas.ConnectionValidationRuleProperty] =
            this[!ConnectionValidationRuleProperty];
        BindCanvasTwoWay(GraphCanvas.IsLevelOfDetailEnabledProperty, nameof(IsLevelOfDetailEnabled));
        BindCanvasTwoWay(GraphCanvas.ShowPortsProperty, nameof(ShowPorts));
        BindCanvasTwoWay(GraphCanvas.LodPortsZoomThresholdProperty, nameof(LodPortsZoomThreshold));
        BindCanvasTwoWay(GraphCanvas.LodLabelsZoomThresholdProperty, nameof(LodLabelsZoomThreshold));
        BindCanvasTwoWay(GraphCanvas.LodSimplifiedZoomThresholdProperty, nameof(LodSimplifiedZoomThreshold));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsMinimapVisibleProperty)
        {
            _minimap.IsVisible = change.GetNewValue<bool>();
        }
    }

    private void BindCanvasTwoWay<T>(StyledProperty<T> property, string sourcePropertyName) =>
        _canvas.Bind(
            property,
            new Binding(sourcePropertyName)
            {
                Source = this,
                Mode = BindingMode.TwoWay
            });

    private void HandleCanvasPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == GraphCanvas.SelectedNodeIdsProperty)
        {
            SetAndRaise(
                SelectedNodeIdsProperty,
                ref _selectedNodeIds,
                _canvas.SelectedNodeIds);
        }
        else if (e.Property == GraphCanvas.SelectedEdgeIdsProperty)
        {
            SetAndRaise(
                SelectedEdgeIdsProperty,
                ref _selectedEdgeIds,
                _canvas.SelectedEdgeIds);
        }
        else if (e.Property == GraphCanvas.SelectedGroupIdsProperty)
        {
            SetAndRaise(
                SelectedGroupIdsProperty,
                ref _selectedGroupIds,
                _canvas.SelectedGroupIds);
        }
    }

    private void HandleCanvasViewportChanged(object? sender, GraphViewportChangedEventArgs e) =>
        ViewportChanged?.Invoke(this, e);

    private static GraphViewport NormalizeViewport(GraphViewport? viewport)
    {
        var value = viewport ?? GraphViewport.Default;
        var zoom = double.IsFinite(value.Zoom) && value.Zoom > 0 ? value.Zoom : 1.0;
        var offset = double.IsFinite(value.Offset.X) && double.IsFinite(value.Offset.Y)
            ? value.Offset
            : GraphPoint.Zero;
        return new GraphViewport(offset, zoom);
    }

    private static T CoerceCanvasValue<T>(
        StyledProperty<T> property,
        AvaloniaObject owner,
        T value)
    {
        var coerce = ((StyledPropertyMetadata<T>)property.GetMetadata(typeof(GraphCanvas)))
            .CoerceValue;
        return coerce is null ? value : coerce(owner, value);
    }
}
