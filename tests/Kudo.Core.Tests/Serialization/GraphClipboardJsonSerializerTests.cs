using Kudo.Core.Serialization;
using Shouldly;

namespace Kudo.Core.Tests.Serialization;

public class GraphClipboardJsonSerializerTests
{
    [Fact]
    public void Clipboard_payload_roundtrip_preserves_graph_elements()
    {
        var serializer = new GraphClipboardJsonSerializer();
        var payload = new GraphClipboardPayload
        {
            Nodes =
            [
                new NodeModel
                    {
                        Id = "node",
                        DefinitionId = "PUMP",
                        Number = "001",
                        Name = "节点",
                        IsLocked = true,
                        Rotation = -27.5
                    }
                    .AddPort(new PortModel
                    {
                        Id = "out",
                        Kind = PortKind.Output,
                        IsVisible = false,
                        IncomingDirection = new GraphPoint(double.NaN, 0),
                        OutgoingDirection = new GraphPoint(4, 3),
                        Capacity = new PortCapacity { MaxOutgoing = null, MaxTotal = 3 }
                    })
            ],
            Edges =
            [
                new EdgeModel
                {
                    Id = "edge",
                    RelationKind = GraphRelationKind.Reference,
                    Label = "信号",
                    RoutingStyle = EdgeRoutingStyle.SmartOrthogonal,
                    StylePreset = GraphEdgeStylePreset.Signal,
                    Waypoints = [new GraphPoint(120, 40)],
                    SourceMarker = EdgeMarker.Diamond,
                    TargetMarker = EdgeMarker.Arrow,
                    Source = new PortReference("node", "out"),
                    Target = new PortReference("target", "in")
                }
            ],
            Groups =
            [
                new GroupModel
                {
                    Id = "group",
                    Title = "信号组",
                    NodeIds = ["node", "target"],
                    Padding = 30
                }
            ],
            Annotations =
            [
                new AnnotationModel
                {
                    Id = "note",
                    Text = "检查信号",
                    Position = new GraphPoint(20, 30),
                    Size = new GraphSize(240, 120),
                    Properties = { ["level"] = "warning" }
                }
            ]
        };

        var json = serializer.Serialize(payload);
        var parsed = serializer.TryDeserialize(json, out var restored);

        json.ShouldContain("\"Kind\":\"Output\"");
        json.ShouldContain("\"RelationKind\":\"Reference\"");
        parsed.ShouldBeTrue();
        restored.ShouldNotBeNull();
        restored.Nodes.Single().Name.ShouldBe("节点");
        restored.Nodes.Single().DefinitionId.ShouldBe("PUMP");
        restored.Nodes.Single().Number.ShouldBe("001");
        restored.Nodes.Single().IsLocked.ShouldBeTrue();
        restored.Nodes.Single().Rotation.ShouldBe(-27.5);
        restored.Nodes.Single().Ports.Single().Id.ShouldBe("out");
        restored.Nodes.Single().Ports.Single().IsVisible.ShouldBeFalse();
        restored.Nodes.Single().Ports.Single().IncomingDirection.ShouldBeNull();
        restored.Nodes.Single().Ports.Single().OutgoingDirection.ShouldBe(new GraphPoint(0.8, 0.6));
        restored.Nodes.Single().Ports.Single().Capacity.MaxTotal.ShouldBe(3);
        restored.Edges.Single().RelationKind.ShouldBe(GraphRelationKind.Reference);
        restored.Edges.Single().Label.ShouldBe("信号");
        restored.Edges.Single().RoutingStyle.ShouldBe(EdgeRoutingStyle.SmartOrthogonal);
        restored.Edges.Single().StylePreset.ShouldBe(GraphEdgeStylePreset.Signal);
        restored.Edges.Single().Appearance.ShouldBe(GraphEdgeAppearance.Signal);
        restored.Edges.Single().Waypoints.ShouldBe([new GraphPoint(120, 40)]);
        restored.Edges.Single().SourceMarker.ShouldBe(EdgeMarker.Diamond);
        restored.Edges.Single().TargetMarker.ShouldBe(EdgeMarker.Arrow);
        restored.Groups.Single().Title.ShouldBe("信号组");
        restored.Groups.Single().NodeIds.ShouldBe(["node", "target"]);
        restored.Groups.Single().Padding.ShouldBe(30);
        var annotation = restored.Annotations.Single();
        annotation.Text.ShouldBe("检查信号");
        annotation.Position.ShouldBe(new GraphPoint(20, 30));
        annotation.Size.ShouldBe(new GraphSize(240, 120));
        annotation.Properties["level"]
            .ShouldBeOfType<System.Text.Json.JsonElement>()
            .GetString()
            .ShouldBe("warning");
    }

    [Fact]
    public void Version_one_payload_without_groups_remains_compatible()
    {
        var serializer = new GraphClipboardJsonSerializer();

        var parsed = serializer.TryDeserialize(
            "{\"format\":\"Kudo.Elements/1\",\"nodes\":[{\"id\":\"node\",\"ports\":[]}],\"edges\":[]}",
            out var payload);

        parsed.ShouldBeTrue();
        payload.ShouldNotBeNull();
        payload.Groups.ShouldBeEmpty();
        payload.Annotations.ShouldBeEmpty();
    }

    [Fact]
    public void Annotation_only_payload_is_supported()
    {
        var serializer = new GraphClipboardJsonSerializer();
        var text = serializer.Serialize(new GraphClipboardPayload
        {
            Annotations = [new AnnotationModel { Id = "note", Text = "说明" }]
        });

        serializer.TryDeserialize(text, out var payload).ShouldBeTrue();
        payload.ShouldNotBeNull();
        payload.Annotations.Single().Text.ShouldBe("说明");
        payload.Nodes.ShouldBeEmpty();
        payload.Edges.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plain text")]
    [InlineData("{\"format\":\"Other/1\",\"nodes\":[{}]}")]
    public void Non_kudo_payload_is_rejected(string? text)
    {
        var serializer = new GraphClipboardJsonSerializer();

        serializer.TryDeserialize(text, out var payload).ShouldBeFalse();

        payload.ShouldBeNull();
    }
}
