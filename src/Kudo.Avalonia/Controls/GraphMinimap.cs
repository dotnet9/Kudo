using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

#pragma warning disable AVP1001

public class GraphMinimap : Control
{
    public static readonly StyledProperty<GraphCanvas?> TargetCanvasProperty =
        AvaloniaProperty.Register<GraphMinimap, GraphCanvas?>(nameof(TargetCanvas));
    public static readonly StyledProperty<IBrush> BackgroundBrushProperty =
        RegisterBrush(nameof(BackgroundBrush), Colors.White);
    public static readonly StyledProperty<IBrush> NodeBrushProperty =
        RegisterBrush(nameof(NodeBrush), Colors.Gray);
    public static readonly StyledProperty<IBrush> SelectedNodeBrushProperty =
        RegisterBrush(nameof(SelectedNodeBrush), Colors.Blue);
    public static readonly StyledProperty<IBrush> GroupBrushProperty =
        RegisterBrush(nameof(GroupBrush), Colors.Transparent);
    public static readonly StyledProperty<IBrush> SelectedGroupBrushProperty =
        RegisterBrush(nameof(SelectedGroupBrush), Colors.Transparent);
    public static readonly StyledProperty<IBrush> EdgeBrushProperty =
        RegisterBrush(nameof(EdgeBrush), Colors.Gray);
    public static readonly StyledProperty<IBrush> ViewportFillBrushProperty =
        RegisterBrush(nameof(ViewportFillBrush), Colors.Transparent);
    public static readonly StyledProperty<IBrush> ViewportStrokeBrushProperty =
        RegisterBrush(nameof(ViewportStrokeBrush), Colors.Blue);
    public static readonly StyledProperty<IBrush> BorderBrushProperty =
        RegisterBrush(nameof(BorderBrush), Colors.Gray);

    private static readonly HashSet<AvaloniaProperty> PaletteProperties =
    [
        BackgroundBrushProperty,
        NodeBrushProperty,
        SelectedNodeBrushProperty,
        GroupBrushProperty,
        SelectedGroupBrushProperty,
        EdgeBrushProperty,
        ViewportFillBrushProperty,
        ViewportStrokeBrushProperty,
        BorderBrushProperty
    ];

    private const double GraphPadding = 20;

    private GraphCanvas? _subscribedCanvas;
    private bool _isDragging;
    private GraphMinimapProjection? _dragProjection;
    private readonly DispatcherTimer _viewportRefreshTimer;
    private readonly GraphMinimapContentRenderer _contentRenderer = new();
    private readonly GraphMinimapContentCache _contentCache = new();
    private bool _isViewportRefreshPending;

