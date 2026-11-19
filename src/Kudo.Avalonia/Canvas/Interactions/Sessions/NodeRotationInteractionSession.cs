using Avalonia;
using Avalonia.Input;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class NodeRotationInteractionSession : GraphInteractionSession
{
    private readonly NodeRotationHandleHitResult _hit;
    private readonly IReadOnlyDictionary<string, NodeTransform> _startTransforms;
    private readonly GraphRect _selectionBounds;
    private readonly GraphPoint _selectionCenter;
    private readonly Point _centerScreenPoint;
    private readonly double _startPointerAngle;

    public NodeRotationInteractionSession(
        GraphCanvas canvas,
        IPointer pointer,
        Point startScreenPoint,
        NodeRotationHandleHitResult hit)
        : base(canvas, pointer, startScreenPoint)
    {
        _hit = hit;
        _startTransforms = hit.SelectedNodes.ToDictionary(
            node => node.Id,
            node => new NodeTransform(node.Position, GraphNodeGeometry.NormalizeRotation(node.Rotation)),
            StringComparer.Ordinal);
        _selectionBounds = canvas.GetSelectedNodeRotationBounds(hit.SelectedNodes) ??
            GraphNodeGeometry.GetCanvasBounds(hit.Node);
        _selectionCenter = new GraphPoint(
            _selectionBounds.X + _selectionBounds.Width / 2,
            _selectionBounds.Y + _selectionBounds.Height / 2);
        _centerScreenPoint = canvas.GraphToScreen(_selectionCenter);
        _startPointerAngle = GetPointerAngle(_centerScreenPoint, startScreenPoint);
        CurrentRotation = GraphNodeGeometry.NormalizeRotation(hit.Node.Rotation);
    }

    public override bool SupportsAutoPan => false;

    public double CurrentRotation { get; private set; }

    public double FrameRotation { get; private set; }

    public string NodeId => _hit.Node.Id;

    public GraphRect SelectionBounds => _selectionBounds;

    public Point RotationHandlePosition => _startTransforms.Count == 1
        ? Canvas.GetNodeRotationHandlePosition(_hit.Node)
        : Canvas.GetSelectionRotationHandlePositions(_selectionBounds, FrameRotation).Handle;

    public override void Update(Point point, KeyModifiers modifiers)
    {
        var delta = ResolveRotationDelta(point, modifiers);
        if (!double.IsFinite(delta))
        {
            return;
        }

        Apply(delta);
        FrameRotation = delta;
        CurrentRotation = _startTransforms.Count == 1
            ? _hit.Node.Rotation
            : delta;
        Canvas.InvalidateNodeGeometry(_startTransforms.Keys);
        Canvas.UpdateNodeControlHostLayout(_startTransforms.Keys);
        Canvas.InvalidateVisual();
    }

    public override void Complete(Point point)
    {
        if (Canvas.Document is not { } document)
        {
            return;
        }

        var commands = document.Nodes
            .Where(node => _startTransforms.ContainsKey(node.Id))
            .Select(node =>
            {
                var start = _startTransforms[node.Id];
                return new SetNodePropertiesCommand(
                    node.Id,
                    node.Number,
                    node.Number,
                    node.Name,
                    node.Name,
                    node.Description,
                    node.Description,
                    start.Position,
                    node.Position,
                    start.Rotation,
                    GraphNodeGeometry.NormalizeRotation(node.Rotation),
                    GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.RotateNodeCommand));
            })
            .Where(command =>
                command.OldPosition != command.NewPosition ||
                Math.Abs(NormalizeAngleDelta(command.NewRotation - command.OldRotation)) >= 0.0001)
            .ToArray();
        if (commands.Length == 0)
        {
            RestoreStartTransforms(document);
            Canvas.InvalidateNodeGeometry(_startTransforms.Keys);
            Canvas.UpdateNodeControlHostLayout(_startTransforms.Keys);
            Canvas.InvalidateVisual();
            return;
        }

        Canvas.CommitGraphEdit(commands.Length == 1
            ? commands[0]
            : new CompositeGraphCommand(
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.RotateNodeCommand),
                commands));
    }

    public override void CaptureLost() => Cancel();

    public override void Cancel()
    {
        if (Canvas.Document is { } document)
        {
            RestoreStartTransforms(document);
        }

        CurrentRotation = _startTransforms[_hit.Node.Id].Rotation;
        FrameRotation = 0;
        Canvas.InvalidateNodeGeometry(_startTransforms.Keys);
        Canvas.UpdateNodeControlHostLayout(_startTransforms.Keys);
        Canvas.InvalidateVisual();
    }

    private void Apply(double delta)
    {
        if (Canvas.Document is not { } document)
        {
            return;
        }

        foreach (var node in document.Nodes)
        {
            if (!_startTransforms.TryGetValue(node.Id, out var start))
            {
                continue;
            }

            var startCenter = new GraphPoint(
                start.Position.X + node.Size.Width / 2,
                start.Position.Y + node.Size.Height / 2);
            var center = GraphNodeGeometry.RotatePoint(startCenter, _selectionCenter, delta);
            node.Position = new GraphPoint(
                center.X - node.Size.Width / 2,
                center.Y - node.Size.Height / 2);
            node.Rotation = GraphNodeGeometry.NormalizeRotation(start.Rotation + delta);
        }
    }

    private void RestoreStartTransforms(GraphDocument document)
    {
        foreach (var node in document.Nodes)
        {
            if (_startTransforms.TryGetValue(node.Id, out var start))
            {
                node.Position = start.Position;
                node.Rotation = start.Rotation;
            }
        }
    }

    private double ResolveRotationDelta(Point point, KeyModifiers modifiers)
    {
        var pointerAngle = GetPointerAngle(_centerScreenPoint, point);
        var rotation = NormalizeAngleDelta(pointerAngle - _startPointerAngle);
        return modifiers.HasFlag(KeyModifiers.Shift)
            ? SnapRotation(rotation)
            : rotation;
    }

    private static double GetPointerAngle(Point center, Point point)
    {
        var delta = point - center;
        return Math.Atan2(delta.Y, delta.X) * 180 / Math.PI;
    }

    private static double NormalizeAngleDelta(double angle)
    {
        var normalized = GraphNodeGeometry.NormalizeRotation(angle);
        return normalized > 180 ? normalized - 360 : normalized;
    }

    private static double SnapRotation(double rotation) =>
        GraphNodeGeometry.NormalizeRotation(
            Math.Round(
                rotation / GraphCanvas.NodeRotationSnapStep,
                MidpointRounding.AwayFromZero) *
            GraphCanvas.NodeRotationSnapStep);

    private readonly record struct NodeTransform(GraphPoint Position, double Rotation);
}
