using Kudo;
using Kudo.Core.Serialization;
using Shouldly;

namespace Kudo.Core.Tests.Serialization;

public class GraphTaskXmlSerializerTests
{
    [Fact]
    public void Roundtrip_preserves_nodes_edges_groups_and_annotations()
    {
        var document = new GraphDocument
        {
            Title = "Roundtrip",
            CreatedBy = "tester",
            CreatedAt = new DateTime(2026, 9, 22, 14, 30, 0),
            Description = "任务说明"
        };
        document.CanvasSettings = new GraphCanvasSettings
        {
            Grid = new GraphGridSettings { IsSnapEnabled = true, Spacing = 24 },
            IsMinimapVisible = false,
            ShowPorts = false,
            PortLabelVisibility = GraphLabelVisibility.SelectedOrHovered,
            SymbolNameVisibility = GraphLabelVisibility.Always,
            SymbolDescriptionVisibility = GraphLabelVisibility.Hovered
        };
        document.AddNode(new NodeModel
        {
            Id = "a",
            DefinitionId = "PUMP",
            Number = "001",
            Name = "图元 A",
            Description = "图元 A 的说明",
            IsLocked = true,
            Rotation = 32.5,
            Ports =
            [
                new PortModel
                {
                    Id = "reference",
                    Kind = PortKind.Reference,
                    IsVisible = false,
                    Anchor = new GraphPoint(1, 0.5),
                    IncomingDirection = new GraphPoint(-2, 0),
                    OutgoingDirection = new GraphPoint(3, 4),
                    DataType = "table",
                    Capacity = new PortCapacity
                    {
                        MaxIncoming = 2,
                        MaxOutgoing = null,
                        MaxTotal = 5
                    }
                }
            ],
            Appearance = new GraphNodeAppearance
            {
                VisualMode = GraphNodeVisualMode.Symbol,
                BackgroundImage = "Assets/a.png",
                Stretch = GraphImageStretch.Uniform,
                Opacity = 0.45,
                TextAppearance = new GraphTextAppearance
                {
                    Foreground = "#102030",
                    FontSize = 18,
                    FontWeight = GraphFontWeight.SemiBold
                },
                PortLabelVisibility = GraphLabelVisibility.Hovered,
                NameVisibility = GraphLabelVisibility.Always,
                DescriptionVisibility = GraphLabelVisibility.Never
            }
        });
        document.AddNode(new NodeModel { Id = "b" });
        document.AddEdge(new EdgeModel
        {
            Id = "edge",
            RelationKind = GraphRelationKind.Reference,
            Source = new PortReference("a", "out"),
            Target = new PortReference("b", "in"),
            RoutingStyle = EdgeRoutingStyle.SmartOrthogonal,
            Waypoints = [new GraphPoint(120, 40), new GraphPoint(180, 80)],
            SourceMarker = EdgeMarker.Circle,
            TargetMarker = EdgeMarker.Arrow,
            Label = "处理结果",
            Appearance = new GraphEdgeAppearance
            {
                StrokeColor = "#2563EB",
                StrokeWidth = 3,
                DashStyle = GraphEdgeDashStyle.DashDot,
                Animation = GraphEdgeAnimationKind.Flow,
                AnimationSpeed = 1.5,
                GlowIntensity = 0.4
            }
        });
        document.AddGroup(new GroupModel
        {
            Id = "inputs",
            Title = "输入信号",
            NodeIds = ["a", "b"],
            Padding = 32,
            IsCollapsed = true
        });
        document.AddAnnotation(new AnnotationModel
        {
            Id = "review-note",
            Text = "发布前复核",
            Position = new GraphPoint(40, 260),
            Size = new GraphSize(260, 140),
            Properties = { ["severity"] = "warning" }
        });

        var serializer = new GraphTaskXmlSerializer();
        var xml = serializer.Serialize(document);
        var restored = serializer.Deserialize(xml);

        restored.Title.ShouldBe("Roundtrip");
        restored.CreatedBy.ShouldBe("tester");
        restored.CreatedAt.ShouldBe(new DateTime(2026, 9, 22, 14, 30, 0));
        restored.Description.ShouldBe("任务说明");
        restored.Nodes.Count.ShouldBe(2);
        restored.Format.ShouldBe("Kudo.Task/1");
        restored.SchemaVersion.ShouldBe("1.0");
        restored.CanvasSettings.Grid.IsSnapEnabled.ShouldBeTrue();
        restored.CanvasSettings.Grid.Spacing.ShouldBe(24);
        restored.CanvasSettings.IsMinimapVisible.ShouldBeFalse();
        restored.CanvasSettings.ShowPorts.ShouldBeFalse();
        restored.CanvasSettings.PortLabelVisibility.ShouldBe(GraphLabelVisibility.SelectedOrHovered);
        restored.CanvasSettings.SymbolNameVisibility.ShouldBe(GraphLabelVisibility.Always);
        restored.CanvasSettings.SymbolDescriptionVisibility.ShouldBe(GraphLabelVisibility.Hovered);
        restored.Nodes[0].Appearance.BackgroundImage.ShouldBe("Assets/a.png");
        restored.Nodes[0].Appearance.VisualMode.ShouldBe(GraphNodeVisualMode.Symbol);
        restored.Nodes[0].Rotation.ShouldBe(32.5);
        restored.Nodes[0].IsLocked.ShouldBeTrue();
        restored.Nodes[0].Appearance.Stretch.ShouldBe(GraphImageStretch.Uniform);
        restored.Nodes[0].Appearance.Opacity.ShouldBe(0.45);
        restored.Nodes[0].Appearance.TextAppearance.Foreground.ShouldBe("#102030");
        restored.Nodes[0].Appearance.TextAppearance.FontSize.ShouldBe(18);
        restored.Nodes[0].Appearance.TextAppearance.FontWeight.ShouldBe(GraphFontWeight.SemiBold);
        restored.Nodes[0].Appearance.PortLabelVisibility.ShouldBe(GraphLabelVisibility.Hovered);
        restored.Nodes[0].Appearance.NameVisibility.ShouldBe(GraphLabelVisibility.Always);
        restored.Nodes[0].Appearance.DescriptionVisibility.ShouldBe(GraphLabelVisibility.Never);
        restored.Nodes[0].Name.ShouldBe("图元 A");
        restored.Nodes[0].DefinitionId.ShouldBe("PUMP");
        restored.Nodes[0].Number.ShouldBe("001");
        restored.Nodes[0].Description.ShouldBe("图元 A 的说明");
        var restoredPort = restored.Nodes[0].Ports.Single();
        restoredPort.Kind.ShouldBe(PortKind.Reference);
        restoredPort.IsVisible.ShouldBeFalse();
        restoredPort.IncomingDirection.ShouldBe(new GraphPoint(-1, 0));
        restoredPort.OutgoingDirection.ShouldBe(new GraphPoint(0.6, 0.8));
        restoredPort.Capacity.ShouldBe(new PortCapacity
        {
            MaxIncoming = 2,
            MaxOutgoing = null,
            MaxTotal = 5
        });
        var restoredEdge = restored.Edges.Single();
        restoredEdge.Id.ShouldBe("edge");
        restoredEdge.RelationKind.ShouldBe(GraphRelationKind.Reference);
        restoredEdge.RoutingStyle.ShouldBe(EdgeRoutingStyle.SmartOrthogonal);
        restoredEdge.Waypoints.ShouldBe([new GraphPoint(120, 40), new GraphPoint(180, 80)]);
        restoredEdge.SourceMarker.ShouldBe(EdgeMarker.Circle);
        restoredEdge.TargetMarker.ShouldBe(EdgeMarker.Arrow);
        restoredEdge.Label.ShouldBe("处理结果");
        restoredEdge.Appearance.StrokeColor.ShouldBe("#2563EB");
        restoredEdge.Appearance.StrokeWidth.ShouldBe(3);
        restoredEdge.Appearance.DashStyle.ShouldBe(GraphEdgeDashStyle.DashDot);
        restoredEdge.Appearance.Animation.ShouldBe(GraphEdgeAnimationKind.Flow);
        restoredEdge.Appearance.AnimationSpeed.ShouldBe(1.5);
        restoredEdge.Appearance.GlowIntensity.ShouldBe(0.4);
        restoredEdge.StylePreset.ShouldBe(GraphEdgeStylePreset.Custom);
        var restoredGroup = restored.Groups.Single();
        restoredGroup.Id.ShouldBe("inputs");
        restoredGroup.Title.ShouldBe("输入信号");
        restoredGroup.NodeIds.ShouldBe(["a", "b"]);
        restoredGroup.Padding.ShouldBe(32);
        restoredGroup.IsCollapsed.ShouldBeTrue();
        var restoredAnnotation = restored.Annotations.Single();
        restoredAnnotation.Id.ShouldBe("review-note");
        restoredAnnotation.Text.ShouldBe("发布前复核");
        restoredAnnotation.Position.ShouldBe(new GraphPoint(40, 260));
        restoredAnnotation.Size.ShouldBe(new GraphSize(260, 140));
        restoredAnnotation.Properties["severity"].ShouldBe("warning");
    }

