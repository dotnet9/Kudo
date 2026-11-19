using Kudo.Avalonia.Designer;
using Kudo.Core.Documents;
using Kudo.Core.Serialization;
using Shouldly;
using System.Windows.Input;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class MultiEdgeSelectionTests
{
    [Fact]
    public void Edge_selection_tracks_document_order_and_primary_edge()
    {
        var document = CreateDocument();
        var canvas = new GraphCanvas { Document = document };
        var notificationCount = 0;
        var command = new CaptureCommand();
        canvas.SelectionChangedCommand = command;
        canvas.SelectionChanged += (_, _) => notificationCount++;

        canvas.SetSelectedEdges(["edge-2", "edge-1"], "edge-2");

        canvas.SelectedEdgeIds.ShouldBe(["edge-1", "edge-2"]);
        canvas.SelectedEdgeId.ShouldBe("edge-2");
        canvas.IsEdgeSelected("edge-1").ShouldBeTrue();
        canvas.IsEdgeSelected("edge-2").ShouldBeTrue();
        canvas.SelectedNodeIds.ShouldBeEmpty();
        notificationCount.ShouldBe(1);
        command.Value.ShouldBeOfType<GraphSelectionChangedEventArgs>()
            .SelectedEdgeIds.ShouldBe(["edge-1", "edge-2"]);
    }

    [Fact]
    public void Toggling_primary_edge_promotes_the_last_remaining_edge()
    {
        var canvas = new GraphCanvas { Document = CreateDocument() };
        canvas.SetSelectedEdges(["edge-1", "edge-2"], "edge-2");

        canvas.ToggleEdgeSelection("edge-2").ShouldBeTrue();

        canvas.SelectedEdgeIds.ShouldBe(["edge-1"]);
        canvas.SelectedEdgeId.ShouldBe("edge-1");

        canvas.ToggleEdgeSelection("edge-3").ShouldBeTrue();

        canvas.SelectedEdgeIds.ShouldBe(["edge-1", "edge-3"]);
        canvas.SelectedEdgeId.ShouldBe("edge-3");
    }

    [Fact]
    public void Deleting_selected_edges_creates_one_undo_entry()
    {
        var document = CreateDocument();
        var session = new GraphDocumentSession(document);
        using var view = new GraphDocumentView(session);
        view.Canvas.SetSelectedEdges(["edge-1", "edge-3"], "edge-3");

        view.EditorContext.Edit.Delete().ShouldBeTrue();

        document.Edges.Select(edge => edge.Id).ShouldBe(["edge-2"]);
        view.Canvas.SelectedEdgeIds.ShouldBeEmpty();
        session.History.CanUndo.ShouldBeTrue();

        view.EditorContext.Edit.Undo().ShouldBeTrue();

        document.Edges.Select(edge => edge.Id).ShouldBe(["edge-1", "edge-2", "edge-3"]);
        session.History.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Rejected_multi_edge_delete_restores_model_and_selection()
    {
        var document = CreateDocument();
        var canvas = new GraphCanvas
        {
            Document = document,
            EditCoordinator = new RejectingEditCoordinator()
        };
        canvas.SetSelectedEdges(["edge-1", "edge-2"], "edge-2");

        canvas.DeleteSelectedEdges().ShouldBeFalse();

        document.Edges.Select(edge => edge.Id).ShouldBe(["edge-1", "edge-2", "edge-3"]);
        canvas.SelectedEdgeIds.ShouldBe(["edge-1", "edge-2"]);
        canvas.SelectedEdgeId.ShouldBe("edge-2");
    }

    [Fact]
    public void Clipboard_serializes_every_selected_edge()
    {
        var document = CreateDocument();
        var service = new GraphClipboardService();

        var content = service.SerializeSubgraphSelection(
            document,
            [],
            ["edge-3", "edge-1"]);

        content.ShouldNotBeNull();
        content.Kind.ShouldBe(GraphClipboardElementKind.Edge);
        content.EdgeIds.ShouldBe(["edge-1", "edge-3"]);
        new GraphClipboardJsonSerializer()
            .TryDeserialize(content.Text, out var payload)
            .ShouldBeTrue();
        payload.ShouldNotBeNull().Edges.Select(edge => edge.Id).ShouldBe(["edge-1", "edge-3"]);
    }

    [Fact]
    public void Paste_plan_selects_every_created_edge()
    {
        var source = CreateDocument();
        var service = new GraphClipboardService();
        var content = service.SerializeSubgraphSelection(
            source,
            [],
            ["edge-1", "edge-2"]);
        var target = new GraphDocument { Nodes = source.Nodes };

        service.TryCreatePastePlan(
                target,
                content.ShouldNotBeNull().Text,
                GraphPoint.Zero,
                out var plan)
            .ShouldBeTrue();

        plan.ShouldNotBeNull().SelectedEdgeIds.Count.ShouldBe(2);
        plan.SelectedEdgeId.ShouldBe(plan.SelectedEdgeIds[0]);
        plan.Command.Execute(target);
        var canvas = new GraphCanvas { Document = target };
        canvas.SetSelectedEdges(plan.SelectedEdgeIds, plan.SelectedEdgeId);
        canvas.SelectedEdgeIds.ShouldBe(plan.SelectedEdgeIds);
    }

    [Fact]
    public void Routing_style_and_waypoint_changes_apply_to_all_selected_edges()
    {
        var document = CreateDocument();
        foreach (var edge in document.Edges.Take(2))
        {
            edge.RoutingStyle = EdgeRoutingStyle.Manual;
            edge.Waypoints = [new GraphPoint(40, 50)];
        }

        var session = new GraphDocumentSession(document);
        using var view = new GraphDocumentView(session);
        view.Canvas.SetSelectedEdges(["edge-1", "edge-2"], "edge-2");

        view.EditorContext.Connections.ClearWaypoints().ShouldBeTrue();
        document.Edges.Take(2).ShouldAllBe(edge => edge.Waypoints.Count == 0);
        session.Undo().ShouldBeTrue();
        document.Edges.Take(2).ShouldAllBe(edge => edge.Waypoints.Count == 1);

        view.EditorContext.Connections.SetRoutingStyle(EdgeRoutingStyle.Orthogonal).ShouldBeTrue();
        document.Edges.Take(2).ShouldAllBe(edge => edge.RoutingStyle == EdgeRoutingStyle.Orthogonal);
        session.Undo().ShouldBeTrue();
        document.Edges.Take(2).ShouldAllBe(edge => edge.RoutingStyle == EdgeRoutingStyle.Manual);
    }

    private static GraphDocument CreateDocument()
    {
        var first = CreateNode("node-1", "out-1", PortKind.Output);
        var second = CreateNode("node-2", "in-2", PortKind.Input, "out-2", PortKind.Output);
        var third = CreateNode("node-3", "in-3", PortKind.Input);
        return new GraphDocument
        {
            Nodes = [first, second, third],
            Edges =
            [
                CreateEdge("edge-1", "node-1", "out-1", "node-2", "in-2"),
                CreateEdge("edge-2", "node-2", "out-2", "node-3", "in-3"),
                CreateEdge("edge-3", "node-1", "out-1", "node-3", "in-3")
            ]
        };
    }

    private static NodeModel CreateNode(
        string nodeId,
        string firstPortId,
        PortKind firstPortKind,
        string? secondPortId = null,
        PortKind secondPortKind = PortKind.Input)
    {
        var ports = new List<PortModel>
        {
            new() { Id = firstPortId, Kind = firstPortKind }
        };
        if (secondPortId is not null)
        {
            ports.Add(new PortModel { Id = secondPortId, Kind = secondPortKind });
        }

        return new NodeModel { Id = nodeId, Ports = ports };
    }

    private static EdgeModel CreateEdge(
        string edgeId,
        string sourceNodeId,
        string sourcePortId,
        string targetNodeId,
        string targetPortId) =>
        new()
        {
            Id = edgeId,
            Source = new PortReference(sourceNodeId, sourcePortId),
            Target = new PortReference(targetNodeId, targetPortId)
        };

    private sealed class RejectingEditCoordinator : IGraphEditCoordinator
    {
        public GraphEditTransactionDecision Evaluate(GraphEditTransactionRequest request) =>
            GraphEditTransactionDecision.Allow;

        public GraphEditTransactionResult Execute(GraphEditTransactionRequest request) =>
            GraphEditTransactionResult.Rejected("拒绝批量删除");
    }

    private sealed class CaptureCommand : ICommand
    {
        public object? Value { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Value = parameter;
    }
}
