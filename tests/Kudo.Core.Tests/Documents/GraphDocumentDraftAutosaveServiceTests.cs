using Kudo.Core.Documents;
using Shouldly;

namespace Kudo.Core.Tests.Documents;

public class GraphDocumentDraftAutosaveServiceTests
{
    [Fact]
    public async Task Autosave_service_debounces_changes_and_removes_saved_draft()
    {
        using var directory = new TemporaryDirectory();
        var manager = new GraphDocumentManager();
        var session = manager.Create("自动保存任务");
        var store = new GraphDocumentDraftStore(directory.Path);
        await using var service = new GraphDocumentDraftAutosaveService(
            manager,
            store,
            TimeSpan.FromMilliseconds(20));
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DraftSaved += _ => saved.TrySetResult();

        session.ExecuteCommand(new AddNodeCommand(new NodeModel { Id = "first" }));
        session.ExecuteCommand(new AddNodeCommand(new NodeModel { Id = "second" }));
        await saved.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        var result = await store.LoadAsync(TestContext.Current.CancellationToken);
        result.Drafts.Single().Document.Nodes.Count.ShouldBe(2);

        session.AcceptSaved(Path.Combine(directory.Path, "saved-task"));
        Directory.Exists(store.GetDraftPath(session.SessionId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Autosave_service_restores_dirty_sessions_and_can_discard_them()
    {
        using var directory = new TemporaryDirectory();
        var store = new GraphDocumentDraftStore(directory.Path);
        var original = new GraphDocumentSession(
            new GraphDocument { Title = "待恢复任务" },
            Path.Combine(directory.Path, "original-task"),
            "restore-session");
        await store.SaveAsync(original, TestContext.Current.CancellationToken);
        var manager = new GraphDocumentManager();
        await using var service = new GraphDocumentDraftAutosaveService(
            manager,
            store,
            TimeSpan.FromMinutes(1));

        var result = await service.RestoreAsync(TestContext.Current.CancellationToken);

        result.Warnings.ShouldBeEmpty();
        var restored = result.Sessions.Single();
        restored.SessionId.ShouldBe("restore-session");
        restored.TaskDirectory.ShouldBe(Path.Combine(directory.Path, "original-task"));
        restored.IsDirty.ShouldBeTrue();
        manager.ActiveSession.ShouldBeSameAs(restored);

        service.Discard(restored);
        Directory.Exists(store.GetDraftPath(restored.SessionId)).ShouldBeFalse();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "Kudo.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, true);
    }
}
