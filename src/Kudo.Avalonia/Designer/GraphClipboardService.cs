using System.ComponentModel;
using Kudo.Core.Serialization;

namespace Kudo.Avalonia.Designer;

public enum GraphClipboardElementKind
{
    [Description("节点")]
    Node,

    [Description("连线")]
    Edge,

    [Description("标注")]
    Annotation
}

public sealed record GraphClipboardContent(
    string Text,
    GraphClipboardElementKind Kind,
    string ElementId)
{
    public IReadOnlyList<string> NodeIds { get; init; } =
        Kind == GraphClipboardElementKind.Node ? [ElementId] : [];

    public IReadOnlyList<string> EdgeIds { get; init; } =
        Kind == GraphClipboardElementKind.Edge ? [ElementId] : [];

    public IReadOnlyList<string> AnnotationIds { get; init; } =
        Kind == GraphClipboardElementKind.Annotation ? [ElementId] : [];

    public IReadOnlyList<string> AssetPaths { get; init; } = [];

    /// <summary>
    /// Gets the source task directory used to resolve <see cref="AssetPaths"/>.
    /// This value is process-local metadata and is not written to the system clipboard.
    /// </summary>
    public string? SourceWorkingDirectory { get; init; }
}

public sealed record GraphPastePlan(
    IGraphCommand Command,
    string? SelectedNodeId,
    string? SelectedEdgeId,
    int NodeCount,
    int EdgeCount,
    int GroupCount)
{
    public IReadOnlyList<string> SelectedNodeIds { get; init; } =
        SelectedNodeId is null ? [] : [SelectedNodeId];

    public IReadOnlyList<string> SelectedEdgeIds { get; init; } =
        SelectedEdgeId is null ? [] : [SelectedEdgeId];

    public string? SelectedAnnotationId { get; init; }

    public IReadOnlyList<string> SelectedAnnotationIds { get; init; } = [];

    public int AnnotationCount { get; init; }
}

public sealed class GraphClipboardService
{
    private readonly GraphClipboardJsonSerializer _serializer = new();

    public GraphClipboardContent? SerializeSubgraphSelection(
        GraphDocument document,
        IEnumerable<string> selectedNodeIds,
        IEnumerable<string> selectedEdgeIds,
        IEnumerable<string>? selectedAnnotationIds = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(selectedNodeIds);
        ArgumentNullException.ThrowIfNull(selectedEdgeIds);
        var selectedIds = selectedNodeIds
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .ToHashSet(StringComparer.Ordinal);
        var nodes = document.Nodes
            .Where(node => selectedIds.Contains(node.Id))
            .ToArray();
        var selectedAnnotationIdSet = (selectedAnnotationIds ?? [])
            .Where(annotationId => !string.IsNullOrWhiteSpace(annotationId))
            .ToHashSet(StringComparer.Ordinal);
        var annotations = document.Annotations
            .Where(annotation => selectedAnnotationIdSet.Contains(annotation.Id))
            .ToArray();
        if (nodes.Length == 0 && annotations.Length == 0)
        {
            var selectedEdgeIdSet = selectedEdgeIds
                .Where(edgeId => !string.IsNullOrWhiteSpace(edgeId))
                .ToHashSet(StringComparer.Ordinal);
            var selectedEdges = document.Edges
                .Where(edge => selectedEdgeIdSet.Contains(edge.Id))
                .ToArray();
            if (selectedEdges.Length == 0)
            {
                return null;
            }

            var edgeText = _serializer.Serialize(new GraphClipboardPayload
            {
                Edges = [.. selectedEdges]
            });
            return new GraphClipboardContent(
                edgeText,
                GraphClipboardElementKind.Edge,
                selectedEdges[0].Id)
            {
                EdgeIds = selectedEdges.Select(edge => edge.Id).ToArray()
            };
        }

        selectedIds.IntersectWith(nodes.Select(node => node.Id));
        var edges = document.Edges
            .Where(edge =>
                selectedIds.Contains(edge.Source.NodeId) &&
                selectedIds.Contains(edge.Target.NodeId))
            .ToArray();
        var groups = document.Groups
            .Where(group =>
                group.NodeIds is { Count: > 0 } &&
                group.NodeIds.All(selectedIds.Contains))
            .ToArray();
        var text = _serializer.Serialize(new GraphClipboardPayload
        {
            Nodes = [.. nodes],
            Edges = [.. edges],
            Groups = [.. groups],
            Annotations = [.. annotations]
        });
        var kind = nodes.Length > 0
            ? GraphClipboardElementKind.Node
            : GraphClipboardElementKind.Annotation;
        var elementId = nodes.FirstOrDefault()?.Id ?? annotations[0].Id;
        return new GraphClipboardContent(text, kind, elementId)
        {
            NodeIds = nodes.Select(node => node.Id).ToArray(),
            EdgeIds = edges.Select(edge => edge.Id).ToArray(),
            AnnotationIds = annotations.Select(annotation => annotation.Id).ToArray(),
            AssetPaths = GetAssetPaths(nodes)
        };
    }

