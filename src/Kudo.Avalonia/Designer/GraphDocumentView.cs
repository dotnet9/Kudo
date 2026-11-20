using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kudo;
using Kudo.Avalonia;
using Kudo.Core.Documents;
using Kudo.Core.Runtime;
using CodeWF.Toolkit.Lang;
using System.Windows.Input;
using DocumentViewLanguage = global::Localization.Kudo_Avalonia.Designer.GraphDocumentView;

namespace Kudo.Avalonia.Designer;

public sealed class GraphDocumentView : GraphDesignerPanel, IDisposable, IGraphViewportHost
{
    public static readonly StyledProperty<ICommand?> NodeDropRequestedCommandProperty =
        AvaloniaProperty.Register<GraphDocumentView, ICommand?>(nameof(NodeDropRequestedCommand));

    public static readonly StyledProperty<IGraphNodeCatalog?> NodeCatalogProperty =
        AvaloniaProperty.Register<GraphDocumentView, IGraphNodeCatalog?>(nameof(NodeCatalog));

    public static readonly StyledProperty<IGraphNodeFactory?> NodeFactoryProperty =
        AvaloniaProperty.Register<GraphDocumentView, IGraphNodeFactory?>(
            nameof(NodeFactory),
            GraphNodeDefinitionFactory.Instance);

    public static readonly StyledProperty<IGraphNodeDropHandler?> NodeDropHandlerProperty =
        AvaloniaProperty.Register<GraphDocumentView, IGraphNodeDropHandler?>(nameof(NodeDropHandler));

    public static readonly StyledProperty<IGraphNodeCreationConfiguratorResolver?>
        NodeCreationConfiguratorsProperty =
            AvaloniaProperty.Register<GraphDocumentView, IGraphNodeCreationConfiguratorResolver?>(
                nameof(NodeCreationConfigurators));

    public static readonly StyledProperty<bool> CreateDroppedNodesAutomaticallyProperty =
        AvaloniaProperty.Register<GraphDocumentView, bool>(
            nameof(CreateDroppedNodesAutomatically),
            true);

    public static readonly StyledProperty<bool> ImportDroppedNodeAssetsProperty =
        AvaloniaProperty.Register<GraphDocumentView, bool>(
            nameof(ImportDroppedNodeAssets),
            true);

