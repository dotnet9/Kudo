using Avalonia;
using Kudo.Core.Documents;
using Shouldly;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class GraphEditTransactionTests
{
    [Fact]
    public void Canvas_rolls_back_an_applied_edit_when_host_rejects_commit()
    {
        var node = new NodeModel
        {
            Id = "node",
            Position = new GraphPoint(10, 20)
        };
        var document = new GraphDocument { Nodes = [node] };
        var coordinator = new RejectingEditCoordinator();
        var canvas = new GraphCanvas
        {
            Document = document,
            EditCoordinator = coordinator
        };
        var command = new MoveNodeCommand(
            node.Id,
            node.Position,
            new GraphPoint(80, 90));
        command.Execute(document);
        GraphEditRejectedEventArgs? rejection = null;
        canvas.GraphEditRejected += (_, args) => rejection = args;

        var committed = canvas.CommitGraphEdit(command);

        committed.ShouldBeFalse();
        node.Position.ShouldBe(new GraphPoint(10, 20));
        coordinator.ExecutionCount.ShouldBe(1);
        rejection.ShouldNotBeNull();
        rejection.Decision.Reason.ShouldBe("宿主拒绝");
    }

    [Fact]
    public void Canvas_accepts_an_applied_edit_as_one_history_entry()
    {
        var node = new NodeModel
        {
            Id = "node",
            Position = new GraphPoint(10, 20)
        };
        var document = new GraphDocument { Nodes = [node] };
        var session = new GraphDocumentSession(document);
        var canvas = new GraphCanvas
        {
            Document = document,
            EditCoordinator = new GraphDocumentSessionEditCoordinator(session)
        };
        var command = new MoveNodeCommand(
            node.Id,
            node.Position,
            new GraphPoint(80, 90));
        command.Execute(document);

        var committed = canvas.CommitGraphEdit(command);

        committed.ShouldBeTrue();
        node.Position.ShouldBe(new GraphPoint(80, 90));
        session.History.UndoCommand.ShouldBeSameAs(command);
        session.Undo().ShouldBeTrue();
        node.Position.ShouldBe(new GraphPoint(10, 20));
        session.History.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Capture_lost_cancels_node_drag_without_committing_history()
    {
        var node = new NodeModel
        {
            Id = "node",
            Position = new GraphPoint(10, 20)
        };
        var document = new GraphDocument { Nodes = [node] };
        var session = new GraphDocumentSession(document);
        var canvas = new GraphCanvas
        {
            Document = document,
            EditCoordinator = new GraphDocumentSessionEditCoordinator(session)
        };
        var startPositions = new Dictionary<string, GraphPoint>(StringComparer.Ordinal)
        {
            [node.Id] = node.Position
        };
        var interaction = new NodeDragInteractionSession(
            canvas,
            null!,
            new Point(0, 0),
            node.Id,
            startPositions);
        node.Position = new GraphPoint(80, 90);

        interaction.CaptureLost();

        node.Position.ShouldBe(new GraphPoint(10, 20));
        session.History.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Rejected_delete_restores_model_selection_and_operation_result()
    {
        var node = new NodeModel { Id = "node" };
        var document = new GraphDocument { Nodes = [node] };
        var canvas = new GraphCanvas
        {
            Document = document,
            EditCoordinator = new RejectingEditCoordinator()
        };
        canvas.SetSelectedNodes([node.Id], node.Id);

        var deleted = canvas.DeleteSelectedNode();

        deleted.ShouldBeFalse();
        document.FindNode(node.Id).ShouldBeSameAs(node);
        canvas.SelectedNodeIds.ShouldBe([node.Id]);
        canvas.SelectedNodeId.ShouldBe(node.Id);
    }

    private sealed class RejectingEditCoordinator : IGraphEditCoordinator
    {
        public int ExecutionCount { get; private set; }

        public GraphEditTransactionDecision Evaluate(GraphEditTransactionRequest request) =>
            GraphEditTransactionDecision.Allow;

        public GraphEditTransactionResult Execute(GraphEditTransactionRequest request)
        {
            ExecutionCount++;
            return GraphEditTransactionResult.Rejected("宿主拒绝");
        }
    }
}
