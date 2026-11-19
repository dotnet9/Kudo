namespace Kudo.Demo;

public static class SampleGraphs
{
    private const int LargeWorkflowColumnCount = 74;

    public const string RendererShowcaseProperty = "KudoDemoRendererShowcase";

    public const string EdgeStylesProperty = "KudoDemoEdgeStyles";

    public const string ThreeDShowcaseProperty = "KudoDemoThreeDShowcase";

    public const string LargeWorkflowProperty = "KudoDemoLargeWorkflow";

    public const string ExtensionBoundaryProperty = "KudoDemoExtensionBoundaries";

    public static NodeModel CreateInputNode(
        string id,
        GraphPoint position,
        IDemoGraphTextProvider? textProvider = null) =>
        new NodeModel
        {
            Id = id,
            Type = "input",
            DefinitionId = "input",
            Name = L(textProvider, DemoGraphTextKey.InputNode),
            Position = position,
            Size = new GraphSize(150, 80)
        }.AddPort(new PortModel
        {
            Id = "out",
            Name = "Out",
            Kind = PortKind.Output,
            DataType = "number",
            Capacity = new PortCapacity { MaxOutgoing = null }
        });

    public static NodeModel CreateTransformNode(
        string id,
        GraphPoint position,
        IDemoGraphTextProvider? textProvider = null) =>
        new NodeModel
        {
            Id = id,
            Type = "transform",
            DefinitionId = "transform",
            Name = L(textProvider, DemoGraphTextKey.TransformNode),
            Position = position,
            Size = new GraphSize(170, 96)
        }
        .AddPort(new PortModel
        {
            Id = "in",
            Name = "In",
            Kind = PortKind.Input,
            DataType = "number"
        })
        .AddPort(new PortModel
        {
            Id = "out",
            Name = "Out",
            Kind = PortKind.Output,
            DataType = "number",
            Capacity = new PortCapacity { MaxOutgoing = null }
        });

    public static NodeModel CreateOutputNode(
        string id,
        GraphPoint position,
        IDemoGraphTextProvider? textProvider = null) =>
        new NodeModel
        {
            Id = id,
            Type = "output",
            DefinitionId = "output",
            Name = L(textProvider, DemoGraphTextKey.OutputNode),
            Position = position,
            Size = new GraphSize(150, 80)
        }.AddPort(new PortModel
        {
            Id = "in",
            Name = "In",
            Kind = PortKind.Input,
            DataType = "number"
        });

    public static GraphDocument CreateBasicDocument(IDemoGraphTextProvider? textProvider = null)
    {
        var input = CreateInputNode("input", new GraphPoint(80, 120), textProvider);
        var transform = CreateTransformNode("transform", new GraphPoint(340, 100), textProvider);
        var output = CreateOutputNode("output", new GraphPoint(620, 120), textProvider);

        var document = new GraphDocument { Title = L(textProvider, DemoGraphTextKey.BasicDocument) };
        document.AddNode(input);
        document.AddNode(transform);
        document.AddNode(output);
        document.AddEdge(new EdgeModel
        {
            Id = "edge-input-transform",
            Source = new PortReference("input", "out"),
            Target = new PortReference("transform", "in"),
            Label = L(textProvider, DemoGraphTextKey.RawData),
            SourceMarker = EdgeMarker.Circle,
            TargetMarker = EdgeMarker.Arrow,
            RoutingStyle = EdgeRoutingStyle.Orthogonal
        });
        document.AddEdge(new EdgeModel
        {
            Id = "edge-transform-output",
            Source = new PortReference("transform", "out"),
            Target = new PortReference("output", "in"),
            Label = L(textProvider, DemoGraphTextKey.ProcessedResult),
            TargetMarker = EdgeMarker.Arrow,
            RoutingStyle = EdgeRoutingStyle.Orthogonal
        });

        return document;
    }

