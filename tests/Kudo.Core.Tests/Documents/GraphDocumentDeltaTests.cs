using System.Text.Json;
using Kudo.Core.Documents;
using Kudo.Core.Serialization;
using Shouldly;

namespace Kudo.Core.Tests.Documents;

public sealed class GraphDocumentDeltaTests
{
    [Fact]
    public void Calculate_returns_an_empty_delta_for_equivalent_documents()
    {
        var source = CreateDocument();
        var target = Clone(source);

        var delta = GraphDocumentDeltaCalculator.Calculate(source, target);

        delta.IsEmpty.ShouldBeTrue();
        delta.Header.ShouldBeNull();
        delta.Nodes.IsEmpty.ShouldBeTrue();
        delta.Edges.IsEmpty.ShouldBeTrue();
        delta.Groups.IsEmpty.ShouldBeTrue();
        delta.Annotations.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Calculate_and_apply_tracks_node_additions_removals_updates_and_order()
    {
        var source = CreateDocument();
        var target = Clone(source);
        var first = target.FindNode("first")!;
        first.Name = "Updated first";
        first.Position = new GraphPoint(80, 42);
        first.Properties["configuration"] = new Dictionary<string, object?>
        {
            ["retryCount"] = 5,
            ["tags"] = new List<object?> { "critical", true }
        };
        first.ExtensionState = CreateState("{\"mode\":\"manual\",\"timeout\":30}");
        target.RemoveNode("second").ShouldBeTrue();
        target.AddNode(new NodeModel
        {
            Id = "third",
            Type = "Terminal",
            Position = new GraphPoint(320, 100),
            Ports = [new PortModel { Id = "in", Kind = PortKind.Input }]
        });
        target.Nodes = [target.FindNode("third")!, first];

        var delta = GraphDocumentDeltaCalculator.Calculate(source, target);
        var applied = GraphDocumentDeltaApplier.Apply(source, delta);

        delta.Nodes.RemovedIds.ShouldBe(["second"]);
        delta.Nodes.Upserts.Select(node => node.Id).ShouldBe(["third", "first"]);
        delta.Nodes.Order.ShouldBe(["third", "first"]);
        Serialize(applied).ShouldBe(Serialize(target));
    }

    [Fact]
    public void Calculate_and_apply_tracks_edge_group_annotation_and_document_header_changes()
    {
        var source = CreateDocument();
        var target = Clone(source);
        target.Title = "Updated document";
        target.CanvasSettings = new GraphCanvasSettings
        {
            Grid = new GraphGridSettings { IsSnapEnabled = true, Spacing = 32 },
            IsMinimapVisible = false
        };
        target.Properties["environment"] = "production";
        target.Edges.Single().Label = "Updated edge";
        target.Edges.Single().Waypoints = [new GraphPoint(150, 20), new GraphPoint(170, 80)];
        target.Edges.Single().Appearance = new GraphEdgeAppearance
        {
            StrokeColor = "#1E40AF",
            StrokeWidth = 2.5,
            DashStyle = GraphEdgeDashStyle.Custom,
            CustomDashPattern = [2, 4],
            Animation = GraphEdgeAnimationKind.Flow,
            AnimationSpeed = 1.5
        };
        target.Groups.Single().IsCollapsed = true;
        target.Groups.Single().Properties["color"] = "blue";
        target.Annotations.Single().Text = "Updated note";
        target.Annotations.Single().Properties["severity"] = "warning";

        var delta = GraphDocumentDeltaCalculator.Calculate(source, target);
        var applied = GraphDocumentDeltaApplier.Apply(source, delta);

        delta.Header.ShouldNotBeNull();
        delta.Edges.Upserts.Select(edge => edge.Id).ShouldBe(["edge"]);
        delta.Groups.Upserts.Select(group => group.Id).ShouldBe(["group"]);
        delta.Annotations.Upserts.Select(annotation => annotation.Id).ShouldBe(["annotation"]);
        Serialize(applied).ShouldBe(Serialize(target));
    }

    [Fact]
    public void Delta_keeps_a_deep_copy_of_extension_state_and_properties()
    {
        var source = CreateDocument();
        var target = Clone(source);
        var targetNode = target.FindNode("first")!;
        targetNode.ExtensionState = CreateState("{\"configuration\":{\"mode\":\"target\"}}");
        targetNode.Properties["configuration"] = new Dictionary<string, object?>
        {
            ["values"] = new List<object?> { 1, 2 }
        };
        var delta = GraphDocumentDeltaCalculator.Calculate(source, target);
        var storedNode = delta.Nodes.Upserts.Single();

        targetNode.ExtensionState = CreateState("{\"configuration\":{\"mode\":\"changed\"}}");
        ((List<object?>)((Dictionary<string, object?>)targetNode.Properties["configuration"]!)["values"]!).Add(3);
        var applied = GraphDocumentDeltaApplier.Apply(source, delta);
        var appliedNode = applied.FindNode("first")!;

        appliedNode.ExtensionState!.Payload.GetProperty("configuration").GetProperty("mode").GetString()
            .ShouldBe("target");
        ((List<object?>)((Dictionary<string, object?>)appliedNode.Properties["configuration"]!)["values"]!)
            .ShouldBe([1, 2]);
        storedNode.ShouldNotBeSameAs(targetNode);
    }

    [Fact]
    public void Calculate_treats_equivalent_json_values_as_equal_across_property_order_and_number_format()
    {
        var source = CreateDocument();
        source.FindNode("first")!.ExtensionState = CreateState(
            "{\"configuration\":{\"mode\":\"automatic\",\"timeout\":30}}");
        var target = Clone(source);
        target.FindNode("first")!.ExtensionState = CreateState(
            "{\"configuration\":{\"timeout\":30.0,\"mode\":\"automatic\"}}");

        var delta = GraphDocumentDeltaCalculator.Calculate(source, target);

        delta.Nodes.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Apply_rejects_a_delta_from_a_different_or_changed_document()
    {
        var source = CreateDocument();
        var target = Clone(source);
        target.Title = "Updated";
        var delta = GraphDocumentDeltaCalculator.Calculate(source, target);
        var other = Clone(source);
        other.Id = "other-document";

        Should.Throw<GraphDocumentDeltaConflictException>(() =>
            GraphDocumentDeltaApplier.Apply(other, delta));

        var changed = Clone(source);
        changed.Title = "A later change";
        Should.Throw<GraphDocumentDeltaConflictException>(() =>
            GraphDocumentDeltaApplier.Apply(changed, delta));
    }

    private static GraphDocument CreateDocument()
    {
        var document = new GraphDocument
        {
            Id = "document",
            Title = "Source document",
            Properties = new Dictionary<string, object?>
            {
                ["environment"] = "development"
            }
        };
        document.AddNode(new NodeModel
        {
            Id = "first",
            Type = "Source",
            Name = "First",
            Position = new GraphPoint(20, 30),
            ExtensionState = CreateState("{\"configuration\":{\"mode\":\"automatic\"}}"),
            Ports = [new PortModel { Id = "out", Kind = PortKind.Output }],
            Properties = new Dictionary<string, object?>
            {
                ["configuration"] = new Dictionary<string, object?>
                {
                    ["retryCount"] = 3
                }
            }
        });
        document.AddNode(new NodeModel
        {
            Id = "second",
            Type = "Sink",
            Position = new GraphPoint(220, 30),
            Ports = [new PortModel { Id = "in", Kind = PortKind.Input }]
        });
        document.AddEdge(new EdgeModel
        {
            Id = "edge",
            Source = new PortReference("first", "out"),
            Target = new PortReference("second", "in"),
            Label = "Source edge",
            Waypoints = [new GraphPoint(120, 50)]
        });
        document.AddGroup(new GroupModel { Id = "group", NodeIds = ["first", "second"] });
        document.AddAnnotation(new AnnotationModel { Id = "annotation", Text = "Source note" });
        return document;
    }

    private static GraphExtensionState CreateState(string json)
    {
        using var payload = JsonDocument.Parse(json);
        return new GraphExtensionState
        {
            SchemaId = "tests.node",
            SchemaVersion = 1,
            Payload = payload.RootElement.Clone()
        };
    }

    private static GraphDocument Clone(GraphDocument source) =>
        new GraphTaskXmlSerializer().Deserialize(Serialize(source));

    private static string Serialize(GraphDocument document) =>
        new GraphTaskXmlSerializer().Serialize(document);
}
