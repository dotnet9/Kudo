using Kudo.Core.Documents;
using Shouldly;

namespace Kudo.Core.Tests.Documents;

public class GraphDocumentDraftStoreTests
{
    [Fact]
    public async Task Draft_store_round_trips_session_and_replaces_previous_snapshot()
    {
        using var directory = new TemporaryDirectory();
        var timestamp = new DateTimeOffset(2026, 8, 3, 8, 30, 0, TimeSpan.Zero);
        var store = new GraphDocumentDraftStore(directory.Path, new FixedTimeProvider(timestamp));
        var document = new GraphDocument { Title = "草稿任务" };
        document.AddNode(new NodeModel
        {
            Id = "image-node",
            Appearance = new GraphNodeAppearance { BackgroundImage = "Assets/a.png" }
        });
        var taskDirectory = Path.Combine(directory.Path, "source-task");
        var workingDirectory = Path.Combine(directory.Path, "working-task");
        var session = new GraphDocumentSession(
            document,
            taskDirectory,
            "session/unsafe",
            workingDirectory);
        Directory.CreateDirectory(Path.Combine(workingDirectory, GraphTaskAssetStore.AssetsDirectoryName));
        File.WriteAllBytes(
            Path.Combine(workingDirectory, GraphTaskAssetStore.AssetsDirectoryName, "a.png"),
            [1, 2, 3]);
        File.WriteAllBytes(
            Path.Combine(workingDirectory, GraphTaskAssetStore.AssetsDirectoryName, "unused.png"),
            [4, 5, 6]);

        var path = await store.SaveAsync(session, TestContext.Current.CancellationToken);
        document.Title = "更新后的草稿";
        await store.SaveAsync(session, TestContext.Current.CancellationToken);
        var result = await store.LoadAsync(TestContext.Current.CancellationToken);

        Path.GetDirectoryName(path).ShouldBe(directory.Path);
        Path.GetFileName(path).ShouldNotContain("session");
        result.Warnings.ShouldBeEmpty();
        var draft = result.Drafts.Single();
        draft.SessionId.ShouldBe("session/unsafe");
        draft.TaskDirectory.ShouldBe(Path.GetFullPath(taskDirectory));
        draft.Document.Title.ShouldBe("更新后的草稿");
        draft.UpdatedAt.ShouldBe(timestamp);
        File.Exists(Path.Combine(
            draft.DraftDirectory,
            GraphTaskAssetStore.AssetsDirectoryName,
            "a.png")).ShouldBeTrue();
        File.Exists(Path.Combine(
            draft.DraftDirectory,
            GraphTaskAssetStore.AssetsDirectoryName,
            "unused.png")).ShouldBeFalse();
        Directory.EnumerateDirectories(directory.Path, "*.draft").Count().ShouldBe(1);
        Directory.EnumerateDirectories(directory.Path).ShouldNotContain(path =>
            path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));

        store.Delete(session).ShouldBeTrue();
        store.Delete(session).ShouldBeFalse();
    }

    [Fact]
    public async Task Draft_store_skips_corrupted_files_and_reports_warning()
    {
        using var directory = new TemporaryDirectory();
        var brokenDirectory = Path.Combine(directory.Path, "broken.draft");
        Directory.CreateDirectory(brokenDirectory);
        File.WriteAllText(Path.Combine(brokenDirectory, "draft.json"), "{ invalid json");
        var store = new GraphDocumentDraftStore(directory.Path);

        var result = await store.LoadAsync(TestContext.Current.CancellationToken);

        result.Drafts.ShouldBeEmpty();
        result.Warnings.Count.ShouldBe(1);
        result.Warnings[0].ShouldContain("broken.draft");
    }

    [Fact]
    public async Task Draft_store_skips_snapshot_with_missing_referenced_asset()
    {
        using var directory = new TemporaryDirectory();
        var workingDirectory = Path.Combine(directory.Path, "working-task");
        var assetsDirectory = Path.Combine(workingDirectory, GraphTaskAssetStore.AssetsDirectoryName);
        Directory.CreateDirectory(assetsDirectory);
        File.WriteAllBytes(Path.Combine(assetsDirectory, "used.png"), [1, 2, 3]);
        var document = new GraphDocument();
        document.AddNode(new NodeModel
        {
            Id = "image-node",
            Appearance = new GraphNodeAppearance { BackgroundImage = "Assets/used.png" }
        });
        var session = new GraphDocumentSession(
            document,
            sessionId: "missing-asset",
            workingDirectory: workingDirectory);
        var store = new GraphDocumentDraftStore(directory.Path);
        var draftDirectory = await store.SaveAsync(
            session,
            TestContext.Current.CancellationToken);
        File.Delete(Path.Combine(draftDirectory, "Assets", "used.png"));

        var result = await store.LoadAsync(TestContext.Current.CancellationToken);

        result.Drafts.ShouldBeEmpty();
        result.Warnings.Count.ShouldBe(1);
        result.Warnings[0].ShouldContain("used.png");
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
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
