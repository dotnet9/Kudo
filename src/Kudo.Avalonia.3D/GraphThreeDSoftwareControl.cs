using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Kudo.Avalonia.ThreeD;

#pragma warning disable AVP1001

public sealed class GraphThreeDSoftwareControl : Control
{
    public static readonly StyledProperty<GraphThreeDScene> SceneProperty =
        AvaloniaProperty.Register<GraphThreeDSoftwareControl, GraphThreeDScene>(
            nameof(Scene),
            GraphThreeDScene.Default);

    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<GraphThreeDSoftwareControl, bool>(
            nameof(IsAnimationEnabled),
            true);

    public static readonly StyledProperty<IBrush> BackgroundBrushProperty =
        RegisterBrush(nameof(BackgroundBrush), Color.FromRgb(248, 250, 252));

    public static readonly StyledProperty<IBrush> GroundBrushProperty =
        RegisterBrush(nameof(GroundBrush), Color.FromArgb(80, 148, 163, 184));

    public static readonly StyledProperty<IBrush> StrokeBrushProperty =
        RegisterBrush(nameof(StrokeBrush), Color.FromArgb(72, 15, 23, 42));

    private readonly DispatcherTimer _animationTimer;
    private bool _isAttachedToVisualTree;
    private bool _isCameraDragging;
    private Point _cameraDragStartPoint;
    private double _cameraStartPitch;
    private double _cameraStartYaw;
    private double _rotation;

    static GraphThreeDSoftwareControl()
    {
        AffectsRender<GraphThreeDSoftwareControl>(
            SceneProperty,
            BackgroundBrushProperty,
            GroundBrushProperty,
            StrokeBrushProperty);
    }