    public static GraphDocument CreateBranchingDocument(IDemoGraphTextProvider? textProvider = null)
    {
        var inputA = CreateInputNode("input-a", new GraphPoint(60, 70), textProvider);
        inputA.Name = L(textProvider, DemoGraphTextKey.RealtimeOrders);
        var inputB = CreateInputNode("input-b", new GraphPoint(60, 250), textProvider);
        inputB.Name = L(textProvider, DemoGraphTextKey.CustomerProfile);
        var transform = CreateTransformNode("merge", new GraphPoint(340, 150), textProvider);
        transform.Name = L(textProvider, DemoGraphTextKey.DataMerge);
        transform.Ports.First(port => port.Kind == PortKind.Input).Capacity =
            new PortCapacity { MaxIncoming = null };
        var riskCheck = CreateTransformNode("risk-check", new GraphPoint(570, 150), textProvider);
        riskCheck.Name = L(textProvider, DemoGraphTextKey.RuleValidation);
        var output = CreateOutputNode("output", new GraphPoint(820, 160), textProvider);
        output.Name = L(textProvider, DemoGraphTextKey.ApprovalDecision);
        output.Ports.Single().Capacity = new PortCapacity { MaxIncoming = null };

        var document = new GraphDocument { Title = L(textProvider, DemoGraphTextKey.OrderRiskControl) };
        document.AddNode(inputA);
        document.AddNode(inputB);
        document.AddNode(transform);
        document.AddNode(riskCheck);
        document.AddNode(output);
        document.AddAnnotation(new AnnotationModel
        {
            Id = "risk-review-note",
            Text = L(textProvider, DemoGraphTextKey.OrderRiskNote),
            Position = new GraphPoint(545, 20),
            Size = new GraphSize(290, 90),
            Properties = { ["purpose"] = "review" }
        });
        document.AddGroup(new GroupModel
        {
            Id = "group-inputs",
            Title = L(textProvider, DemoGraphTextKey.BusinessInputs),
            NodeIds = ["input-a", "input-b"],
            Padding = 28
        });
        document.AddEdge(new EdgeModel
        {
            Id = "edge-input-a",
            Source = new PortReference("input-a", "out"),
            Target = new PortReference("merge", "in"),
            Label = L(textProvider, DemoGraphTextKey.OrderData),
            TargetMarker = EdgeMarker.Arrow,
            RoutingStyle = EdgeRoutingStyle.Bezier
        });
        document.AddEdge(new EdgeModel
        {
            Id = "edge-input-b",
            Source = new PortReference("input-b", "out"),
            Target = new PortReference("merge", "in"),
            Label = L(textProvider, DemoGraphTextKey.ProfileFeatures),
            SourceMarker = EdgeMarker.Diamond,
            TargetMarker = EdgeMarker.Arrow,
            RoutingStyle = EdgeRoutingStyle.Orthogonal
        });
        document.AddEdge(new EdgeModel
        {
            Id = "edge-risk-check",
            Source = new PortReference("merge", "out"),
            Target = new PortReference("risk-check", "in"),
            Label = L(textProvider, DemoGraphTextKey.RiskFeatures),
            TargetMarker = EdgeMarker.Arrow,
            RoutingStyle = EdgeRoutingStyle.Bezier
        });
        document.AddEdge(new EdgeModel
        {
            Id = "edge-risk-result",
            Source = new PortReference("risk-check", "out"),
            Target = new PortReference("output", "in"),
            Label = L(textProvider, DemoGraphTextKey.RiskConclusion),
            TargetMarker = EdgeMarker.Arrow,
            RoutingStyle = EdgeRoutingStyle.Direct
        });
        document.AddEdge(new EdgeModel
        {
            Id = "edge-output",
            Source = new PortReference("merge", "out"),
            Target = new PortReference("output", "in"),
            Label = L(textProvider, DemoGraphTextKey.OrderContext),
            TargetMarker = EdgeMarker.Arrow,
            RoutingStyle = EdgeRoutingStyle.SmartOrthogonal
        });
        return document;
    }

