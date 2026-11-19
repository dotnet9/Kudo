using System.Collections;
using System.Text.Json;

namespace Kudo;

/// <summary>
/// 节点扩展状态投影出的 SDK 结构。业务状态是唯一事实来源，尺寸和端口是可重建结果。
/// </summary>
public sealed record GraphNodeProjection(
    GraphSize Size,
    IReadOnlyList<PortModel> Ports)
{
    public GraphNodeProjection DeepClone() => new(
        Size,
        GraphModelSnapshot.ClonePorts(Ports));
}

/// <summary>
/// 将业务扩展状态转换为 Kudo 能参与布局、连线和持久化的节点结构。
/// </summary>
public interface IGraphNodeStateProjectionProvider
{
    GraphNodeProjection Project(NodeModel node, GraphExtensionState state);
}

public interface IGraphNodeStateProjectionResolver
{
    bool TryProject(
        NodeModel node,
        GraphExtensionState state,
        out GraphNodeProjection? projection);
}

public static class GraphNodeProjectionValidator
{
    public static void Validate(GraphNodeProjection projection, string parameterName = "projection")
    {
        ArgumentNullException.ThrowIfNull(projection, parameterName);
        if (!double.IsFinite(projection.Size.Width) || projection.Size.Width <= 0 ||
            !double.IsFinite(projection.Size.Height) || projection.Size.Height <= 0)
        {
            throw new ArgumentException(
                "The projected size must contain finite positive values.",
                parameterName);
        }

        if (projection.Ports is null || projection.Ports.Any(port => port is null))
        {
            throw new ArgumentException(
                "The projected port collection cannot contain null values.",
                parameterName);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var port in projection.Ports)
        {
            if (string.IsNullOrWhiteSpace(port.Id))
            {
                throw new ArgumentException("Every projected port requires an ID.", parameterName);
            }

            if (!ids.Add(port.Id))
            {
                throw new ArgumentException(
                    $"Projected port ID '{port.Id}' is duplicated.",
                    parameterName);
            }

            if (!Enum.IsDefined(port.Kind) || !Enum.IsDefined(port.State))
            {
                throw new ArgumentException(
                    $"Projected port '{port.Id}' contains an unsupported enum value.",
                    parameterName);
            }

            if (port.Anchor is { } anchor &&
                (!double.IsFinite(anchor.X) || !double.IsFinite(anchor.Y)))
            {
                throw new ArgumentException(
                    $"Projected port '{port.Id}' contains a non-finite anchor.",
                    parameterName);
            }
        }
    }
}

internal static class GraphModelSnapshot
{
    public static GraphDocument CloneDocument(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new GraphDocument
        {
            Format = document.Format,
            Id = document.Id,
            SchemaVersion = document.SchemaVersion,
            Title = document.Title,
            CreatedBy = document.CreatedBy,
            CreatedAt = document.CreatedAt,
            Description = document.Description,
            CanvasSettings = CloneCanvasSettings(document.CanvasSettings),
            Nodes = (document.Nodes ?? []).Select(CloneNode).ToList(),
            Groups = (document.Groups ?? []).Select(CloneGroup).ToList(),
            Edges = (document.Edges ?? []).Select(CloneEdge).ToList(),
            Annotations = (document.Annotations ?? []).Select(CloneAnnotation).ToList(),
            Properties = CloneProperties(document.Properties)
        };
    }

    public static GraphCanvasSettings CloneCanvasSettings(GraphCanvasSettings? settings)
    {
        settings ??= GraphCanvasSettings.Default;
        return settings with
        {
            Grid = (settings.Grid ?? GraphGridSettings.Default) with { }
        };
    }