    public static readonly StyledProperty<bool> IsMinimapVisibleProperty =
        AvaloniaProperty.Register<GraphDocumentView, bool>(
            nameof(IsMinimapVisible),
            true,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsInteractionHintVisibleProperty =
        AvaloniaProperty.Register<GraphDocumentView, bool>(
            nameof(IsInteractionHintVisible),
            true);

    public static readonly DirectProperty<GraphDocumentView, GraphPropertyEditorContext?>
        PropertyEditorContextProperty =
            AvaloniaProperty.RegisterDirect<GraphDocumentView, GraphPropertyEditorContext?>(
                nameof(PropertyEditorContext),
                view => view.PropertyEditorContext);

    private Border? _hint;
    private TextBlock? _hintText;
    private TextBlock? _rulerOriginText;
    private Border? _rulerCorner;
    private Border? _root;
    private bool _canvasCommandCommitted;
    private bool _hasInitializedViewport;
    private bool _isCommandRefresh;
    private bool _isDisposed;
    private bool _isSynchronizingCanvasSettings;
    private readonly GraphFileImageResolver _imageResolver;
    private readonly GraphNodeBackgroundImageResolver _backgroundImageResolver;
    private GraphPropertyEditorContext? _propertyEditorContext;
    private Func<string, GraphNodeDefinition?>? _nodeDefinitionResolver;

    public GraphDocumentView(
        GraphDocumentSession session,
        IGraphRuntimeSession? runtimeSession = null,
        IGraphEditCoordinator? editCoordinator = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        if (runtimeSession is not null && !ReferenceEquals(runtimeSession.Document, session.Document))
        {
            throw new ArgumentException("Runtime session must target the view document.", nameof(runtimeSession));
        }

        RuntimeSession = runtimeSession;
        var effectiveEditCoordinator =
            editCoordinator ?? new GraphDocumentSessionEditCoordinator(session);
        var canvasSettings = (session.Document.CanvasSettings ?? GraphCanvasSettings.Default).Normalize();
        session.Document.CanvasSettings = canvasSettings;
        _imageResolver = new GraphFileImageResolver(() => Session.WorkingDirectory);
        _backgroundImageResolver = new GraphNodeBackgroundImageResolver(node =>
            _nodeDefinitionResolver?.Invoke(node.Type) ?? ResolveDefinitionFromCatalog(node.Type));
        Canvas = new GraphCanvas
        {
            Document = session.Document,
            Viewport = session.Viewport,
            GridSettings = canvasSettings.Grid,
            IsLevelOfDetailEnabled = canvasSettings.IsLevelOfDetailEnabled,
            ShowPorts = canvasSettings.ShowPorts,
            LodPortsZoomThreshold = canvasSettings.LodPortsZoomThreshold,
            LodLabelsZoomThreshold = canvasSettings.LodLabelsZoomThreshold,
            LodSimplifiedZoomThreshold = canvasSettings.LodSimplifiedZoomThreshold,
            PortLabelVisibility = canvasSettings.PortLabelVisibility,
            SymbolNameVisibility = canvasSettings.SymbolNameVisibility,
            SymbolDescriptionVisibility = canvasSettings.SymbolDescriptionVisibility,
            RuntimeSnapshot = runtimeSession?.CurrentSnapshot,
            BreakpointNodeIds = runtimeSession?.BreakpointNodeIds ?? Array.Empty<string>(),
            EditCoordinator = effectiveEditCoordinator,
            ImageResolver = _imageResolver,
            BackgroundImageResolver = _backgroundImageResolver,
            MinimumNodeSizeResolver = ResolveMinimumNodeSize
        };
        Minimap = new GraphMinimap
        {
            TargetCanvas = Canvas,
            Width = 190,
            Height = 126,
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        Minimap.IsVisible = canvasSettings.IsMinimapVisible;
        SetCurrentValue(IsMinimapVisibleProperty, canvasSettings.IsMinimapVisible);
        HorizontalRuler = new GraphRuler
        {
            TargetCanvas = Canvas,
            Orientation = Orientation.Horizontal,
            IsOriginLabelVisible = false
        };
        VerticalRuler = new GraphRuler
        {
            TargetCanvas = Canvas,
            Orientation = Orientation.Vertical,
            IsOriginLabelVisible = false
        };
        EditorContext = new GraphEditorContext(this, runtimeSession);

        var canvasHost = new Grid();
        canvasHost.Children.Add(Canvas);
        canvasHost.Children.Add(Minimap);
        _hintText = new TextBlock
        {
            Text = L(DocumentViewLanguage.InteractionHint),
            FontSize = 11,
            Foreground = SecondaryTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        var hintClose = new Button
        {
            Content = new TextBlock { Text = "✕", FontSize = 10 },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = SecondaryTextBrush
        };
        hintClose.Click += (_, _) =>
        {
            SetCurrentValue(IsInteractionHintVisibleProperty, false);
            InteractionHintDismissed?.Invoke(this, EventArgs.Empty);
        };
        _hint = new Border
        {
            IsVisible = IsInteractionHintVisible,
            Margin = new Thickness(12),
            Padding = new Thickness(9, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = ElevatedBrush,
            BorderBrush = DividerBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { _hintText, hintClose }
            }
        };
        canvasHost.Children.Add(_hint);

        var host = new Grid
        {
            RowDefinitions = new RowDefinitions("22,*"),
            ColumnDefinitions = new ColumnDefinitions("34,*")
        };
        _rulerOriginText = new TextBlock
        {
            Text = "0",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _rulerCorner = new Border
        {
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = _rulerOriginText
        };
        HorizontalRuler.PropertyChanged += HandleRulerAppearanceChanged;
        SynchronizeRulerCornerAppearance();
        Grid.SetRow(HorizontalRuler, 0);
        Grid.SetColumn(HorizontalRuler, 1);
        Grid.SetRow(VerticalRuler, 1);
        Grid.SetColumn(VerticalRuler, 0);
        Grid.SetRow(canvasHost, 1);
        Grid.SetColumn(canvasHost, 1);
        host.Children.Add(_rulerCorner);
        host.Children.Add(HorizontalRuler);
        host.Children.Add(VerticalRuler);
        host.Children.Add(canvasHost);
        _root = new Border
        {
            Margin = new Thickness(4),
            Background = PanelBrush,
            BorderBrush = DividerBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = host
        };
        Content = _root;

        AttachedToVisualTree += HandleAttachedToVisualTree;
        DetachedFromVisualTree += HandleDetachedFromVisualTree;
        Canvas.AttachedToVisualTree += HandleCanvasAttachedToVisualTree;
        DragDrop.SetAllowDrop(Canvas, true);
        Canvas.AddHandler(DragDrop.DragOverEvent, HandleNodeDragOver);
        Canvas.AddHandler(DragDrop.DragLeaveEvent, HandleNodeDragLeave);
        Canvas.AddHandler(DragDrop.DropEvent, HandleNodeDrop);
        Canvas.GraphEditCommitted += HandleGraphEditCommitted;
        Canvas.GraphEditRejected += HandleGraphEditRejected;
        Canvas.EditPolicyChanged += HandleEditPolicyChanged;
        Canvas.GraphChanged += HandleGraphChanged;
        Canvas.SelectionChanged += HandleStateChanged;
        Canvas.ConnectionValidationChanged += HandleConnectionValidationChanged;
        Canvas.ViewportChanged += HandleCanvasViewportChanged;
        Canvas.PropertyChanged += HandleCanvasPropertyChanged;
        if (RuntimeSession is not null)
        {
            RuntimeSession.SnapshotChanged += HandleRuntimeSnapshotChanged;
            RuntimeSession.BreakpointHit += HandleRuntimeBreakpointHit;
            RuntimeSession.BreakpointsChanged += HandleBreakpointsChanged;
            RuntimeSession.WatchesChanged += HandleWatchesChanged;
            RuntimeSession.DiagnosticEmitted += HandleRuntimeDiagnosticEmitted;
        }

        UpdatePropertyEditorContext();
    }

    public GraphDocumentSession Session { get; }

    public IGraphRuntimeSession? RuntimeSession { get; }

    public IGraphEditCoordinator EditCoordinator
    {
        get => Canvas.EditCoordinator;
        set => Canvas.EditCoordinator = value;
    }

    public IConnectionValidationRule? ConnectionValidationRule
    {
        get => Canvas.ConnectionValidationRule;
        set => Canvas.ConnectionValidationRule = value;
    }

    public GraphCanvas Canvas { get; }

    /// <summary>
    /// Gets the last graph-space pointer position recorded by the canvas.
    /// </summary>
    public GraphPoint? LastPointerGraphPoint => Canvas.LastPointerGraphPoint;

    public long LastPointerGraphPointVersion => Canvas.LastPointerGraphPointVersion;

    public bool IsReadOnly
    {
        get => Canvas.IsReadOnly;
        set => Canvas.IsReadOnly = value;
    }

    /// <summary>
    /// Gets or sets the complete canvas viewport for this document.
    /// </summary>
    public GraphViewport Viewport
    {
        get => Canvas.Viewport;
        set => Canvas.Viewport = value;
    }

    /// <summary>
    /// Gets or sets the runtime zoom-range policy for this document view.
    /// It is not persisted into the graph document.
    /// </summary>
    public GraphViewportOptions ViewportOptions
    {
        get => Canvas.ViewportOptions;
        set => Canvas.ViewportOptions = value;
    }

    public Rect? VisibleGraphBounds => Canvas.VisibleGraphBounds;

    /// <summary>Raised when the user closes the interaction hint bar.</summary>
    public event EventHandler? InteractionHintDismissed;

    public GraphMinimap Minimap { get; }

    public bool IsMinimapVisible
    {
        get => GetValue(IsMinimapVisibleProperty);
        set => SetValue(IsMinimapVisibleProperty, value);
    }

    public bool IsInteractionHintVisible
    {
        get => GetValue(IsInteractionHintVisibleProperty);
        set => SetValue(IsInteractionHintVisibleProperty, value);
    }

    public GraphRuler HorizontalRuler { get; }

    public GraphRuler VerticalRuler { get; }

    public GraphEditorContext EditorContext { get; }

    public string? LastMessage { get; private set; }

    public GraphPropertyEditorContext? PropertyEditorContext
    {
        get => _propertyEditorContext;
        private set => SetAndRaise(
            PropertyEditorContextProperty,
            ref _propertyEditorContext,
            value);
    }

    public Func<string, GraphNodeDefinition?>? NodeDefinitionResolver
    {
        get => _nodeDefinitionResolver;
        set
        {
            if (ReferenceEquals(_nodeDefinitionResolver, value))
            {
                return;
            }

            _nodeDefinitionResolver = value;
            EnsureBackgroundImageMappingAssets();
            Canvas.BackgroundImageResolver = _backgroundImageResolver;
            UpdatePropertyEditorContext();
        }
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

    public event EventHandler? StateChanged;

    public event EventHandler<GraphViewportChangedEventArgs>? ViewportChanged;

    public event EventHandler<GraphRuntimeDiagnosticEventArgs>? RuntimeDiagnosticReceived;

    public event Action<GraphNodeDropRequest>? NodeDropRequested;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        AttachedToVisualTree -= HandleAttachedToVisualTree;
        DetachedFromVisualTree -= HandleDetachedFromVisualTree;
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        HorizontalRuler.PropertyChanged -= HandleRulerAppearanceChanged;
        Canvas.AttachedToVisualTree -= HandleCanvasAttachedToVisualTree;
        Canvas.RemoveHandler(DragDrop.DragOverEvent, HandleNodeDragOver);
        Canvas.RemoveHandler(DragDrop.DragLeaveEvent, HandleNodeDragLeave);
        Canvas.RemoveHandler(DragDrop.DropEvent, HandleNodeDrop);
        Canvas.GraphEditCommitted -= HandleGraphEditCommitted;
        Canvas.GraphEditRejected -= HandleGraphEditRejected;
        Canvas.EditPolicyChanged -= HandleEditPolicyChanged;
        Canvas.GraphChanged -= HandleGraphChanged;
        Canvas.SelectionChanged -= HandleStateChanged;
        Canvas.ConnectionValidationChanged -= HandleConnectionValidationChanged;
        Canvas.ViewportChanged -= HandleCanvasViewportChanged;
        Canvas.PropertyChanged -= HandleCanvasPropertyChanged;
        if (RuntimeSession is not null)
        {
            RuntimeSession.SnapshotChanged -= HandleRuntimeSnapshotChanged;
            RuntimeSession.BreakpointHit -= HandleRuntimeBreakpointHit;
            RuntimeSession.BreakpointsChanged -= HandleBreakpointsChanged;
            RuntimeSession.WatchesChanged -= HandleWatchesChanged;
            RuntimeSession.DiagnosticEmitted -= HandleRuntimeDiagnosticEmitted;
        }

        StateChanged = null;
        ViewportChanged = null;
        RuntimeDiagnosticReceived = null;
        NodeDropRequested = null;
        Canvas.ImageResolver = null;
        _imageResolver.Dispose();
        PropertyEditorContext = null;

        GC.SuppressFinalize(this);
    }

    public bool ApplyCanvasSettings(GraphCanvasSettings settings)
    {
        var command = GraphPropertyCommandFactory.CreateCanvasSettingsCommand(
            Session.Document,
            settings);
        return command is not null && TryExecuteCommand(command);
    }

    public void ExecuteCommand(IGraphCommand command) => TryExecuteCommand(command);

    public Point GraphToScreen(GraphPoint point) => Canvas.GraphToScreen(point);

    public GraphPoint ScreenToGraph(Point point) => Canvas.ScreenToGraph(point);

    public bool SetViewport(GraphViewport viewport) => Canvas.SetViewport(viewport);

    public bool SetZoom(double zoom) => Canvas.SetZoom(zoom);

    public bool ZoomAt(Point screenPoint, double targetZoom) => Canvas.ZoomAt(screenPoint, targetZoom);

    public bool ZoomBy(double factor) => Canvas.ZoomBy(factor);

    public bool ZoomBy(double factor, Point screenPoint) => Canvas.ZoomBy(factor, screenPoint);

    public bool PanBy(Vector screenDelta) => Canvas.PanBy(screenDelta);

    public bool CenterViewport(GraphPoint graphPoint) => Canvas.CenterViewport(graphPoint);

    public bool ResetViewport() => Canvas.ResetViewport();

    public bool FitToDocument(Size? viewportSize = null, double padding = 48) =>
        Canvas.FitToDocument(viewportSize, padding);

    public bool FitToSelection(Size? viewportSize = null, double padding = 64) =>
        Canvas.FitToSelection(viewportSize, padding);

    public bool CanExecuteCommand(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CanAuthorizeCommand(command, undo: false) &&
               CanCoordinateCommand(command, GraphEditTransactionKind.Execute);
    }

    public bool CanUndo =>
        Session.History.UndoCommand is { } command &&
        CanAuthorizeCommand(command, undo: true) &&
        CanCoordinateCommand(command, GraphEditTransactionKind.Undo);

    public bool CanRedo =>
        Session.History.RedoCommand is { } command &&
        CanAuthorizeCommand(command, undo: false) &&
        CanCoordinateCommand(command, GraphEditTransactionKind.Redo);

    public bool TryAuthorizeCommand(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return TryAuthorizeCommand(command, undo: false);
    }

    public bool TryExecuteCommand(
        IGraphCommand command,
        GraphOperationContext? operationContext = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!TryAuthorizeCommand(command))
        {
            return false;
        }

        if (!TryCoordinateCommand(
                command,
                GraphEditTransactionKind.Execute,
                out var changes,
                operationContext ?? GraphOperationContext.User()))
        {
            return false;
        }

        RefreshAfterCommand(changes);
        return true;
    }

    public bool Undo()
    {
        if (Session.History.UndoCommand is not { } command ||
            !TryAuthorizeCommand(command, undo: true) ||
            !TryCoordinateCommand(command, GraphEditTransactionKind.Undo, out var changes))
        {
            return false;
        }

        RefreshAfterCommand(changes);
        return true;
    }

    public bool Redo()
    {
        if (Session.History.RedoCommand is not { } command ||
            !TryAuthorizeCommand(command, undo: false) ||
            !TryCoordinateCommand(command, GraphEditTransactionKind.Redo, out var changes))
        {
            return false;
        }

        RefreshAfterCommand(changes);
        return true;
    }

    private bool CanCoordinateCommand(
        IGraphCommand command,
        GraphEditTransactionKind kind) =>
        EditCoordinator.Evaluate(new GraphEditTransactionRequest(
            Session.Document,
            command,
            kind)).IsAllowed;

    private bool TryCoordinateCommand(
        IGraphCommand command,
        GraphEditTransactionKind kind,
        out GraphChangeSet changes,
        GraphOperationContext? operationContext = null)
    {
        var result = EditCoordinator.Execute(new GraphEditTransactionRequest(
            Session.Document,
            command,
            kind,
            operationContext ?? GraphOperationContext.Programmatic));
        changes = result.Changes;
        if (result.IsAccepted)
        {
            return true;
        }

        Canvas.ReportGraphEditRejection(
            command,
            result.Reason,
            undo: kind == GraphEditTransactionKind.Undo);
        return false;
    }

    private bool CanAuthorizeCommand(IGraphCommand command, bool undo)
    {
        if (IsReadOnly)
        {
            return false;
        }

        var requests = undo
            ? GraphEditRequestFactory.CreateUndo(Session.Document, command)
            : GraphEditRequestFactory.Create(Session.Document, command);
        return requests.All(Canvas.CanAuthorizeEdit);
    }

    private bool TryAuthorizeCommand(IGraphCommand command, bool undo)
    {
        if (IsReadOnly)
        {
            return false;
        }

        var requests = undo
            ? GraphEditRequestFactory.CreateUndo(Session.Document, command)
            : GraphEditRequestFactory.Create(Session.Document, command);
        foreach (var request in requests)
        {
            if (!Canvas.TryAuthorizeEdit(request))
            {
                return false;
            }
        }

        return true;
    }

    public void SetMessage(string? message)
    {
        LastMessage = message;
        RaiseStateChanged();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsMinimapVisibleProperty)
        {
            Minimap.IsVisible = change.GetNewValue<bool>();
            if (!_isSynchronizingCanvasSettings)
            {
                RaiseStateChanged();
            }
        }
        else if (change.Property == IsInteractionHintVisibleProperty && _hint is not null)
        {
            _hint.IsVisible = change.GetNewValue<bool>();
        }
        else if (change.Property == NodeCatalogProperty)
        {
            UpdatePropertyEditorContext();
        }
    }

    protected override void OnPaletteChanged()
    {
        if (_root is not null)
        {
            _root.Background = PanelBrush;
            _root.BorderBrush = DividerBrush;
        }

        if (_hint is not null)
        {
            _hint.Background = ElevatedBrush;
            _hint.BorderBrush = DividerBrush;
        }

        if (_hintText is not null)
        {
            _hintText.Foreground = SecondaryTextBrush;
        }
    }

    private void HandleRulerAppearanceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == GraphRuler.BackgroundProperty ||
            e.Property == GraphRuler.BorderBrushProperty ||
            e.Property == GraphRuler.ForegroundProperty ||
            e.Property == GraphRuler.FontSizeProperty)
        {
            SynchronizeRulerCornerAppearance();
        }
    }

