using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using Kudo;
using Kudo.Core.Serialization;

namespace Kudo.Core.Documents;

public sealed record GraphDocumentDraft(
    string SessionId,
    GraphDocument Document,
    string? TaskDirectory,
    DateTimeOffset UpdatedAt,
    string DraftDirectory);

public sealed record GraphDocumentDraftLoadResult(
    IReadOnlyList<GraphDocumentDraft> Drafts,
    IReadOnlyList<string> Warnings);

public sealed class GraphDocumentDraftStore
{
    private const string DraftSearchPattern = "*.draft";
    private const string MetadataFileName = "draft.json";
    private readonly TimeProvider _timeProvider;
    private readonly GraphTaskXmlSerializer _serializer = new();

    public GraphDocumentDraftStore(string directoryPath, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        DirectoryPath = Path.GetFullPath(directoryPath);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string DirectoryPath { get; }

    public async Task<string> SaveAsync(
        GraphDocumentSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        Directory.CreateDirectory(DirectoryPath);
        var targetPath = GetDraftPath(session.SessionId);
        var temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(temporaryPath);
        try
        {
            var metadata = new GraphDocumentDraftMetadata(
                session.SessionId,
                session.TaskDirectory,
                _timeProvider.GetUtcNow());
            await using (var metadataStream = new FileStream(
                             Path.Combine(temporaryPath, MetadataFileName),
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                        metadataStream,
                        metadata,
                        GraphDocumentDraftJsonContext.Default.GraphDocumentDraftMetadata,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await using (var taskStream = new FileStream(
                             Path.Combine(temporaryPath, GraphDocumentManager.TaskFileName),
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous))
            {
                await _serializer.SaveAsync(taskStream, session.Document, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (session.WorkingDirectory is { } workingDirectory)
            {
                await GraphTaskAssetStore.CopyReferencedAssetsAsync(
                        session.Document,
                        workingDirectory,
                        temporaryPath,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            ReplaceDirectory(temporaryPath, targetPath);
            return targetPath;
        }
        finally
        {
            TryDeleteDirectory(temporaryPath);
        }
    }

    public async Task<GraphDocumentDraftLoadResult> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return new GraphDocumentDraftLoadResult([], []);
        }

        var drafts = new List<GraphDocumentDraft>();
        var warnings = new List<string>();
        foreach (var path in Directory.EnumerateDirectories(DirectoryPath, DraftSearchPattern))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var metadataStream = File.OpenRead(Path.Combine(path, MetadataFileName));
                var metadata = await JsonSerializer.DeserializeAsync(
                        metadataStream,
                        GraphDocumentDraftJsonContext.Default.GraphDocumentDraftMetadata,
                        cancellationToken)
                    .ConfigureAwait(false);
                await using var taskStream = File.OpenRead(
                    Path.Combine(path, GraphDocumentManager.TaskFileName));
                var document = await _serializer.LoadAsync(taskStream, cancellationToken)
                    .ConfigureAwait(false);
                if (metadata is null || string.IsNullOrWhiteSpace(metadata.SessionId))
                {
                    warnings.Add(GraphTaskLocalization.Format(
                        GraphTaskLocalization.DraftIncomplete,
                        Path.GetFileName(path)));
                    continue;
                }

                GraphTaskAssetStore.ValidateReferencedAssets(document, path);
                drafts.Add(new GraphDocumentDraft(
                    metadata.SessionId,
                    document,
                    metadata.TaskDirectory,
                    metadata.UpdatedAt,
                    path));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or JsonException or XmlException or GraphSerializationException)
            {
                warnings.Add(GraphTaskLocalization.Format(
                    GraphTaskLocalization.DraftLoadFailed,
                    Path.GetFileName(path),
                    exception.Message));
            }
        }

        return new GraphDocumentDraftLoadResult(
            drafts.OrderBy(draft => draft.UpdatedAt).ToArray(),
            warnings);
    }

    public bool Delete(GraphDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Delete(session.SessionId);
    }

    public bool Delete(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var path = GetDraftPath(sessionId);
        if (!Directory.Exists(path))
        {
            return false;
        }

        Directory.Delete(path, true);
        return true;
    }

    public string GetDraftPath(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sessionId));
        var name = Convert.ToHexString(bytes).ToLowerInvariant();
        return Path.Combine(DirectoryPath, $"{name}.draft");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void ReplaceDirectory(string sourcePath, string targetPath)
    {
        if (!Directory.Exists(targetPath))
        {
            Directory.Move(sourcePath, targetPath);
            return;
        }

        var backupPath = $"{targetPath}.{Guid.NewGuid():N}.bak";
        Directory.Move(targetPath, backupPath);
        try
        {
            Directory.Move(sourcePath, targetPath);
            TryDeleteDirectory(backupPath);
        }
        catch
        {
            if (!Directory.Exists(targetPath) && Directory.Exists(backupPath))
            {
                Directory.Move(backupPath, targetPath);
            }

            throw;
        }
    }
}

internal sealed record GraphDocumentDraftMetadata(
    string SessionId,
    string? TaskDirectory,
    DateTimeOffset UpdatedAt);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
[JsonSerializable(typeof(GraphDocumentDraftMetadata))]
internal partial class GraphDocumentDraftJsonContext : JsonSerializerContext;