    public static NodeModel CloneNode(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new NodeModel
        {
            Id = node.Id,
            Type = node.Type,
            DefinitionId = node.DefinitionId,
            Number = node.Number,
            Name = node.Name,
            Description = node.Description,
            Position = node.Position,
            Size = node.Size,
            Rotation = node.Rotation,
            IsLocked = node.IsLocked,
            Appearance = CloneAppearance(node.Appearance),
            ExtensionState = node.ExtensionState?.DeepClone(),
            Ports = (node.Ports ?? []).Select(ClonePort).ToList(),
            Properties = CloneProperties(node.Properties)
        };
    }

    public static GroupModel CloneGroup(GroupModel group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return new GroupModel
        {
            Id = group.Id,
            Title = group.Title,
            NodeIds = (group.NodeIds ?? []).ToList(),
            Padding = group.Padding,
            IsCollapsed = group.IsCollapsed,
            Properties = CloneProperties(group.Properties)
        };
    }

    public static EdgeModel CloneEdge(EdgeModel edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        return new EdgeModel
        {
            Id = edge.Id,
            Source = edge.Source,
            Target = edge.Target,
            RelationKind = edge.RelationKind,
            RoutingStyle = edge.RoutingStyle,
            StylePreset = edge.StylePreset,
            Appearance = CloneEdgeAppearance(edge.Appearance),
            Waypoints = (edge.Waypoints ?? []).ToList(),
            SourceMarker = edge.SourceMarker,
            TargetMarker = edge.TargetMarker,
            Label = edge.Label,
            State = edge.State,
            Properties = CloneProperties(edge.Properties)
        };
    }

    public static AnnotationModel CloneAnnotation(AnnotationModel annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return new AnnotationModel
        {
            Id = annotation.Id,
            Text = annotation.Text,
            Position = annotation.Position,
            Size = annotation.Size,
            Properties = CloneProperties(annotation.Properties)
        };
    }

    public static IReadOnlyList<PortModel> ClonePorts(IEnumerable<PortModel> ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        return ports.Select(ClonePort).ToArray();
    }

    public static PortModel ClonePort(PortModel port)
    {
        ArgumentNullException.ThrowIfNull(port);
        return new PortModel
        {
            Id = port.Id,
            Name = port.Name,
            Kind = port.Kind,
            IsVisible = port.IsVisible,
            Anchor = port.Anchor,
            IncomingDirection = port.IncomingDirection,
            OutgoingDirection = port.OutgoingDirection,
            DataType = port.DataType,
            Capacity = (port.Capacity ?? new PortCapacity()) with { },
            State = port.State,
            Properties = CloneProperties(port.Properties)
        };
    }

    public static GraphNodeAppearance CloneAppearance(GraphNodeAppearance? appearance)
    {
        appearance ??= new GraphNodeAppearance();
        return appearance with
        {
            TextAppearance = (appearance.TextAppearance ?? new GraphTextAppearance()) with { }
        };
    }

    public static GraphEdgeAppearance CloneEdgeAppearance(GraphEdgeAppearance? appearance)
    {
        appearance ??= new GraphEdgeAppearance();
        return appearance with
        {
            CustomDashPattern = (appearance.CustomDashPattern ?? []).ToArray()
        };
    }

    public static Dictionary<string, object?> CloneProperties(
        IReadOnlyDictionary<string, object?>? properties) => properties is null
        ? new Dictionary<string, object?>(StringComparer.Ordinal)
        : properties.ToDictionary(
            pair => pair.Key,
            pair => CloneValue(pair.Value),
            StringComparer.Ordinal);

    public static object? CloneValue(object? value) => value switch
    {
        null => null,
        JsonElement element => element.Clone(),
        IReadOnlyDictionary<string, object?> dictionary => CloneProperties(dictionary),
        IDictionary dictionary => CloneDictionary(dictionary),
        IEnumerable values when value is not string => values
            .Cast<object?>()
            .Select(CloneValue)
            .ToList(),
        _ => value
    };

    private static Dictionary<string, object?> CloneDictionary(IDictionary dictionary)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not null)
            {
                result[entry.Key.ToString() ?? string.Empty] = CloneValue(entry.Value);
            }
        }

        return result;
    }
}
