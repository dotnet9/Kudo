// GraphDocumentEditor 的结构化变更与批量通知测试。
using Kudo;
using Shouldly;

namespace Kudo.Core.Tests;

public class GraphDocumentEditorTests
{
    [Fact]
    public void Execute_and_undo_publish_deterministic_element_changes()
    {
        var document = new GraphDocument();
        var editor = new GraphDocumentEditor(document);
        var published = new List<GraphDocumentChangedEventArgs>();
        editor.Changed += (_, e) => published.Add(e);
        var command = new AddNodeCommand(new NodeModel { Id = "node-a" });

        var executed = editor.Execute(command);
        var undone = editor.Undo(command);

        executed.Nodes.AddedIds.ShouldBe(["node-a"]);
        executed.Kind.HasFlag(GraphChangeKind.Structure).ShouldBeTrue();
        undone.Nodes.RemovedIds.ShouldBe(["node-a"]);
        published.Select(item => item.Direction).ShouldBe([
            GraphDocumentEditDirection.Execute,
            GraphDocumentEditDirection.Undo
        ]);
        document.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void Batch_merges_multiple_commands_into_one_notification()
    {
        var document = new GraphDocument();
        var editor = new GraphDocumentEditor(document);
        var published = new List<GraphDocumentChangedEventArgs>();
        editor.Changed += (_, e) => published.Add(e);

        using (editor.BeginBatch())
        {
            editor.Execute(new AddNodeCommand(new NodeModel { Id = "node-a" }));
            editor.Execute(new AddNodeCommand(new NodeModel { Id = "node-b" }));
            editor.Execute(new AddAnnotationCommand(new AnnotationModel { Id = "note" }));
        }

        published.Count.ShouldBe(1);
        published[0].Changes.Nodes.AddedIds.ShouldBe(["node-a", "node-b"]);
        published[0].Changes.Annotations.AddedIds.ShouldBe(["note"]);
        published[0].Direction.ShouldBe(GraphDocumentEditDirection.Execute);
    }

    [Fact]
    public void Unknown_extension_command_uses_safe_full_refresh_fallback()
    {
        var editor = new GraphDocumentEditor(new GraphDocument());

        var changes = editor.Execute(new UnknownCommand());

        changes.RequiresFullRefresh.ShouldBeTrue();
        changes.Nodes.RequiresReset.ShouldBeTrue();
    }

    [Fact]
    public void Canvas_settings_command_publishes_settings_change()
    {
        var document = new GraphDocument();
        var editor = new GraphDocumentEditor(document);
        var command = new SetCanvasSettingsCommand(
            document.Id,
            document.CanvasSettings,
            document.CanvasSettings with { SymbolNameVisibility = GraphLabelVisibility.Always });

        var changes = editor.Execute(command);

        changes.Kind.ShouldBe(GraphChangeKind.Settings);
        document.CanvasSettings.SymbolNameVisibility.ShouldBe(GraphLabelVisibility.Always);
    }

    private sealed class UnknownCommand : IGraphCommand
    {
        public string Name => "未知扩展命令";

        public void Execute(GraphDocument document) => document.Title = "已执行";

        public void Undo(GraphDocument document) => document.Title = string.Empty;
    }
}
