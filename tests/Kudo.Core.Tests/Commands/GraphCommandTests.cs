using Kudo;
using Shouldly;

namespace Kudo.Core.Tests;

public class GraphCommandTests
{
    [Fact]
    public void Add_node_command_can_be_undone_and_executed_again()
    {
        var document = new GraphDocument();
        var node = new NodeModel { Id = "node" };
        var command = new AddNodeCommand(node);

        command.Execute(document);
        command.Undo(document);
        command.Execute(document);

        document.Nodes.ShouldBe([node]);
    }

    [Fact]
    public void Annotation_commands_support_add_edit_move_resize_remove_and_undo()
    {
        var document = new GraphDocument();
        var annotation = new AnnotationModel
        {
            Id = "note",
            Text = "原始说明",
            Position = new GraphPoint(20, 30),
            Size = new GraphSize(220, 120)
        };
        var add = new AddAnnotationCommand(annotation);

        add.Execute(document);
        new SetAnnotationTextCommand(annotation.Id, annotation.Text, "更新说明").Execute(document);
        new MoveAnnotationCommand(
            annotation.Id,
            annotation.Position,
            new GraphPoint(80, 90)).Execute(document);
        var resize = new ResizeAnnotationCommand(
            annotation.Id,
            annotation.Position,
            annotation.Size,
            new GraphPoint(70, 80),
            new GraphSize(300, 180));
        resize.Execute(document);

        annotation.Text.ShouldBe("更新说明");
        annotation.Position.ShouldBe(new GraphPoint(70, 80));
        annotation.Size.ShouldBe(new GraphSize(300, 180));

        resize.Undo(document);
        annotation.Position.ShouldBe(new GraphPoint(80, 90));
        annotation.Size.ShouldBe(new GraphSize(220, 120));

        var remove = new RemoveAnnotationCommand(annotation.Id);
        remove.Execute(document);
        document.Annotations.ShouldBeEmpty();
        remove.Undo(document);
        document.Annotations.ShouldBe([annotation]);
    }

    [Fact]
    public void Set_annotation_properties_command_updates_all_editable_values_and_restores_snapshot()
    {
        var annotation = new AnnotationModel
        {
            Id = "note",
            Text = "原始说明",
            Position = new GraphPoint(20, 30),
            Size = new GraphSize(220, 120),
            Properties = { ["severity"] = "warning" }
        };
        var document = new GraphDocument { Annotations = [annotation] };
        var newProperties = new Dictionary<string, object?>
        {
            ["severity"] = "critical",
            ["priority"] = 2
        };
        var command = new SetAnnotationPropertiesCommand(
            annotation.Id,
            annotation.Text,
            "更新说明",
            annotation.Position,
            new GraphPoint(80, 90),
            annotation.Size,
            new GraphSize(300, 180),
            annotation.Properties,
            newProperties);
        newProperties["priority"] = 99;

        command.Execute(document);

        annotation.Text.ShouldBe("更新说明");
        annotation.Position.ShouldBe(new GraphPoint(80, 90));
        annotation.Size.ShouldBe(new GraphSize(300, 180));
        annotation.Properties["severity"].ShouldBe("critical");
        annotation.Properties["priority"].ShouldBe(2);

        command.Undo(document);

        annotation.Text.ShouldBe("原始说明");
        annotation.Position.ShouldBe(new GraphPoint(20, 30));
        annotation.Size.ShouldBe(new GraphSize(220, 120));
        annotation.Properties.ShouldBe(new Dictionary<string, object?>
        {
            ["severity"] = "warning"
        });
    }