    public GraphMinimap()
    {
        ClipToBounds = true;
        _viewportRefreshTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60)
        };
        _viewportRefreshTimer.Tick += HandleViewportRefreshTick;
    }

    public GraphCanvas? TargetCanvas
    {
        get => GetValue(TargetCanvasProperty);
        set => SetValue(TargetCanvasProperty, value);
    }

    public IBrush BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    public IBrush NodeBrush
    {
        get => GetValue(NodeBrushProperty);
        set => SetValue(NodeBrushProperty, value);
    }

    public IBrush SelectedNodeBrush
    {
        get => GetValue(SelectedNodeBrushProperty);
        set => SetValue(SelectedNodeBrushProperty, value);
    }

    public IBrush GroupBrush
    {
        get => GetValue(GroupBrushProperty);
        set => SetValue(GroupBrushProperty, value);
    }

    public IBrush SelectedGroupBrush
    {
        get => GetValue(SelectedGroupBrushProperty);
        set => SetValue(SelectedGroupBrushProperty, value);
    }

    public IBrush EdgeBrush
    {
        get => GetValue(EdgeBrushProperty);
        set => SetValue(EdgeBrushProperty, value);
    }

    public IBrush ViewportFillBrush
    {
        get => GetValue(ViewportFillBrushProperty);
        set => SetValue(ViewportFillBrushProperty, value);
    }

    public IBrush ViewportStrokeBrush
    {
        get => GetValue(ViewportStrokeBrushProperty);
        set => SetValue(ViewportStrokeBrushProperty, value);
    }

    public IBrush BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public GraphMinimapProjection? CalculateProjection(Size minimapSize)
    {
        var canvas = TargetCanvas;
        var graphBounds = canvas?.GetDocumentGraphBounds();
        if (canvas is null || graphBounds is null || minimapSize.Width <= 0 || minimapSize.Height <= 0)
        {
            return null;
        }

        var viewportBounds = canvas.VisibleGraphBounds;
        var extent = graphBounds.Value.Inflate(GraphPadding);
        if (extent.Width <= 0 || extent.Height <= 0)
        {
            return null;
        }

        var scale = Math.Min(minimapSize.Width / extent.Width, minimapSize.Height / extent.Height);
        if (!double.IsFinite(scale) || scale <= 0)
        {
            return null;
        }

        var horizontalSpace = minimapSize.Width - extent.Width * scale;
        var verticalSpace = minimapSize.Height - extent.Height * scale;
        var origin = new GraphPoint(
            extent.X - horizontalSpace / 2 / scale,
            extent.Y - verticalSpace / 2 / scale);

        return new GraphMinimapProjection(extent, origin, scale, viewportBounds);
    }

    public bool NavigateTo(Point minimapPoint)
    {
        var canvas = TargetCanvas;
        var projection = CalculateProjection(Bounds.Size);
        if (canvas is null || projection is null || canvas.VisibleGraphBounds is null)
        {
            return false;
        }

        return NavigateTo(minimapPoint, projection);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var palette = CreateRenderPalette();
        context.DrawRectangle(palette.BackgroundBrush, palette.BorderPen, Bounds, 4, 4);

        var canvas = TargetCanvas;
        var document = canvas?.Document;
        var projection = CalculateProjection(Bounds.Size);
        if (canvas is null || document is null || projection is null)
        {
            return;
        }

        if (!_contentCache.TryRender(
                context,
                Bounds.Size,
                canvas,
                document,
                projection,
                palette,
                _contentRenderer))
        {
            _contentRenderer.Render(context, canvas, document, projection, palette);
        }

        foreach (var group in document.Groups)
        {
            if (!canvas.IsGroupSelected(group.Id) ||
                GraphGroupBoundsCalculator.Calculate(document, group) is not { } bounds)
            {
                continue;
            }

            context.DrawRectangle(
                palette.SelectedGroupBrush,
                null,
                projection.GraphToMinimap(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height)),
                2,
                2);
        }

        foreach (var node in document.Nodes)
        {
            if (!canvas.IsNodeSelected(node.Id) || !canvas.IsNodeVisible(node.Id))
            {
                continue;
            }

            var bounds = GraphNodeGeometry.GetCanvasBounds(node);
            var position = projection.GraphToMinimap(new GraphPoint(bounds.X, bounds.Y));
            var nodeRect = new Rect(
                position.X,
                position.Y,
                Math.Max(4, bounds.Width * projection.Scale),
                Math.Max(3, bounds.Height * projection.Scale));
            context.DrawRectangle(palette.SelectedNodeBrush, null, nodeRect, 2, 2);
        }

        if (projection.ViewportBounds is { } viewportBounds)
        {
            var viewportRect = projection.GraphToMinimap(viewportBounds);
            context.DrawRectangle(palette.ViewportBrush, palette.ViewportPen, viewportRect);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TargetCanvasProperty)
        {
            UnsubscribeFromTarget();
            SubscribeToTarget(change.NewValue as GraphCanvas);
            _contentCache.Invalidate();
            InvalidateVisual();
        }

        if (IsPaletteProperty(change.Property))
        {
            _contentCache.Invalidate();
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeToTarget(TargetCanvas);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _viewportRefreshTimer.Stop();
        _isViewportRefreshPending = false;
        UnsubscribeFromTarget();
        _contentCache.Invalidate();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _contentCache.Invalidate();
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var projection = CalculateProjection(Bounds.Size);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            projection is null ||
            !NavigateTo(e.GetPosition(this), projection))
        {
            return;
        }

        _isDragging = true;
        _dragProjection = projection;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging || _dragProjection is null)
        {
            return;
        }

        NavigateTo(e.GetPosition(this), _dragProjection);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDragging)
        {
            return;
        }

        EndPointerInteraction(e.Pointer);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
        _dragProjection = null;
    }

    private void SubscribeToTarget(GraphCanvas? canvas)
    {
        if (canvas is null || ReferenceEquals(canvas, _subscribedCanvas))
        {
            return;
        }

        _subscribedCanvas = canvas;
        canvas.PropertyChanged += OnTargetPropertyChanged;
        canvas.ViewportChanged += OnTargetViewportChanged;
        canvas.GraphChanged += OnTargetGraphChanged;
        canvas.EdgeRoutingConfigurationChanged += OnTargetGraphChanged;
        canvas.SelectionChanged += OnTargetSelectionChanged;
        canvas.SizeChanged += OnTargetSizeChanged;
    }

    private void UnsubscribeFromTarget()
    {
        if (_subscribedCanvas is null)
        {
            return;
        }

        _subscribedCanvas.PropertyChanged -= OnTargetPropertyChanged;
        _subscribedCanvas.ViewportChanged -= OnTargetViewportChanged;
        _subscribedCanvas.GraphChanged -= OnTargetGraphChanged;
        _subscribedCanvas.EdgeRoutingConfigurationChanged -= OnTargetGraphChanged;
        _subscribedCanvas.SelectionChanged -= OnTargetSelectionChanged;
        _subscribedCanvas.SizeChanged -= OnTargetSizeChanged;
        _subscribedCanvas = null;
    }

    private void OnTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == GraphCanvas.DocumentProperty ||
            e.Property == GraphCanvas.SelectedNodeIdProperty)
        {
            if (e.Property == GraphCanvas.DocumentProperty)
            {
                _contentCache.Invalidate();
            }

            InvalidateVisual();
        }
    }

    private void OnTargetViewportChanged(object? sender, GraphViewportChangedEventArgs e) =>
        QueueViewportRefresh();

    private void QueueViewportRefresh()
    {
        _isViewportRefreshPending = true;
        if (!_viewportRefreshTimer.IsEnabled)
        {
            _viewportRefreshTimer.Start();
        }
    }

    private void HandleViewportRefreshTick(object? sender, EventArgs e)
    {
        if (!_isViewportRefreshPending)
        {
            _viewportRefreshTimer.Stop();
            return;
        }

        _isViewportRefreshPending = false;
        InvalidateVisual();
    }

    private void OnTargetGraphChanged(object? sender, EventArgs e)
    {
        _contentCache.Invalidate();
        InvalidateVisual();
    }

    private void OnTargetSelectionChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnTargetSizeChanged(object? sender, SizeChangedEventArgs e) => InvalidateVisual();

    private void EndPointerInteraction(IPointer pointer)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _dragProjection = null;
        pointer.Capture(null);
    }

    private bool NavigateTo(Point minimapPoint, GraphMinimapProjection projection)
    {
        var canvas = TargetCanvas;
        if (canvas is null || canvas.VisibleGraphBounds is null)
        {
            return false;
        }

        return canvas.CenterViewport(projection.MinimapToGraph(minimapPoint));
    }

    private GraphMinimapRenderPalette CreateRenderPalette() =>
        new(
            BackgroundBrush,
            NodeBrush,
            SelectedNodeBrush,
            GroupBrush,
            SelectedGroupBrush,
            new Pen(EdgeBrush, 1),
            ViewportFillBrush,
            new Pen(ViewportStrokeBrush, 2),
            new Pen(BorderBrush, 1));

    private static StyledProperty<IBrush> RegisterBrush(string name, Color defaultColor) =>
        AvaloniaProperty.Register<GraphMinimap, IBrush>(
            name,
            new ImmutableSolidColorBrush(defaultColor));

    private static bool IsPaletteProperty(AvaloniaProperty property) =>
        PaletteProperties.Contains(property);
}

#pragma warning restore AVP1001

public sealed record GraphMinimapProjection(
    Rect Extent,
    GraphPoint Origin,
    double Scale,
    Rect? ViewportBounds)
{
    public Point GraphToMinimap(GraphPoint point) =>
        new((point.X - Origin.X) * Scale, (point.Y - Origin.Y) * Scale);

    public Rect GraphToMinimap(Rect rect)
    {
        var position = GraphToMinimap(new GraphPoint(rect.X, rect.Y));
        return new Rect(position.X, position.Y, rect.Width * Scale, rect.Height * Scale);
    }

    public GraphPoint MinimapToGraph(Point point) =>
        new(point.X / Scale + Origin.X, point.Y / Scale + Origin.Y);
}
