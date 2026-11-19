using System.ComponentModel;
using Kudo.Avalonia;
using System.Text.Json;

namespace Kudo.Avalonia.Designer;

public enum GraphPropertyTargetKind
{
    [Description("无属性目标")]
    None,

    [Description("文档")]
    Document,

    [Description("单个节点")]
    Node,

    [Description("多个节点")]
    NodeSelection,

    [Description("连线")]
    Edge,

    [Description("标注")]
    Annotation,

    [Description("单个分组")]
    Group,

    [Description("多个分组")]
    GroupSelection
}

public sealed record GraphPropertyContext(
    GraphPropertyTargetKind Kind,
    GraphDocument? Document,
    IReadOnlyList<NodeModel> Nodes,
    EdgeModel? Edge)
{
    public static GraphPropertyContext Empty { get; } =
        new(GraphPropertyTargetKind.None, null, [], null);

    public NodeModel? Node => Kind == GraphPropertyTargetKind.Node ? Nodes.SingleOrDefault() : null;

    public IReadOnlyList<GroupModel> Groups { get; init; } = [];

    public GroupModel? Group => Kind == GraphPropertyTargetKind.Group
        ? Groups.SingleOrDefault()
        : null;

    public AnnotationModel? Annotation { get; init; }
}

public static class GraphPropertyContextResolver
{
    public static GraphPropertyContext Resolve(
        GraphDocument? document,
        IEnumerable<string>? selectedNodeIds = null,
        string? primaryNodeId = null,
        string? selectedEdgeId = null,
        string? selectedAnnotationId = null,
        IEnumerable<string>? selectedGroupIds = null)
    {
        if (document is null)
        {
            return GraphPropertyContext.Empty;
        }

        var selectedIds = (selectedNodeIds ?? [])
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(primaryNodeId))
        {
            selectedIds.Add(primaryNodeId);
        }

        var nodes = document.Nodes
            .Where(node => selectedIds.Contains(node.Id))
            .ToArray();
        var groupIds = (selectedGroupIds ?? [])
            .Where(groupId => !string.IsNullOrWhiteSpace(groupId))
            .ToHashSet(StringComparer.Ordinal);
        var groups = document.Groups
            .Where(group => groupIds.Contains(group.Id))
            .ToArray();
        if (groups.Length > 0)
        {
            var resolvedNodeIds = nodes
                .Select(node => node.Id)
                .ToHashSet(StringComparer.Ordinal);
            var groupedNodeIds = groups
                .SelectMany(group => group.NodeIds ?? [])
                .ToHashSet(StringComparer.Ordinal);
            if (resolvedNodeIds.Count == 0 || resolvedNodeIds.SetEquals(groupedNodeIds))
            {
                return new GraphPropertyContext(
                    groups.Length == 1
                        ? GraphPropertyTargetKind.Group
                        : GraphPropertyTargetKind.GroupSelection,
                    document,
                    nodes,
                    null)
                {
                    Groups = groups
                };
            }
        }

        if (nodes.Length > 1)
        {
            return new GraphPropertyContext(
                GraphPropertyTargetKind.NodeSelection,
                document,
                nodes,
                null);
        }

        if (nodes.Length == 1)
        {
            return new GraphPropertyContext(
                GraphPropertyTargetKind.Node,
                document,
                nodes,
                null);
        }

        var edge = string.IsNullOrWhiteSpace(selectedEdgeId)
            ? null
            : document.Edges.FirstOrDefault(candidate => candidate.Id == selectedEdgeId);
        if (edge is not null)
        {
            return new GraphPropertyContext(GraphPropertyTargetKind.Edge, document, [], edge);
        }

        var annotation = string.IsNullOrWhiteSpace(selectedAnnotationId)
            ? null
            : document.FindAnnotation(selectedAnnotationId);
        return annotation is null
            ? new GraphPropertyContext(GraphPropertyTargetKind.Document, document, [], null)
            : new GraphPropertyContext(GraphPropertyTargetKind.Annotation, document, [], null)
            {
                Annotation = annotation
            };
    }
}

