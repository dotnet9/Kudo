using Kudo;
using Kudo.Core.Documents;
using Shouldly;

namespace Kudo.Core.Tests;

public class GraphCommandHistoryTests
{
    [Fact]
    public void Operation_changed_is_published_for_execute_undo_and_redo()
    {
        var document = new GraphDocument();
        var editor = new GraphDocumentEditor(document);
        var history = new GraphCommandHistory();
        var operations = new List<GraphHistoryOperationChangedEventArgs>();
        history.OperationChanged += (_, operation) => operations.Add(operation);
        var command = new AddNodeCommand(new NodeModel { Id = "node-a" });

        history.Execute(
            editor,
            command,
            GraphOperationContext.User(GraphOperationInteractions.DragDrop));
        history.Undo(editor).ShouldBeTrue();
        history.Undo(editor).ShouldBeFalse();
        history.Redo(editor).ShouldBeTrue();

        operations.Count.ShouldBe(3);
        operations[0].Direction.ShouldBe(GraphDocumentEditDirection.Execute);
        operations[0].OperationContext.IsUserInitiated.ShouldBeTrue();
        operations[0].OperationContext.InteractionKey.ShouldBe(GraphOperationInteractions.DragDrop);
        operations[0].Changes.Nodes.AddedIds.ShouldBe(["node-a"]);
        operations[1].Direction.ShouldBe(GraphDocumentEditDirection.Undo);
        operations[1].OperationContext.InteractionKey.ShouldBe(GraphOperationInteractions.DragDrop);
        operations[1].Changes.Nodes.RemovedIds.ShouldBe(["node-a"]);
        operations[2].Direction.ShouldBe(GraphDocumentEditDirection.Redo);
        operations[2].OperationContext.InteractionKey.ShouldBe(GraphOperationInteractions.DragDrop);
        operations[2].Changes.Nodes.AddedIds.ShouldBe(["node-a"]);
        document.FindNode("node-a").ShouldNotBeNull();
    }
}
