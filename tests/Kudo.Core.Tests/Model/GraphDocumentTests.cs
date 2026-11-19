using Kudo;
using Shouldly;

namespace Kudo.Core.Tests;

public class GraphDocumentTests
{
    [Fact]
    public void Edge_style_preset_updates_appearance_and_notifies_mvvm_consumers()
    {
        var edge = new EdgeModel();
        var changedProperties = new List<string?>();
        edge.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        edge.StylePreset = GraphEdgeStylePreset.Signal;

        edge.Appearance.ShouldBe(GraphEdgeAppearance.Signal);
        changedProperties.ShouldContain(nameof(EdgeModel.StylePreset));
        changedProperties.ShouldContain(nameof(EdgeModel.Appearance));

        edge.Appearance = GraphEdgeAppearance.Warning.WithColor("#123456");

        edge.StylePreset.ShouldBe(GraphEdgeStylePreset.Custom);
        edge.Appearance.StrokeColor.ShouldBe("#123456");
    }

    [Fact]
    public void Edge_appearance_normalizes_invalid_values_without_losing_valid_style()
    {
        var appearance = new GraphEdgeAppearance
        {
            StrokeColor = "  #2563EB  ",
            StrokeWidth = -2,
            DashStyle = GraphEdgeDashStyle.Custom,
            CustomDashPattern = [8, double.NaN, -1, 2],
            Opacity = 2,
            Animation = GraphEdgeAnimationKind.Flow,
            AnimationSpeed = double.PositiveInfinity,
            GlowIntensity = -1
        }.Normalize();

        appearance.StrokeColor.ShouldBe("#2563EB");
        appearance.StrokeWidth.ShouldBeNull();
        appearance.CustomDashPattern.ShouldBe([8, 2]);
        appearance.Opacity.ShouldBe(1);
        appearance.AnimationSpeed.ShouldBe(1);
        appearance.GlowIntensity.ShouldBe(0);
        appearance.IsAnimated.ShouldBeTrue();
    }

    [Fact]
    public void AddNode_keeps_node_available_by_id()
    {
        var document = new GraphDocument();
        var node = new NodeModel { Id = "node-1" };

        document.AddNode(node);

        document.FindNode("node-1").ShouldBeSameAs(node);
    }

    [Fact]
    public void Add_ranges_keep_order_and_references()
    {
        var document = new GraphDocument();
        var nodes = new[]
        {
            new NodeModel { Id = "node-1" },
            new NodeModel { Id = "node-2" }
        };
        var edges = new[]
        {
            new EdgeModel { Id = "edge-1" },
            new EdgeModel { Id = "edge-2" }
        };
        var groups = new[]
        {
            new GroupModel { Id = "group-1" },
            new GroupModel { Id = "group-2" }
        };
        var annotations = new[]
        {
            new AnnotationModel { Id = "annotation-1" },
            new AnnotationModel { Id = "annotation-2" }
        };

        document.AddNodes(nodes);
        document.AddEdges(edges);
        document.AddGroups(groups);
        document.AddAnnotations(annotations);

        document.Nodes.ShouldBe(nodes);
        document.Edges.ShouldBe(edges);
        document.Groups.ShouldBe(groups);
        document.Annotations.ShouldBe(annotations);
        document.Nodes[0].ShouldBeSameAs(nodes[0]);
        document.Edges[1].ShouldBeSameAs(edges[1]);
        document.Groups[0].ShouldBeSameAs(groups[0]);
        document.Annotations[1].ShouldBeSameAs(annotations[1]);
    }

