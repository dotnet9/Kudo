using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Kudo.Avalonia;

/// <summary>
/// Displays graph coordinates for one axis of a <see cref="GraphCanvas"/>.
/// </summary>
public sealed class GraphRuler : TemplatedControl
{
    public static readonly StyledProperty<GraphCanvas?> TargetCanvasProperty =
        AvaloniaProperty.Register<GraphRuler, GraphCanvas?>(nameof(TargetCanvas));

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<GraphRuler, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public static readonly StyledProperty<IBrush> MinorTickBrushProperty =
        AvaloniaProperty.Register<GraphRuler, IBrush>(nameof(MinorTickBrush), Brushes.LightGray);

    public static readonly StyledProperty<IBrush> MajorTickBrushProperty =
        AvaloniaProperty.Register<GraphRuler, IBrush>(nameof(MajorTickBrush), Brushes.Gray);

    public static readonly StyledProperty<double> MinorTickLengthProperty =
        AvaloniaProperty.Register<GraphRuler, double>(nameof(MinorTickLength), 3.0);

    public static readonly StyledProperty<double> MajorTickLengthProperty =
        AvaloniaProperty.Register<GraphRuler, double>(nameof(MajorTickLength), 7.0);

    public static readonly StyledProperty<double> MinimumLabelSpacingProperty =
        AvaloniaProperty.Register<GraphRuler, double>(nameof(MinimumLabelSpacing), 42.0);

    public static readonly StyledProperty<bool> IsOriginLabelVisibleProperty =
        AvaloniaProperty.Register<GraphRuler, bool>(nameof(IsOriginLabelVisible), true);

    private const int MaximumTickCount = 2048;
    private static readonly HashSet<AvaloniaProperty> AppearanceProperties =
    [
        BackgroundProperty,
        MinorTickBrushProperty,
        MajorTickBrushProperty,
        BorderBrushProperty,
        ForegroundProperty,
        FontSizeProperty,
        MinorTickLengthProperty,
        MajorTickLengthProperty,
        MinimumLabelSpacingProperty,
        IsOriginLabelVisibleProperty
    ];

    private GraphCanvas? _subscribedCanvas;

    static GraphRuler()
    {
        BackgroundProperty.OverrideDefaultValue<GraphRuler>(Brushes.Transparent);
        BorderBrushProperty.OverrideDefaultValue<GraphRuler>(Brushes.Gray);
        ForegroundProperty.OverrideDefaultValue<GraphRuler>(Brushes.Gray);
        FontSizeProperty.OverrideDefaultValue<GraphRuler>(9.0);
    }

