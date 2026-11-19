using Kudo.Core.Documents;
using Kudo.Core.Runtime;
using Kudo.Core.Serialization;
using Shouldly;

namespace Kudo.Core.Tests.Documents;

public class GraphDocumentManagerTests
{
    [Fact]
    public void Add_accepts_existing_document_and_makes_it_active()
    {
        var manager = new GraphDocumentManager();
        var document = new GraphDocument { Title = "已有文档" };

        var taskDirectory = Path.Combine(Path.GetTempPath(), "Kudo.Tests", GraphId.NewId());
        var session = manager.Add(document, taskDirectory);

        session.Document.ShouldBeSameAs(document);
        session.TaskDirectory.ShouldBe(Path.GetFullPath(taskDirectory));
        manager.ActiveSession.ShouldBeSameAs(session);
        manager.Sessions.ShouldContain(session);
    }

    [Fact]
    public void Add_rolls_back_session_when_a_change_subscriber_fails()
    {
        var manager = new GraphDocumentManager();
        var existing = manager.Create("已有任务");
        var notificationCount = 0;
        manager.SessionsChanged += (_, _) =>
        {
            notificationCount++;
            if (manager.Sessions.Count > 1)
            {
                throw new InvalidOperationException("Document creation failed.");
            }
        };

        Should.Throw<InvalidOperationException>(() => manager.Create("加载失败任务"));

        manager.Sessions.ShouldBe([existing]);
        manager.ActiveSession.ShouldBeSameAs(existing);
        notificationCount.ShouldBe(2);
    }

