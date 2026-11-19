using Kudo;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasGroupController(GraphCanvasGroupContext context)
{
    public bool CanGroupSelectedNodes()
    {
        if (context.IsReadOnly())
        {
            return false;
        }

        var document = context.Document();
        if (document is null)
        {
            return false;
        }

        var selectedNodeIds = context.GetDocumentOrderedNodeIds(context.SelectedNodeIds());
        if (selectedNodeIds.Count < 2 ||
            selectedNodeIds.Any(nodeId => document.FindGroupContainingNode(nodeId) is not null))
        {
            return false;
        }

        var nodes = document.Nodes
            .Where(node => selectedNodeIds.Contains(node.Id, StringComparer.Ordinal))
            .ToArray();
        return context.CanAuthorizeEdit(new GraphEditRequest(
            GraphEditOperation.CreateGroup,
            document,
            Nodes: nodes));
    }

    public bool CanUngroupSelectedNodes()
    {
        if (context.IsReadOnly())
        {
            return false;
        }

        var document = context.Document();
        if (document is null)
        {
            return false;
        }

        var groups = document.Groups.Where(group => context.IsGroupSelected(group.Id)).ToArray();
        return groups.Length > 0 && groups.All(group => context.CanAuthorizeEdit(new GraphEditRequest(
            GraphEditOperation.RemoveGroups,
            document,
            Group: group)));
    }

    public bool GroupSelectedNodes(string? title = null)
    {
        var document = context.Document();
        if (document is null || context.IsReadOnly())
        {
            return false;
        }

        var nodeIds = context.GetDocumentOrderedNodeIds(context.SelectedNodeIds());
        if (nodeIds.Count < 2 ||
            nodeIds.Any(nodeId => document.FindGroupContainingNode(nodeId) is not null))
        {
            return false;
        }

        var nodes = document.Nodes
            .Where(node => nodeIds.Contains(node.Id, StringComparer.Ordinal))
            .ToArray();
        if (!context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.CreateGroup,
                document,
                Nodes: nodes)))
        {
            return false;
        }

        var group = new GroupModel
        {
            Title = string.IsNullOrWhiteSpace(title)
                ? GraphLocalization.Format(
                    global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.DefaultGroupTitle,
                    document.Groups.Count + 1)
                : title.Trim(),
            NodeIds = [.. nodeIds]
        };
        var command = new AddGroupCommand(
            group,
            GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.AddGroupCommand));
        command.Execute(document);
        return context.CommitGraphEdit(command);
    }

    public bool UngroupSelectedNodes()
    {
        if (context.IsReadOnly())
        {
            return false;
        }

        var document = context.Document();
        if (document is null)
        {
            return false;
        }

        var groupIds = document.Groups
            .Where(group => context.IsGroupSelected(group.Id))
            .Select(group => group.Id)
            .ToArray();
        if (groupIds.Length == 0)
        {
            return false;
        }

        var groups = document.Groups
            .Where(group => groupIds.Contains(group.Id, StringComparer.Ordinal))
            .ToArray();
        foreach (var group in groups)
        {
            if (!context.CanPerformEdit(new GraphEditRequest(
                    GraphEditOperation.RemoveGroups,
                    document,
                    Group: group)))
            {
                return false;
            }
        }

        var commands = groupIds
            .Select(groupId => (IGraphCommand)new RemoveGroupCommand(
                groupId,
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.RemoveGroupCommand)))
            .ToArray();
        var command = commands.Length == 1
            ? commands[0]
            : new CompositeGraphCommand(
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.RemoveSelectedGroupsCommand),
                commands);
        command.Execute(document);
        return context.CommitGraphEdit(command);
    }

    public bool CanToggleSelectedGroupCollapsed()
    {
        if (context.IsReadOnly() ||
            context.Document() is not { } document ||
            context.GetSelectedGroups() is not [var group])
        {
            return false;
        }

        return context.CanAuthorizeEdit(new GraphEditRequest(
            GraphEditOperation.SetGroupCollapsed,
            document,
            Group: group));
    }

    public bool ToggleSelectedGroupCollapsed()
    {
        var group = context.GetSelectedGroups().SingleOrDefault();
        return group is not null && SetGroupCollapsed(group.Id, !group.IsCollapsed);
    }

    public bool SetGroupCollapsed(string groupId, bool isCollapsed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        if (context.IsReadOnly())
        {
            return false;
        }

        var document = context.Document();
        var group = document?.FindGroup(groupId);
        if (document is null || group is null || group.IsCollapsed == isCollapsed)
        {
            return false;
        }

        if (!context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.SetGroupCollapsed,
                document,
                Group: group)))
        {
            return false;
        }

        var command = new SetGroupCollapsedCommand(
            group.Id,
            group.IsCollapsed,
            isCollapsed,
            GraphLocalization.Get(isCollapsed
                ? global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.CollapseGroupCommand
                : global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.ExpandGroupCommand));
        command.Execute(document);
        if (!context.CommitGraphEdit(command))
        {
            return false;
        }

        context.SetSelectedNodes(group.NodeIds, group.NodeIds.FirstOrDefault());
        return true;
    }
}

internal sealed class GraphCanvasGroupContext
{
    public required Func<GraphDocument?> Document { get; init; }

    public required Func<bool> IsReadOnly { get; init; }

    public required Func<HashSet<string>> SelectedNodeIds { get; init; }

    public required Func<IEnumerable<string>, IReadOnlyList<string>> GetDocumentOrderedNodeIds { get; init; }

    public required Func<string, bool> IsGroupSelected { get; init; }

    public required Func<IReadOnlyList<GroupModel>> GetSelectedGroups { get; init; }

    public required Func<GraphEditRequest, bool> CanAuthorizeEdit { get; init; }

    public required Func<GraphEditRequest, bool> CanPerformEdit { get; init; }

    public required Action<IEnumerable<string>, string?> SetSelectedNodes { get; init; }

    public required Func<IGraphCommand, bool> CommitGraphEdit { get; init; }
}