    public static GraphDocument CreateRendererShowcaseDocument(IDemoGraphTextProvider? textProvider = null)
    {
        var camera = CreateInputNode("camera", new GraphPoint(70, 170), textProvider);
        camera.Name = L(textProvider, DemoGraphTextKey.IndustrialCamera);
        camera.Ports[0].DataType = "image";
        camera.Ports[0].Name = "Frame";

        var denoise = CreateTransformNode("denoise", new GraphPoint(330, 90), textProvider);
        denoise.Name = L(textProvider, DemoGraphTextKey.ImageDenoising);
        denoise.Ports.ForEach(port => port.DataType = "image");

        var inspect = CreateTransformNode("inspect", new GraphPoint(580, 90), textProvider);
        inspect.Name = L(textProvider, DemoGraphTextKey.DefectInspection);
        inspect.Ports[0].DataType = "image";
        inspect.Ports[1].DataType = "result";

        var archive = CreateOutputNode("archive", new GraphPoint(830, 40), textProvider);
        archive.Name = L(textProvider, DemoGraphTextKey.ResultArchive);
        archive.Ports[0].DataType = "result";

        var alarm = CreateOutputNode("alarm", new GraphPoint(830, 230), textProvider);
        alarm.Name = L(textProvider, DemoGraphTextKey.AlarmIntegration);
        alarm.Ports[0].DataType = "result";
        inspect.Ports[1].Capacity = new PortCapacity { MaxOutgoing = null };

        var document = new GraphDocument { Title = L(textProvider, DemoGraphTextKey.RendererDocument) };
        document.Properties[RendererShowcaseProperty] = true;
        document.AddNode(camera);
        document.AddNode(denoise);
        document.AddNode(inspect);
        document.AddNode(archive);
        document.AddNode(alarm);
        document.AddGroup(new GroupModel
        {
            Id = "preprocess",
            Title = L(textProvider, DemoGraphTextKey.PreprocessingInference),
            NodeIds = ["denoise", "inspect"],
            Padding = 30
        });
        document.AddEdge(new EdgeModel
        {
            Id = "camera-denoise",
            Source = new PortReference("camera", "out"),
            Target = new PortReference("denoise", "in"),
            Label = L(textProvider, DemoGraphTextKey.RawFrame),
            TargetMarker = EdgeMarker.Arrow,
            RoutingStyle = EdgeRoutingStyle.Bezier
        });
        document.AddEdge(new EdgeModel
        {
            Id = "denoise-inspect",
            Source = new PortReference("denoise", "out"),
            Target = new PortReference("inspect", "in"),
            Label = L(textProvider, DemoGraphTextKey.EnhancedImage),
            TargetMarker = EdgeMarker.Arrow,
            RoutingStyle = EdgeRoutingStyle.Direct
        });
        document.AddEdge(new EdgeModel
        {
            Id = "inspect-archive",
            Source = new PortReference("inspect", "out"),
            Target = new PortReference("archive", "in"),
            Label = L(textProvider, DemoGraphTextKey.InspectionResult),
            TargetMarker = EdgeMarker.Arrow,
            RoutingStyle = EdgeRoutingStyle.SmartOrthogonal
        });
        document.AddEdge(new EdgeModel
        {
            Id = "inspect-alarm",
            Source = new PortReference("inspect", "out"),
            Target = new PortReference("alarm", "in"),
            Label = L(textProvider, DemoGraphTextKey.DefectEvent),
            TargetMarker = EdgeMarker.Diamond,
            RoutingStyle = EdgeRoutingStyle.SmartOrthogonal
        });
        return document;
    }

    public static GraphDocument CreateEdgeStylesDocument(IDemoGraphTextProvider? textProvider = null)
    {
        var document = new GraphDocument
        {
            Title = L(textProvider, DemoGraphTextKey.EdgeStylesDocument),
            Properties = { [EdgeStylesProperty] = true }
        };
        var flowNodeIds = AddEdgeStyleColumn(
            document,
            "state",
            70,
            [
                new EdgeStyleSample(
                    "success",
                    "Success · Direct",
                    EdgeRoutingStyle.Direct,
                    GraphEdgeStylePreset.Success),
                new EdgeStyleSample(
                    "warning",
                    "Warning · Bezier",
                    EdgeRoutingStyle.Bezier,
                    GraphEdgeStylePreset.Warning),
                new EdgeStyleSample(
                    "error",
                    "Error · Orthogonal",
                    EdgeRoutingStyle.Orthogonal,
                    GraphEdgeStylePreset.Error),
                new EdgeStyleSample(
                    "disabled",
                    "Disabled · Smart",
                    EdgeRoutingStyle.SmartOrthogonal,
                    GraphEdgeStylePreset.Disabled),
                new EdgeStyleSample(
                    "highlighted",
                    "Highlighted · Manual",
                    EdgeRoutingStyle.Manual,
                    GraphEdgeStylePreset.Highlighted,
                    [new GraphPoint(255, 600)]),
                new EdgeStyleSample(
                    "custom",
                    "Custom pattern · Glow",
                    EdgeRoutingStyle.Bezier,
                    GraphEdgeStylePreset.Custom,
                    Appearance: new GraphEdgeAppearance
                    {
                        StrokeColor = "#DC2626",
                        StrokeWidth = 3,
                        DashStyle = GraphEdgeDashStyle.Custom,
                        CustomDashPattern = [12, 3, 2, 3],
                        GlowColor = "#DC2626",
                        GlowIntensity = 0.35
                    })
            ]);
        var effectsNodeIds = AddEdgeStyleColumn(
            document,
            "animation",
            620,
            [
                new EdgeStyleSample(
                    "active-flow",
                    "ActiveFlow · Forward",
                    EdgeRoutingStyle.Bezier,
                    GraphEdgeStylePreset.ActiveFlow),
                new EdgeStyleSample(
                    "reverse-flow",
                    "ReverseFlow · Smart",
                    EdgeRoutingStyle.SmartOrthogonal,
                    GraphEdgeStylePreset.ReverseFlow),
                new EdgeStyleSample(
                    "bidirectional",
                    "Bidirectional · Flow",
                    EdgeRoutingStyle.Direct,
                    GraphEdgeStylePreset.Bidirectional,
                    SourceMarker: EdgeMarker.Circle,
                    TargetMarker: EdgeMarker.Diamond),
                new EdgeStyleSample(
                    "rainbow",
                    "Rainbow",
                    EdgeRoutingStyle.Bezier,
                    GraphEdgeStylePreset.Rainbow),
                new EdgeStyleSample(
                    "pulse",
                    "Pulse · Heartbeat",
                    EdgeRoutingStyle.Orthogonal,
                    GraphEdgeStylePreset.Pulse),
                new EdgeStyleSample(
                    "signal",
                    "Signal packets",
                    EdgeRoutingStyle.Bezier,
                    GraphEdgeStylePreset.Signal)
            ]);
        document.AddGroup(new GroupModel
        {
            Id = "state-presets",
            Title = "State presets",
            NodeIds = flowNodeIds,
            Padding = 28
        });
        document.AddGroup(new GroupModel
        {
            Id = "animation-presets",
            Title = "Animation presets",
            NodeIds = effectsNodeIds,
            Padding = 28
        });
        return document;
    }