    private void SynchronizeRulerCornerAppearance()
    {
        if (_rulerCorner is not null)
        {
            _rulerCorner.Background = HorizontalRuler.Background;
            _rulerCorner.BorderBrush = HorizontalRuler.BorderBrush;
        }

        if (_rulerOriginText is not null)
        {
            _rulerOriginText.Foreground = HorizontalRuler.Foreground;
            _rulerOriginText.FontSize = HorizontalRuler.FontSize;
        }
    }

    private void HandleGraphChanged(object? sender, EventArgs e)
    {
        if (_canvasCommandCommitted)
        {
            _canvasCommandCommitted = false;
        }
        else if (!_isCommandRefresh)
        {
            Session.MarkDirty();
        }

        SaveViewport();
        RaiseStateChanged();
    }

    private void HandleGraphEditCommitted(object? sender, GraphEditCommittedEventArgs e)
    {
        _canvasCommandCommitted = true;
        if (e.Command is SetGroupCollapsedCommand groupCommand)
        {
            var group = Session.Document.FindGroup(groupCommand.GroupId);
            LastMessage = F(
                groupCommand.NewValue
                    ? DocumentViewLanguage.GroupCollapsed
                    : DocumentViewLanguage.GroupExpanded,
                group?.Title ?? groupCommand.GroupId);
        }
    }

