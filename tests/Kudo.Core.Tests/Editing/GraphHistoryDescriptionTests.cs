using Kudo;
using Shouldly;

namespace Kudo.Core.Tests;

public class GraphHistoryDescriptionTests
{
    [Fact]
    public void Built_in_provider_describes_standard_commands_without_localized_text()
    {
        var document = new GraphDocument();
        var node = new NodeModel { Id = "node-a", Type = "Pump", DefinitionId = "pmp", Number = "001", Name = "pump12" };
        var command = new AddNodeCommand(node);
        var registry = new GraphHistoryDescriptionRegistry();
        registry.Register(new GraphBuiltInHistoryDescriptionProvider());

        registry.TryDescribe(
            new GraphHistoryDescriptionContext(
                document,
                command,
                GraphDocumentEditDirection.Undo,
                GraphChangeSet.ForCommand(command, undo: true)),
            out var description).ShouldBeTrue();

        description.ShouldNotBeNull();
        description.OperationKey.ShouldBe(GraphHistoryOperationKeys.AddNode);
        description.Subject.Type.ShouldBe("Pump");
        description.Subject.DisplayName.ShouldBe("001-pump12-pmp");
    }

    [Fact]
    public void Later_provider_overrides_built_in_description_and_single_composites_are_normalized()
    {
        var document = new GraphDocument();
        var node = new NodeModel { Id = "node-a", Type = "Pump" };
        var command = new AddNodeCommand(node);
        var registry = new GraphHistoryDescriptionRegistry();
        registry.Register(new GraphBuiltInHistoryDescriptionProvider());
        registry.Register(new OverrideProvider());

        var composite = new CompositeGraphCommand("batch", [command]);
        registry.TryDescribe(
            new GraphHistoryDescriptionContext(
                document,
                composite,
                GraphDocumentEditDirection.Redo,
                GraphChangeSet.ForCommand(composite)),
            out var description).ShouldBeTrue();

        description.ShouldNotBeNull();
        description.OperationKey.ShouldBe("custom.add");
        description.Changes.Count.ShouldBe(1);
        description.Changes[0].Key.ShouldBe("custom.value");
    }

    [Fact]
    public void Built_in_provider_describes_drop_paste_and_duplicate_interactions()
    {
        var document = new GraphDocument();
        var node = new NodeModel { Id = "node-a", Type = "Pump" };
        var provider = new GraphBuiltInHistoryDescriptionProvider();
        var registry = new GraphHistoryDescriptionRegistry();
        registry.Register(provider);

        registry.TryDescribe(new GraphHistoryDescriptionContext(
            document,
            new AddNodeCommand(node),
            GraphDocumentEditDirection.Execute,
            GraphChangeSet.Empty,
            OperationContext: GraphOperationContext.User(GraphOperationInteractions.DragDrop)),
            out var dropDescription).ShouldBeTrue();
        dropDescription!
            .OperationKey.ShouldBe(GraphHistoryOperationKeys.DropNode);

        var composite = new CompositeGraphCommand(
            "paste",
            [new AddNodeCommand(node)]);
        registry.TryDescribe(new GraphHistoryDescriptionContext(
            document,
            composite,
            GraphDocumentEditDirection.Execute,
            GraphChangeSet.Empty,
            OperationContext: GraphOperationContext.User(GraphOperationInteractions.Paste)),
            out var pasteDescription).ShouldBeTrue();
        pasteDescription!
            .OperationKey.ShouldBe(GraphHistoryOperationKeys.Paste);

        registry.TryDescribe(new GraphHistoryDescriptionContext(
            document,
            composite,
            GraphDocumentEditDirection.Execute,
            GraphChangeSet.Empty,
            OperationContext: GraphOperationContext.User(GraphOperationInteractions.Duplicate)),
            out var duplicateDescription).ShouldBeTrue();
        duplicateDescription!
            .OperationKey.ShouldBe(GraphHistoryOperationKeys.Duplicate);
    }

