using Kudo.Core.Documents;
using Shouldly;

namespace Kudo.Core.Tests.Documents;

public sealed class GraphDocumentSessionEditCoordinatorTests
{
    [Fact]
    public void Execute_records_one_history_entry_and_supports_coordinated_undo_redo()
    {
        var document = new GraphDocument();
        var session = new GraphDocumentSession(document);
        var coordinator = new GraphDocumentSessionEditCoordinator(session);
        var command = new AddNodeCommand(new NodeModel { Id = "node" });

        var execute = coordinator.Execute(new GraphEditTransactionRequest(
            document,
            command,
            GraphEditTransactionKind.Execute));

        execute.IsAccepted.ShouldBeTrue();
        session.History.UndoCommand.ShouldBeSameAs(command);
        document.FindNode("node").ShouldNotBeNull();

        var undo = coordinator.Execute(new GraphEditTransactionRequest(
            document,
            command,
            GraphEditTransactionKind.Undo));
        undo.IsAccepted.ShouldBeTrue();
        document.FindNode("node").ShouldBeNull();

        var redo = coordinator.Execute(new GraphEditTransactionRequest(
            document,
            command,
            GraphEditTransactionKind.Redo));
        redo.IsAccepted.ShouldBeTrue();
        document.FindNode("node").ShouldNotBeNull();
    }

    [Fact]
    public void Commit_applied_records_without_executing_command_again()
    {
        var node = new NodeModel
        {
            Id = "node",
            Position = new GraphPoint(10, 20)
        };
        var document = new GraphDocument { Nodes = [node] };
        var session = new GraphDocumentSession(document);
        var coordinator = new GraphDocumentSessionEditCoordinator(session);
        var command = new MoveNodeCommand(
            node.Id,
            node.Position,
            new GraphPoint(80, 90));
        command.Execute(document);

        var result = coordinator.Execute(new GraphEditTransactionRequest(
            document,
            command,
            GraphEditTransactionKind.CommitApplied));

        result.IsAccepted.ShouldBeTrue();
        node.Position.ShouldBe(new GraphPoint(80, 90));
        session.History.UndoCommand.ShouldBeSameAs(command);
        session.Undo().ShouldBeTrue();
        node.Position.ShouldBe(new GraphPoint(10, 20));
        session.History.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Coordinator_rejects_a_transaction_for_another_document()
    {
        var session = new GraphDocumentSession(new GraphDocument());
        var coordinator = new GraphDocumentSessionEditCoordinator(session);
        var otherDocument = new GraphDocument();
        var command = new AddNodeCommand(new NodeModel { Id = "node" });

        var result = coordinator.Execute(new GraphEditTransactionRequest(
            otherDocument,
            command,
            GraphEditTransactionKind.Execute));

        result.IsAccepted.ShouldBeFalse();
        otherDocument.Nodes.ShouldBeEmpty();
        session.History.CanUndo.ShouldBeFalse();
    }
}