    private void HandleGraphEditRejected(object? sender, GraphEditRejectedEventArgs e)
    {
        LastMessage = string.IsNullOrWhiteSpace(e.Decision.Reason)
            ? L(DocumentViewLanguage.OperationNotAllowed)
            : e.Decision.Reason;
        RaiseStateChanged();
    }

    private void HandleEditPolicyChanged(object? sender, EventArgs e)
    {
        RaiseStateChanged();
    }

    private void HandleNodeDragOver(object? sender, DragEventArgs e)
    {
        if (IsReadOnly || !GraphNodeDragDrop.TryGetNodeType(e.DataTransfer, out var nodeType))
        {
            Canvas.ClearNodePlacementPreview();
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var screenPosition = e.GetPosition(Canvas);
        if (!new Rect(Canvas.Bounds.Size).Contains(screenPosition))
        {
            Canvas.ClearNodePlacementPreview();
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var definition = ResolveDropDefinition(e.DataTransfer, nodeType);
        var previewSize = definition?.GetEffectiveDefaultSize() ?? new GraphSize(160, 88);
        if (!double.IsFinite(previewSize.Width) || previewSize.Width <= 0 ||
            !double.IsFinite(previewSize.Height) || previewSize.Height <= 0)
        {
            previewSize = new GraphSize(160, 88);
        }

        Canvas.UpdateNodePlacementPreview(
            Canvas.ScreenToGraph(screenPosition),
            previewSize,
            definition?.Name ?? nodeType,
            visualMode: definition?.DefaultAppearance.VisualMode ?? GraphNodeVisualMode.Card);
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void HandleNodeDragLeave(object? sender, DragEventArgs e)
    {
        Canvas.ClearNodePlacementPreview();
        e.Handled = true;
    }

    private async void HandleNodeDrop(object? sender, DragEventArgs e)
    {
        if (IsReadOnly || !GraphNodeDragDrop.TryGetNodeType(e.DataTransfer, out var nodeType))
        {
            Canvas.ClearNodePlacementPreview();
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var screenPosition = e.GetPosition(Canvas);
        if (!new Rect(Canvas.Bounds.Size).Contains(screenPosition))
        {
            Canvas.ClearNodePlacementPreview();
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var graphPosition = Canvas.NodePlacementPreview?.Position
            ?? Canvas.ScreenToGraph(screenPosition);
        Canvas.ClearNodePlacementPreview();
        var definition = ResolveDropDefinition(e.DataTransfer, nodeType);
        var request = new GraphNodeDropRequest(nodeType, graphPosition);
        if (CreateDroppedNodesAutomatically &&
            definition is not null &&
            NodeFactory is not null)
        {
            e.DragEffects = await TryCreateDroppedNodeAsync(definition, request.Position)
                    .ConfigureAwait(true)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
            return;
        }

        NodeDropRequested?.Invoke(request);
        if (NodeDropRequestedCommand?.CanExecute(request) == true)
        {
            NodeDropRequestedCommand.Execute(request);
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private GraphNodeDefinition? ResolveDropDefinition(
        IDataTransfer? dataTransfer,
        string nodeType)
    {
        if (GraphNodeDragDrop.TryGetDefinition(dataTransfer, out var definition))
        {
            return definition;
        }

        return NodeCatalog?.FindByNodeType(nodeType) ??
            _nodeDefinitionResolver?.Invoke(nodeType);
    }

    public bool TryCreateNodeFromDefinition(
        GraphNodeDefinition definition,
        GraphPoint position,
        GraphNodeCreationOptions? options = null,
        GraphOperationContext? operationContext = null) =>
        TryCreateNodeFromDefinitionCore(
            definition,
            position,
            options,
            operationContext ?? GraphOperationContext.User());

    public async ValueTask<bool> TryCreateNodeFromDefinitionAsync(
        GraphNodeDefinition definition,
        GraphPoint position,
        GraphNodeCreationOptions? options = null,
        bool isDragDrop = false,
        CancellationToken cancellationToken = default,
        GraphOperationContext? operationContext = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        operationContext ??= GraphOperationContext.User(
            isDragDrop ? GraphOperationInteractions.DragDrop : null);
        if (NodeCreationConfigurators is { } configurators)
        {
            var configured = await configurators.ConfigureCreationAsync(
                    new GraphNodeCreationConfigurationContext(
                        Session,
                        definition,
                        position,
                        isDragDrop,
                        options),
                    cancellationToken)
                .ConfigureAwait(true);
            if (configured.IsCancelled)
            {
                return false;
            }

            options = configured.Options ?? options;
        }

        var result = NodeDropHandler is { } handler
            ? await handler.HandleAsync(
                    new GraphNodeDropContext(
                        Session,
                        definition,
                        position,
                        isDragDrop,
                        options),
                    cancellationToken)
                .ConfigureAwait(true)
            : GraphNodeDropResult.Create(options: options);

        return result.Kind switch
        {
            GraphNodeDropResultKind.Cancel => false,
            GraphNodeDropResultKind.Handled => true,
            _ => TryCreateNodeFromDefinitionCore(
                result.Definition ?? definition,
                result.Position ?? position,
                result.Options ?? options,
                operationContext)
        };
    }

    private bool TryCreateNodeFromDefinitionCore(
        GraphNodeDefinition definition,
        GraphPoint position,
        GraphNodeCreationOptions? options,
        GraphOperationContext operationContext)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Kind == GraphNodeDefinitionKind.Annotation)
        {
            return TryCreateAnnotationFromDefinition(
                definition,
                position,
                options,
                operationContext);
        }

        var nodeFactory = NodeFactory;
        if (nodeFactory is null)
        {
            return false;
        }

        var nodeId = CreateUniqueNodeId(Session.Document, definition.NodeType);
        var node = nodeFactory.CreateNode(definition, nodeId, position);
        node.DefinitionId = definition.Id;
        node.Number = GraphNodeNumberAllocator.Next(Session.Document);
        node.Name ??= definition.Name;
        ApplyCreationOptions(node, options);
        if (nodeFactory is ProjectedGraphNodeFactory projectedFactory)
        {
            projectedFactory.ApplyProjection(node);
        }

        var command = new AddNodeCommand(
            node,
            F(DocumentViewLanguage.AddNodeCommand, definition.Name));
        if (!TryAuthorizeCommand(command))
        {
            return false;
        }

        if (!TryImportDroppedNodeAsset(definition, node))
        {
            return false;
        }

        if (!TryExecuteCommand(command, operationContext))
        {
            return false;
        }

        Canvas.SetSelectedNodes([node.Id], node.Id);
        LastMessage = F(DocumentViewLanguage.NodeAdded, node.Name);
        RaiseStateChanged();
        return true;
    }

    private bool TryPreviewCommand(IGraphCommand command)
    {
        if (!TryAuthorizeCommand(command))
        {
            return false;
        }

        command.Execute(Session.Document);
        Canvas.InvalidateVisual();
        return true;
    }

    private bool TryCommitPreviewCommand(IGraphCommand command)
    {
        if (!CanAuthorizeCommand(command, undo: false))
        {
            return false;
        }

        if (!TryCoordinateCommand(
                command,
                GraphEditTransactionKind.CommitApplied,
                out var changes,
                GraphOperationContext.User()))
        {
            command.Undo(Session.Document);
            RefreshAfterCommand(GraphChangeSet.ForCommand(command, undo: true));
            return false;
        }

        RefreshAfterCommand(changes);
        return true;
    }

    private bool TryCreateAnnotationFromDefinition(
        GraphNodeDefinition definition,
        GraphPoint position,
        GraphNodeCreationOptions? options,
        GraphOperationContext operationContext)
    {
        var document = Session.Document;
        var annotation = new AnnotationModel
        {
            Id = CreateUniqueAnnotationId(document),
            Text = string.IsNullOrWhiteSpace(options?.Name)
                ? definition.Name
                : options.Name,
            Position = position,
            Size = options?.Size ?? definition.DefaultSize,
            Properties = GraphNodeDefinitionValue.MaterializeDictionary(
                options?.Properties ?? definition.DefaultProperties)
        };
        var command = new AddAnnotationCommand(
            annotation,
            F(DocumentViewLanguage.AddAnnotationCommand, definition.Name));
        if (!TryAuthorizeCommand(command) || !TryExecuteCommand(command, operationContext))
        {
            return false;
        }

        Canvas.SetSelectedAnnotation(annotation.Id);
        Canvas.BeginAnnotationTextEdit(annotation.Id);
        LastMessage = F(DocumentViewLanguage.AnnotationAdded, definition.Name);
        RaiseStateChanged();
        return true;
    }

    private bool TryImportDroppedNodeAsset(
        GraphNodeDefinition definition,
        NodeModel node)
    {
        if (!ImportDroppedNodeAssets)
        {
            return true;
        }

        var modelAsset = node.Appearance.Normalize().BackgroundImage;
        if (modelAsset is null && definition.BackgroundImageMappings.Count == 0)
        {
            return true;
        }

        try
        {
            if (modelAsset is not null)
            {
                var sourcePath = definition.ResolveResourcePath(modelAsset);
                if (sourcePath is null || !File.Exists(sourcePath))
                {
                    LastMessage = F(DocumentViewLanguage.ModelAssetMissing, modelAsset);
                    RaiseStateChanged();
                    return false;
                }

                var taskAsset = new GraphTaskAssetStore(Session).ImportImage(sourcePath);
                node.Appearance = node.Appearance with { BackgroundImage = taskAsset };
            }

            ImportBackgroundImageMappings(definition, node);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            LastMessage = F(
                DocumentViewLanguage.ModelAssetImportFailed,
                modelAsset ?? definition.BackgroundImageMappings.FirstOrDefault()?.Resource ?? string.Empty,
                exception.Message);
            RaiseStateChanged();
            return false;
        }
    }

    private static string CreateUniqueNodeId(GraphDocument document, string nodeType)
    {
        var sequence = document.Nodes.Count + 1;
        string nodeId;
        do
        {
            nodeId = $"{nodeType}-{sequence}";
            sequence++;
        }
        while (document.FindNode(nodeId) is not null);

        return nodeId;
    }

    private static string CreateUniqueAnnotationId(GraphDocument document)
    {
        var sequence = document.Annotations.Count + 1;
        string annotationId;
        do
        {
            annotationId = $"Annotation-{sequence}";
            sequence++;
        }
        while (document.FindAnnotation(annotationId) is not null);

        return annotationId;
    }

    private async ValueTask<bool> TryCreateDroppedNodeAsync(
        GraphNodeDefinition definition,
        GraphPoint position) =>
        await TryCreateNodeFromDefinitionAsync(
                definition,
                position,
                isDragDrop: true)
            .ConfigureAwait(true);

    private static void ApplyCreationOptions(
        NodeModel node,
        GraphNodeCreationOptions? options)
    {
        if (options is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.Name))
        {
            node.Name = options.Name;
        }

        if (options.Description is not null)
        {
            node.Description = options.Description;
        }

        if (options.Size is { } size)
        {
            node.Size = size;
        }

        if (options.Appearance is { } appearance)
        {
            node.Appearance = appearance.Normalize();
        }

        if (options.ExtensionState is { } extensionState)
        {
            node.ExtensionState = extensionState.DeepClone();
        }

        if (options.Properties is not null)
        {
            foreach (var pair in options.Properties)
            {
                node.Properties[pair.Key] = GraphNodeDefinitionValue.MaterializeValue(pair.Value);
            }
        }

        if (options.Ports is not null)
        {
            node.Ports.Clear();
            node.Ports.AddRange(options.Ports.Select(GraphNodeDefinitionValue.ClonePort));
        }
    }

    private void HandleStateChanged(object? sender, EventArgs e)
    {
        RaiseStateChanged();
    }

    private void HandleConnectionValidationChanged(object? sender, ConnectionValidationResult validation)
    {
        LastMessage = validation.IsValid
            ? validation.Message ?? L(DocumentViewLanguage.ConnectionCreated)
            : validation.Message;
        RaiseStateChanged();
    }

    private void HandleCanvasAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_hasInitializedViewport || Session.Viewport != GraphViewport.Default)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                if (!_isDisposed && !_hasInitializedViewport &&
                    FitToDocument())
                {
                    _hasInitializedViewport = true;
                }
            },
            DispatcherPriority.Loaded);
    }

