using Kudo.Avalonia.Designer;
using Kudo.Core.Documents;
using Kudo.Core.Serialization;
using Shouldly;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class ConnectionValidationIntegrationTests
{
    [Fact]
    public void Canvas_rule_is_used_by_validation_and_reconnection()
    {
        var document = CreateDocument(includeEdge: true);
        var rule = new RejectConnectionRule();
        var canvas = new GraphCanvas
        {
            Document = document,
            ConnectionValidationRule = rule
        };

        var validation = canvas.ValidateConnection(Source, ReplacementTarget);
        var reconnected = canvas.ReconnectEdge("edge", Source, ReplacementTarget);

        validation.IsValid.ShouldBeFalse();
        reconnected.ShouldBeFalse();
        document.Edges.Single().Target.ShouldBe(Target);
        canvas.LastConnectionValidation?.Message.ShouldBe("宿主拒绝");
        rule.CallCount.ShouldBe(2);
    }

    [Fact]
    public void Connection_preview_uses_the_same_rule_before_snapping()
    {
        var document = CreateDocument();
        var sourceNode = document.FindNode(Source.NodeId)!;
        var sourcePort = document.FindPort(Source)!;
        var targetNode = document.FindNode(Target.NodeId)!;
        var targetPort = document.FindPort(Target)!;
        var rule = new RejectConnectionRule();
        var canvas = new GraphCanvas
        {
            Document = document,
            ConnectionValidationRule = rule
        };
        var start = new PortHitResult(
            sourceNode,
            sourcePort,
            Source,
            canvas.GetPortScreenPosition(sourceNode, sourcePort));

        var candidate = canvas.FindCompatibleConnectionCandidate(
            canvas.GetPortScreenPosition(targetNode, targetPort),
            start);

        candidate.ShouldBeNull();
        rule.CallCount.ShouldBe(1);
    }

    [Fact]
    public void Programmatic_designer_connection_uses_the_canvas_rule()
    {
        var document = CreateDocument();
        using var view = new GraphDocumentView(new GraphDocumentSession(document));
        var rule = new RejectConnectionRule();
        view.ConnectionValidationRule = rule;

        var connected = view.EditorContext.Connections.Connect(Source, Target);

        connected.ShouldBeFalse();
        document.Edges.ShouldBeEmpty();
        view.Canvas.LastConnectionValidation?.Message.ShouldBe("宿主拒绝");
        rule.CallCount.ShouldBe(1);
    }

    [Fact]
    public void Clipboard_paste_filters_edges_with_the_same_rule()
    {
        var sourceDocument = CreateDocument(includeEdge: true);
        var payload = new GraphClipboardPayload
        {
            Nodes = sourceDocument.Nodes,
            Edges = sourceDocument.Edges
        };
        var text = new GraphClipboardJsonSerializer().Serialize(payload);
        var rule = new RejectConnectionRule();
        var service = new GraphClipboardService();

        var created = service.TryCreatePastePlan(
            new GraphDocument(),
            text,
            GraphPoint.Zero,
            out var plan,
            connectionValidationRule: rule,
            validationTextProvider: GraphCanvas.ConnectionValidationTextProvider);

        created.ShouldBeTrue();
        plan.ShouldNotBeNull().NodeCount.ShouldBe(3);
        plan.EdgeCount.ShouldBe(0);
        rule.CallCount.ShouldBe(1);
    }

    [Fact]
    public void Clipboard_paste_at_aligns_the_payload_top_left()
    {
        var source = new GraphDocument
        {
            Nodes =
            [
                new NodeModel
                {
                    Id = "source",
                    Position = new GraphPoint(100, 140),
                    Size = new GraphSize(120, 80),
                    Ports = []
                },
                new NodeModel
                {
                    Id = "source-2",
                    Position = new GraphPoint(260, 220),
                    Size = new GraphSize(80, 60),
                    Ports = []
                }
            ]
        };
        var text = new GraphClipboardJsonSerializer().Serialize(new GraphClipboardPayload
        {
            Nodes = source.Nodes
        });
        var target = new GraphDocument();
        var service = new GraphClipboardService();

        service.TryCreatePastePlanAt(target, text, new GraphPoint(500, 600), out var plan)
            .ShouldBeTrue();
        plan.ShouldNotBeNull();
        plan.Command.Execute(target);

        target.FindNode(plan.SelectedNodeIds[0])!.Position.ShouldBe(new GraphPoint(500, 600));
        target.Nodes.OrderBy(node => node.Position.X).First().Position.ShouldBe(new GraphPoint(500, 600));
        target.Nodes.OrderBy(node => node.Position.X).Last().Position.ShouldBe(new GraphPoint(660, 680));
    }

    [Fact]
    public void Clipboard_paste_at_moves_internal_edge_waypoints_with_the_selection()
    {
        var source = CreateDocument(includeEdge: true);
        source.Edges[0].Waypoints = [new GraphPoint(-40, -20)];
        var text = new GraphClipboardJsonSerializer().Serialize(new GraphClipboardPayload
        {
            Nodes = source.Nodes.Take(2).ToList(),
            Edges = source.Edges
        });
        var target = new GraphDocument();
        var service = new GraphClipboardService();

        service.TryCreatePastePlanAt(target, text, new GraphPoint(500, 600), out var plan)
            .ShouldBeTrue();
        plan.ShouldNotBeNull();
        plan.Command.Execute(target);

        target.Edges.ShouldHaveSingleItem().Waypoints.ShouldHaveSingleItem()
            .ShouldBe(new GraphPoint(500, 600));
        target.Nodes.Min(node => node.Position.X).ShouldBe(540);
        target.Nodes.Min(node => node.Position.Y).ShouldBe(620);
    }

    private static readonly PortReference Source = new("source", "out");
    private static readonly PortReference Target = new("target", "in");
    private static readonly PortReference ReplacementTarget = new("replacement", "in");

    private static GraphDocument CreateDocument(bool includeEdge = false)
    {
        var document = new GraphDocument
        {
            Nodes =
            [
                new NodeModel
                {
                    Id = "source",
                    Position = new GraphPoint(0, 0),
                    Ports =
                    [
                        new PortModel
                        {
                            Id = "out",
                            Kind = PortKind.Output,
                            Capacity = new PortCapacity { MaxOutgoing = null }
                        }
                    ]
                },
                new NodeModel
                {
                    Id = "target",
                    Position = new GraphPoint(300, 0),
                    Ports =
                    [
                        new PortModel
                        {
                            Id = "in",
                            Kind = PortKind.Input,
                            Capacity = new PortCapacity { MaxIncoming = null }
                        }
                    ]
                },
                new NodeModel
                {
                    Id = "replacement",
                    Position = new GraphPoint(600, 0),
                    Ports =
                    [
                        new PortModel
                        {
                            Id = "in",
                            Kind = PortKind.Input,
                            Capacity = new PortCapacity { MaxIncoming = null }
                        }
                    ]
                }
            ]
        };
        if (includeEdge)
        {
            document.Edges.Add(new EdgeModel
            {
                Id = "edge",
                Source = Source,
                Target = Target
            });
        }

        return document;
    }

    private sealed class RejectConnectionRule : IConnectionValidationRule
    {
        public int CallCount { get; private set; }

        public ConnectionValidationResult Validate(in ConnectionValidationRequest request)
        {
            CallCount++;
            return ConnectionValidationResult.Invalid("宿主拒绝", "HOST1001");
        }
    }
}
