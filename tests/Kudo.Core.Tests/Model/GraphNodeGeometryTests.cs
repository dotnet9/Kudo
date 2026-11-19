using Shouldly;

namespace Kudo.Core.Tests;

public class GraphNodeGeometryTests
{
    [Fact]
    public void Canvas_bounds_and_point_transforms_use_independent_node_rotation()
    {
        var node = new NodeModel
        {
            Position = new GraphPoint(10, 20),
            Size = new GraphSize(100, 40),
            Rotation = 90
        };

        var bounds = GraphNodeGeometry.GetCanvasBounds(node);
        var transformed = GraphNodeGeometry.TransformPoint(node, new GraphPoint(10, 40));

        bounds.X.ShouldBe(40, 0.0001);
        bounds.Y.ShouldBe(-10, 0.0001);
        bounds.Width.ShouldBe(40, 0.0001);
        bounds.Height.ShouldBe(100, 0.0001);
        transformed.X.ShouldBe(60, 0.0001);
        transformed.Y.ShouldBe(-10, 0.0001);
        var restored = GraphNodeGeometry.InverseTransformPoint(node, transformed);
        restored.X.ShouldBe(10, 0.0001);
        restored.Y.ShouldBe(40, 0.0001);
    }

    [Fact]
    public void Node_property_command_updates_and_restores_rotation()
    {
        var node = new NodeModel { Id = "node", Rotation = 15 };
        var document = new GraphDocument { Nodes = [node] };
        var command = new SetNodePropertiesCommand(
            node.Id,
            node.Number,
            node.Number,
            node.Name,
            "旋转节点",
            node.Description,
            node.Description,
            node.Position,
            new GraphPoint(20, 30),
            node.Rotation,
            450);

        command.Execute(document);

        node.Name.ShouldBe("旋转节点");
        node.Position.ShouldBe(new GraphPoint(20, 30));
        node.Rotation.ShouldBe(90);
        var changes = GraphChangeSet.ForCommand(command);
        changes.Kind.HasFlag(GraphChangeKind.Geometry).ShouldBeTrue();
        changes.Kind.HasFlag(GraphChangeKind.Routing).ShouldBeTrue();
        changes.Kind.HasFlag(GraphChangeKind.Properties).ShouldBeTrue();

        command.Undo(document);

        node.Rotation.ShouldBe(15);
    }

    [Fact]
    public void Node_property_command_without_rotation_preserves_existing_rotation()
    {
        var node = new NodeModel { Id = "node", Rotation = 35 };
        var document = new GraphDocument { Nodes = [node] };
        var command = new SetNodePropertiesCommand(
            node.Id,
            node.Number,
            node.Number,
            node.Name,
            "新标题",
            node.Description,
            node.Description,
            node.Position,
            node.Position);

        command.Execute(document);
        node.Rotation.ShouldBe(35);

        command.Undo(document);
        node.Rotation.ShouldBe(35);
    }

    [Fact]
    public void Name_only_node_property_command_reports_property_and_appearance_changes()
    {
        var node = new NodeModel { Id = "node", Name = "旧名称" };
        var command = new SetNodePropertiesCommand(
            node.Id,
            node.Number,
            node.Number,
            node.Name,
            "新名称",
            node.Description,
            node.Description,
            node.Position,
            node.Position);

        var changes = GraphChangeSet.ForCommand(command);

        changes.Kind.ShouldBe(GraphChangeKind.Properties | GraphChangeKind.Appearance);
        changes.Nodes.ModifiedIds.ShouldBe([node.Id]);
        changes.Edges.ShouldBe(GraphElementChangeSet.Empty);
        changes.Groups.ShouldBe(GraphElementChangeSet.Empty);
    }
}