    private void HandleRuntimeSnapshotChanged(object? sender, GraphRuntimeSnapshot snapshot)
    {
        if (_isDisposed)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ApplyRuntimeSnapshot(snapshot));
            return;
        }

        ApplyRuntimeSnapshot(snapshot);
    }

    private void HandleRuntimeBreakpointHit(object? sender, GraphRuntimeBreakpointHitEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ApplyRuntimeBreakpointHit(e.NodeId));
            return;
        }

        ApplyRuntimeBreakpointHit(e.NodeId);
    }

    private void ApplyRuntimeBreakpointHit(string nodeId)
    {
        if (_isDisposed)
        {
            return;
        }

        LastMessage = F(DocumentViewLanguage.BreakpointHit, nodeId);
        RaiseStateChanged();
    }

    private void HandleBreakpointsChanged(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ApplyBreakpoints);
            return;
        }

        ApplyBreakpoints();
    }

    private void ApplyBreakpoints()
    {
        if (_isDisposed)
        {
            return;
        }

        Canvas.BreakpointNodeIds = RuntimeSession?.BreakpointNodeIds.ToArray() ?? [];
        RaiseStateChanged();
    }

    private void HandleWatchesChanged(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(NotifyWatchesChanged);
            return;
        }

        NotifyWatchesChanged();
    }

    private void NotifyWatchesChanged()
    {
        if (!_isDisposed)
        {
            RaiseStateChanged();
        }
    }

    private void HandleRuntimeDiagnosticEmitted(object? sender, GraphRuntimeDiagnosticEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => PublishRuntimeDiagnostic(e));
            return;
        }

        PublishRuntimeDiagnostic(e);
    }

    private void PublishRuntimeDiagnostic(GraphRuntimeDiagnosticEventArgs e)
    {
        if (!_isDisposed)
        {
            RuntimeDiagnosticReceived?.Invoke(this, e);
        }
    }

    private void ApplyRuntimeSnapshot(GraphRuntimeSnapshot snapshot)
    {
        if (_isDisposed)
        {
            return;
        }

        Canvas.RuntimeSnapshot = snapshot;
        Session.RuntimeStatus = snapshot.Status;
        if (snapshot.Status == RuntimeStatus.Faulted)
        {
            LastMessage = string.IsNullOrWhiteSpace(snapshot.Message)
                ? L(DocumentViewLanguage.RuntimeFaulted)
                : F(DocumentViewLanguage.RuntimeFaultedWithMessage, snapshot.Message);
        }

        RaiseStateChanged();
    }

    private void HandleCanvasPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == GraphCanvas.IsReadOnlyProperty)
        {
            RaiseStateChanged();
        }
    }

    private void EnsureBackgroundImageMappingAssets()
    {
        foreach (var node in Session.Document.Nodes)
        {
            var definition = _nodeDefinitionResolver?.Invoke(node.Type) ??
                ResolveDefinitionFromCatalog(node.Type);
            if (definition?.BackgroundImageMappings.Count > 0)
            {
                ImportBackgroundImageMappings(definition, node);
            }
        }
    }

    private void ImportBackgroundImageMappings(
        GraphNodeDefinition definition,
        NodeModel node)
    {
        if (definition.BackgroundImageMappings.Count == 0)
        {
            return;
        }

        var assets = node.Properties.TryGetValue(
                GraphNodePropertyKeys.BackgroundImageMappings,
                out var existing) &&
            existing is IReadOnlyDictionary<string, object?> existingDictionary
                ? existingDictionary.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var store = new GraphTaskAssetStore(Session);
        foreach (var mapping in definition.BackgroundImageMappings)
        {
            var sourcePath = definition.ResolveResourcePath(mapping.Resource);
            if (sourcePath is null || !File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    F(
                        DocumentViewLanguage.BackgroundImageMappingAssetMissing,
                        definition.Id,
                        mapping.Resource),
                    sourcePath);
            }

            var resource = mapping.NormalizedResource;
            if (!assets.ContainsKey(resource))
            {
                assets[resource] = store.ImportImage(sourcePath);
            }
        }

        node.Properties[GraphNodePropertyKeys.BackgroundImageMappings] = assets;
    }

    private void HandleCanvasViewportChanged(object? sender, GraphViewportChangedEventArgs e)
    {
        SaveViewport();
        ViewportChanged?.Invoke(this, e);
    }

    private void HandleAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        I18nManager.Instance.CultureChanged += HandleCultureChanged;
        HandleCultureChanged(sender, EventArgs.Empty);
    }

    private void HandleDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e) =>
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        if (_hintText is not null)
        {
            _hintText.Text = L(DocumentViewLanguage.InteractionHint);
        }
    }

    private void SaveViewport() => Session.Viewport = Canvas.Viewport;

    private void RefreshAfterCommand(GraphChangeSet changes)
    {
        var existingNodeIds = Canvas.SelectedNodeIds
            .Where(nodeId => Session.Document.FindNode(nodeId) is not null)
            .ToArray();
        if (existingNodeIds.Length != Canvas.SelectedNodeIds.Count)
        {
            Canvas.SetSelectedNodes(existingNodeIds, Canvas.SelectedNodeId);
        }

        var existingEdgeIds = Canvas.SelectedEdgeIds
            .Where(edgeId => Session.Document.Edges.Any(edge => edge.Id == edgeId))
            .ToArray();
        if (existingEdgeIds.Length != Canvas.SelectedEdgeIds.Count)
        {
            Canvas.SetSelectedEdges(existingEdgeIds, Canvas.SelectedEdgeId);
        }

        if (Canvas.SelectedAnnotationId is { } annotationId &&
            Session.Document.FindAnnotation(annotationId) is null)
        {
            Canvas.SelectedAnnotationId = null;
        }

        _isCommandRefresh = true;
        try
        {
            if (changes.Kind.HasFlag(GraphChangeKind.Settings))
            {
                SynchronizeCanvasSettings();
            }

            Canvas.ApplyGraphChanges(changes);
        }
        finally
        {
            _isCommandRefresh = false;
        }
    }

    private void RaiseStateChanged()
    {
        UpdatePropertyEditorContext();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdatePropertyEditorContext()
    {
        PropertyEditorContext = new GraphPropertyEditorContext(
            Session,
            Canvas.SelectedNodeIds,
            Canvas.SelectedNodeId,
            Canvas.SelectedEdgeId,
            RuntimeSession?.BreakpointNodeIds,
            setMessage: SetMessage,
            isReadOnly: IsReadOnly,
            tryExecuteCommand: command => TryExecuteCommand(command),
            selectedAnnotationId: Canvas.SelectedAnnotationId,
            selectedGroupIds: Canvas.SelectedGroupIds,
            resolveNodeDefinition: _nodeDefinitionResolver ?? ResolveDefinitionFromCatalog,
            tryPreviewCommand: TryPreviewCommand,
            tryCommitPreviewCommand: TryCommitPreviewCommand);
    }

    private void SynchronizeCanvasSettings()
    {
        var settings = Session.Document.CanvasSettings.Normalize();
        _isSynchronizingCanvasSettings = true;
        try
        {
            IsMinimapVisible = settings.IsMinimapVisible;
            Canvas.GridSettings = settings.Grid;
            Canvas.IsLevelOfDetailEnabled = settings.IsLevelOfDetailEnabled;
            Canvas.ShowPorts = settings.ShowPorts;
            Canvas.LodPortsZoomThreshold = settings.LodPortsZoomThreshold;
            Canvas.LodLabelsZoomThreshold = settings.LodLabelsZoomThreshold;
            Canvas.LodSimplifiedZoomThreshold = settings.LodSimplifiedZoomThreshold;
            Canvas.PortLabelVisibility = settings.PortLabelVisibility;
            Canvas.SymbolNameVisibility = settings.SymbolNameVisibility;
            Canvas.SymbolDescriptionVisibility = settings.SymbolDescriptionVisibility;
        }
        finally
        {
            _isSynchronizingCanvasSettings = false;
        }
    }

    private GraphNodeDefinition? ResolveDefinitionFromCatalog(string nodeType) =>
        NodeCatalog?.FindByNodeType(nodeType);

    private GraphSize? ResolveMinimumNodeSize(NodeModel node) =>
        (_nodeDefinitionResolver?.Invoke(node.Type) ?? ResolveDefinitionFromCatalog(node.Type))?.MinimumSize;

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}

public sealed record GraphNodeDropRequest(string NodeType, GraphPoint Position)
{
    public GraphOperationContext OperationContext { get; } =
        GraphOperationContext.User(GraphOperationInteractions.DragDrop);
}