    public GraphRuler()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
    }

    public GraphCanvas? TargetCanvas
    {
        get => GetValue(TargetCanvasProperty);
        set => SetValue(TargetCanvasProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public IBrush MinorTickBrush
    {
        get => GetValue(MinorTickBrushProperty);
        set => SetValue(MinorTickBrushProperty, value);
    }

    public IBrush MajorTickBrush
    {
        get => GetValue(MajorTickBrushProperty);
        set => SetValue(MajorTickBrushProperty, value);
    }

    public double MinorTickLength
    {
        get => GetValue(MinorTickLengthProperty);
        set => SetValue(MinorTickLengthProperty, value);
    }

    public double MajorTickLength
    {
        get => GetValue(MajorTickLengthProperty);
        set => SetValue(MajorTickLengthProperty, value);
    }

    public double MinimumLabelSpacing
    {
        get => GetValue(MinimumLabelSpacingProperty);
        set => SetValue(MinimumLabelSpacingProperty, value);
    }

    public bool IsOriginLabelVisible
    {
        get => GetValue(IsOriginLabelVisibleProperty);
        set => SetValue(IsOriginLabelVisibleProperty, value);
    }

    /// <summary>
    /// Calculates the visible ticks using the same adaptive grid metrics as the target canvas.
    /// </summary>
    public IReadOnlyList<GraphRulerTick> CalculateTicks(Size rulerSize)
    {
        var canvas = TargetCanvas;
        var length = Orientation == Orientation.Horizontal ? rulerSize.Width : rulerSize.Height;
        if (canvas is null || !double.IsFinite(length) || length <= 0)
        {
            return [];
        }

        var viewport = canvas.Viewport;
        var zoom = viewport.Zoom;
        if (!double.IsFinite(zoom) || zoom <= 0)
        {
            return [];
        }

        var metrics = canvas.GetGridRenderMetrics();
        if (!double.IsFinite(metrics.MinorSpacing) || metrics.MinorSpacing <= 0)
        {
            return [];
        }

        var offset = Orientation == Orientation.Horizontal
            ? viewport.Offset.X
            : viewport.Offset.Y;
        if (!double.IsFinite(offset))
        {
            return [];
        }

        var firstGraphValue = -offset / zoom;
        var lastGraphValue = (length - offset) / zoom;
        var firstIndexValue = Math.Floor(Math.Min(firstGraphValue, lastGraphValue) / metrics.MinorSpacing);
        var lastIndexValue = Math.Ceiling(Math.Max(firstGraphValue, lastGraphValue) / metrics.MinorSpacing);
        var tickCount = lastIndexValue - firstIndexValue + 1;
        if (!double.IsFinite(firstIndexValue) ||
            !double.IsFinite(lastIndexValue) ||
            firstIndexValue < long.MinValue ||
            lastIndexValue > long.MaxValue ||
            tickCount is <= 0 or > MaximumTickCount)
        {
            return [];
        }

        var firstIndex = (long)firstIndexValue;
        var lastIndex = (long)lastIndexValue;
        var labelStride = GetLabelStride(metrics.MajorSpacing * zoom);
        var isOriginLabelVisible = IsOriginLabelVisible;
        var result = new List<GraphRulerTick>((int)tickCount);
        for (var index = firstIndex; index <= lastIndex; index++)
        {
            var graphValue = index * metrics.MinorSpacing;
            var screenPosition = graphValue * zoom + offset;
            if (screenPosition < -0.5 || screenPosition > length + 0.5)
            {
                continue;
            }

            var isMajor = index % metrics.MajorLineInterval == 0;
            var majorIndex = isMajor ? index / metrics.MajorLineInterval : 0;
            var label = isMajor &&
                majorIndex % labelStride == 0 &&
                (isOriginLabelVisible || index != 0)
                ? FormatCoordinate(graphValue)
                : null;
            result.Add(new GraphRulerTick(graphValue, screenPosition, isMajor, label));
        }

        return result;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(Background, null, new Rect(Bounds.Size));

        var borderPen = new Pen(BorderBrush ?? Brushes.Transparent, 1);
        if (Orientation == Orientation.Horizontal)
        {
            context.DrawLine(borderPen, new Point(0, Bounds.Height - 0.5), new Point(Bounds.Width, Bounds.Height - 0.5));
        }
        else
        {
            context.DrawLine(borderPen, new Point(Bounds.Width - 0.5, 0), new Point(Bounds.Width - 0.5, Bounds.Height));
        }

        var minorPen = new Pen(MinorTickBrush, 1);
        var majorPen = new Pen(MajorTickBrush, 1);
        foreach (var tick in CalculateTicks(Bounds.Size))
        {
            DrawTick(context, tick, tick.IsMajor ? majorPen : minorPen);
            if (tick.Label is { } label)
            {
                DrawLabel(context, tick.ScreenPosition, label);
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TargetCanvasProperty)
        {
            UnsubscribeFromTarget();
            SubscribeToTarget(change.NewValue as GraphCanvas);
            InvalidateVisual();
        }

        if (change.Property == OrientationProperty || AppearanceProperties.Contains(change.Property))
        {
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
        UnsubscribeFromTarget();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateVisual();
    }

    private void DrawTick(DrawingContext context, GraphRulerTick tick, IPen pen)
    {
        var length = NormalizeLength(tick.IsMajor ? MajorTickLength : MinorTickLength);
        if (Orientation == Orientation.Horizontal)
        {
            context.DrawLine(
                pen,
                new Point(tick.ScreenPosition, Math.Max(0, Bounds.Height - length)),
                new Point(tick.ScreenPosition, Bounds.Height));
        }
        else
        {
            context.DrawLine(
                pen,
                new Point(Math.Max(0, Bounds.Width - length), tick.ScreenPosition),
                new Point(Bounds.Width, tick.ScreenPosition));
        }
    }

    private void DrawLabel(DrawingContext context, double position, string label)
    {
        var text = CreateText(label, NormalizeFontSize(FontSize), Foreground ?? Brushes.Gray);
        const double padding = 2;
        if (Orientation == Orientation.Horizontal)
        {
            var x = ClampCoordinate(position - text.Width / 2, padding, Bounds.Width - text.Width - padding);
            context.DrawText(text, new Point(x, padding));
            return;
        }

        var center = new Point(
            Math.Max(text.Height / 2 + padding, Bounds.Width - NormalizeLength(MajorTickLength) - text.Height / 2 - padding),
            ClampCoordinate(position, text.Width / 2 + padding, Bounds.Height - text.Width / 2 - padding));
        using (context.PushTransform(Matrix.CreateRotation(-Math.PI / 2, center)))
        {
            context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
        }
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
        canvas.GraphChanged += OnTargetChanged;
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
        _subscribedCanvas.GraphChanged -= OnTargetChanged;
        _subscribedCanvas.SizeChanged -= OnTargetSizeChanged;
        _subscribedCanvas = null;
    }

    private void OnTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == GraphCanvas.GridSettingsProperty)
        {
            InvalidateVisual();
        }
    }

    private void OnTargetViewportChanged(object? sender, GraphViewportChangedEventArgs e) =>
        InvalidateVisual();

    private void OnTargetChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnTargetSizeChanged(object? sender, SizeChangedEventArgs e) => InvalidateVisual();

    private int GetLabelStride(double majorPixelSpacing)
    {
        var minimumSpacing = double.IsFinite(MinimumLabelSpacing) && MinimumLabelSpacing > 0
            ? MinimumLabelSpacing
            : 42;
        if (!double.IsFinite(majorPixelSpacing) || majorPixelSpacing <= 0)
        {
            return 1;
        }

        var stride = Math.Ceiling(minimumSpacing / majorPixelSpacing);
        return stride >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)stride);
    }

    private static string FormatCoordinate(double value)
    {
        var normalized = Math.Abs(value) < 0.0000001 ? 0 : value;
        var magnitude = Math.Abs(normalized);
        return magnitude is >= 1_000_000_000 or > 0 and < 0.001
            ? normalized.ToString("0.###E+0", CultureInfo.CurrentCulture)
            : normalized.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static double NormalizeLength(double value) =>
        double.IsFinite(value) && value >= 0 ? value : 0;

    private static double NormalizeFontSize(double value) =>
        double.IsFinite(value) && value > 0 ? value : 9;

    private static double ClampCoordinate(double value, double minimum, double maximum) =>
        maximum >= minimum ? Math.Clamp(value, minimum, maximum) : minimum;

    private static FormattedText CreateText(string text, double fontSize, IBrush brush) =>
        new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            brush);
}

public readonly record struct GraphRulerTick(
    double GraphValue,
    double ScreenPosition,
    bool IsMajor,
    string? Label);