public static class GraphPropertyCommandFactory
{
    public static IGraphCommand? CreateNodeLockCommand(NodeModel node, bool isLocked)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.IsLocked == isLocked
            ? null
            : new SetNodeLockedCommand(
                node.Id,
                node.IsLocked,
                isLocked,
                GraphLocalization.Get(isLocked
                    ? global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.LockNodesCommand
                    : global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.UnlockNodesCommand));
    }

    public static IGraphCommand? CreateNodePropertiesCommand(
        NodeModel node,
        string number,
        string? name,
        string? description,
        GraphPoint position) =>
        CreateNodePropertiesCommand(node, number, name, description, position, node.Rotation);

    public static IGraphCommand? CreateNodePropertiesCommand(
        NodeModel node,
        string number,
        string? name,
        string? description,
        GraphPoint position,
        double rotation)
    {
        ArgumentNullException.ThrowIfNull(node);
        var normalizedRotation = GraphNodeGeometry.NormalizeRotation(rotation);
        return number == node.Number &&
               name == node.Name &&
               description == node.Description &&
               position == node.Position &&
               normalizedRotation.Equals(GraphNodeGeometry.NormalizeRotation(node.Rotation))
            ? null
            : new SetNodePropertiesCommand(
                node.Id,
                node.Number,
                number,
                node.Name,
                name,
                node.Description,
                description,
                node.Position,
                position,
                node.Rotation,
                normalizedRotation,
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.EditNodePropertiesCommand));
    }

    public static IGraphCommand? CreateNodeAppearanceCommand(
        NodeModel node,
        GraphNodeAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(appearance);
        var normalized = appearance.Normalize();
        var current = (node.Appearance ?? new GraphNodeAppearance()).Normalize();
        return CreateNodeAppearanceCommand(node.Id, current, normalized);
    }

    public static IGraphCommand? CreateNodeAppearanceCommand(
        string nodeId,
        GraphNodeAppearance oldAppearance,
        GraphNodeAppearance newAppearance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(oldAppearance);
        ArgumentNullException.ThrowIfNull(newAppearance);
        var oldNormalized = oldAppearance.Normalize();
        var newNormalized = newAppearance.Normalize();
        return newNormalized == oldNormalized
            ? null
            : new SetNodeAppearanceCommand(
                nodeId,
                oldNormalized,
                newNormalized,
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.EditNodeAppearanceCommand));
    }

    public static IGraphCommand? CreateNodeExtensionPropertiesCommand(
        NodeModel node,
        IReadOnlyDictionary<string, object?> properties)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(properties);
        return CreateNodeExtensionPropertiesCommand(node.Id, node.Properties, properties);
    }

    public static IGraphCommand? CreateNodeExtensionPropertiesCommand(
        string nodeId,
        IReadOnlyDictionary<string, object?> oldProperties,
        IReadOnlyDictionary<string, object?> newProperties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(oldProperties);
        ArgumentNullException.ThrowIfNull(newProperties);
        return PropertiesEqual(oldProperties, newProperties)
            ? null
            : new SetNodeExtensionPropertiesCommand(
                nodeId,
                oldProperties,
                newProperties,
                GraphLocalization.Get(
                    global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.EditNodeExtensionPropertiesCommand));
    }

    public static IGraphCommand? CreateEdgePropertiesCommand(
        EdgeModel edge,
        string? label,
        EdgeRoutingStyle routingStyle,
        GraphEdgeStylePreset stylePreset,
        EdgeMarker sourceMarker,
        EdgeMarker targetMarker)
    {
        ArgumentNullException.ThrowIfNull(edge);
        var commandName = GraphLocalization.Get(
            global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.EditEdgePropertiesCommand);
        var commands = new List<IGraphCommand>(2);
        if (label != edge.Label ||
            routingStyle != edge.RoutingStyle ||
            sourceMarker != edge.SourceMarker ||
            targetMarker != edge.TargetMarker)
        {
            commands.Add(new SetEdgePropertiesCommand(
                edge.Id,
                edge.Label,
                label,
                edge.RoutingStyle,
                routingStyle,
                edge.SourceMarker,
                sourceMarker,
                edge.TargetMarker,
                targetMarker,
                commandName));
        }

        if (stylePreset != edge.StylePreset)
        {
            commands.Add(new SetEdgeStylePresetCommand(
                edge.Id,
                edge.StylePreset,
                edge.Appearance,
                stylePreset,
                commandName));
        }

        return commands.Count switch
        {
            0 => null,
            1 => commands[0],
            _ => new CompositeGraphCommand(commandName, commands)
        };
    }

    public static IGraphCommand? CreateAnnotationPropertiesCommand(
        AnnotationModel annotation,
        string? text,
        GraphPoint position,
        GraphSize size,
        IReadOnlyDictionary<string, object?> properties)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(properties);
        return text == annotation.Text &&
               position == annotation.Position &&
               size == annotation.Size &&
               PropertiesEqual(annotation.Properties, properties)
            ? null
            : new SetAnnotationPropertiesCommand(
                annotation.Id,
                annotation.Text,
                text,
                annotation.Position,
                position,
                annotation.Size,
                size,
                annotation.Properties,
                properties,
                GraphLocalization.Get(
                    global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.EditAnnotationPropertiesCommand));
    }

    public static IGraphCommand? CreateGroupPropertiesCommand(
        GroupModel group,
        string? title,
        double padding,
        bool isCollapsed,
        IReadOnlyDictionary<string, object?> properties)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(properties);
        var commandName = GraphLocalization.Get(
            global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.EditGroupPropertiesCommand);
        var commands = new List<IGraphCommand>(2);
        if (title != group.Title ||
            padding != group.Padding ||
            !PropertiesEqual(group.Properties, properties))
        {
            commands.Add(new SetGroupPropertiesCommand(
                group.Id,
                group.Title,
                title,
                group.Padding,
                padding,
                group.Properties,
                properties,
                commandName));
        }

        if (isCollapsed != group.IsCollapsed)
        {
            commands.Add(new SetGroupCollapsedCommand(
                group.Id,
                group.IsCollapsed,
                isCollapsed,
                commandName));
        }

        return commands.Count switch
        {
            0 => null,
            1 => commands[0],
            _ => new CompositeGraphCommand(commandName, commands)
        };
    }

    public static IGraphCommand? CreateDocumentTitleCommand(
        GraphDocument document,
        string title)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(title);
        return title == document.Title
            ? null
            : new SetDocumentTitleCommand(
                document.Id,
                document.Title,
                title,
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.EditCanvasPropertiesCommand));
    }

    public static IGraphCommand? CreateDocumentPropertiesCommand(
        GraphDocument document,
        string title,
        string createdBy,
        DateTime createdAt,
        string description)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(createdBy);
        ArgumentNullException.ThrowIfNull(description);
        return title == document.Title &&
               createdBy == document.CreatedBy &&
               createdAt == document.CreatedAt &&
               description == document.Description
            ? null
            : new SetDocumentPropertiesCommand(
                document.Id,
                document.Title,
                title,
                document.CreatedBy,
                createdBy,
                document.CreatedAt,
                createdAt,
                document.Description,
                description,
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.EditCanvasPropertiesCommand));
    }

    public static IGraphCommand? CreateCanvasSettingsCommand(
        GraphDocument document,
        GraphCanvasSettings settings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();
        var current = (document.CanvasSettings ?? GraphCanvasSettings.Default).Normalize();
        return normalized == current
            ? null
            : new SetCanvasSettingsCommand(
                document.Id,
                current,
                normalized,
                GraphLocalization.Get(
                    global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.EditCanvasSettingsCommand));
    }

    private static bool PropertiesEqual(
        IReadOnlyDictionary<string, object?> first,
        IReadOnlyDictionary<string, object?> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        foreach (var pair in first)
        {
            if (!second.TryGetValue(pair.Key, out var value) ||
                !PropertyValuesEqual(pair.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PropertyValuesEqual(object? first, object? second)
    {
        if (Equals(first, second))
        {
            return true;
        }

        try
        {
            return JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return false;
        }
    }
}