    [Fact]
    public void Sessions_keep_independent_viewport_runtime_and_dirty_state()
    {
        var manager = new GraphDocumentManager();
        var first = manager.Create("画板一");
        var second = manager.Create("画板二");

        first.Viewport = new GraphViewport(new GraphPoint(10, 20), 2);
        first.RuntimeStatus = RuntimeStatus.Running;
        first.MarkDirty();

        second.Viewport.ShouldBe(GraphViewport.Default);
        second.RuntimeStatus.ShouldBe(RuntimeStatus.Stopped);
        second.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void Runtime_status_changed_only_fires_when_the_status_changes()
    {
        var session = new GraphDocumentSession(new GraphDocument());
        var changeCount = 0;
        session.RuntimeStatusChanged += (_, _) => changeCount++;

        session.RuntimeStatus = RuntimeStatus.Running;
        session.RuntimeStatus = RuntimeStatus.Running;
        session.RuntimeStatus = RuntimeStatus.Paused;

        changeCount.ShouldBe(2);
    }

    [Fact]
    public void Session_content_changed_reports_every_edit_while_dirty()
    {
        var session = new GraphDocumentSession(new GraphDocument());
        var changedCount = 0;
        session.ContentChanged += (_, _) => changedCount++;

        session.ExecuteCommand(new AddNodeCommand(new NodeModel { Id = "first" }));
        session.ExecuteCommand(new AddNodeCommand(new NodeModel { Id = "second" }));
        session.MarkDirty();

        changedCount.ShouldBe(3);
        session.IsDirty.ShouldBeTrue();
        session.SessionId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Session_routes_execute_undo_and_redo_through_document_editor()
    {
        var session = new GraphDocumentSession(new GraphDocument());
        var changes = new List<GraphDocumentChangedEventArgs>();
        session.Editor.Changed += (_, e) => changes.Add(e);

        session.ExecuteCommand(new AddNodeCommand(new NodeModel { Id = "node" }));
        session.Undo().ShouldBeTrue();
        session.Redo().ShouldBeTrue();

        changes.Select(item => item.Direction).ShouldBe([
            GraphDocumentEditDirection.Execute,
            GraphDocumentEditDirection.Undo,
            GraphDocumentEditDirection.Redo
        ]);
        session.Document.FindNode("node").ShouldNotBeNull();
        session.LastChangeSet.Nodes.AddedIds.ShouldBe(["node"]);
    }

    [Fact]
    public void Closing_active_session_activates_previous_session()
    {
        var manager = new GraphDocumentManager();
        var first = manager.Create("画板一");
        var second = manager.Create("画板二");

        var closed = manager.Close(second);

        closed.ShouldBeTrue();
        manager.ActiveSession.ShouldBeSameAs(first);
        manager.Sessions.ShouldBe([first]);
    }

    [Fact]
    public void Undo_and_redo_follow_the_saved_revision()
    {
        var session = new GraphDocumentSession(new GraphDocument());
        var node = new NodeModel { Id = "node" };

        session.ExecuteCommand(new AddNodeCommand(node));

        session.IsDirty.ShouldBeTrue();
        session.History.CanUndo.ShouldBeTrue();
        session.Undo().ShouldBeTrue();
        session.IsDirty.ShouldBeFalse();
        session.History.CanRedo.ShouldBeTrue();

        session.Redo().ShouldBeTrue();
        session.AcceptSaved(Path.Combine(Path.GetTempPath(), "Kudo.Tests", GraphId.NewId()));
        session.IsDirty.ShouldBeFalse();

        session.Undo().ShouldBeTrue();
        session.IsDirty.ShouldBeTrue();
        session.Redo().ShouldBeTrue();
        session.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void Executing_after_undo_clears_redo_without_affecting_other_sessions()
    {
        var manager = new GraphDocumentManager();
        var first = manager.Create("画板一");
        var second = manager.Create("画板二");
        first.ExecuteCommand(new AddNodeCommand(new NodeModel { Id = "first" }));
        first.Undo().ShouldBeTrue();

        first.ExecuteCommand(new AddNodeCommand(new NodeModel { Id = "replacement" }));

        first.History.CanRedo.ShouldBeFalse();
        first.Document.FindNode("replacement").ShouldNotBeNull();
        second.History.CanUndo.ShouldBeFalse();
        second.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task Manager_saves_and_opens_task_directory_with_imported_assets()
    {
        using var directory = new TemporaryDirectory();
        var sourceImage = Path.Combine(directory.Path, "source.png");
        await File.WriteAllBytesAsync(
            sourceImage,
            [1, 2, 3, 4],
            TestContext.Current.CancellationToken);
        var manager = new GraphDocumentManager();
        var session = manager.Create("任务目录");
        var relativePath = await new GraphTaskAssetStore(session)
            .ImportImageAsync(sourceImage, TestContext.Current.CancellationToken);
        session.Document.AddNode(new NodeModel
        {
            Id = "image-node",
            Appearance = new GraphNodeAppearance { BackgroundImage = relativePath }
        });
        var unusedAsset = Path.Combine(
            session.WorkingDirectory.ShouldNotBeNull(),
            GraphTaskAssetStore.AssetsDirectoryName,
            "unused.png");
        await File.WriteAllBytesAsync(
            unusedAsset,
            [9, 8, 7],
            TestContext.Current.CancellationToken);
        session.MarkDirty();
        var taskDirectory = Path.Combine(directory.Path, "大一统模型");

        await manager.SaveAsync(session, taskDirectory, TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(taskDirectory, GraphDocumentManager.TaskFileName)).ShouldBeTrue();
        File.Exists(Path.Combine(taskDirectory, GraphDocumentManager.ManifestFileName)).ShouldBeTrue();
        File.Exists(Path.Combine(taskDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .ShouldBeTrue();
        File.Exists(Path.Combine(
            taskDirectory,
            GraphTaskAssetStore.AssetsDirectoryName,
            "unused.png")).ShouldBeFalse();
        session.TaskDirectory.ShouldBe(taskDirectory);
        session.IsDirty.ShouldBeFalse();

        var reopenedManager = new GraphDocumentManager();
        var reopened = await reopenedManager.OpenAsync(
            taskDirectory,
            TestContext.Current.CancellationToken);
        reopened.Document.Title.ShouldBe("任务目录");
        reopened.Document.FindNode("image-node")!.Appearance.BackgroundImage.ShouldBe(relativePath);
        reopened.WorkingDirectory.ShouldBe(taskDirectory);
    }

    [Fact]
    public async Task Manager_saves_and_opens_mdl_package_with_referenced_assets()
    {
        using var directory = new TemporaryDirectory();
        var sourceImage = Path.Combine(directory.Path, "source.png");
        await File.WriteAllBytesAsync(
            sourceImage,
            [1, 2, 3, 4],
            TestContext.Current.CancellationToken);
        var manager = new GraphDocumentManager();
        var session = manager.Create("压缩模型");
        var relativePath = await new GraphTaskAssetStore(session)
            .ImportImageAsync(sourceImage, TestContext.Current.CancellationToken);
        session.Document.AddNode(new NodeModel
        {
            Id = "image-node",
            Appearance = new GraphNodeAppearance { BackgroundImage = relativePath }
        });
        var unusedAsset = Path.Combine(
            session.WorkingDirectory.ShouldNotBeNull(),
            GraphTaskAssetStore.AssetsDirectoryName,
            "unused.png");
        await File.WriteAllBytesAsync(
            unusedAsset,
            [9, 8, 7],
            TestContext.Current.CancellationToken);
        session.MarkDirty();
        var packagePath = Path.Combine(directory.Path, "压缩模型.mdl");
        var sourceWorkingDirectory = session.WorkingDirectory.ShouldNotBeNull();

        await manager.SaveAsync(session, packagePath, TestContext.Current.CancellationToken);

        Directory.Exists(sourceWorkingDirectory).ShouldBeFalse();
        session.IsPackage.ShouldBeTrue();
        session.PackagePath.ShouldBe(Path.GetFullPath(packagePath));
        session.TaskDirectory.ShouldBeNull();
        session.IsDirty.ShouldBeFalse();
        using (var archive = System.IO.Compression.ZipFile.OpenRead(packagePath))
        {
            archive.Entries.Select(entry => entry.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ShouldBe(new[]
                {
                    GraphDocumentManager.ManifestFileName,
                    GraphDocumentManager.TaskFileName,
                    relativePath
                }.OrderBy(name => name, StringComparer.Ordinal));
        }

        var reopenedManager = new GraphDocumentManager();
        var reopened = await reopenedManager.OpenAsync(
            packagePath,
            TestContext.Current.CancellationToken);
        reopened.IsPackage.ShouldBeTrue();
        reopened.Document.Title.ShouldBe("压缩模型");
        reopened.Document.FindNode("image-node")!.Appearance.BackgroundImage.ShouldBe(relativePath);
        File.Exists(reopened.WorkingDirectory.ShouldNotBeNull() +
                    Path.DirectorySeparatorChar + relativePath.Replace('/', Path.DirectorySeparatorChar))
            .ShouldBeTrue();
        reopenedManager.Close(reopened).ShouldBeTrue();
        Directory.Exists(reopened.WorkingDirectory).ShouldBeFalse();
    }

    [Fact]
    public async Task Manager_reuses_open_session_for_the_same_task_directory()
    {
        using var directory = new TemporaryDirectory();
        var manager = new GraphDocumentManager();
        var source = manager.Create("任务目录");
        var taskDirectory = Path.Combine(directory.Path, "same-task");
        await manager.SaveAsync(
            source,
            taskDirectory,
            TestContext.Current.CancellationToken);
        manager.Create("临时画布");

        var reopened = await manager.OpenAsync(
            taskDirectory + Path.DirectorySeparatorChar,
            TestContext.Current.CancellationToken);

        reopened.ShouldBeSameAs(source);
        manager.ActiveSession.ShouldBeSameAs(source);
        manager.Sessions.Count.ShouldBe(2);
        manager.TryGetSessionByTaskDirectory(taskDirectory, out var existing).ShouldBeTrue();
        existing.ShouldBeSameAs(source);
    }

    [Fact]
    public async Task Manager_can_save_a_package_session_to_a_directory()
    {
        using var directory = new TemporaryDirectory();
        var manager = new GraphDocumentManager();
        var source = manager.Create("可转换模型");
        var packagePath = Path.Combine(directory.Path, "model.mdl");

        await manager.SaveAsync(source, packagePath, TestContext.Current.CancellationToken);
        var reopened = await manager.OpenAsync(packagePath, TestContext.Current.CancellationToken);
        var packageWorkingDirectory = reopened.WorkingDirectory.ShouldNotBeNull();
        var targetDirectory = Path.Combine(directory.Path, "directory-model");

        await manager.SaveAsync(reopened, targetDirectory, TestContext.Current.CancellationToken);

        reopened.IsPackage.ShouldBeFalse();
        reopened.PackagePath.ShouldBeNull();
        reopened.TaskDirectory.ShouldBe(targetDirectory);
        File.Exists(Path.Combine(targetDirectory, GraphDocumentManager.ManifestFileName)).ShouldBeTrue();
        File.Exists(Path.Combine(targetDirectory, GraphDocumentManager.TaskFileName)).ShouldBeTrue();
        Directory.Exists(packageWorkingDirectory).ShouldBeFalse();
    }

    [Fact]
    public async Task Manager_rejects_save_when_a_referenced_task_asset_is_missing()
    {
        using var directory = new TemporaryDirectory();
        var workingDirectory = Path.Combine(directory.Path, "working");
        Directory.CreateDirectory(workingDirectory);
        var document = new GraphDocument();
        document.AddNode(new NodeModel
        {
            Id = "missing-image",
            Appearance = new GraphNodeAppearance { BackgroundImage = "Assets/missing.png" }
        });
        var manager = new GraphDocumentManager();
        var session = manager.Add(document, workingDirectory: workingDirectory);
        session.MarkDirty();
        var taskDirectory = Path.Combine(directory.Path, "target");

        var exception = await Should.ThrowAsync<FileNotFoundException>(async () =>
            await manager.SaveAsync(
                session,
                taskDirectory,
                TestContext.Current.CancellationToken));

        exception.FileName.ShouldBe(Path.Combine(workingDirectory, "Assets", "missing.png"));
        File.Exists(Path.Combine(taskDirectory, GraphDocumentManager.TaskFileName)).ShouldBeFalse();
        session.IsDirty.ShouldBeTrue();
        session.TaskDirectory.ShouldBeNull();
    }

    [Fact]
    public async Task Manager_rejects_open_when_a_referenced_task_asset_is_missing()
    {
        using var directory = new TemporaryDirectory();
        var taskDirectory = Path.Combine(directory.Path, "incomplete-task");
        Directory.CreateDirectory(taskDirectory);
        var document = new GraphDocument();
        document.AddNode(new NodeModel
        {
            Id = "missing-image",
            Appearance = new GraphNodeAppearance { BackgroundImage = "Assets/missing.png" }
        });
        await using (var stream = File.Create(
                         Path.Combine(taskDirectory, GraphDocumentManager.TaskFileName)))
        {
            await new GraphTaskXmlSerializer().SaveAsync(
                stream,
                document,
                TestContext.Current.CancellationToken);
        }
        await using (var stream = File.Create(
                         Path.Combine(taskDirectory, GraphDocumentManager.ManifestFileName)))
        {
            await GraphTaskPackage.WriteManifestAsync(
                stream,
                TestContext.Current.CancellationToken);
        }
        var manager = new GraphDocumentManager();

        var exception = await Should.ThrowAsync<FileNotFoundException>(async () =>
            await manager.OpenAsync(taskDirectory, TestContext.Current.CancellationToken));

        exception.FileName.ShouldBe(Path.Combine(taskDirectory, "Assets", "missing.png"));
        manager.Sessions.ShouldBeEmpty();
        manager.ActiveSession.ShouldBeNull();
    }

    [Fact]
    public async Task Manager_duplicate_copies_only_referenced_assets()
    {
        using var directory = new TemporaryDirectory();
        var workingDirectory = Path.Combine(directory.Path, "working");
        var assetsDirectory = Path.Combine(workingDirectory, GraphTaskAssetStore.AssetsDirectoryName);
        Directory.CreateDirectory(assetsDirectory);
        await File.WriteAllBytesAsync(
            Path.Combine(assetsDirectory, "used.png"),
            [1, 2, 3],
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(assetsDirectory, "unused.png"),
            [4, 5, 6],
            TestContext.Current.CancellationToken);
        var document = new GraphDocument();
        document.AddNode(new NodeModel
        {
            Id = "image-node",
            Appearance = new GraphNodeAppearance { BackgroundImage = "Assets/used.png" }
        });
        var manager = new GraphDocumentManager();
        var source = manager.Add(document, workingDirectory: workingDirectory);
        GraphDocumentSession? duplicate = null;

        try
        {
            duplicate = await manager.DuplicateAsync(
                source,
                cancellationToken: TestContext.Current.CancellationToken);
            var duplicateDirectory = duplicate.WorkingDirectory.ShouldNotBeNull();

            File.Exists(Path.Combine(duplicateDirectory, "Assets", "used.png")).ShouldBeTrue();
            File.Exists(Path.Combine(duplicateDirectory, "Assets", "unused.png")).ShouldBeFalse();
            duplicate.Document.Id.ShouldNotBe(source.Document.Id);
        }
        finally
        {
            if (duplicate?.WorkingDirectory is { } duplicateDirectory &&
                Directory.Exists(duplicateDirectory))
            {
                Directory.Delete(duplicateDirectory, true);
            }
        }
    }

    [Fact]
    public void Command_batch_does_not_leave_untracked_dirty_state_after_full_undo()
    {
        var session = new GraphDocumentSession(new GraphDocument());
        using (session.Editor.BeginBatch())
        {
            session.ExecuteCommand(new AddNodeCommand(new NodeModel { Id = "first" }));
            session.ExecuteCommand(new AddNodeCommand(new NodeModel { Id = "second" }));
        }

        session.IsDirty.ShouldBeTrue();
        session.Undo().ShouldBeTrue();
        session.Undo().ShouldBeTrue();

        session.IsDirty.ShouldBeFalse();
        session.Document.Nodes.ShouldBeEmpty();
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
