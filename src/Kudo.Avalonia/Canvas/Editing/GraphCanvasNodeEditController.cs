using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasNodeEditController(GraphCanvasEditContext context)
{
    public bool ResizeNode(string nodeId, GraphPoint position, GraphSize size)
    {
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "The node position must use finite coordinates.");
        }

        if (!double.IsFinite(size.Width) || size.Width <= 0 ||
            !double.IsFinite(size.Height) || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "The node size must contain finite positive values.");
        }

        if (context.IsReadOnly())
        {
            return false;
        }

        var document = context.Document();
        var node = document?.FindNode(nodeId);
        if (document is null || node is null || node.IsLocked)
        {
            return false;
        }

        if (!context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.ResizeNode,
                document,
                Nodes: [node])))
        {
            return false;
        }

        var requestedSize = context.ClampNodeSize(node, size);
        if (node.Position == position &&
            node.Size == requestedSize)
        {
            return false;
        }

        var command = new ResizeNodeCommand(
            node.Id,
            node.Position,
            node.Size,
            position,
            requestedSize,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.ResizeNodeCommand));
        command.Execute(document);
        if (!context.CommitGraphEdit(command))
        {
            return false;
        }

        context.SetSelectedNodes([node.Id], node.Id);
        return true;
    }

    public bool RotateNode(string nodeId, double rotation)
    {
        if (!double.IsFinite(rotation))
        {
            throw new ArgumentOutOfRangeException(nameof(rotation), "The node rotation must be finite.");
        }

        var document = context.Document();
        if (context.IsReadOnly() || document?.FindNode(nodeId) is not { } node || node.IsLocked)
        {
            return false;
        }

        var normalizedRotation = GraphNodeGeometry.NormalizeRotation(rotation);
        if (GraphNodeGeometry.NormalizeRotation(node.Rotation).Equals(normalizedRotation) ||
            !context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.EditNodeProperties,
                document,
                Nodes: [node])))
        {
            return false;
        }

        var command = new SetNodePropertiesCommand(
            node.Id,
            node.Number,
            node.Number,
            node.Name,
            node.Name,
            node.Description,
            node.Description,
            node.Position,
            node.Position,
            node.Rotation,
            normalizedRotation,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.RotateNodeCommand));
        command.Execute(document);
        if (!context.CommitGraphEdit(command))
        {
            return false;
        }

        context.SetSelectedNodes([node.Id], node.Id);
        return true;
    }

    public bool SetSelectedNodesLocked(bool value)
    {
        var document = context.Document();
        if (context.IsReadOnly() || document is null)
        {
            return false;
        }

        var nodes = document.Nodes
            .Where(node => context.SelectedNodeIds().Contains(node.Id) && node.IsLocked != value)
            .ToArray();
        if (nodes.Length == 0)
        {
            return false;
        }

        var commands = nodes
            .Select(node => (IGraphCommand)new SetNodeLockedCommand(
                node.Id,
                node.IsLocked,
                value,
                GraphLocalization.Get(value
                    ? global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.LockNodesCommand
                    : global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.UnlockNodesCommand)))
            .ToArray();
        var command = commands.Length == 1
            ? commands[0]
            : new CompositeGraphCommand(
                GraphLocalization.Get(value
                    ? global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.LockNodesCommand
                    : global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.UnlockNodesCommand),
                commands);
        command.Execute(document);
        return context.CommitGraphEdit(command);
    }

    public bool ToggleSelectedNodesLocked()
    {
        var document = context.Document();
        if (document is null)
        {
            return false;
        }

        var nodes = document.Nodes
            .Where(node => context.SelectedNodeIds().Contains(node.Id))
            .ToArray();
        return nodes.Length > 0 && SetSelectedNodesLocked(nodes.Any(node => !node.IsLocked));
    }

    public bool NudgeSelectedNodes(GraphPoint offset)
    {
        var document = context.Document();
        if (context.IsReadOnly() ||
            document is null ||
            context.IsPointerInteracting() ||
            !double.IsFinite(offset.X) ||
            !double.IsFinite(offset.Y) ||
            offset == GraphPoint.Zero)
        {
            return false;
        }

        var oldPositions = document.Nodes
            .Where(node => context.SelectedNodeIds().Contains(node.Id) && !node.IsLocked)
            .ToDictionary(node => node.Id, node => node.Position, StringComparer.Ordinal);
        if (oldPositions.Count == 0)
        {
            return false;
        }

        var selectedNodes = document.Nodes
            .Where(node => oldPositions.ContainsKey(node.Id) && !node.IsLocked)
            .ToArray();
        if (!context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.MoveNodes,
                document,
                Nodes: selectedNodes)))
        {
            return false;
        }

        var newPositions = oldPositions.ToDictionary(
            pair => pair.Key,
            pair => new GraphPoint(pair.Value.X + offset.X, pair.Value.Y + offset.Y),
            StringComparer.Ordinal);
        if (newPositions.Values.Any(position =>
                !double.IsFinite(position.X) || !double.IsFinite(position.Y)))
        {
            return false;
        }

        var command = new SetNodePositionsCommand(
            oldPositions,
            newPositions,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.NudgeSelectedNodesCommand));
        command.Execute(document);
        return context.CommitGraphEdit(command);
    }

    public bool DeleteSelectedNode()
    {
        var document = context.Document();
        if (context.IsReadOnly() || document is null || context.SelectedNodeIds().Count == 0)
        {
            return false;
        }

        var selectedNodeIds = document.Nodes
            .Where(node => context.SelectedNodeIds().Contains(node.Id) && !node.IsLocked)
            .Select(node => node.Id)
            .ToArray();
        if (selectedNodeIds.Length == 0)
        {
            return false;
        }

        var selectedNodes = document.Nodes
            .Where(node => selectedNodeIds.Contains(node.Id, StringComparer.Ordinal))
            .ToArray();
        if (!context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.DeleteNodes,
                document,
                Nodes: selectedNodes)))
        {
            return false;
        }

        var commands = selectedNodeIds
            .Select(nodeId => (IGraphCommand)new RemoveNodeCommand(
                nodeId,
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.RemoveNodeCommand)))
            .ToArray();
        var command = commands.Length == 1
            ? commands[0]
            : new CompositeGraphCommand(
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.RemoveSelectedNodesCommand),
                commands);
        command.Execute(document);
        if (!context.CommitGraphEdit(command))
        {
            return false;
        }

        var remainingSelectedNodeIds = context.SelectedNodeIds()
            .Where(nodeId => !selectedNodeIds.Contains(nodeId, StringComparer.Ordinal))
            .Where(nodeId => document.FindNode(nodeId) is not null)
            .ToArray();
        context.SetSelectedNodes(remainingSelectedNodeIds, remainingSelectedNodeIds.FirstOrDefault());
        return true;
    }

    public bool CanDeleteSelectedNodes()
    {
        var document = context.Document();
        if (context.IsReadOnly() || document is null)
        {
            return false;
        }

        var nodes = document.Nodes
            .Where(node => context.SelectedNodeIds().Contains(node.Id) && !node.IsLocked)
            .ToArray();
        return nodes.Length > 0 && context.CanAuthorizeEdit(new GraphEditRequest(
            GraphEditOperation.DeleteNodes,
            document,
            Nodes: nodes));
    }
}
