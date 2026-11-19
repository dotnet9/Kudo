// 分组命令：负责分组增删、折叠状态和属性变更。
namespace Kudo;

public sealed class AddGroupCommand(GroupModel group, string name = "Create node group") : GraphCommand(name)
{
    public GroupModel Group { get; } = group ?? throw new ArgumentNullException(nameof(group));

    public override void Execute(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(Group.Id))
        {
            throw new InvalidOperationException("A group ID is required.");
        }

        if (document.FindGroup(Group.Id) is not null)
        {
            throw new InvalidOperationException($"A group with ID '{Group.Id}' already exists.");
        }

        if (!double.IsFinite(Group.Padding) || Group.Padding < 0)
        {
            throw new InvalidOperationException("Group padding must be a finite non-negative number.");
        }

        var nodeIds = (Group.NodeIds ?? [])
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (nodeIds.Length < 2 || nodeIds.Length != (Group.NodeIds?.Count ?? 0))
        {
            throw new InvalidOperationException("A group requires at least two distinct node IDs.");
        }

        foreach (var nodeId in nodeIds)
        {
            if (document.FindNode(nodeId) is null)
            {
                throw new InvalidOperationException($"The node with ID '{nodeId}' does not exist.");
            }

            if (document.FindGroupContainingNode(nodeId) is { } existingGroup)
            {
                throw new InvalidOperationException(
                    $"The node with ID '{nodeId}' already belongs to group '{existingGroup.Id}'.");
            }
        }

        Group.NodeIds = [.. nodeIds];
        Group.Properties ??= [];
        document.AddGroup(Group);
    }

    public override void Undo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.RemoveGroup(Group.Id))
        {
            throw new InvalidOperationException($"The group with ID '{Group.Id}' no longer exists.");
        }
    }
}
public sealed class RemoveGroupCommand(string groupId, string name = "Ungroup nodes") : GraphCommand(name)
{
    private GroupModel? _group;
    private int _index;

    public string GroupId { get; } = string.IsNullOrWhiteSpace(groupId)
        ? throw new ArgumentException("A group ID is required.", nameof(groupId))
        : groupId;

    public GroupModel? RemovedGroup => _group;

    public override void Execute(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var index = document.Groups.FindIndex(group => group.Id == GroupId);
        if (index < 0)
        {
            throw new InvalidOperationException($"The group with ID '{GroupId}' does not exist.");
        }

        _index = index;
        _group = document.Groups[index];
        document.Groups.RemoveAt(index);
    }

    public override void Undo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_group is null)
        {
            throw new InvalidOperationException("The command has not been executed.");
        }

        if (document.FindGroup(GroupId) is not null)
        {
            throw new InvalidOperationException($"A group with ID '{GroupId}' already exists.");
        }

        document.Groups.Insert(Math.Min(_index, document.Groups.Count), _group);
    }
}

public sealed class SetGroupCollapsedCommand(
    string groupId,
    bool oldValue,
    bool newValue,
    string? name = null) : GraphCommand(name ?? (newValue ? "Collapse node group" : "Expand node group"))
{
    public string GroupId { get; } = string.IsNullOrWhiteSpace(groupId)
        ? throw new ArgumentException("A group ID is required.", nameof(groupId))
        : groupId;

    public bool OldValue { get; } = oldValue;

    public bool NewValue { get; } = newValue;

    public override void Execute(GraphDocument document) => SetCollapsed(document, NewValue);

    public override void Undo(GraphDocument document) => SetCollapsed(document, OldValue);

    private void SetCollapsed(GraphDocument document, bool value)
    {
        ArgumentNullException.ThrowIfNull(document);
        var group = document.FindGroup(GroupId) ??
            throw new InvalidOperationException($"The group with ID '{GroupId}' does not exist.");
        group.IsCollapsed = value;
    }
}

public sealed class SetGroupPropertiesCommand : GraphCommand
{
    private readonly Dictionary<string, object?> _oldProperties;
    private readonly Dictionary<string, object?> _newProperties;

    public SetGroupPropertiesCommand(
        string groupId,
        string? oldTitle,
        string? newTitle,
        double oldPadding,
        double newPadding,
        IReadOnlyDictionary<string, object?> oldProperties,
        IReadOnlyDictionary<string, object?> newProperties,
        string name = "Edit group properties")
        : base(name)
    {
        GroupId = string.IsNullOrWhiteSpace(groupId)
            ? throw new ArgumentException("A group ID is required.", nameof(groupId))
            : groupId;
        ValidatePadding(oldPadding, nameof(oldPadding));
        ValidatePadding(newPadding, nameof(newPadding));
        OldTitle = oldTitle;
        NewTitle = newTitle;
        OldPadding = oldPadding;
        NewPadding = newPadding;
        _oldProperties = CopyProperties(oldProperties, nameof(oldProperties));
        _newProperties = CopyProperties(newProperties, nameof(newProperties));
    }

    public string GroupId { get; }

    public string? OldTitle { get; }

    public string? NewTitle { get; }

    public double OldPadding { get; }

    public double NewPadding { get; }

    public IReadOnlyDictionary<string, object?> OldProperties => _oldProperties;

    public IReadOnlyDictionary<string, object?> NewProperties => _newProperties;

    public override void Execute(GraphDocument document) =>
        Apply(document, NewTitle, NewPadding, _newProperties);

    public override void Undo(GraphDocument document) =>
        Apply(document, OldTitle, OldPadding, _oldProperties);

    private void Apply(
        GraphDocument document,
        string? title,
        double padding,
        IReadOnlyDictionary<string, object?> properties)
    {
        ArgumentNullException.ThrowIfNull(document);
        var group = document.FindGroup(GroupId) ??
            throw new InvalidOperationException($"The group with ID '{GroupId}' does not exist.");
        group.Title = title;
        group.Padding = padding;
        group.Properties = CopyProperties(properties, nameof(properties));
    }

    private static void ValidatePadding(double padding, string parameterName)
    {
        if (!double.IsFinite(padding) || padding < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                padding,
                "Group padding must be a finite non-negative number.");
        }
    }

    private static Dictionary<string, object?> CopyProperties(
        IReadOnlyDictionary<string, object?> properties,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(properties, parameterName);
        return new Dictionary<string, object?>(properties, StringComparer.Ordinal);
    }
}
