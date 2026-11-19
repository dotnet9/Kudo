using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Kudo.Core.Documents;

/// <summary>
/// Defines the small package envelope shared by directory tasks and .mdl files.
/// </summary>
public static class GraphTaskPackage
{
    public const string PackageExtension = ".mdl";
    public const string ManifestFileName = "Manifest.xml";
    public const string TaskFileName = "Task.xml";
    public const string AssetsDirectoryName = "Assets";
    public const string Format = "Kudo.Package";
    public const string Version = "1.0";

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static bool IsPackagePath(string path) =>
        string.Equals(Path.GetExtension(path), PackageExtension, StringComparison.OrdinalIgnoreCase);

    public static async Task WriteManifestAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = Utf8NoBom.GetBytes(
            $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            $"<Package Format=\"{Format}\" Version=\"{Version}\" Entry=\"{TaskFileName}\" />\n");
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public static async Task ValidateManifestAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            CloseInput = false
        };
        using var reader = XmlReader.Create(stream, settings);
        await reader.MoveToContentAsync().ConfigureAwait(false);
        if (reader.NodeType != XmlNodeType.Element ||
            !string.Equals(reader.Name, "Package", StringComparison.Ordinal) ||
            !string.Equals(reader.GetAttribute("Format"), Format, StringComparison.Ordinal) ||
            !string.Equals(reader.GetAttribute("Version"), Version, StringComparison.Ordinal) ||
            !string.Equals(reader.GetAttribute("Entry"), TaskFileName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The package manifest is invalid.");
        }

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                throw new InvalidDataException("The package manifest is invalid.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public static async Task ExtractAsync(
        string packagePath,
        string targetDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        Directory.CreateDirectory(targetDirectory);

        await using var packageStream = new FileStream(
            packagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);
        Dictionary<string, ZipArchiveEntry> entries;
        try
        {
            entries = archive.Entries.ToDictionary(
                entry => entry.FullName,
                StringComparer.Ordinal);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The package contains duplicate entries.", exception);
        }

        if (!entries.TryGetValue(ManifestFileName, out var manifestEntry) ||
            !entries.TryGetValue(TaskFileName, out var taskEntry))
        {
            throw new InvalidDataException(
                $"The package must contain {ManifestFileName} and {TaskFileName}.");
        }

        await using (var manifestStream = manifestEntry.Open())
        {
            await ValidateManifestAsync(manifestStream, cancellationToken).ConfigureAwait(false);
        }

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryName = entry.FullName.EndsWith("/", StringComparison.Ordinal)
                ? entry.FullName[..^1]
                : entry.FullName;
            ValidateEntryName(entryName);
            if (!string.Equals(entryName, ManifestFileName, StringComparison.Ordinal) &&
                !string.Equals(entryName, TaskFileName, StringComparison.Ordinal) &&
                !string.Equals(entryName, AssetsDirectoryName, StringComparison.Ordinal) &&
                !entryName.StartsWith($"{AssetsDirectoryName}/", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unsupported package entry '{entry.FullName}'.");
            }

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            var destination = ResolveEntryPath(targetDirectory, entryName);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = entry.Open();
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }
    }

    public static void ValidateEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) ||
            entryName.Contains("\\", StringComparison.Ordinal) ||
            entryName.StartsWith("/", StringComparison.Ordinal) ||
            entryName.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException($"Invalid package entry path '{entryName}'.");
        }
    }

    public static string ResolveEntryPath(string rootDirectory, string entryName)
    {
        ValidateEntryName(entryName);
        var root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(
            root,
            entryName.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Package entry '{entryName}' escapes the package root.");
        }

        return candidate;
    }
}