    [Fact]
    public void Built_in_provider_describes_node_and_document_property_changes()
    {
        var document = new GraphDocument
        {
            Id = "document",
            Title = "旧标题",
            CreatedBy = "旧创建人",
            CreatedAt = new DateTime(2026, 9, 22, 10, 0, 0),
            Description = "旧描述"
        };
        var node = new NodeModel
        {
            Id = "node",
            Type = "Pump",
            Number = "001",
            Name = "旧名称",
            Description = "旧节点描述",
            Position = new GraphPoint(10, 20),
            Rotation = 15
        };
        document.AddNode(node);
        var provider = new GraphBuiltInHistoryDescriptionProvider();

        var nodeCommand = new SetNodePropertiesCommand(
            node.Id,
            node.Number,
            "002",
            node.Name,
            "新名称",
            node.Description,
            "新节点描述",
            node.Position,
            new GraphPoint(30, 40),
            node.Rotation,
            45);
        var nodeDescription = provider.Describe(new GraphHistoryDescriptionContext(
            document,
            nodeCommand,
            GraphDocumentEditDirection.Execute,
            GraphChangeSet.ForCommand(nodeCommand)));

        nodeDescription.ShouldNotBeNull();
        nodeDescription.Changes.Select(change => change.Key).ShouldBe([
            GraphHistoryPropertyKeys.NodeNumber,
            GraphHistoryPropertyKeys.NodeName,
            GraphHistoryPropertyKeys.NodeDescription,
            GraphHistoryPropertyKeys.NodePosition,
            GraphHistoryPropertyKeys.NodeRotation
        ]);

        var documentCommand = new SetDocumentPropertiesCommand(
            document.Id,
            document.Title,
            "新标题",
            document.CreatedBy,
            "新创建人",
            document.CreatedAt,
            document.CreatedAt.AddMinutes(30),
            document.Description,
            "新描述");
        var documentDescription = provider.Describe(new GraphHistoryDescriptionContext(
            document,
            documentCommand,
            GraphDocumentEditDirection.Execute,
            GraphChangeSet.ForCommand(documentCommand)));

        documentDescription.ShouldNotBeNull();
        documentDescription.Changes.Select(change => change.Key).ShouldBe([
            GraphHistoryPropertyKeys.DocumentTitle,
            GraphHistoryPropertyKeys.DocumentCreatedBy,
            GraphHistoryPropertyKeys.DocumentCreatedAt,
            GraphHistoryPropertyKeys.DocumentDescription
        ]);
    }

    [Fact]
    public void Built_in_provider_describes_connection_endpoints()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "source", Name = "源图元" }
            .AddPort(new PortModel { Id = "out", Name = "输出", Kind = PortKind.Output }));
        document.AddNode(new NodeModel { Id = "target", Name = "目标图元" }
            .AddPort(new PortModel { Id = "in", Name = "输入", Kind = PortKind.Input }));
        var edge = new EdgeModel
        {
            Id = "edge",
            Source = new PortReference("source", "out"),
            Target = new PortReference("target", "in")
        };
        var command = new AddEdgeCommand(edge);
        var description = new GraphBuiltInHistoryDescriptionProvider().Describe(
            new GraphHistoryDescriptionContext(
                document,
                command,
                GraphDocumentEditDirection.Execute,
                GraphChangeSet.ForCommand(command)));

        description.ShouldNotBeNull();
        description.OperationKey.ShouldBe(GraphHistoryOperationKeys.AddConnection);
        description.Subject.Kind.ShouldBe("connection");
        description.Subject.Metadata!["source-node-id"].ShouldBe("source");
        description.Subject.Metadata["target-port-id"].ShouldBe("in");
    }

    [Fact]
    public void History_identity_uses_number_instance_name_and_definition_id()
    {
        GraphHistoryIdentity.FormatNode(new NodeModel
        {
            Number = "001",
            Name = "pump12",
            DefinitionId = "pmp"
        }).ShouldBe("001-pump12-pmp");
    }

    private sealed class OverrideProvider : IGraphHistoryDescriptionProvider
    {
        public GraphHistoryDescription? Describe(GraphHistoryDescriptionContext context) =>
            context.Command is AddNodeCommand
                ? new GraphHistoryDescription(
                    "custom.add",
                    changes: [new GraphHistoryPropertyChange("custom.value", 1, 2)])
                : null;
    }
}
