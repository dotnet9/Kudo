using System.Security.Cryptography;
using Kudo;
using Kudo.Core.Serialization;

namespace Kudo.Core.Documents;

public sealed class GraphTaskAssetStore
{
    public const string AssetsDirectoryName = "Assets";

    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".bmp",
        ".svg"
    };

    private readonly GraphDocumentSession _session;

    public GraphTaskAssetStore(GraphDocumentSession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    public async Task<string> ImportImageAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                GraphTaskLocalization.Get(GraphTaskLocalization.ImageAssetNotFound),
                source);
        }

        var extension = Path.GetExtension(source);
        if (!SupportedImageExtensions.Contains(extension))
        {
            throw new NotSupportedException(GraphTaskLocalization.Format(
                GraphTaskLocalization.ImageFormatNotSupported,
                extension));
        }

        var root = _session.EnsureWorkingDirectory();
        var assetsDirectory = Path.Combine(root, AssetsDirectoryName);
        Directory.CreateDirectory(assetsDirectory);
        var hash = await ComputeHashAsync(source, cancellationToken).ConfigureAwait(false);
        var safeName = SanitizeFileName(Path.GetFileNameWithoutExtension(source));
        var fileName = $"{safeName}_{hash[..8].ToLowerInvariant()}{extension.ToLowerInvariant()}";
        var target = Path.Combine(assetsDirectory, fileName);
        if (!File.Exists(target))
        {
            try
            {
                await CopyFileAsync(
                        source,
                        target,
                        cancellationToken,
                        overwrite: false)
                    .ConfigureAwait(false);
            }
            catch (IOException) when (File.Exists(target))
            {
                // Another importer completed the same content-addressed asset first.
            }
        }

        return $"{AssetsDirectoryName}/{fileName}";
    }

    public string ImportImage(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                GraphTaskLocalization.Get(GraphTaskLocalization.ImageAssetNotFound),
                source);
        }

        var extension = Path.GetExtension(source);
        if (!SupportedImageExtensions.Contains(extension))
        {
            throw new NotSupportedException(GraphTaskLocalization.Format(
                GraphTaskLocalization.ImageFormatNotSupported,
                extension));
        }

        var root = _session.EnsureWorkingDirectory();
        var assetsDirectory = Path.Combine(root, AssetsDirectoryName);
        Directory.CreateDirectory(assetsDirectory);
        using var stream = File.OpenRead(source);
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        var safeName = SanitizeFileName(Path.GetFileNameWithoutExtension(source));
        var fileName = $"{safeName}_{hash[..8].ToLowerInvariant()}{extension.ToLowerInvariant()}";
        var target = Path.Combine(assetsDirectory, fileName);
        if (!File.Exists(target))
        {
            try
            {
                CopyFile(source, target, overwrite: false);
            }
            catch (IOException) when (File.Exists(target))
            {
                // Another importer completed the same content-addressed asset first.
            }
        }

        return $"{AssetsDirectoryName}/{fileName}";
    }

    public string? ResolvePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || _session.WorkingDirectory is null)
        {
            return null;
        }

        return TryResolvePath(_session.WorkingDirectory, relativePath, out var path)
            ? path
            : null;
    }

    public static Task CopyAssetsAsync(
        string sourceTaskDirectory,
        string targetTaskDirectory) =>
        CopyAssetsAsync(sourceTaskDirectory, targetTaskDirectory, CancellationToken.None);

    public static async Task CopyAssetsAsync(
        string sourceTaskDirectory,
        string targetTaskDirectory,
        CancellationToken cancellationToken)
    {
        var sourceAssets = Path.Combine(Path.GetFullPath(sourceTaskDirectory), AssetsDirectoryName);
        if (!Directory.Exists(sourceAssets))
        {
            return;
        }

        var targetRoot = Path.GetFullPath(targetTaskDirectory);
        var targetAssets = Path.Combine(targetRoot, AssetsDirectoryName);
        Directory.CreateDirectory(targetAssets);
        foreach (var sourcePath in Directory.EnumerateFiles(sourceAssets, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceAssets, sourcePath);
            var targetPath = Path.GetFullPath(Path.Combine(targetAssets, relativePath));
            if (!IsWithinDirectory(targetAssets, targetPath))
            {
                throw new InvalidDataException(
                    GraphTaskLocalization.Get(GraphTaskLocalization.AssetPathEscapesDirectory));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await CopyFileAsync(sourcePath, targetPath, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task CopyReferencedAssetsAsync(
        GraphDocument document,
        string sourceTaskDirectory,
        string targetTaskDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await CopyAssetsAsync(
                sourceTaskDirectory,
                targetTaskDirectory,
                GetReferencedAssetPaths(document),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static void ValidateReferencedAssets(
        GraphDocument document,
        string taskDirectory)
    {
        ArgumentNullException.ThrowIfNull(document);
        var root = Path.GetFullPath(taskDirectory);
        foreach (var relativePath in GetReferencedAssetPaths(document))
        {
            var assetPath = ResolveTaskAssetPath(root, relativePath);
            if (!File.Exists(assetPath))
            {
                throw new FileNotFoundException(
                    GraphTaskLocalization.Format(
                        GraphTaskLocalization.ReferencedAssetNotFound,
                        relativePath),
                    assetPath);
            }
        }
    }

    public static IReadOnlyList<string> GetReferencedAssetPaths(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return GetReferencedAssetPathsCore(document).ToArray();
    }

    public static string ResolveAssetPath(string taskDirectory, string relativePath) =>
        ResolveTaskAssetPath(Path.GetFullPath(taskDirectory), relativePath);

    public static Task CopyAssetsAsync(
        string sourceTaskDirectory,
        string targetTaskDirectory,
        IEnumerable<string> relativePaths) =>
        CopyAssetsAsync(
            sourceTaskDirectory,
            targetTaskDirectory,
            relativePaths,
            CancellationToken.None);

    public static async Task CopyAssetsAsync(
        string sourceTaskDirectory,
        string targetTaskDirectory,
        IEnumerable<string> relativePaths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        var sourceRoot = Path.GetFullPath(sourceTaskDirectory);
        var targetRoot = Path.GetFullPath(targetTaskDirectory);

        foreach (var relativePath in relativePaths.Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = ResolveTaskAssetPath(sourceRoot, relativePath);

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    GraphTaskLocalization.Format(
                        GraphTaskLocalization.ReferencedAssetNotFound,
                        relativePath),
                    sourcePath);
            }

            var targetPath = ResolveTaskAssetPath(targetRoot, relativePath);

            if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await CopyFileAsync(sourcePath, targetPath, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<string> GetReferencedAssetPathsCore(GraphDocument document) =>
        (document.Nodes ?? [])
            .SelectMany(GetReferencedAssetPaths)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> GetReferencedAssetPaths(NodeModel node)
    {
        var background = (node.Appearance ?? new GraphNodeAppearance()).Normalize().BackgroundImage;
        if (background is not null)
        {
            yield return background;
        }

        if (node.Properties.TryGetValue(GraphNodePropertyKeys.BackgroundImageMappings, out var value) &&
            value is IReadOnlyDictionary<string, object?> mappings)
        {
            foreach (var mapped in mappings.Values.OfType<string>())
            {
                yield return mapped;
            }
        }
    }

    private static string ResolveTaskAssetPath(string taskRoot, string relativePath)
    {
        var assetsRoot = Path.Combine(taskRoot, AssetsDirectoryName);
        if (!IsTaskAssetPath(relativePath) ||
            !TryResolvePath(taskRoot, relativePath, out var path) ||
            !IsWithinDirectory(assetsRoot, path))
        {
            throw new InvalidDataException(GraphTaskLocalization.Format(
                GraphTaskLocalization.InvalidAssetPath,
                relativePath));
        }

        return path;
    }

    public static bool TryResolvePath(
        string taskDirectory,
        string relativePath,
        out string path)
    {
        path = string.Empty;
        if (Path.IsPathRooted(relativePath))
        {
            return false;
        }

        var root = Path.GetFullPath(taskDirectory);
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinDirectory(root, candidate))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    private static async Task<string> ComputeHashAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string targetPath,
        CancellationToken cancellationToken,
        bool overwrite = true)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var input = new FileStream(
                             sourcePath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, targetPath, overwrite);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    private static void CopyFile(string sourcePath, string targetPath, bool overwrite)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(sourcePath, temporaryPath);
            File.Move(temporaryPath, targetPath, overwrite);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static bool IsTaskAssetPath(string path)
    {
        if (Path.IsPathRooted(path) ||
            !path.StartsWith($"{AssetsDirectoryName}/", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = path.Split('/');
        return segments.Length > 1 && segments.All(segment =>
            !string.IsNullOrWhiteSpace(segment) &&
            segment is not "." and not "..");
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "image" : sanitized;
    }

    private static bool IsWithinDirectory(string directory, string path)
    {
        var root = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path);
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