    public static GraphDocument CreateThreeDDocument(IDemoGraphTextProvider? textProvider = null)
    {
        var document = CreateBasicDocument(textProvider);
        document.Title = L(textProvider, DemoGraphTextKey.ThreeDDocument);
        document.Properties[ThreeDShowcaseProperty] = true;
        var transform = document.FindNode("transform");
        if (transform is not null)
        {
            transform.Size = new GraphSize(250, 190);
            transform.Position = new GraphPoint(335, 70);
        }

        return document;
    }

    public static GraphDocument CreateLargeWorkflowDocument(
        int nodeCount = 5000,
        IDemoGraphTextProvider? textProvider = null)
    {
        if (nodeCount < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeCount),
                L(textProvider, DemoGraphTextKey.LargeWorkflowMinimumTwo));
        }

        const double horizontalSpacing = 142;
        const double verticalSpacing = 86;
        var stages = CreateLargeWorkflowStages(textProvider);
        var nodes = new List<NodeModel>(nodeCount);
        for (var index = 0; index < nodeCount; index++)
        {
            var column = index % LargeWorkflowColumnCount;
            var row = index / LargeWorkflowColumnCount;
            var stage = stages[column % stages.Length];
            nodes.Add(new NodeModel
            {
                Id = $"pipeline-node-{index:D5}",
                Type = $"pipeline-{column % stages.Length}",
                Name = $"{stage} {index + 1}",
                Position = new GraphPoint(column * horizontalSpacing, row * verticalSpacing),
                Size = new GraphSize(112, 54),
                Properties =
                {
                    ["stage"] = stage,
                    ["pipelineRow"] = row + 1,
                    ["sequence"] = index + 1
                }
            }
            .AddPort(new PortModel
            {
                Id = "in",
                Name = "In",
                Kind = PortKind.Input,
                DataType = "telemetry",
                Capacity = new PortCapacity { MaxIncoming = null }
            })
            .AddPort(new PortModel
            {
                Id = "out",
                Name = "Out",
                Kind = PortKind.Output,
                DataType = "telemetry",
                Capacity = new PortCapacity { MaxOutgoing = null }
            }));
        }

        var edges = new List<EdgeModel>(nodeCount + nodeCount / 10);
        for (var index = 1; index < nodeCount; index++)
        {
            var isRowStart = index % LargeWorkflowColumnCount == 0;
            var sourceIndex = isRowStart ? index - LargeWorkflowColumnCount : index - 1;
            edges.Add(CreateLargeWorkflowEdge(
                edges.Count,
                nodes[sourceIndex],
                nodes[index],
                isRowStart ? EdgeRoutingStyle.Orthogonal : EdgeRoutingStyle.Direct));
        }

        for (var rowStart = 0;
             rowStart + LargeWorkflowColumnCount < nodeCount;
             rowStart += LargeWorkflowColumnCount)
        {
            var nextRowStart = rowStart + LargeWorkflowColumnCount;
            for (var column = 12; column < LargeWorkflowColumnCount; column += 12)
            {
                var sourceIndex = rowStart + column;
                var targetIndex = nextRowStart + column;
                if (targetIndex >= nodeCount)
                {
                    break;
                }

                edges.Add(CreateLargeWorkflowEdge(
                    edges.Count,
                    nodes[sourceIndex],
                    nodes[targetIndex],
                    EdgeRoutingStyle.Bezier));
            }
        }

        var document = new GraphDocument
        {
            Title = F(textProvider, DemoGraphTextKey.LargeWorkflowTitle, nodeCount),
            Properties =
            {
                [LargeWorkflowProperty] = true,
                ["businessScenario"] = L(textProvider, DemoGraphTextKey.BusinessScenario),
                ["layoutColumns"] = LargeWorkflowColumnCount
            }
        };
        document.AddNodes(nodes);
        document.AddEdges(edges);
        return document;
    }

    private static string[] CreateLargeWorkflowStages(IDemoGraphTextProvider? textProvider) =>
    [
        L(textProvider, DemoGraphTextKey.StageDataAcquisition),
        L(textProvider, DemoGraphTextKey.StageProtocolParsing),
        L(textProvider, DemoGraphTextKey.StageDataCleaning),
        L(textProvider, DemoGraphTextKey.StageFeatureComputation),
        L(textProvider, DemoGraphTextKey.StageRuleEvaluation),
        L(textProvider, DemoGraphTextKey.StageEventRouting),
        L(textProvider, DemoGraphTextKey.StageMetricAggregation),
        L(textProvider, DemoGraphTextKey.StageStorageOutput)
    ];

    private static string L(IDemoGraphTextProvider? textProvider, DemoGraphTextKey key) =>
        DemoGraphText.Get(textProvider, key);

    private static string F(
        IDemoGraphTextProvider? textProvider,
        DemoGraphTextKey key,
        params object?[] arguments) =>
        DemoGraphText.Format(textProvider, key, arguments);

    private static EdgeModel CreateLargeWorkflowEdge(
        int index,
        NodeModel source,
        NodeModel target,
        EdgeRoutingStyle routingStyle) =>
        new()
        {
            Id = $"pipeline-edge-{index:D5}",
            Source = new PortReference(source.Id, "out"),
            Target = new PortReference(target.Id, "in"),
            TargetMarker = EdgeMarker.Arrow,
            RoutingStyle = routingStyle
        };

    private static List<string> AddEdgeStyleColumn(
        GraphDocument document,
        string prefix,
        double x,
        IReadOnlyList<EdgeStyleSample> samples)
    {
        var nodeIds = new List<string>(samples.Count * 2);
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            var y = 70 + index * 112;
            var sourceId = $"{prefix}-{sample.Id}-source";
            var targetId = $"{prefix}-{sample.Id}-target";
            document.AddNode(CreateEdgeStyleNode(sourceId, sample.Label, new GraphPoint(x, y), true));
            document.AddNode(CreateEdgeStyleNode(targetId, "Target", new GraphPoint(x + 300, y), false));
            document.AddEdge(new EdgeModel
            {
                Id = $"{prefix}-{sample.Id}",
                Source = new PortReference(sourceId, "out"),
                Target = new PortReference(targetId, "in"),
                Label = sample.Label,
                RoutingStyle = sample.RoutingStyle,
                StylePreset = sample.StylePreset,
                Waypoints = sample.Waypoints ?? [],
                SourceMarker = sample.SourceMarker,
                TargetMarker = sample.TargetMarker
            });
            if (sample.Appearance is { } appearance)
            {
                document.Edges[^1].Appearance = appearance;
            }

            nodeIds.Add(sourceId);
            nodeIds.Add(targetId);
        }

        return nodeIds;
    }

    private static NodeModel CreateEdgeStyleNode(
        string id,
        string title,
        GraphPoint position,
        bool isSource) =>
        new NodeModel
        {
            Id = id,
            Type = isSource ? "style-source" : "style-target",
            Name = title,
            Position = position,
            Size = new GraphSize(132, 58)
        }.AddPort(new PortModel
        {
            Id = isSource ? "out" : "in",
            Name = isSource ? "Out" : "In",
            Kind = isSource ? PortKind.Output : PortKind.Input,
            DataType = "signal",
            Capacity = isSource
                ? new PortCapacity { MaxOutgoing = null }
                : new PortCapacity { MaxIncoming = null }
        });

    private sealed record EdgeStyleSample(
        string Id,
        string Label,
        EdgeRoutingStyle RoutingStyle,
        GraphEdgeStylePreset StylePreset,
        List<GraphPoint>? Waypoints = null,
        EdgeMarker SourceMarker = EdgeMarker.None,
        EdgeMarker TargetMarker = EdgeMarker.Arrow,
        GraphEdgeAppearance? Appearance = null);
}
