namespace Kudo;

public static class GraphValidationCodes
{
    public const string EmptyDocumentId = "GK1001";
    public const string EmptyNodeId = "GK1002";
    public const string DuplicateNodeId = "GK1003";
    public const string EmptyPortId = "GK1004";
    public const string DuplicatePortId = "GK1005";
    public const string EmptyEdgeId = "GK1006";
    public const string DuplicateEdgeId = "GK1007";
    public const string EmptyGroupId = "GK1008";
    public const string DuplicateGroupId = "GK1009";
    public const string EmptyAnnotationId = "GK1010";
    public const string DuplicateAnnotationId = "GK1011";
    public const string MissingEdgeEndpoint = "GK1101";
    public const string SelfConnection = "GK1102";
    public const string IncompatiblePortKinds = "GK1103";
    public const string InvalidRelationKind = "GK1104";
    public const string IncompatibleDataType = "GK1105";
    public const string DuplicateConnection = "GK1106";
    public const string ConnectionLimitExceeded = "GK1107";
    public const string ConnectionDisabled = "GK1108";
    public const string EmptyGroup = "GK1201";
    public const string MissingGroupNode = "GK1202";
    public const string DuplicateGroupNode = "GK1203";
    public const string MultipleGroupMembership = "GK1204";
    public const string InvalidGroupPadding = "GK1205";
    public const string InvalidAnnotationBounds = "GK1301";
}

public sealed class GraphValidator
{
    private readonly IGraphValidationRule[] _rules;

    public GraphValidator(IEnumerable<IGraphValidationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToArray();
        if (_rules.Any(rule => rule is null))
        {
            throw new ArgumentException("The validation rule collection cannot contain null values.", nameof(rules));
        }
    }

    public IReadOnlyList<IGraphValidationRule> Rules => _rules;

