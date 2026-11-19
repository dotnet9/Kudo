using System.Text.Json;
using Kudo;
using Shouldly;

namespace Kudo.Core.Tests;

public class ReconfigureNodeCommandTests
{
    [Fact]
    public void Reject_policy_preserves_document_when_a_projected_port_removes_a_connected_endpoint()
    {
        var (document, node, edge) = CreateConnectedDocument();
        var command = new ReconfigureNodeCommand(
            node.Id,
            State("full"),
            new GraphNodeProjection(new GraphSize(240, 80), [Input("input")]));

        var exception = Should.Throw<GraphNodeReconfigurationException>(() => command.Execute(document));

        exception.ConflictingEdgeIds.ShouldBe([edge.Id]);
        node.ExtensionState.ShouldBeNull();
        node.Size.ShouldBe(new GraphSize(160, 88));
        node.Ports.Select(port => port.Id).ShouldBe(["input", "output"]);
        document.Edges.ShouldBe([edge]);
    }

    [Fact]
    public void Remove_policy_updates_atomically_and_undo_restores_state_structure_and_edges()
    {
        var (document, node, edge) = CreateConnectedDocument();
        var command = new ReconfigureNodeCommand(
            node.Id,
            State("collapsed"),
            new GraphNodeProjection(new GraphSize(120, 48), [Input("input")]),
            GraphPortConflictPolicy.RemoveConflictingEdges);

        command.Execute(document);

        node.ExtensionState!.SchemaId.ShouldBe("test.special");
        node.ExtensionState.Payload.GetProperty("mode").GetString().ShouldBe("collapsed");
        node.Size.ShouldBe(new GraphSize(120, 48));
        node.Ports.Select(port => port.Id).ShouldBe(["input"]);
        document.Edges.ShouldBeEmpty();
        command.RemovedEdges.ShouldBe([edge]);

        command.Undo(document);

        node.ExtensionState.ShouldBeNull();
        node.Size.ShouldBe(new GraphSize(160, 88));
        node.Ports.Select(port => port.Id).ShouldBe(["input", "output"]);
        document.Edges.ShouldBe([edge]);

        command.Execute(document);
        document.Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Command_snapshots_caller_owned_state_ports_and_properties()
    {
        var node = new NodeModel { Id = "special" };
        var document = new GraphDocument { Nodes = [node] };
        var properties = new Dictionary<string, object?> { ["role"] = "before" };
        var port = Input("input");
        port.IsVisible = false;
        port.Properties = properties;
        var state = State("full");
        var command = new ReconfigureNodeCommand(
            node.Id,
            state,
            new GraphNodeProjection(new GraphSize(200, 70), [port]));
        properties["role"] = "after";
        port.Id = "changed";

        command.Execute(document);

        node.Ports.Single().Id.ShouldBe("input");
        node.Ports.Single().IsVisible.ShouldBeFalse();
        node.Ports.Single().Properties["role"].ShouldBe("before");
    }

    [Fact]
    public void Reconfiguration_uses_the_additional_connection_rule()
    {
        var (document, node, edge) = CreateConnectedDocument();
        var command = new ReconfigureNodeCommand(
            node.Id,
            State("full"),
            new GraphNodeProjection(
                new GraphSize(200, 70),
                [Input("input"), Output("output")]),
            connectionValidationRule: new RejectConnectionRule());

        var exception = Should.Throw<GraphNodeReconfigurationException>(() => command.Execute(document));

        exception.ConflictingEdgeIds.ShouldBe([edge.Id]);
        document.Edges.ShouldBe([edge]);
    }

    private static (GraphDocument Document, NodeModel Node, EdgeModel Edge) CreateConnectedDocument()
    {
        var node = new NodeModel
        {
            Id = "special",
            Size = new GraphSize(160, 88),
            Ports = [Input("input"), Output("output")]
        };
        var target = new NodeModel
        {
            Id = "target",
            Ports = [Input("input")]
        };
        var edge = new EdgeModel
        {
            Id = "edge",
            Source = new PortReference(node.Id, "output"),
            Target = new PortReference(target.Id, "input")
        };
        return (new GraphDocument { Nodes = [node, target], Edges = [edge] }, node, edge);
    }

    private static PortModel Input(string id) => new()
    {
        Id = id,
        Kind = PortKind.Input,
        Capacity = new PortCapacity { MaxIncoming = null, MaxOutgoing = 0 }
    };

    private static PortModel Output(string id) => new()
    {
        Id = id,
        Kind = PortKind.Output,
        Capacity = new PortCapacity { MaxIncoming = 0, MaxOutgoing = null }
    };

    private static GraphExtensionState State(string mode)
    {
        using var document = JsonDocument.Parse($$"""{"mode":"{{mode}}"}""");
        return new GraphExtensionState
        {
            SchemaId = "test.special",
            SchemaVersion = 1,
            Payload = document.RootElement.Clone()
        };
    }

    private sealed class RejectConnectionRule : IConnectionValidationRule
    {
        public ConnectionValidationResult Validate(in ConnectionValidationRequest request) =>
            ConnectionValidationResult.Invalid("宿主拒绝");
    }
}
