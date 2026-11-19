using Avalonia;
using Avalonia.Input;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class NodeDragInteractionSession(
    GraphCanvas canvas,
    IPointer pointer,
    Point startScreenPoint,
    string primaryNodeId,
    IReadOnlyDictionary<string, GraphPoint> startPositions)
    : GraphInteractionSession(canvas, pointer, startScreenPoint)
{
    public override void Update(Point point, KeyModifiers modifiers)
    {
        if (Canvas.Document is not { } document)
        {
            return;
        }

        var delta = GetGraphDragDelta(point);
        var selectedNodes = document.Nodes
            .Where(node => startPositions.ContainsKey(node.Id) && !node.IsLocked)
            .ToArray();
        var referenceNodes = document.Nodes
            .Where(node => !startPositions.ContainsKey(node.Id) && Canvas.IsNodeVisible(node.Id))
            .ToArray();
        var effectivePrimaryNodeId = primaryNodeId ?? selectedNodes.FirstOrDefault()?.Id;
        if (effectivePrimaryNodeId is null)
        {
            return;
        }

        var snapResult = GraphNodeSnapper.Snap(
            selectedNodes,
            referenceNodes,
            startPositions,
            effectivePrimaryNodeId,
            delta,
            Canvas.GridSettings,
            Canvas.Viewport.Zoom);
        foreach (var node in document.Nodes)
        {
            if (snapResult.NodePositions.TryGetValue(node.Id, out var position))
            {
                node.Position = position;
            }
        }

        Canvas.ActiveAlignmentGuides = snapResult.AlignmentGuides;
        Canvas.InvalidateNodeGeometry(startPositions.Keys);
        Canvas.UpdateNodeControlHostLayout(startPositions.Keys);
        Canvas.InvalidateVisual();
    }

    public override void Complete(Point point)
    {
        if (Canvas.Document is not { } document)
        {
            return;
        }

        var newPositions = document.Nodes
            .Where(node => startPositions.ContainsKey(node.Id))
            .ToDictionary(node => node.Id, node => node.Position, StringComparer.Ordinal);
        if (startPositions.All(pair =>
                newPositions.TryGetValue(pair.Key, out var position) && position == pair.Value))
        {
            return;
        }

        Canvas.CommitGraphEdit(new SetNodePositionsCommand(
            startPositions,
            newPositions,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.MoveSelectedNodesCommand)));
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
            if (startPositions.TryGetValue(node.Id, out var position))
            {
                node.Position = position;
            }
        }

        Canvas.InvalidateNodeGeometry(startPositions.Keys);
        Canvas.UpdateNodeControlHostLayout(startPositions.Keys);
    }
}
