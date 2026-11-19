using Avalonia;
using Kudo.Core.Documents;
using Shouldly;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class NodeLockAndPortVisibilityTests
{
    [Fact]
    public void Locked_node_remains_selectable_and_rejects_geometry_mutations()
    {
        var node = new NodeModel
        {
            Id = "locked",
            IsLocked = true,
            Position = new GraphPoint(10, 20),
            Size = new GraphSize(160, 80),
            Rotation = 15
        };
        var document = new GraphDocument { Nodes = [node] };
        var canvas = new GraphCanvas { Document = document };

        canvas.SetSelectedNodes([node.Id], node.Id);

        canvas.SelectedNodeIds.ShouldBe([node.Id]);
        canvas.SelectedNodeId.ShouldBe(node.Id);
        canvas.NudgeSelectedNodes(new GraphPoint(5, 6)).ShouldBeFalse();
        canvas.ResizeNode(node.Id, new GraphPoint(30, 40), new GraphSize(200, 100)).ShouldBeFalse();
        canvas.RotateNode(node.Id, 45).ShouldBeFalse();
        canvas.DeleteSelectedNode().ShouldBeFalse();
        node.Position.ShouldBe(new GraphPoint(10, 20));
        node.Size.ShouldBe(new GraphSize(160, 80));
        node.Rotation.ShouldBe(15);
        document.FindNode(node.Id).ShouldBeSameAs(node);
    }

    [Fact]
    public void Mixed_selection_moves_and_deletes_only_unlocked_nodes()
    {
        var locked = new NodeModel
        {
            Id = "locked",
            IsLocked = true,
            Position = new GraphPoint(10, 20)
        };
        var editable = new NodeModel
        {
            Id = "editable",
            Position = new GraphPoint(30, 40)
        };
        var document = new GraphDocument { Nodes = [locked, editable] };
        var canvas = new GraphCanvas { Document = document };
        canvas.SetSelectedNodes([locked.Id, editable.Id], editable.Id);

        canvas.NudgeSelectedNodes(new GraphPoint(5, 6)).ShouldBeTrue();

        locked.Position.ShouldBe(new GraphPoint(10, 20));
        editable.Position.ShouldBe(new GraphPoint(35, 46));
        canvas.DeleteSelectedNode().ShouldBeTrue();
        document.FindNode(locked.Id).ShouldBeSameAs(locked);
        document.FindNode(editable.Id).ShouldBeNull();
        canvas.SelectedNodeIds.ShouldBe([locked.Id]);
        canvas.SelectedNodeId.ShouldBe(locked.Id);
    }

    [Fact]
    public void Batch_locking_is_recorded_as_one_undo_entry()
    {
        var first = new NodeModel { Id = "first" };
        var second = new NodeModel { Id = "second" };
        var document = new GraphDocument { Nodes = [first, second] };
        var session = new GraphDocumentSession(document);
        var canvas = new GraphCanvas
        {
            Document = document,
            EditCoordinator = new GraphDocumentSessionEditCoordinator(session)
        };
        canvas.SetSelectedNodes([first.Id, second.Id], first.Id);

        canvas.SetSelectedNodesLocked(true).ShouldBeTrue();

        first.IsLocked.ShouldBeTrue();
        second.IsLocked.ShouldBeTrue();
        session.History.UndoCommand.ShouldBeOfType<CompositeGraphCommand>();
        session.Undo().ShouldBeTrue();
        first.IsLocked.ShouldBeFalse();
        second.IsLocked.ShouldBeFalse();
        session.History.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Port_hit_testing_respects_canvas_port_and_lod_visibility()
    {
        var port = new PortModel
        {
            Id = "out",
            Kind = PortKind.Output,
            Anchor = new GraphPoint(1, 0.5)
        };
        var node = new NodeModel
        {
            Id = "node",
            Position = new GraphPoint(100, 100),
            Size = new GraphSize(160, 80),
            Ports = [port]
        };
        var canvas = new GraphCanvas
        {
            Document = new GraphDocument { Nodes = [node] }
        };

        var point = canvas.GetPortScreenPosition(node, port);
        canvas.HitTestPort(point).ShouldNotBeNull().Port.ShouldBeSameAs(port);

        canvas.ShowPorts = false;
        canvas.HitTestPort(point).ShouldBeNull();

        canvas.ShowPorts = true;
        port.IsVisible = false;
        canvas.HitTestPort(point).ShouldBeNull();

        port.IsVisible = true;
        canvas.Viewport = new GraphViewport(GraphPoint.Zero, 0.2);
        point = canvas.GetPortScreenPosition(node, port);
        canvas.HitTestPort(point).ShouldBeNull();

        canvas.IsLevelOfDetailEnabled = false;
        canvas.HitTestPort(point).ShouldNotBeNull().Port.ShouldBeSameAs(port);
    }
}
