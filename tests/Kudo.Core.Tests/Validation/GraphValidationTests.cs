using Kudo;
using Shouldly;

namespace Kudo.Core.Tests;

public class GraphValidationTests
{
    [Fact]
    public void Structure_rule_accepts_valid_document()
    {
        var document = CreateDocument();
        document.AddEdge(CreateEdge("edge", "source", "target"));

        var issues = new GraphStructureValidationRule().Validate(document);

        issues.ShouldBeEmpty();
    }

    [Fact]
    public void Structure_rule_reports_identifiers_and_missing_endpoints()
    {
        var document = new GraphDocument { Id = string.Empty };
        document.AddNode(new NodeModel { Id = "node" }
            .AddPort(new PortModel { Id = "port" })
            .AddPort(new PortModel { Id = "port" }));
        document.AddNode(new NodeModel { Id = "node" });
        document.AddEdge(new EdgeModel
        {
            Id = "edge",
            Source = new PortReference("missing", "out"),
            Target = new PortReference("node", "missing")
        });
        document.AddEdge(new EdgeModel { Id = "edge" });

        var issues = new GraphStructureValidationRule().Validate(document).ToArray();

        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.EmptyDocumentId);
        issues.Single(issue => issue.Code == GraphValidationCodes.EmptyDocumentId)
            .Message.ShouldBe("Document ID cannot be empty.");
        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.DuplicateNodeId);
        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.DuplicatePortId);
        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.DuplicateEdgeId);
        issues.Count(issue => issue.Code == GraphValidationCodes.MissingEdgeEndpoint).ShouldBe(2);
    }

    [Fact]
    public void Structure_rule_reports_connection_semantics_and_limits()
    {
        var document = CreateDocument();
        document.AddNode(new NodeModel { Id = "other-source" }
            .AddPort(new PortModel
            {
                Id = "out",
                Kind = PortKind.Output,
                DataType = "text",
                Capacity = new PortCapacity { MaxOutgoing = null }
            }));
        document.AddEdge(CreateEdge("edge-1", "source", "target"));
        document.AddEdge(CreateEdge("edge-2", "source", "target"));
        document.AddEdge(CreateEdge("edge-3", "other-source", "target"));

        var issues = new GraphStructureValidationRule().Validate(document).ToArray();

        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.DuplicateConnection);
        issues.Count(issue => issue.Code == GraphValidationCodes.ConnectionLimitExceeded).ShouldBe(2);
        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.IncompatibleDataType);
    }

    [Fact]
    public void Structure_rule_allows_input_output_ports_as_source_or_target()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "first" }
            .AddPort(new PortModel
            {
                Id = "a",
                Kind = PortKind.InputOutput,
                DataType = "fluid",
                Capacity = new PortCapacity { MaxIncoming = null, MaxOutgoing = null }
            }));
        document.AddNode(new NodeModel { Id = "second" }
            .AddPort(new PortModel
            {
                Id = "b",
                Kind = PortKind.InputOutput,
                DataType = "fluid",
                Capacity = new PortCapacity { MaxIncoming = null, MaxOutgoing = null }
            }));
        var validation = document.ValidateConnection(
            new PortReference("first", "a"),
            new PortReference("second", "b"));
        document.AddEdge(new EdgeModel
        {
            Id = "edge",
            Source = new PortReference("first", "a"),
            Target = new PortReference("second", "b")
        });

        var issues = new GraphStructureValidationRule().Validate(document).ToArray();

        issues.ShouldNotContain(issue => issue.Code == GraphValidationCodes.IncompatiblePortKinds);
        validation.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Structure_rule_keeps_connection_and_reference_semantics_separate()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "first" }
            .AddPort(new PortModel { Id = "connection", Kind = PortKind.Output })
            .AddPort(new PortModel { Id = "reference", Kind = PortKind.Reference }));
        document.AddNode(new NodeModel { Id = "second" }
            .AddPort(new PortModel { Id = "connection", Kind = PortKind.Input })
            .AddPort(new PortModel { Id = "reference", Kind = PortKind.Reference }));
        document.AddEdge(new EdgeModel
        {
            Id = "invalid-reference",
            RelationKind = GraphRelationKind.Reference,
            Source = new PortReference("first", "connection"),
            Target = new PortReference("second", "connection")
        });
        document.AddEdge(new EdgeModel
        {
            Id = "invalid-connection",
            RelationKind = GraphRelationKind.Connection,
            Source = new PortReference("first", "reference"),
            Target = new PortReference("second", "reference")
        });

        var issues = new GraphStructureValidationRule().Validate(document).ToArray();

        issues.Count(issue => issue.Code == GraphValidationCodes.IncompatiblePortKinds).ShouldBe(2);
        issues.ShouldNotContain(issue => issue.Code == GraphValidationCodes.IncompatibleDataType);
    }

    [Fact]
    public void Structure_rule_reports_invalid_group_structure()
    {
        var document = CreateDocument();
        document.Groups =
        [
            new GroupModel
            {
                Id = string.Empty,
                NodeIds = []
            },
            new GroupModel
            {
                Id = "duplicate",
                NodeIds = ["source", "source", "missing"],
                Padding = -1
            },
            new GroupModel
            {
                Id = "duplicate",
                NodeIds = ["source", "target"]
            }
        ];

        var issues = new GraphStructureValidationRule().Validate(document).ToArray();

        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.EmptyGroupId);
        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.DuplicateGroupId);
        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.EmptyGroup);
        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.MissingGroupNode);
        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.DuplicateGroupNode);
        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.MultipleGroupMembership);
        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.InvalidGroupPadding);
    }

    [Fact]
    public void Structure_rule_reports_invalid_annotation_identifiers_and_bounds()
    {
        var document = CreateDocument();
        document.Annotations =
        [
            new AnnotationModel { Id = string.Empty, Size = GraphSize.Empty },
            new AnnotationModel { Id = "duplicate", Position = new GraphPoint(double.NaN, 0) },
            new AnnotationModel { Id = "duplicate" }
        ];

        var issues = new GraphStructureValidationRule().Validate(document).ToArray();

        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.EmptyAnnotationId);
        issues.ShouldContain(issue => issue.Code == GraphValidationCodes.DuplicateAnnotationId);
        issues.Count(issue => issue.Code == GraphValidationCodes.InvalidAnnotationBounds).ShouldBe(2);
    }

    [Fact]
    public void Validator_aggregates_rules_and_orders_by_severity()
    {
        var validator = new GraphValidator([
            new StaticRule(
                new GraphValidationIssue(ValidationSeverity.Info, "提示", Code: "INFO"),
                new GraphValidationIssue(ValidationSeverity.Error, "错误", Code: "ERROR")),
            new StaticRule(new GraphValidationIssue(ValidationSeverity.Warning, "警告", Code: "WARNING"))
        ]);

        var issues = validator.Validate(new GraphDocument());

        issues.Select(issue => issue.Severity).ShouldBe([
            ValidationSeverity.Error,
            ValidationSeverity.Warning,
            ValidationSeverity.Info
        ]);
    }

    private static GraphDocument CreateDocument()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "source" }
            .AddPort(new PortModel
            {
                Id = "out",
                Kind = PortKind.Output,
                DataType = "number",
                Capacity = new PortCapacity { MaxOutgoing = null }
            }));
        document.AddNode(new NodeModel { Id = "target" }
            .AddPort(new PortModel
            {
                Id = "in",
                Kind = PortKind.Input,
                DataType = "number",
                Capacity = new PortCapacity { MaxIncoming = 1 }
            }));
        return document;
    }

    private static EdgeModel CreateEdge(string id, string sourceNodeId, string targetNodeId) => new()
    {
        Id = id,
        Source = new PortReference(sourceNodeId, "out"),
        Target = new PortReference(targetNodeId, "in")
    };

    private sealed class StaticRule(params GraphValidationIssue[] issues) : IGraphValidationRule
    {
        public IEnumerable<GraphValidationIssue> Validate(GraphDocument document) => issues;
    }
}