    [Fact]
    public void Roundtrip_preserves_declarative_edge_style_preset()
    {
        var document = new GraphDocument();
        document.AddEdge(new EdgeModel
        {
            Id = "active-edge",
            StylePreset = GraphEdgeStylePreset.ActiveFlow
        });
        var serializer = new GraphTaskXmlSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(document));

        var edge = restored.Edges.Single();
        edge.StylePreset.ShouldBe(GraphEdgeStylePreset.ActiveFlow);
        edge.Appearance.ShouldBe(GraphEdgeAppearance.ActiveFlow);
    }

    [Fact]
    public void Payload_without_current_task_format_is_rejected()
    {
        var serializer = new GraphTaskXmlSerializer();

        var exception = Should.Throw<GraphSerializationException>(() => serializer.Deserialize("""
            <Task Id="unsupported" />
            """));

        exception.Message.ShouldContain("Unsupported task format");
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData(null)]
    public void Payload_with_unsupported_or_null_schema_version_is_rejected(string? schemaVersion)
    {
        var serializer = new GraphTaskXmlSerializer();
        var schemaAttribute = schemaVersion is null
            ? string.Empty
            : $" SchemaVersion=\"{schemaVersion}\"";

        var exception = Should.Throw<GraphSerializationException>(() => serializer.Deserialize(
            $"<Task Format=\"Kudo.Task/1\"{schemaAttribute} />"));

        exception.Message.ShouldContain("Unsupported schema version");
    }

    [Fact]
    public void Null_nested_collections_are_normalized_on_load()
    {
        var serializer = new GraphTaskXmlSerializer();

        var restored = serializer.Deserialize("""
            <Task Format="Kudo.Task/1" SchemaVersion="1.0">
              <Nodes>
                <Node Id="node">
                  <Ports><Port Id="out" /></Ports>
                </Node>
              </Nodes>
              <Groups><Group Id="group" /></Groups>
              <Edges><Edge Id="edge" /></Edges>
              <Annotations><Annotation Id="note" /></Annotations>
            </Task>
            """);

        restored.Properties.ShouldNotBeNull();
        restored.Nodes[0].Properties.ShouldNotBeNull();
        restored.Nodes[0].Ports[0].Properties.ShouldNotBeNull();
        restored.Groups[0].NodeIds.ShouldNotBeNull();
        restored.Groups[0].Properties.ShouldNotBeNull();
        restored.Edges[0].Waypoints.ShouldNotBeNull();
        restored.Edges[0].Appearance.ShouldNotBeNull();
        restored.Edges[0].Properties.ShouldNotBeNull();
        restored.Annotations[0].Properties.ShouldNotBeNull();
    }

    [Fact]
    public void Task_asset_paths_cannot_escape_the_assets_directory()
    {
        var serializer = new GraphTaskXmlSerializer();
        var document = new GraphDocument();
        document.AddNode(new NodeModel
        {
            Id = "unsafe",
            Appearance = new GraphNodeAppearance { BackgroundImage = "Assets/../outside.png" }
        });

        var exception = Should.Throw<GraphSerializationException>(() => serializer.Serialize(document));

        exception.Message.ShouldContain("invalid task asset path");
    }

    [Fact]
    public async Task Async_roundtrip_preserves_custom_properties()
    {
        var document = new GraphDocument { Title = "异步保存" };
        document.Properties["enabled"] = true;
        var serializer = new GraphTaskXmlSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(stream, document, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var restored = await serializer.LoadAsync(stream, TestContext.Current.CancellationToken);

        restored.Title.ShouldBe("异步保存");
        restored.Properties["enabled"].ShouldBe(true);
    }

    [Fact]
    public void Roundtrip_supports_integer_and_nested_extension_properties()
    {
        var document = new GraphDocument();
        document.Properties["layoutColumns"] = 74;
        document.AddNode(new NodeModel
        {
            Id = "pipeline-node",
            Properties =
            {
                ["pipelineRow"] = 1,
                ["sequence"] = 5000,
                ["metadata"] = new Dictionary<string, object?>
                {
                    ["retries"] = 3,
                    ["samples"] = new List<object?> { 1, 2L, 0.5 }
                }
            }
        });
        var serializer = new GraphTaskXmlSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(document));

        restored.Properties["layoutColumns"].ShouldBe(74);
        var properties = restored.Nodes.Single().Properties;
        properties["pipelineRow"].ShouldBe(1);
        properties["sequence"].ShouldBe(5000);
        var metadata = properties["metadata"].ShouldBeOfType<Dictionary<string, object?>>();
        metadata["retries"].ShouldBe(3);
        metadata["samples"].ShouldBeOfType<List<object?>>().ShouldBe([1, 2, 0.5]);
    }

    [Fact]
    public void Serialize_keeps_chinese_text_readable()
    {
        var document = new GraphDocument { Title = "中文任务" };
        document.AddEdge(new EdgeModel { Id = "edge", Label = "处理结果" });
        var serializer = new GraphTaskXmlSerializer();

        var xml = serializer.Serialize(document);

        xml.ShouldContain("中文任务");
        xml.ShouldContain("处理结果");
    }

    [Fact]
    public void Xml_extension_properties_are_normalized_to_clr_values_on_load()
    {
        var serializer = new GraphTaskXmlSerializer();

        var restored = serializer.Deserialize("""
            <Task Format="Kudo.Task/1" SchemaVersion="1.0">
              <Nodes>
                <Node Id="node">
                  <Properties>
                    <Property Name="Locked" Type="Boolean">true</Property>
                    <Property Name="Priority" Type="Integer">3</Property>
                    <Property Name="Ratio" Type="Double">0.75</Property>
                    <Property Name="Tags" Type="Array">
                      <Item Type="String">demo</Item>
                      <Item Type="String">xml</Item>
                    </Property>
                    <Property Name="Metadata" Type="Object">
                      <Property Name="Owner" Type="String">Kudo</Property>
                    </Property>
                  </Properties>
                </Node>
              </Nodes>
              <Properties>
                <Property Name="Sample" Type="String">中文任务</Property>
              </Properties>
            </Task>
            """);

        restored.Properties["Sample"].ShouldBe("中文任务");
        var properties = restored.Nodes.Single().Properties;
        properties["Locked"].ShouldBe(true);
        properties["Priority"].ShouldBe(3);
        properties["Ratio"].ShouldBe(0.75);
        properties["Tags"].ShouldBeOfType<List<object?>>()
            .ShouldBe(["demo", "xml"]);
        properties["Metadata"].ShouldBeOfType<Dictionary<string, object?>>()
            ["Owner"].ShouldBe("Kudo");
    }

    [Fact]
    public void Serialize_uses_pascal_attribute_names_and_string_enums()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel
        {
            Appearance = new GraphNodeAppearance { VisualMode = GraphNodeVisualMode.Text }
        });
        var serializer = new GraphTaskXmlSerializer(new GraphSerializationOptions(false));

        var xml = serializer.Serialize(document);

        xml.ShouldContain("Format=\"Kudo.Task/1\"");
        xml.ShouldContain("SchemaVersion=\"1.0\"");
        xml.ShouldContain("<CanvasSettings");
        xml.ShouldContain("VisualMode=\"Text\"");
        xml.Contains("format=", StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void Lowercase_envelope_attributes_are_rejected()
    {
        var serializer = new GraphTaskXmlSerializer();

        var exception = Should.Throw<GraphSerializationException>(() => serializer.Deserialize("""
            <Task format="Kudo.Task/1" schemaVersion="1.0" />
            """));

        exception.Message.ShouldContain("Unsupported task format");
    }
}