    public bool TryCreatePastePlan(
        GraphDocument document,
        string? text,
        GraphPoint offset,
        out GraphPastePlan? plan,
        string? commandName = null,
        IConnectionValidationRule? connectionValidationRule = null,
        IGraphValidationTextProvider? validationTextProvider = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        commandName = string.IsNullOrWhiteSpace(commandName)
            ? GraphLocalization.Get(
                global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.PasteSelectionCommand)
            : commandName;
        plan = null;
        if (!_serializer.TryDeserialize(text, out var payload) || payload is null)
        {
            return false;
        }

        var nodes = new List<NodeModel>(payload.Nodes.Count);
        var nodeIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var portIds = new Dictionary<(string NodeId, string PortId), string>();
        var validationDocument = new GraphDocument
        {
            Nodes = [.. document.Nodes],
            Edges = [.. document.Edges],
            Groups = [.. document.Groups]
        };
        foreach (var node in payload.Nodes)
        {
            if (node is null ||
                string.IsNullOrWhiteSpace(node.Id) ||
                node.Ports is null ||
                !nodeIds.TryAdd(node.Id, GraphId.NewId()))
            {
                return false;
            }

            var oldNodeId = node.Id;
            node.Id = nodeIds[oldNodeId];
            node.Type = string.IsNullOrWhiteSpace(node.Type) ? "node" : node.Type;
            node.Number = GraphNodeNumberAllocator.Next(validationDocument);
            node.Position = new GraphPoint(node.Position.X + offset.X, node.Position.Y + offset.Y);
            node.Properties ??= [];
            foreach (var port in node.Ports)
            {
                if (port is null || string.IsNullOrWhiteSpace(port.Id))
                {
                    return false;
                }

                var oldPortId = port.Id;
                var newPortId = GraphId.NewId();
                if (!portIds.TryAdd((oldNodeId, oldPortId), newPortId))
                {
                    return false;
                }

                port.Id = newPortId;
                port.Name = port.Name?.Trim() ?? string.Empty;
                port.Properties ??= [];
            }

            nodes.Add(node);
            validationDocument.Nodes.Add(node);
        }

        var edges = new List<EdgeModel>(payload.Edges.Count);
        foreach (var edge in payload.Edges)
        {
            if (edge is null || edge.Source is null || edge.Target is null)
            {
                return false;
            }

            var source = RemapReference(edge.Source, nodeIds, portIds);
            var target = RemapReference(edge.Target, nodeIds, portIds);
            if (source is null ||
                target is null ||
                validationDocument.FindPort(source) is null ||
                validationDocument.FindPort(target) is null ||
                !validationDocument.ValidateConnection(
                    source,
                    target,
                    edge.RelationKind,
                    connectionValidationRule,
                    textProvider: validationTextProvider).IsValid)
            {
                continue;
            }

            edge.Waypoints ??= [];
            if (edge.Waypoints.Any(point =>
                    !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            {
                return false;
            }

            if (nodeIds.ContainsKey(edge.Source.NodeId) && nodeIds.ContainsKey(edge.Target.NodeId))
            {
                edge.Waypoints = edge.Waypoints
                    .Select(point => new GraphPoint(point.X + offset.X, point.Y + offset.Y))
                    .ToList();
            }

            edge.Id = GraphId.NewId();
            edge.Source = source;
            edge.Target = target;
            edge.Properties ??= [];
            edges.Add(edge);
            validationDocument.Edges.Add(edge);
        }

        var groups = new List<GroupModel>(payload.Groups.Count);
        foreach (var group in payload.Groups)
        {
            if (group is null ||
                string.IsNullOrWhiteSpace(group.Id) ||
                group.NodeIds is null ||
                !double.IsFinite(group.Padding) ||
                group.Padding < 0)
            {
                return false;
            }

            var remappedNodeIds = new List<string>(group.NodeIds.Count);
            foreach (var oldNodeId in group.NodeIds)
            {
                if (string.IsNullOrWhiteSpace(oldNodeId) ||
                    !nodeIds.TryGetValue(oldNodeId, out var newNodeId) ||
                    remappedNodeIds.Contains(newNodeId, StringComparer.Ordinal))
                {
                    return false;
                }

                remappedNodeIds.Add(newNodeId);
            }

            if (remappedNodeIds.Count < 2 ||
                remappedNodeIds.Any(nodeId => validationDocument.FindGroupContainingNode(nodeId) is not null))
            {
                return false;
            }

            group.Id = GraphId.NewId();
            group.NodeIds = remappedNodeIds;
            group.Properties ??= [];
            groups.Add(group);
            validationDocument.Groups.Add(group);
        }

        var annotations = new List<AnnotationModel>(payload.Annotations.Count);
        var annotationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var annotation in payload.Annotations)
        {
            if (annotation is null ||
                string.IsNullOrWhiteSpace(annotation.Id) ||
                !annotationIds.Add(annotation.Id) ||
                !double.IsFinite(annotation.Position.X) ||
                !double.IsFinite(annotation.Position.Y) ||
                !double.IsFinite(annotation.Size.Width) ||
                !double.IsFinite(annotation.Size.Height) ||
                annotation.Size.Width <= 0 ||
                annotation.Size.Height <= 0)
            {
                return false;
            }

            annotation.Id = GraphId.NewId();
            annotation.Position = new GraphPoint(
                annotation.Position.X + offset.X,
                annotation.Position.Y + offset.Y);
            annotation.Properties ??= [];
            annotations.Add(annotation);
        }

        if (nodes.Count == 0 &&
            edges.Count == 0 &&
            groups.Count == 0 &&
            annotations.Count == 0)
        {
            return false;
        }

        var commands = new List<IGraphCommand>(
            nodes.Count + edges.Count + groups.Count + annotations.Count);
        commands.AddRange(nodes.Select(node => new AddNodeCommand(node, commandName)));
        commands.AddRange(edges.Select(edge => new AddEdgeCommand(edge, commandName)));
        commands.AddRange(groups.Select(group => new AddGroupCommand(group, commandName)));
        commands.AddRange(annotations.Select(annotation =>
            new AddAnnotationCommand(annotation, commandName)));
        plan = new GraphPastePlan(
            new CompositeGraphCommand(commandName, commands),
            nodes.FirstOrDefault()?.Id,
            nodes.Count == 0 ? edges.FirstOrDefault()?.Id : null,
            nodes.Count,
            edges.Count,
            groups.Count)
        {
            SelectedNodeIds = nodes.Select(node => node.Id).ToArray(),
            SelectedEdgeIds = nodes.Count == 0
                ? edges.Select(edge => edge.Id).ToArray()
                : [],
            SelectedAnnotationId = nodes.Count == 0 && edges.Count == 0
                ? annotations.FirstOrDefault()?.Id
                : null,
            SelectedAnnotationIds = annotations.Select(annotation => annotation.Id).ToArray(),
            AnnotationCount = annotations.Count
        };
        return true;
    }

    /// <summary>
    /// Creates a paste plan with the copied content's top-left point aligned to
    /// <paramref name="targetPoint"/>.
    /// </summary>
    public bool TryCreatePastePlanAt(
        GraphDocument document,
        string? text,
        GraphPoint targetPoint,
        out GraphPastePlan? plan,
        string? commandName = null,
        IConnectionValidationRule? connectionValidationRule = null,
        IGraphValidationTextProvider? validationTextProvider = null)
    {
        plan = null;
        if (!double.IsFinite(targetPoint.X) ||
            !double.IsFinite(targetPoint.Y) ||
            !TryGetPasteAnchor(text, out var sourceTopLeft))
        {
            return false;
        }

        return TryCreatePastePlan(
            document,
            text,
            new GraphPoint(
                targetPoint.X - sourceTopLeft.X,
                targetPoint.Y - sourceTopLeft.Y),
            out plan,
            commandName,
            connectionValidationRule,
            validationTextProvider);
    }

    /// <summary>
    /// Gets the top-left model point used as the anchor for a paste operation.
    /// </summary>
    public bool TryGetPasteAnchor(string? text, out GraphPoint anchor)
    {
        anchor = GraphPoint.Zero;
        if (!_serializer.TryDeserialize(text, out var payload) || payload is null)
        {
            return false;
        }

        var lefts = payload.Nodes
            .Where(node => node is not null)
            .Select(node => GraphNodeGeometry.GetCanvasBounds(node).X)
            .Concat(payload.Annotations
                .Where(annotation => annotation is not null)
                .Select(annotation => annotation.Position.X))
            .Concat(payload.Edges
                .Where(edge => edge is not null)
                .SelectMany(edge => edge.Waypoints ?? [])
                .Select(point => point.X))
            .ToArray();
        var tops = payload.Nodes
            .Where(node => node is not null)
            .Select(node => GraphNodeGeometry.GetCanvasBounds(node).Y)
            .Concat(payload.Annotations
                .Where(annotation => annotation is not null)
                .Select(annotation => annotation.Position.Y))
            .Concat(payload.Edges
                .Where(edge => edge is not null)
                .SelectMany(edge => edge.Waypoints ?? [])
                .Select(point => point.Y))
            .ToArray();
        if (lefts.Any(value => !double.IsFinite(value)) ||
            tops.Any(value => !double.IsFinite(value)))
        {
            return false;
        }

        if (lefts.Length == 0 || tops.Length == 0)
        {
            return payload.Edges.Count > 0;
        }

        anchor = new GraphPoint(lefts.Min(), tops.Min());
        return true;
    }

    private static PortReference? RemapReference(
        PortReference reference,
        IReadOnlyDictionary<string, string> nodeIds,
        IReadOnlyDictionary<(string NodeId, string PortId), string> portIds)
    {
        if (!nodeIds.TryGetValue(reference.NodeId, out var nodeId))
        {
            return reference;
        }

        return portIds.TryGetValue((reference.NodeId, reference.PortId), out var portId)
            ? new PortReference(nodeId, portId)
            : null;
    }

    private static IReadOnlyList<string> GetAssetPaths(IEnumerable<NodeModel> nodes) =>
        nodes
            .SelectMany(node =>
            {
                var paths = new List<string>();
                if ((node.Appearance ?? new GraphNodeAppearance()).Normalize().BackgroundImage is { } background)
                {
                    paths.Add(background);
                }

                if (node.Properties.TryGetValue(
                        GraphNodePropertyKeys.BackgroundImageMappings,
                        out var value) &&
                    value is IReadOnlyDictionary<string, object?> mappings)
                {
                    paths.AddRange(mappings.Values.OfType<string>());
                }

                return paths;
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