    [Fact]
    public void Annotation_can_be_found_and_removed_without_affecting_graph_elements()
    {
        var document = CreateConnectableDocument();
        var annotation = document.AddAnnotation(new AnnotationModel
        {
            Id = "note",
            Text = "检查输入参数"
        });

        document.FindAnnotation(annotation.Id).ShouldBeSameAs(annotation);
        document.RemoveAnnotation(annotation.Id).ShouldBeTrue();

        document.FindAnnotation(annotation.Id).ShouldBeNull();
        document.Nodes.Count.ShouldBe(2);
        document.Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Add_range_rejects_null_element_without_partial_write()
    {
        var document = new GraphDocument();
        var existing = document.AddNode(new NodeModel { Id = "existing" });

        Should.Throw<ArgumentException>(() => document.AddNodes(
            [new NodeModel { Id = "new" }, null!]));

        document.Nodes.ShouldBe([existing]);
    }

    [Fact]
    public void Add_range_does_not_write_when_enumeration_fails()
    {
        var document = new GraphDocument();
        var existing = document.AddNode(new NodeModel { Id = "existing" });

        Should.Throw<InvalidOperationException>(() => document.AddNodes(EnumerateThenFail()));

        document.Nodes.ShouldBe([existing]);
    }

    [Fact]
    public void FindPort_resolves_port_reference()
    {
        var document = new GraphDocument();
        var node = new NodeModel { Id = "node-1" }
            .AddPort(new PortModel { Id = "out", Kind = PortKind.Output });
        document.AddNode(node);

        var port = document.FindPort(new PortReference("node-1", "out"));

        port.ShouldNotBeNull();
        port.Kind.ShouldBe(PortKind.Output);
    }

    [Fact]
    public void TryConnect_adds_edge_for_valid_ports()
    {
        var document = CreateConnectableDocument();

        var connected = document.TryConnect(
            new PortReference("source", "out"),
            new PortReference("target", "in"),
            out var edge,
            out var validation);

        connected.ShouldBeTrue();
        validation.IsValid.ShouldBeTrue();
        edge.ShouldNotBeNull();
        edge.RelationKind.ShouldBe(GraphRelationKind.Connection);
        document.Edges.ShouldContain(edge);
    }

    [Fact]
    public void TryConnect_creates_reference_relation_only_between_reference_ports()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "source" }
            .AddPort(new PortModel
            {
                Id = "reference",
                Kind = PortKind.Reference,
                DataType = "table",
                Capacity = new PortCapacity { MaxOutgoing = null }
            }));
        document.AddNode(new NodeModel { Id = "target" }
            .AddPort(new PortModel
            {
                Id = "reference",
                Kind = PortKind.Reference,
                DataType = "column"
            })
            .AddPort(new PortModel { Id = "input", Kind = PortKind.Input }));

        document.TryConnect(
            new PortReference("source", "reference"),
            new PortReference("target", "reference"),
            out var edge,
            out var validation,
            GraphRelationKind.Reference).ShouldBeTrue();

        validation.IsValid.ShouldBeTrue();
        edge.ShouldNotBeNull().RelationKind.ShouldBe(GraphRelationKind.Reference);
        document.ValidateConnection(
                new PortReference("source", "reference"),
                new PortReference("target", "input"),
                GraphRelationKind.Reference)
            .IsValid.ShouldBeFalse();
        document.ValidateConnection(
                new PortReference("source", "reference"),
                new PortReference("target", "reference"),
                GraphRelationKind.Connection,
                ignoredEdgeId: edge.Id)
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Input_output_port_can_send_and_receive_connections()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "source" }
            .AddPort(new PortModel { Id = "out", Kind = PortKind.Output }));
        document.AddNode(new NodeModel { Id = "middle" }
            .AddPort(new PortModel
            {
                Id = "both",
                Kind = PortKind.InputOutput,
                Capacity = new PortCapacity
                {
                    MaxIncoming = null,
                    MaxOutgoing = null,
                    MaxTotal = null
                }
            }));
        document.AddNode(new NodeModel { Id = "target" }
            .AddPort(new PortModel { Id = "in", Kind = PortKind.Input }));

        document.TryConnect(
            new PortReference("source", "out"),
            new PortReference("middle", "both"),
            out _,
            out _).ShouldBeTrue();
        document.TryConnect(
            new PortReference("middle", "both"),
            new PortReference("target", "in"),
            out _,
            out _).ShouldBeTrue();
    }

    [Fact]
    public void Connection_capacity_enforces_incoming_outgoing_and_total_limits()
    {
        var incomingDocument = CreateCapacityDocument(new PortCapacity
        {
            MaxIncoming = 1,
            MaxOutgoing = null,
            MaxTotal = null
        });
        incomingDocument.AddEdge(CreateEdge("existing", "source-a", "limited"));
        incomingDocument.ValidateConnection(
                new PortReference("source-b", "port"),
                new PortReference("limited", "port"))
            .IsValid.ShouldBeFalse();

        var outgoingDocument = CreateCapacityDocument(new PortCapacity
        {
            MaxIncoming = null,
            MaxOutgoing = 1,
            MaxTotal = null
        });
        outgoingDocument.AddEdge(CreateEdge("existing", "limited", "target-a"));
        outgoingDocument.ValidateConnection(
                new PortReference("limited", "port"),
                new PortReference("target-b", "port"))
            .IsValid.ShouldBeFalse();

        var totalDocument = CreateCapacityDocument(new PortCapacity
        {
            MaxIncoming = null,
            MaxOutgoing = null,
            MaxTotal = 1
        });
        totalDocument.AddEdge(CreateEdge("existing", "source-a", "limited"));
        totalDocument.ValidateConnection(
                new PortReference("limited", "port"),
                new PortReference("target-a", "port"))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void TryConnect_rejects_input_to_output_direction()
    {
        var document = CreateConnectableDocument();

        var connected = document.TryConnect(
            new PortReference("target", "in"),
            new PortReference("source", "out"),
            out var edge,
            out var validation);

        connected.ShouldBeFalse();
        validation.IsValid.ShouldBeFalse();
        edge.ShouldBeNull();
        document.Edges.ShouldBeEmpty();
    }

    [Fact]
    public void TryConnect_rejects_duplicate_connection()
    {
        var document = CreateConnectableDocument();
        document.TryConnect(
            new PortReference("source", "out"),
            new PortReference("target", "in"),
            out _,
            out _).ShouldBeTrue();

        var connected = document.TryConnect(
            new PortReference("source", "out"),
            new PortReference("target", "in"),
            out _,
            out var validation);

        connected.ShouldBeFalse();
        validation.Message.ShouldNotBeNullOrWhiteSpace();
        document.Edges.Count.ShouldBe(1);
    }

    [Fact]
    public void TryConnect_rejects_second_connection_to_single_input()
    {
        var document = CreateConnectableDocument();
        document.AddNode(new NodeModel { Id = "other-source" }
            .AddPort(new PortModel
            {
                Id = "out",
                Kind = PortKind.Output,
                DataType = "number",
                Capacity = new PortCapacity { MaxOutgoing = null }
            }));
        document.TryConnect(
            new PortReference("source", "out"),
            new PortReference("target", "in"),
            out _,
            out _).ShouldBeTrue();

        var connected = document.TryConnect(
            new PortReference("other-source", "out"),
            new PortReference("target", "in"),
            out _,
            out var validation);

        connected.ShouldBeFalse();
        validation.IsValid.ShouldBeFalse();
        document.Edges.Count.ShouldBe(1);
    }

    [Fact]
    public void ValidateConnection_can_ignore_the_edge_being_reconnected()
    {
        var document = CreateConnectableDocument();
        document.TryConnect(
            new PortReference("source", "out"),
            new PortReference("target", "in"),
            out var edge,
            out _).ShouldBeTrue();

        document.ValidateConnection(edge!.Source, edge.Target).IsValid.ShouldBeFalse();

        var validation = document.ValidateConnection(
            edge.Source,
            edge.Target,
            ignoredEdgeId: edge.Id);

        validation.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateConnection_still_counts_other_edges_during_reconnection()
    {
        var document = CreateConnectableDocument();
        document.AddNode(new NodeModel { Id = "replacement" }
            .AddPort(new PortModel
            {
                Id = "in",
                Kind = PortKind.Input,
                DataType = "number",
                Capacity = new PortCapacity { MaxIncoming = 1 }
            }));
        document.AddNode(new NodeModel { Id = "other-source" }
            .AddPort(new PortModel
            {
                Id = "out",
                Kind = PortKind.Output,
                DataType = "number",
                Capacity = new PortCapacity { MaxOutgoing = null }
            }));
        document.TryConnect(
            new PortReference("source", "out"),
            new PortReference("target", "in"),
            out var reconnectingEdge,
            out _).ShouldBeTrue();
        document.TryConnect(
            new PortReference("other-source", "out"),
            new PortReference("replacement", "in"),
            out _,
            out _).ShouldBeTrue();

        var validation = document.ValidateConnection(
            reconnectingEdge!.Source,
            new PortReference("replacement", "in"),
            ignoredEdgeId: reconnectingEdge.Id);

        validation.IsValid.ShouldBeFalse();
        validation.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryConnect_rejects_disabled_ports()
    {
        var document = CreateConnectableDocument();
        document.FindNode("target")!.Ports[0].State = PortState.Disabled;

        var connected = document.TryConnect(
            new PortReference("source", "out"),
            new PortReference("target", "in"),
            out var edge,
            out var validation);

        connected.ShouldBeFalse();
        edge.ShouldBeNull();
        validation.IsValid.ShouldBeFalse();
        validation.Message.ShouldNotBeNull().ShouldContain("disabled", Case.Insensitive);
    }

    [Fact]
    public void RemoveNode_removes_node_and_connected_edges()
    {
        var document = CreateConnectableDocument();
        document.TryConnect(
            new PortReference("source", "out"),
            new PortReference("target", "in"),
            out _,
            out _).ShouldBeTrue();

        var removed = document.RemoveNode("source");

        removed.ShouldBeTrue();
        document.FindNode("source").ShouldBeNull();
        document.Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Groups_can_be_found_and_node_removal_cleans_membership()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "first" });
        document.AddNode(new NodeModel { Id = "second" });
        var group = document.AddGroup(new GroupModel
        {
            Id = "inputs",
            Title = "输入",
            NodeIds = ["first", "second"]
        });

        document.FindGroup("inputs").ShouldBeSameAs(group);
        document.FindGroupContainingNode("second").ShouldBeSameAs(group);

        document.RemoveNode("first").ShouldBeTrue();
        group.NodeIds.ShouldBe(["second"]);

        document.RemoveNode("second").ShouldBeTrue();
        document.Groups.ShouldBeEmpty();
    }

    private static GraphDocument CreateConnectableDocument()
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
                DataType = "number"
            }));

        return document;
    }

    private static GraphDocument CreateCapacityDocument(PortCapacity capacity)
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "limited" }
            .AddPort(new PortModel
            {
                Id = "port",
                Kind = PortKind.InputOutput,
                Capacity = capacity
            }));
        foreach (var id in new[] { "source-a", "source-b" })
        {
            document.AddNode(new NodeModel { Id = id }
                .AddPort(new PortModel { Id = "port", Kind = PortKind.Output }));
        }

        foreach (var id in new[] { "target-a", "target-b" })
        {
            document.AddNode(new NodeModel { Id = id }
                .AddPort(new PortModel { Id = "port", Kind = PortKind.Input }));
        }

        return document;
    }

    private static EdgeModel CreateEdge(string id, string sourceNodeId, string targetNodeId) => new()
    {
        Id = id,
        Source = new PortReference(sourceNodeId, "port"),
        Target = new PortReference(targetNodeId, "port")
    };

    private static IEnumerable<NodeModel> EnumerateThenFail()
    {
        yield return new NodeModel { Id = "new" };
        throw new InvalidOperationException("测试延迟枚举失败。");
    }
}
