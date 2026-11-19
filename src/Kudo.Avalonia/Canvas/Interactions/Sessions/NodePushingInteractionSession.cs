using Avalonia;
using Avalonia.Input;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class NodePushingInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint)
    : GraphInteractionSession(canvas, pointer, startScreenPoint)
{
    private const double ActivationDistance = 10;
    private IReadOnlyDictionary<string, GraphPoint> _startPositions =
        new Dictionary<string, GraphPoint>(StringComparer.Ordinal);

    public GraphPoint StartGraphPoint { get; } = canvas.ScreenToGraph(startScreenPoint);

    public GraphNodePushAxis? Axis { get; private set; }

    public double Amount { get; private set; }

    public override void Update(Point point, KeyModifiers modifiers)
    {
        if (Canvas.Document is not { } document)
        {
            return;
        }

        var graphDelta = GetGraphDragDelta(point);
        if (Axis is null)
        {
            var screenDelta = point - StartScreenPoint;
            if (Math.Max(Math.Abs(screenDelta.X), Math.Abs(screenDelta.Y)) < ActivationDistance)
            {
                return;
            }

            Axis = Math.Abs(screenDelta.X) >= Math.Abs(screenDelta.Y)
                ? GraphNodePushAxis.Horizontal
                : GraphNodePushAxis.Vertical;
            var nodes = Canvas.GetNodePushCandidates(StartGraphPoint, Axis.Value);
            if (nodes.Count == 0 ||
                !Canvas.CanPerformEdit(new GraphEditRequest(
                    GraphEditOperation.MoveNodes,
                    document,
                    Nodes: nodes)))
            {
                Axis = null;
                return;
            }

            _startPositions = nodes.ToDictionary(
                node => node.Id,
                node => node.Position,
                StringComparer.Ordinal);
        }

        Amount = Axis == GraphNodePushAxis.Horizontal ? graphDelta.X : graphDelta.Y;
        var positions = GraphCanvasNodePushController.CreatePushedNodePositions(_startPositions, Axis.Value, Amount);
        if (positions.Values.Any(position => !GraphPointValidation.IsFinite(position)))
        {
            return;
        }

        foreach (var node in document.Nodes)
        {
            if (positions.TryGetValue(node.Id, out var position))
            {
                node.Position = position;
            }
        }

        Canvas.InvalidateNodeGeometry(_startPositions.Keys);
        Canvas.UpdateNodeControlHostLayout(_startPositions.Keys);
        Canvas.InvalidateVisual();
    }

    public override void Complete(Point point)
    {
        if (Canvas.Document is not { } document ||
            Axis is null ||
            _startPositions.Count == 0)
        {
            return;
        }

        var newPositions = document.Nodes
            .Where(node => _startPositions.ContainsKey(node.Id))
            .ToDictionary(node => node.Id, node => node.Position, StringComparer.Ordinal);
        if (_startPositions.All(pair =>
                newPositions.TryGetValue(pair.Key, out var position) && position == pair.Value))
        {
            return;
        }

        Canvas.CommitGraphEdit(new SetNodePositionsCommand(
            _startPositions,
            newPositions,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.PushNodesCommand)));
    }

    public override void CaptureLost() => Cancel();

    public override void Cancel()
    {
        if (Canvas.Document is not { } document)
        {
            return;
        }

        foreach (var node in document.Nodes)
        {
            if (_startPositions.TryGetValue(node.Id, out var position))
            {
                node.Position = position;
            }
        }

        Canvas.InvalidateNodeGeometry(_startPositions.Keys);
        Canvas.UpdateNodeControlHostLayout(_startPositions.Keys);
        Canvas.InvalidateVisual();
    }
}
