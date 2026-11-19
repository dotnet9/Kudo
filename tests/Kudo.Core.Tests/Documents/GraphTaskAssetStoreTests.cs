using Kudo.Core.Documents;
using Shouldly;

namespace Kudo.Core.Tests.Documents;

public class GraphTaskAssetStoreTests
{
    [Fact]
    public async Task Copy_assets_copies_only_requested_relative_paths()
    {
        using var directory = new TemporaryDirectory();
        var sourceDirectory = Path.Combine(directory.Path, "source");
        var targetDirectory = Path.Combine(directory.Path, "target");
        var sourceAssets = Path.Combine(sourceDirectory, GraphTaskAssetStore.AssetsDirectoryName);
        Directory.CreateDirectory(sourceAssets);
        await File.WriteAllBytesAsync(
            Path.Combine(sourceAssets, "used.png"),
            [1, 2, 3],
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(sourceAssets, "unused.png"),
            [4, 5, 6],
            TestContext.Current.CancellationToken);

        await GraphTaskAssetStore.CopyAssetsAsync(
            sourceDirectory,
            targetDirectory,
            ["Assets/used.png", "Assets/used.png"],
            TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(targetDirectory, "Assets", "used.png")).ShouldBeTrue();
        File.Exists(Path.Combine(targetDirectory, "Assets", "unused.png")).ShouldBeFalse();
    }

    [Fact]
    public async Task Concurrent_image_imports_share_one_complete_asset_without_temporary_files()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = Path.Combine(directory.Path, "shared.png");
        var sourceBytes = new byte[4 * 1024 * 1024];
        new Random(42).NextBytes(sourceBytes);
        await File.WriteAllBytesAsync(
            sourcePath,
            sourceBytes,
            TestContext.Current.CancellationToken);
        var workingDirectory = Path.Combine(directory.Path, "working");
        var session = new GraphDocumentSession(
            new GraphDocument(),
            workingDirectory: workingDirectory);
        var store = new GraphTaskAssetStore(session);

        var relativePaths = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            store.ImportImageAsync(sourcePath, TestContext.Current.CancellationToken)));

        relativePaths.Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
        var targetPath = Path.Combine(
            workingDirectory,
            relativePaths[0].Replace('/', Path.DirectorySeparatorChar));
        (await File.ReadAllBytesAsync(targetPath, TestContext.Current.CancellationToken))
            .ShouldBe(sourceBytes);
        Directory.EnumerateFiles(
                workingDirectory,
                "*.tmp",
                SearchOption.AllDirectories)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Image_imports_reuse_same_content_and_separate_same_name_different_content()
    {
        using var directory = new TemporaryDirectory();
        var firstDirectory = Path.Combine(directory.Path, "first");
        var secondDirectory = Path.Combine(directory.Path, "second");
        var thirdDirectory = Path.Combine(directory.Path, "third");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        Directory.CreateDirectory(thirdDirectory);
        var firstSource = Path.Combine(firstDirectory, "background.png");
        var sameContentSource = Path.Combine(secondDirectory, "background.png");
        var differentContentSource = Path.Combine(thirdDirectory, "background.png");
        await File.WriteAllBytesAsync(
            firstSource,
            [1, 2, 3],
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            sameContentSource,
            [1, 2, 3],
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            differentContentSource,
            [3, 2, 1],
            TestContext.Current.CancellationToken);
        var workingDirectory = Path.Combine(directory.Path, "working");
        var session = new GraphDocumentSession(
            new GraphDocument(),
            workingDirectory: workingDirectory);
        var store = new GraphTaskAssetStore(session);

        var first = await store.ImportImageAsync(
            firstSource,
            TestContext.Current.CancellationToken);
        var sameContent = await store.ImportImageAsync(
            sameContentSource,
            TestContext.Current.CancellationToken);
        var differentContent = await store.ImportImageAsync(
            differentContentSource,
            TestContext.Current.CancellationToken);

        sameContent.ShouldBe(first);
        differentContent.ShouldNotBe(first);
        var assetsDirectory = Path.Combine(
            workingDirectory,
            GraphTaskAssetStore.AssetsDirectoryName);
        Directory.EnumerateFiles(assetsDirectory, "background_*.png")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ShouldBe(new[]
            {
                Path.GetFileName(first),
                Path.GetFileName(differentContent)
            }.OrderBy(name => name, StringComparer.Ordinal));
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