    public IReadOnlyList<GraphValidationIssue> Validate(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _rules
            .SelectMany(rule => rule.Validate(document) ?? [])
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.ElementId, StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed class GraphStructureValidationRule : IGraphValidationRule
{
    private readonly IGraphValidationTextProvider? _textProvider;

    public GraphStructureValidationRule(IGraphValidationTextProvider? textProvider = null) =>
        _textProvider = textProvider;

    public IEnumerable<GraphValidationIssue> Validate(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<GraphValidationIssue>();
        ValidateIdentifiers(document, issues);
        ValidateGroups(document, issues);
        ValidateAnnotations(document, issues);
        ValidateEdges(document, issues);
        return issues;
    }

    private void ValidateIdentifiers(
        GraphDocument document,
        ICollection<GraphValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(document.Id))
        {
            issues.Add(Error(L(GraphValidationTextKey.EmptyDocumentId), code: GraphValidationCodes.EmptyDocumentId));
        }

        foreach (var node in document.Nodes.Where(node => string.IsNullOrWhiteSpace(node.Id)))
        {
            issues.Add(Error(L(GraphValidationTextKey.EmptyNodeId), code: GraphValidationCodes.EmptyNodeId));
        }

        foreach (var group in document.Nodes
                     .Where(node => !string.IsNullOrWhiteSpace(node.Id))
                     .GroupBy(node => node.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(Error(
                F(GraphValidationTextKey.DuplicateNodeId, group.Key),
                group.Key,
                GraphValidationCodes.DuplicateNodeId));
        }

        foreach (var node in document.Nodes)
        {
            foreach (var port in node.Ports.Where(port => string.IsNullOrWhiteSpace(port.Id)))
            {
                issues.Add(Error(
                    F(GraphValidationTextKey.EmptyPortId, DisplayNode(node)),
                    EmptyToNull(node.Id),
                    GraphValidationCodes.EmptyPortId));
            }

            foreach (var group in node.Ports
                         .Where(port => !string.IsNullOrWhiteSpace(port.Id))
                         .GroupBy(port => port.Id, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                issues.Add(Error(
                    F(GraphValidationTextKey.DuplicatePortId, DisplayNode(node), group.Key),
                    EmptyToNull(node.Id),
                    GraphValidationCodes.DuplicatePortId));
            }
        }

        foreach (var edge in document.Edges.Where(edge => string.IsNullOrWhiteSpace(edge.Id)))
        {
            issues.Add(Error(L(GraphValidationTextKey.EmptyEdgeId), code: GraphValidationCodes.EmptyEdgeId));
        }

        foreach (var group in document.Edges
                     .Where(edge => !string.IsNullOrWhiteSpace(edge.Id))
                     .GroupBy(edge => edge.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(Error(
                F(GraphValidationTextKey.DuplicateEdgeId, group.Key),
                group.Key,
                GraphValidationCodes.DuplicateEdgeId));
        }

        foreach (var group in document.Groups.Where(group => string.IsNullOrWhiteSpace(group.Id)))
        {
            issues.Add(Error(L(GraphValidationTextKey.EmptyGroupId), code: GraphValidationCodes.EmptyGroupId));
        }

        foreach (var group in document.Groups
                     .Where(group => !string.IsNullOrWhiteSpace(group.Id))
                     .GroupBy(group => group.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(Error(
                F(GraphValidationTextKey.DuplicateGroupId, group.Key),
                group.Key,
                GraphValidationCodes.DuplicateGroupId));
        }

        foreach (var annotation in document.Annotations.Where(annotation =>
                     string.IsNullOrWhiteSpace(annotation.Id)))
        {
            issues.Add(Error(
                L(GraphValidationTextKey.EmptyAnnotationId),
                code: GraphValidationCodes.EmptyAnnotationId));
        }

        foreach (var group in document.Annotations
                     .Where(annotation => !string.IsNullOrWhiteSpace(annotation.Id))
                     .GroupBy(annotation => annotation.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(Error(
                F(GraphValidationTextKey.DuplicateAnnotationId, group.Key),
                group.Key,
                GraphValidationCodes.DuplicateAnnotationId));
        }
    }

    private void ValidateAnnotations(
        GraphDocument document,
        ICollection<GraphValidationIssue> issues)
    {
        foreach (var annotation in document.Annotations)
        {
            if (double.IsFinite(annotation.Position.X) &&
                double.IsFinite(annotation.Position.Y) &&
                double.IsFinite(annotation.Size.Width) &&
                double.IsFinite(annotation.Size.Height) &&
                annotation.Size.Width > 0 &&
                annotation.Size.Height > 0)
            {
                continue;
            }

            issues.Add(Error(
                F(GraphValidationTextKey.InvalidAnnotationBounds, DisplayAnnotation(annotation)),
                EmptyToNull(annotation.Id),
                GraphValidationCodes.InvalidAnnotationBounds));
        }
    }

    private void ValidateGroups(
        GraphDocument document,
        ICollection<GraphValidationIssue> issues)
    {
        var memberships = new Dictionary<string, List<GroupModel>>(StringComparer.Ordinal);
        foreach (var group in document.Groups)
        {
            var elementId = EmptyToNull(group.Id);
            var nodeIds = group.NodeIds ?? [];
            if (nodeIds.Count == 0)
            {
                issues.Add(Error(
                    F(GraphValidationTextKey.EmptyGroup, DisplayGroup(group)),
                    elementId,
                    GraphValidationCodes.EmptyGroup));
            }

            if (!double.IsFinite(group.Padding) || group.Padding < 0)
            {
                issues.Add(Error(
                    F(GraphValidationTextKey.InvalidGroupPadding, DisplayGroup(group)),
                    elementId,
                    GraphValidationCodes.InvalidGroupPadding));
            }

            foreach (var duplicate in nodeIds
                         .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
                         .GroupBy(nodeId => nodeId, StringComparer.Ordinal)
                         .Where(item => item.Count() > 1))
            {
                issues.Add(Error(
                    F(
                        GraphValidationTextKey.DuplicateGroupNode,
                        DisplayGroup(group),
                        duplicate.Key),
                    elementId,
                    GraphValidationCodes.DuplicateGroupNode));
            }

            foreach (var nodeId in nodeIds.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(nodeId) || document.FindNode(nodeId) is null)
                {
                    issues.Add(Error(
                        F(GraphValidationTextKey.MissingGroupNode, DisplayGroup(group), nodeId),
                        elementId,
                        GraphValidationCodes.MissingGroupNode));
                    continue;
                }

                if (!memberships.TryGetValue(nodeId, out var groups))
                {
                    groups = [];
                    memberships.Add(nodeId, groups);
                }

                groups.Add(group);
            }
        }

        foreach (var membership in memberships.Where(item => item.Value.Count > 1))
        {
            issues.Add(Error(
                F(GraphValidationTextKey.MultipleGroupMembership, membership.Key),
                membership.Key,
                GraphValidationCodes.MultipleGroupMembership));
        }
    }

    private void ValidateEdges(
        GraphDocument document,
        ICollection<GraphValidationIssue> issues)
    {
        foreach (var edge in document.Edges)
        {
            var elementId = EmptyToNull(edge.Id);
            var sourceNode = document.FindNode(edge.Source.NodeId);
            var targetNode = document.FindNode(edge.Target.NodeId);
            var sourcePort = document.FindPort(edge.Source);
            var targetPort = document.FindPort(edge.Target);
            if (sourceNode is null || targetNode is null || sourcePort is null || targetPort is null)
            {
                issues.Add(Error(
                    F(GraphValidationTextKey.MissingEdgeEndpoint, DisplayEdge(edge)),
                    elementId,
                    GraphValidationCodes.MissingEdgeEndpoint));
                continue;
            }

            if (sourceNode.Id == targetNode.Id)
            {
                issues.Add(Error(
                    F(GraphValidationTextKey.SelfConnection, DisplayEdge(edge)),
                    elementId,
                    GraphValidationCodes.SelfConnection));
            }

            if (!Enum.IsDefined(edge.RelationKind))
            {
                issues.Add(Error(
                    F(GraphValidationTextKey.InvalidRelationKind, DisplayEdge(edge)),
                    elementId,
                    GraphValidationCodes.InvalidRelationKind));
                continue;
            }

            if (!DefaultConnectionValidationRule.ArePortKindsCompatible(
                    edge.RelationKind,
                    sourcePort.Kind,
                    targetPort.Kind))
            {
                issues.Add(Error(
                    F(GraphValidationTextKey.IncompatiblePortKinds, DisplayEdge(edge)),
                    elementId,
                    GraphValidationCodes.IncompatiblePortKinds));
            }

            if (edge.RelationKind == GraphRelationKind.Connection &&
                !AreDataTypesCompatible(sourcePort.DataType, targetPort.DataType))
            {
                issues.Add(Error(
                    F(GraphValidationTextKey.IncompatibleDataType, DisplayEdge(edge)),
                    elementId,
                    GraphValidationCodes.IncompatibleDataType));
            }
        }

        foreach (var group in document.Edges
                     .GroupBy(edge => (edge.Source, edge.Target, edge.RelationKind))
                     .Where(group => group.Count() > 1))
        {
            foreach (var edge in group.Skip(1))
            {
                issues.Add(Error(
                    F(GraphValidationTextKey.DuplicateConnection, DisplayEdge(edge)),
                    EmptyToNull(edge.Id),
                    GraphValidationCodes.DuplicateConnection));
            }
        }

        ValidateConnectionLimits(document, issues);
    }

    private void ValidateConnectionLimits(
        GraphDocument document,
        ICollection<GraphValidationIssue> issues)
    {
        foreach (var node in document.Nodes)
        {
            foreach (var port in node.Ports)
            {
                var reference = new PortReference(node.Id, port.Id);
                var outgoing = document.Edges.Where(edge => edge.Source == reference).ToArray();
                var incoming = document.Edges.Where(edge => edge.Target == reference).ToArray();
                var all = document.Edges
                    .Where(edge => edge.Source == reference || edge.Target == reference)
                    .ToArray();
                var capacity = (port.Capacity ?? new PortCapacity()).Normalize();
                var overflow = new HashSet<EdgeModel>();
                AddOverflow(outgoing, capacity.MaxOutgoing, overflow);
                AddOverflow(incoming, capacity.MaxIncoming, overflow);
                AddOverflow(all, capacity.MaxTotal, overflow);

                foreach (var edge in document.Edges.Where(overflow.Contains))
                {
                    issues.Add(Error(
                        F(GraphValidationTextKey.ConnectionLimitExceeded, DisplayEdge(edge)),
                        EmptyToNull(edge.Id),
                        GraphValidationCodes.ConnectionLimitExceeded));
                }
            }
        }
    }

    private static void AddOverflow(
        IReadOnlyList<EdgeModel> edges,
        int? limit,
        ISet<EdgeModel> overflow)
    {
        if (limit is not { } value)
        {
            return;
        }

        foreach (var edge in edges.Skip(Math.Max(0, value)))
        {
            overflow.Add(edge);
        }
    }

    private static GraphValidationIssue Error(string message, string? elementId = null, string? code = null) =>
        new(ValidationSeverity.Error, message, elementId, code);

    private static string DisplayNode(NodeModel node) =>
        string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name;

    private string DisplayEdge(EdgeModel edge) =>
        string.IsNullOrWhiteSpace(edge.Id) ? L(GraphValidationTextKey.UntitledEdge) : edge.Id;

    private string DisplayGroup(GroupModel group) =>
        string.IsNullOrWhiteSpace(group.Title)
            ? string.IsNullOrWhiteSpace(group.Id) ? L(GraphValidationTextKey.UntitledGroup) : group.Id
            : group.Title;

    private string DisplayAnnotation(AnnotationModel annotation) =>
        string.IsNullOrWhiteSpace(annotation.Text)
            ? string.IsNullOrWhiteSpace(annotation.Id)
                ? L(GraphValidationTextKey.UntitledAnnotation)
                : annotation.Id
            : annotation.Text;

    private string L(GraphValidationTextKey key) => GraphValidationText.Get(_textProvider, key);

    private string F(GraphValidationTextKey key, params object?[] arguments) =>
        GraphValidationText.Format(_textProvider, key, arguments);

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool AreDataTypesCompatible(string? sourceDataType, string? targetDataType) =>
        string.IsNullOrWhiteSpace(sourceDataType) ||
        string.IsNullOrWhiteSpace(targetDataType) ||
        string.Equals(sourceDataType, targetDataType, StringComparison.OrdinalIgnoreCase);
}