    public GraphThreeDSoftwareControl()
    {
        ClipToBounds = true;
        Focusable = true;
        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 30)
        };
        _animationTimer.Tick += HandleAnimationTick;
    }

    public GraphThreeDScene Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value ?? GraphThreeDScene.Default);
    }

    public bool IsAnimationEnabled
    {
        get => GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    public IBrush BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    public IBrush GroundBrush
    {
        get => GetValue(GroundBrushProperty);
        set => SetValue(GroundBrushProperty, value);
    }

    public IBrush StrokeBrush
    {
        get => GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    public bool IsAnimationActive => _animationTimer.IsEnabled;

    public void RotateCamera(double yawDelta, double pitchDelta)
    {
        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        Scene = (scene with
        {
            Yaw = scene.Yaw + yawDelta,
            Pitch = scene.Pitch + pitchDelta,
            AutoRotate = false
        }).Normalize();
    }

    public void AdjustZoom(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
        {
            return;
        }

        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        Scene = (scene with
        {
            Zoom = scene.Zoom * factor,
            AutoRotate = false
        }).Normalize();
    }

    public void ResetCamera()
    {
        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        Scene = (scene with
        {
            Zoom = 1.0,
            Pitch = 24,
            Yaw = 34,
            AutoRotate = false
        }).Normalize();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(BackgroundBrush, null, bounds);
        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        if (scene.Nodes.Count > 0)
        {
            RenderGraphScene(context, bounds, scene);
            return;
        }

        RenderFallbackShapes(context, bounds, scene);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttachedToVisualTree = true;
        UpdateAnimationState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttachedToVisualTree = false;
        _animationTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty)
        {
            UpdateAnimationState();
        }
        else if (change.Property == IsAnimationEnabledProperty)
        {
            UpdateAnimationState();
            if (IsAnimationEnabled)
            {
                InvalidateVisual();
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        Focus();
        _isCameraDragging = true;
        _cameraDragStartPoint = e.GetPosition(this);
        _cameraStartPitch = scene.Pitch;
        _cameraStartYaw = scene.Yaw;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isCameraDragging)
        {
            return;
        }

        var delta = e.GetPosition(this) - _cameraDragStartPoint;
        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        Scene = (scene with
        {
            Yaw = _cameraStartYaw + delta.X * 0.45,
            Pitch = _cameraStartPitch - delta.Y * 0.35,
            AutoRotate = false
        }).Normalize();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isCameraDragging)
        {
            return;
        }

        _isCameraDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isCameraDragging = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        AdjustZoom(e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1);
        e.Handled = true;
    }

    private void RenderGraphScene(DrawingContext context, Rect bounds, GraphThreeDScene scene)
    {
        DrawGround(context, bounds);
        var scale = Math.Min(bounds.Width, bounds.Height) * 0.16 * scene.Zoom;
        var yaw = scene.Yaw + (scene.AutoRotate ? _rotation : 0);
        var projectedNodes = scene.Nodes
            .Select(node => ProjectNode(node, bounds, scale, scene.Pitch, yaw))
            .GroupBy(projected => projected.Node.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToDictionary(projected => projected.Node.Id, StringComparer.Ordinal);

        DrawGraphEdges(context, scene.Edges, projectedNodes);
        foreach (var projected in projectedNodes.Values.OrderBy(projected => projected.Depth))
        {
            DrawGraphNode(context, projected, yaw, scene.ShapeKind);
        }
    }

    private void RenderFallbackShapes(DrawingContext context, Rect bounds, GraphThreeDScene scene)
    {
        var placements = CreatePlacements(scene.ShapeCount, bounds);
        var baseSize = Math.Min(bounds.Width, bounds.Height) * (scene.ShapeCount == 1 ? 0.26 : 0.17) * scene.Zoom;
        var strokePen = new Pen(StrokeBrush, 1);

        DrawGround(context, bounds);

        for (var index = placements.Count - 1; index >= 0; index--)
        {
            var placement = placements[index];
            var size = baseSize * placement.Scale;
            var yaw = scene.Yaw + (scene.AutoRotate ? _rotation : 0) + placement.YawOffset;
            var color = Vary(scene.Color.ToAvaloniaColor(), 0.88 + index % 3 * 0.08);
            if (scene.ShapeKind == GraphThreeDShapeKind.Pyramid)
            {
                DrawPyramid(context, placement.Center, size, yaw, color, strokePen);
            }
            else
            {
                DrawCube(context, placement.Center, size, yaw, color, strokePen);
            }
        }
    }

    private void UpdateAnimationState()
    {
        if (_isAttachedToVisualTree &&
            IsAnimationEnabled &&
            (Scene ?? GraphThreeDScene.Default).Normalize().AutoRotate)
        {
            _animationTimer.Start();
            return;
        }

        _animationTimer.Stop();
    }

    private void HandleAnimationTick(object? sender, EventArgs e)
    {
        _rotation = (_rotation + 1.6) % 360;
        InvalidateVisual();
    }

    private static ProjectedNode ProjectNode(
        GraphThreeDNode node,
        Rect bounds,
        double scale,
        double pitch,
        double yaw)
    {
        var normalized = node.Normalize();
        var point = normalized.Position;
        var yawRadians = yaw * Math.PI / 180.0;
        var pitchRadians = pitch * Math.PI / 180.0;
        var yawCos = Math.Cos(yawRadians);
        var yawSin = Math.Sin(yawRadians);
        var pitchCos = Math.Cos(pitchRadians);
        var pitchSin = Math.Sin(pitchRadians);
        var rotatedX = point.X * yawCos - point.Z * yawSin;
        var rotatedZ = point.X * yawSin + point.Z * yawCos;
        var rotatedY = point.Y * pitchCos - rotatedZ * pitchSin;
        var depth = point.Y * pitchSin + rotatedZ * pitchCos;
        var perspective = Math.Clamp(1.0 / (1.0 + (depth + 4.0) * 0.08), 0.62, 1.28);
        var center = new Point(
            bounds.Width / 2 + rotatedX * scale * perspective,
            bounds.Height * 0.56 - rotatedY * scale * perspective + depth * scale * 0.12 * perspective);
        var size = normalized.Size.Normalize();

        return new ProjectedNode(
            normalized,
            center,
            depth,
            perspective,
            Math.Max(18, size.Width * scale * 0.46 * perspective),
            Math.Max(14, size.Height * scale * 0.58 * perspective),
            Math.Max(8, size.Depth * scale * 0.72 * perspective));
    }

    private void DrawGraphEdges(
        DrawingContext context,
        IEnumerable<GraphThreeDEdge> edges,
        IReadOnlyDictionary<string, ProjectedNode> projectedNodes)
    {
        foreach (var edge in edges.OrderBy(edge => edge.IsSelected ? 1 : 0))
        {
            if (!projectedNodes.TryGetValue(edge.SourceNodeId, out var source) ||
                !projectedNodes.TryGetValue(edge.TargetNodeId, out var target))
            {
                continue;
            }

            var color = edge.IsSelected
                ? Color.FromRgb(245, 158, 11)
                : Color.FromArgb(130, 71, 85, 105);
            var pen = new Pen(new ImmutableSolidColorBrush(color), edge.IsSelected ? 3.0 : 1.6);
            context.DrawLine(pen, source.Center, target.Center);
            if (edge.IsSelected)
            {
                context.DrawEllipse(
                    new ImmutableSolidColorBrush(Color.FromArgb(220, 245, 158, 11)),
                    null,
                    target.Center,
                    4,
                    4);
            }
        }
    }

    private void DrawGraphNode(
        DrawingContext context,
        ProjectedNode projected,
        double yaw,
        GraphThreeDShapeKind fallbackShapeKind)
    {
        var node = projected.Node;
        var color = node.Color.ToAvaloniaColor();
        var strokeColor = node.IsSelected
            ? Color.FromRgb(245, 158, 11)
            : Color.FromArgb(78, 15, 23, 42);
        var strokePen = new Pen(new ImmutableSolidColorBrush(strokeColor), node.IsSelected ? 2.4 : 1);
        if (node.IsSelected)
        {
            context.DrawEllipse(
                new ImmutableSolidColorBrush(Color.FromArgb(54, 245, 158, 11)),
                null,
                projected.Center + new Point(0, projected.Height * 0.35),
                projected.Width * 0.62,
                Math.Max(5, projected.Height * 0.16));
        }

        if ((node.ShapeKind ?? fallbackShapeKind) == GraphThreeDShapeKind.Pyramid)
        {
            DrawPyramid(
                context,
                projected.Center,
                Math.Max(projected.Width, Math.Max(projected.Height, projected.DepthSize)),
                yaw,
                color,
                strokePen);
        }
        else
        {
            DrawBox(
                context,
                projected.Center,
                projected.Width,
                projected.Height,
                projected.DepthSize,
                yaw,
                color,
                strokePen);
        }

        DrawNodeLabel(context, projected);
    }

    private static void DrawNodeLabel(DrawingContext context, ProjectedNode projected)
    {
        var node = projected.Node;
        var label = string.IsNullOrWhiteSpace(node.Title)
            ? string.IsNullOrWhiteSpace(node.Type) ? node.Id : node.Type
            : node.Title;
        if (string.IsNullOrWhiteSpace(label) || projected.Width < 34 || projected.Height < 18)
        {
            return;
        }

        var fontSize = Math.Clamp(projected.Height * 0.18, 8, 12);
        var text = CreateText(label, fontSize, new ImmutableSolidColorBrush(Color.FromRgb(15, 23, 42)));
        var bounds = new Rect(
            projected.Center.X - projected.Width / 2 + 5,
            projected.Center.Y - projected.Height / 2 + 4,
            Math.Max(1, projected.Width - 10),
            Math.Max(1, Math.Min(projected.Height - 8, text.Height + 2)));
        using (context.PushClip(bounds))
        {
            context.DrawText(text, bounds.TopLeft);
        }
    }

    private void DrawGround(DrawingContext context, Rect bounds)
    {
        context.DrawEllipse(
            GroundBrush,
            null,
            new Point(bounds.Width / 2, bounds.Height * 0.80),
            bounds.Width * 0.36,
            Math.Max(8, bounds.Height * 0.055));
    }

    private static IReadOnlyList<ShapePlacement> CreatePlacements(int count, Rect bounds)
    {
        var normalizedCount = Math.Clamp(count, 1, 9);
        if (normalizedCount == 1)
        {
            return [new ShapePlacement(new Point(bounds.Width / 2, bounds.Height * 0.52), 1.0, 0)];
        }

        var columns = Math.Min(3, normalizedCount);
        var rows = (int)Math.Ceiling(normalizedCount / (double)columns);
        var stepX = bounds.Width / (columns + 1);
        var stepY = bounds.Height * 0.52 / (rows + 1);
        var placements = new List<ShapePlacement>(normalizedCount);

        for (var index = 0; index < normalizedCount; index++)
        {
            var row = index / columns;
            var column = index % columns;
            var x = stepX * (column + 1);
            var y = bounds.Height * 0.26 + stepY * (row + 1);
            var depthScale = 0.86 + row * 0.08;
            placements.Add(new ShapePlacement(new Point(x, y), depthScale, index * 18));
        }

        return placements;
    }

    private static void DrawBox(
        DrawingContext context,
        Point center,
        double width,
        double height,
        double depth,
        double yaw,
        Color color,
        IPen strokePen)
    {
        var turn = Math.Cos(yaw * Math.PI / 180.0);
        var offset = new Point(depth * (0.58 + 0.16 * turn), -depth * 0.52);
        var front = new[]
        {
            center + new Point(-width / 2, -height / 2),
            center + new Point(width / 2, -height / 2),
            center + new Point(width / 2, height / 2),
            center + new Point(-width / 2, height / 2)
        };
        var top = new[] { front[0] + offset, front[1] + offset, front[1], front[0] };
        var side = new[] { front[1] + offset, front[2] + offset, front[2], front[1] };

        DrawPolygon(context, side, Shade(color, 0.66), strokePen);
        DrawPolygon(context, top, Shade(color, 1.18), strokePen);
        DrawPolygon(context, front, Shade(color, 0.90), strokePen);
    }

    private static void DrawCube(
        DrawingContext context,
        Point center,
        double size,
        double yaw,
        Color color,
        IPen strokePen)
    {
        var turn = Math.Cos(yaw * Math.PI / 180.0);
        var offset = new Point(size * (0.34 + 0.08 * turn), -size * 0.28);
        var front = new[]
        {
            center + new Point(-size * 0.52, -size * 0.34),
            center + new Point(size * 0.50, -size * 0.38),
            center + new Point(size * 0.50, size * 0.56),
            center + new Point(-size * 0.50, size * 0.68)
        };
        var top = new[] { front[0] + offset, front[1] + offset, front[1], front[0] };
        var side = new[] { front[1] + offset, front[2] + offset, front[2], front[1] };

        DrawPolygon(context, side, Shade(color, 0.64), strokePen);
        DrawPolygon(context, top, Shade(color, 1.20), strokePen);
        DrawPolygon(context, front, Shade(color, 0.88), strokePen);
    }

    private static void DrawPyramid(
        DrawingContext context,
        Point center,
        double size,
        double yaw,
        Color color,
        IPen strokePen)
    {
        var turn = Math.Cos(yaw * Math.PI / 180.0);
        var apex = center + new Point(size * 0.10 * turn, -size * 0.86);
        var left = center + new Point(-size * 0.74, size * 0.58);
        var right = center + new Point(size * 0.72, size * 0.52);
        var back = center + new Point(size * (0.20 + 0.10 * turn), size * 0.04);

        DrawPolygon(context, [apex, back, right], Shade(color, 0.65), strokePen);
        DrawPolygon(context, [apex, left, back], Shade(color, 1.12), strokePen);
        DrawPolygon(context, [apex, left, right], Shade(color, 0.86), strokePen);
    }

    private static void DrawPolygon(
        DrawingContext context,
        IReadOnlyList<Point> points,
        IBrush fill,
        IPen strokePen)
    {
        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(points[0], true);
            for (var index = 1; index < points.Count; index++)
            {
                geometryContext.LineTo(points[index]);
            }

            geometryContext.EndFigure(true);
        }

        context.DrawGeometry(fill, strokePen, geometry);
    }

    private static Color Vary(Color color, double factor) =>
        Color.FromArgb(
            color.A,
            Scale(color.R, factor),
            Scale(color.G, factor),
            Scale(color.B, factor));

    private static IBrush Shade(Color color, double factor) =>
        new ImmutableSolidColorBrush(Vary(color, factor));

    private static byte Scale(byte value, double factor) =>
        (byte)Math.Clamp(value * factor, 0, 255);

    private static StyledProperty<IBrush> RegisterBrush(string name, Color defaultColor) =>
        AvaloniaProperty.Register<GraphThreeDSoftwareControl, IBrush>(
            name,
            new ImmutableSolidColorBrush(defaultColor));

    private static FormattedText CreateText(string text, double fontSize, IBrush brush) =>
        new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            brush);

    private sealed record ShapePlacement(Point Center, double Scale, double YawOffset);

    private sealed record ProjectedNode(
        GraphThreeDNode Node,
        Point Center,
        double Depth,
        double Perspective,
        double Width,
        double Height,
        double DepthSize);
}

#pragma warning restore AVP1001