    [Fact]
    public void Composite_command_executes_and_undoes_children_as_one_operation()
    {
        var document = new GraphDocument();
        var first = new NodeModel { Id = "first" };
        var second = new NodeModel { Id = "second" };
        var command = new CompositeGraphCommand(
            "添加多个节点",
            [new AddNodeCommand(first), new AddNodeCommand(second)]);

        command.Execute(document);

        document.Nodes.ShouldBe([first, second]);

        command.Undo(document);

        document.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void Remove_node_command_restores_node_and_connected_edges_in_order()
    {
        var document = CreateConnectedDocument();
        var unrelated = new EdgeModel { Id = "unrelated" };
        document.Edges.Insert(0, unrelated);
        var connected = document.Edges[1];
        var command = new RemoveNodeCommand("source");

        command.Execute(document);
        command.Undo(document);

        document.FindNode("source").ShouldNotBeNull();
        document.Edges.ShouldBe([unrelated, connected]);
    }

    [Fact]
    public void Group_commands_create_remove_and_restore_group()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "first" });
        document.AddNode(new NodeModel { Id = "second" });
        var group = new GroupModel
        {
            Id = "group",
            Title = "输入",
            NodeIds = ["first", "second"]
        };
        var add = new AddGroupCommand(group);

        add.Execute(document);
        document.Groups.ShouldBe([group]);

        add.Undo(document);
        document.Groups.ShouldBeEmpty();

        add.Execute(document);
        var remove = new RemoveGroupCommand(group.Id);
        remove.Execute(document);
        document.Groups.ShouldBeEmpty();

        remove.Undo(document);
        document.Groups.ShouldBe([group]);
    }

    [Fact]
    public void Add_group_command_rejects_invalid_or_already_grouped_nodes()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "first" });
        document.AddNode(new NodeModel { Id = "second" });
        new AddGroupCommand(new GroupModel
        {
            Id = "existing",
            NodeIds = ["first", "second"]
        }).Execute(document);

        Should.Throw<InvalidOperationException>(() => new AddGroupCommand(new GroupModel
        {
            Id = "overlap",
            NodeIds = ["first", "missing"]
        }).Execute(document));
        Should.Throw<InvalidOperationException>(() => new AddGroupCommand(new GroupModel
        {
            Id = "duplicate",
            NodeIds = ["second", "second"]
        }).Execute(document));
        Should.Throw<InvalidOperationException>(() => new AddGroupCommand(new GroupModel
        {
            Id = string.Empty,
            NodeIds = ["first", "second"]
        }).Execute(new GraphDocument
        {
            Nodes =
            [
                new NodeModel { Id = "first" },
                new NodeModel { Id = "second" }
            ]
        }));
    }

    [Fact]
    public void Set_group_collapsed_command_restores_previous_state()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "first" });
        document.AddNode(new NodeModel { Id = "second" });
        var group = document.AddGroup(new GroupModel
        {
            Id = "group",
            NodeIds = ["first", "second"]
        });
        var command = new SetGroupCollapsedCommand(group.Id, false, true);

        command.Execute(document);

        group.IsCollapsed.ShouldBeTrue();
        command.Name.ShouldBe("Collapse node group");

        command.Undo(document);

        group.IsCollapsed.ShouldBeFalse();
    }

    [Fact]
    public void Set_group_properties_command_copies_values_and_restores_previous_state()
    {
        var document = new GraphDocument
        {
            Nodes =
            [
                new NodeModel { Id = "first" },
                new NodeModel { Id = "second" }
            ]
        };
        var group = document.AddGroup(new GroupModel
        {
            Id = "group",
            Title = "旧分组",
            NodeIds = ["first", "second"],
            Padding = 24,
            Properties = { ["color"] = "blue" }
        });
        var newProperties = new Dictionary<string, object?> { ["color"] = "red" };
        var command = new SetGroupPropertiesCommand(
            group.Id,
            group.Title,
            "新分组",
            group.Padding,
            36,
            group.Properties,
            newProperties);

        command.Execute(document);

        group.Title.ShouldBe("新分组");
        group.Padding.ShouldBe(36);
        group.Properties["color"].ShouldBe("red");
        newProperties["color"] = "changed";
        group.Properties["color"].ShouldBe("red");

        command.Undo(document);

        group.Title.ShouldBe("旧分组");
        group.Padding.ShouldBe(24);
        group.Properties["color"].ShouldBe("blue");
    }

    [Fact]
    public void Remove_node_command_restores_group_membership_and_empty_group()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "first" });
        document.AddNode(new NodeModel { Id = "second" });
        var group = document.AddGroup(new GroupModel
        {
            Id = "group",
            NodeIds = ["first", "second"]
        });
        var removeFirst = new RemoveNodeCommand("first");
        var removeSecond = new RemoveNodeCommand("second");

        removeFirst.Execute(document);
        group.NodeIds.ShouldBe(["second"]);
        removeSecond.Execute(document);
        document.Groups.ShouldBeEmpty();

        removeSecond.Undo(document);
        document.Groups.ShouldBe([group]);
        group.NodeIds.ShouldBe(["second"]);
        removeFirst.Undo(document);
        group.NodeIds.ShouldBe(["first", "second"]);
    }

    [Fact]
    public void Position_and_routing_commands_restore_previous_values()
    {
        var document = CreateConnectedDocument();
        var node = document.FindNode("source")!;
        var edge = document.Edges.Single();
        var move = new MoveNodeCommand(node.Id, node.Position, new GraphPoint(50, 80));
        var route = new SetEdgeRoutingStyleCommand(
            edge.Id,
            edge.RoutingStyle,
            EdgeRoutingStyle.Orthogonal);

        move.Execute(document);
        route.Execute(document);
        route.Undo(document);
        move.Undo(document);

        node.Position.ShouldBe(GraphPoint.Zero);
        edge.RoutingStyle.ShouldBe(EdgeRoutingStyle.Bezier);
    }

    [Fact]
    public void Resize_node_command_restores_previous_bounds()
    {
        var document = CreateConnectedDocument();
        var node = document.FindNode("source")!;
        var oldPosition = node.Position;
        var oldSize = node.Size;
        var newPosition = new GraphPoint(20, 30);
        var newSize = new GraphSize(240, 160);
        var command = new ResizeNodeCommand(
            node.Id,
            oldPosition,
            oldSize,
            newPosition,
            newSize,
            "调整节点大小");

        command.Execute(document);
        node.Position.ShouldBe(newPosition);
        node.Size.ShouldBe(newSize);

        command.Undo(document);
        node.Position.ShouldBe(oldPosition);
        node.Size.ShouldBe(oldSize);
    }

    [Fact]
    public void Reconnect_edge_command_restores_previous_endpoints()
    {
        var document = CreateConnectedDocument();
        document.AddNode(new NodeModel { Id = "replacement" }
            .AddPort(new PortModel { Id = "in", Kind = PortKind.Input }));
        var edge = document.Edges.Single();
        var oldSource = edge.Source;
        var oldTarget = edge.Target;
        var newTarget = new PortReference("replacement", "in");
        var command = new ReconnectEdgeCommand(
            edge.Id,
            oldSource,
            oldTarget,
            oldSource,
            newTarget);

        command.Execute(document);
        edge.Source.ShouldBe(oldSource);
        edge.Target.ShouldBe(newTarget);

        command.Undo(document);
        edge.Source.ShouldBe(oldSource);
        edge.Target.ShouldBe(oldTarget);
    }

    [Fact]
    public void Property_commands_update_and_restore_model_values()
    {
        var document = CreateConnectedDocument();
        document.Title = "旧画板";
        var node = document.FindNode("source")!;
        node.Name = "旧节点";
        node.Description = "旧描述";
        var edge = document.Edges.Single();
        edge.Label = "旧 Label";
        edge.SourceMarker = EdgeMarker.Circle;
        edge.TargetMarker = EdgeMarker.Arrow;
        var nodeCommand = new SetNodePropertiesCommand(
            node.Id,
            node.Number,
            node.Number,
            node.Name,
            "新节点",
            node.Description,
            "新描述",
            node.Position,
            new GraphPoint(30, 40));
        var edgeCommand = new SetEdgePropertiesCommand(
            edge.Id,
            edge.Label,
            "新 Label",
            edge.RoutingStyle,
            EdgeRoutingStyle.Orthogonal,
            edge.SourceMarker,
            EdgeMarker.Diamond,
            edge.TargetMarker,
            EdgeMarker.Circle,
            "编辑连线属性");
        var documentCommand = new SetDocumentTitleCommand(document.Id, document.Title, "新画板");
        var settingsCommand = new SetCanvasSettingsCommand(
            document.Id,
            document.CanvasSettings,
            document.CanvasSettings with { IsMinimapVisible = false });
        var appearanceCommand = new SetNodeAppearanceCommand(
            node.Id,
            node.Appearance,
            new GraphNodeAppearance
            {
                BackgroundImage = "Assets/background.png",
                Stretch = GraphImageStretch.Uniform,
                Opacity = 0.6
            });

        nodeCommand.Execute(document);
        appearanceCommand.Execute(document);
        edgeCommand.Execute(document);
        documentCommand.Execute(document);
        settingsCommand.Execute(document);

        node.Name.ShouldBe("新节点");
        node.Description.ShouldBe("新描述");
        node.Position.ShouldBe(new GraphPoint(30, 40));
        node.Appearance.BackgroundImage.ShouldBe("Assets/background.png");
        node.Appearance.Stretch.ShouldBe(GraphImageStretch.Uniform);
        node.Appearance.Opacity.ShouldBe(0.6);
        edge.Label.ShouldBe("新 Label");
        edge.RoutingStyle.ShouldBe(EdgeRoutingStyle.Orthogonal);
        edge.SourceMarker.ShouldBe(EdgeMarker.Diamond);
        edge.TargetMarker.ShouldBe(EdgeMarker.Circle);
        document.Title.ShouldBe("新画板");
        document.CanvasSettings.IsMinimapVisible.ShouldBeFalse();

        settingsCommand.Undo(document);
        documentCommand.Undo(document);
        edgeCommand.Undo(document);
        appearanceCommand.Undo(document);
        nodeCommand.Undo(document);

        node.Name.ShouldBe("旧节点");
        node.Description.ShouldBe("旧描述");
        node.Position.ShouldBe(GraphPoint.Zero);
        node.Appearance.ShouldBe(new GraphNodeAppearance());
        edge.Label.ShouldBe("旧 Label");
        edge.RoutingStyle.ShouldBe(EdgeRoutingStyle.Bezier);
        edge.SourceMarker.ShouldBe(EdgeMarker.Circle);
        edge.TargetMarker.ShouldBe(EdgeMarker.Arrow);
        document.Title.ShouldBe("旧画板");
        document.CanvasSettings.ShouldBe(GraphCanvasSettings.Default);
    }

    [Fact]
    public void Edge_style_preset_command_restores_custom_appearance_on_undo()
    {
        var document = CreateConnectedDocument();
        var edge = document.Edges.Single();
        edge.Appearance = GraphEdgeAppearance.Warning.WithColor("#123456");
        var oldAppearance = edge.Appearance;
        var command = new SetEdgeStylePresetCommand(
            edge.Id,
            edge.StylePreset,
            oldAppearance,
            GraphEdgeStylePreset.ActiveFlow);

        command.Execute(document);

        edge.StylePreset.ShouldBe(GraphEdgeStylePreset.ActiveFlow);
        edge.Appearance.ShouldBe(GraphEdgeAppearance.ActiveFlow);

        command.Undo(document);

        edge.StylePreset.ShouldBe(GraphEdgeStylePreset.Custom);
        edge.Appearance.ShouldBe(oldAppearance);
    }

    [Fact]
    public void Set_edge_waypoints_command_copies_inputs_and_restores_distinct_collections()
    {
        var document = CreateConnectedDocument();
        var edge = document.Edges.Single();
        edge.Waypoints = [new GraphPoint(10, 20)];
        var oldWaypoints = edge.Waypoints.ToArray();
        var newWaypoints = new[] { new GraphPoint(30, 40), new GraphPoint(50, 60) };
        var command = new SetEdgeWaypointsCommand(edge.Id, oldWaypoints, newWaypoints);

        newWaypoints[0] = GraphPoint.Zero;
        command.Execute(document);

        edge.Waypoints.ShouldBe([new GraphPoint(30, 40), new GraphPoint(50, 60)]);
        edge.Waypoints.ShouldNotBeSameAs(newWaypoints);

        command.Undo(document);

        edge.Waypoints.ShouldBe([new GraphPoint(10, 20)]);
        edge.Waypoints.ShouldNotBeSameAs(oldWaypoints);
    }

    [Fact]
    public void Waypoint_commands_add_move_remove_and_restore_by_index()
    {
        var document = CreateConnectedDocument();
        var edge = document.Edges.Single();
        edge.Waypoints = [new GraphPoint(10, 10), new GraphPoint(30, 30)];
        var added = new GraphPoint(20, 20);
        var moved = new GraphPoint(25, 15);
        var add = new AddEdgeWaypointCommand(edge.Id, 1, added);
        var move = new MoveEdgeWaypointCommand(edge.Id, 1, added, moved);
        var remove = new RemoveEdgeWaypointCommand(edge.Id, 1, moved);

        add.Execute(document);
        move.Execute(document);
        remove.Execute(document);
        edge.Waypoints.ShouldBe([new GraphPoint(10, 10), new GraphPoint(30, 30)]);

        remove.Undo(document);
        move.Undo(document);
        add.Undo(document);
        edge.Waypoints.ShouldBe([new GraphPoint(10, 10), new GraphPoint(30, 30)]);

        add.Execute(document);
        edge.Waypoints.ShouldBe([new GraphPoint(10, 10), added, new GraphPoint(30, 30)]);
    }

    [Fact]
    public void Waypoint_commands_reject_invalid_coordinates_indices_and_stale_state()
    {
        var document = CreateConnectedDocument();
        var edge = document.Edges.Single();
        edge.Waypoints = [new GraphPoint(10, 10)];

        Should.Throw<ArgumentOutOfRangeException>(() => new AddEdgeWaypointCommand(
            edge.Id,
            0,
            new GraphPoint(double.NaN, 10)));
        Should.Throw<InvalidOperationException>(() => new AddEdgeWaypointCommand(
            edge.Id,
            2,
            GraphPoint.Zero).Execute(document));
        Should.Throw<InvalidOperationException>(() => new MoveEdgeWaypointCommand(
            edge.Id,
            0,
            GraphPoint.Zero,
            new GraphPoint(20, 20)).Execute(document));
        Should.Throw<InvalidOperationException>(() => new RemoveEdgeWaypointCommand(
            edge.Id,
            1,
            new GraphPoint(10, 10)).Execute(document));
    }

    private static GraphDocument CreateConnectedDocument()
    {
        var document = new GraphDocument();
        var source = new NodeModel { Id = "source" }
            .AddPort(new PortModel { Id = "out", Kind = PortKind.Output });
        var target = new NodeModel { Id = "target" }
            .AddPort(new PortModel { Id = "in", Kind = PortKind.Input });
        document.AddNode(source);
        document.AddNode(target);
        document.AddEdge(new EdgeModel
        {
            Id = "edge",
            Source = new PortReference("source", "out"),
            Target = new PortReference("target", "in")
        });
        return document;
    }
}
