// 文档会话与管理器：协调编辑器、历史、Dirty 状态、任务目录和持久化生命周期。
using System.IO.Compression;
using Kudo;
using Kudo.Core.Runtime;
using Kudo.Core.Serialization;

namespace Kudo.Core.Documents;

public sealed class GraphDocumentSession
{
    private bool _hasUntrackedChanges;
    private bool _lastKnownDirty;
    private bool _ownsWorkingDirectory;
    private long _savedRevision;
    private RuntimeStatus _runtimeStatus = RuntimeStatus.Stopped;

    public GraphDocumentSession(
        GraphDocument document,
        string? taskDirectory = null,
        string? sessionId = null,
        string? workingDirectory = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        TaskDirectory = NormalizeDirectory(taskDirectory);
        WorkingDirectory = NormalizeDirectory(workingDirectory) ?? TaskDirectory;
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? GraphId.NewId() : sessionId;
        Editor = new GraphDocumentEditor(Document);
        History.Changed += HandleHistoryChanged;
        Editor.Changed += HandleEditorChanged;
    }

    public string SessionId { get; }

    public GraphDocument Document { get; }

    public string? TaskDirectory { get; private set; }

    public string? PackagePath { get; private set; }

    public string? StoragePath => PackagePath ?? TaskDirectory;

    public bool IsPackage => PackagePath is not null;

    internal bool OwnsWorkingDirectory => _ownsWorkingDirectory;

    public string? WorkingDirectory { get; private set; }

    public GraphViewport Viewport { get; set; } = GraphViewport.Default;

    public RuntimeStatus RuntimeStatus
    {
        get => _runtimeStatus;
        set
        {
            if (_runtimeStatus == value)
            {
                return;
            }

            _runtimeStatus = value;
            RuntimeStatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public GraphCommandHistory History { get; } = new();

    public GraphDocumentEditor Editor { get; }

    public GraphChangeSet LastChangeSet => Editor.LastChangeSet;

    public bool IsDirty => _hasUntrackedChanges || History.CurrentRevision != _savedRevision;

    public event EventHandler? DirtyStateChanged;

    public event EventHandler? ContentChanged;

    public event EventHandler? RuntimeStatusChanged;

    public void MarkDirty() => Synchronize();

    public GraphChangeSet Synchronize(GraphChangeSet? changes = null)
    {
        return Editor.Synchronize(changes);
    }

    public GraphChangeSet ExecuteCommand(
        IGraphCommand command,
        GraphOperationContext? operationContext = null) =>
        History.Execute(Editor, command, operationContext);

    public GraphChangeSet RecordExecutedCommand(
        IGraphCommand command,
        GraphOperationContext? operationContext = null)
    {
        var changes = Editor.RecordExecuted(command);
        History.RecordExecuted(command, operationContext);
        return changes;
    }

    public bool Undo() => History.Undo(Editor);

    public bool Redo() => History.Redo(Editor);

    public string EnsureWorkingDirectory()
    {
        if (WorkingDirectory is null)
        {
            WorkingDirectory = Path.Combine(
                Path.GetTempPath(),
                "Kudo",
                "sessions",
                SessionId);
            _ownsWorkingDirectory = true;
        }

        Directory.CreateDirectory(WorkingDirectory);
        return WorkingDirectory;
    }

    public void AcceptSaved(string? taskDirectory = null)
    {
        PackagePath = null;
        if (!string.IsNullOrWhiteSpace(taskDirectory))
        {
            TaskDirectory = NormalizeDirectory(taskDirectory);
            WorkingDirectory = TaskDirectory;
            _ownsWorkingDirectory = false;
        }
        _hasUntrackedChanges = false;
        _savedRevision = History.CurrentRevision;
        RaiseDirtyStateChanged();
    }

    internal void AcceptPackageSaved(string packagePath, string workingDirectory)
    {
        PackagePath = NormalizeFilePath(packagePath);
        TaskDirectory = null;
        WorkingDirectory = NormalizeDirectory(workingDirectory);
        _ownsWorkingDirectory = true;
        _hasUntrackedChanges = false;
        _savedRevision = History.CurrentRevision;
        RaiseDirtyStateChanged();
    }

    internal void ReleasePackageWorkingDirectory(string? workingDirectory = null)
    {
        if (workingDirectory is null && !IsPackage && !_ownsWorkingDirectory)
        {
            return;
        }

        var path = workingDirectory ?? WorkingDirectory;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void HandleHistoryChanged(object? sender, EventArgs e)
    {
        ContentChanged?.Invoke(this, EventArgs.Empty);
        RaiseDirtyStateChanged();
    }

    private void HandleEditorChanged(object? sender, GraphDocumentChangedEventArgs e)
    {
        if (e.Direction != GraphDocumentEditDirection.Synchronize)
        {
            return;
        }

        _hasUntrackedChanges = true;
        ContentChanged?.Invoke(this, EventArgs.Empty);
        RaiseDirtyStateChanged();
    }

    private void RaiseDirtyStateChanged()
    {
        var isDirty = IsDirty;
        if (_lastKnownDirty == isDirty)
        {
            return;
        }

        _lastKnownDirty = isDirty;
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string? NormalizeDirectory(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string NormalizeFilePath(string path) =>
        Path.GetFullPath(path.Trim());
}

public sealed class GraphDocumentManager
{
    public const string TaskFileName = GraphTaskPackage.TaskFileName;
    public const string ManifestFileName = GraphTaskPackage.ManifestFileName;
    public const string PackageExtension = GraphTaskPackage.PackageExtension;

    private static readonly StringComparer DirectoryComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly List<GraphDocumentSession> _sessions = [];
    private readonly GraphTaskXmlSerializer _serializer = new();

    public IReadOnlyList<GraphDocumentSession> Sessions => _sessions;

    public GraphDocumentSession? ActiveSession { get; private set; }

    public event EventHandler? SessionsChanged;

    public GraphDocumentSession Create(string? title = null)
    {
        var document = new GraphDocument();
        if (!string.IsNullOrWhiteSpace(title))
        {
            document.Title = title;
        }

        return Add(document);
    }

    public GraphDocumentSession Add(
        GraphDocument document,
        string? taskDirectory = null,
        string? sessionId = null,
        string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var session = new GraphDocumentSession(document, taskDirectory, sessionId, workingDirectory);
        AddSession(session);
        return session;
    }

    public async Task<GraphDocumentSession> OpenAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            return await OpenDirectoryAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }

        if (File.Exists(fullPath) && GraphTaskPackage.IsPackagePath(fullPath))
        {
            return await OpenPackageAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }

        throw new FileNotFoundException(
            GraphTaskLocalization.Format(GraphTaskLocalization.TaskDirectoryNotFound, fullPath),
            fullPath);
    }

    public async Task<GraphDocumentSession> OpenDirectoryAsync(
        string taskDirectory,
        CancellationToken cancellationToken = default)
    {
        var directory = RequireTaskDirectory(taskDirectory);
        if (TryFindSessionByTaskDirectory(directory, out var existingSession))
        {
            SetActive(existingSession);
            return existingSession;
        }

        var taskPath = Path.Combine(directory, TaskFileName);
        await using var stream = File.OpenRead(taskPath);
        var document = await _serializer.LoadAsync(stream, cancellationToken);
        GraphTaskAssetStore.ValidateReferencedAssets(document, directory);
        return Add(document, directory);
    }

    public async Task<GraphDocumentSession> OpenPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        var path = RequirePackagePath(packagePath);
        if (TryFindSessionByPackagePath(path, out var existingSession))
        {
            SetActive(existingSession);
            return existingSession;
        }

        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "Kudo",
            "sessions",
            $"package-{Guid.NewGuid():N}");
        try
        {
            await GraphTaskPackage.ExtractAsync(path, workingDirectory, cancellationToken)
                .ConfigureAwait(false);
            await using var stream = File.OpenRead(Path.Combine(workingDirectory, TaskFileName));
            var document = await _serializer.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
            GraphTaskAssetStore.ValidateReferencedAssets(document, workingDirectory);
            var session = Add(document, workingDirectory: workingDirectory);
            session.AcceptPackageSaved(path, workingDirectory);
            return session;
        }
        catch
        {
            TryDeleteDirectory(workingDirectory);
            throw;
        }
    }

    public GraphDocumentSession Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            return OpenDirectory(fullPath);
        }

        if (File.Exists(fullPath) && GraphTaskPackage.IsPackagePath(fullPath))
        {
            return OpenPackageAsync(fullPath).GetAwaiter().GetResult();
        }

        throw new FileNotFoundException(
            GraphTaskLocalization.Format(GraphTaskLocalization.TaskDirectoryNotFound, fullPath),
            fullPath);
    }

    public GraphDocumentSession OpenDirectory(string taskDirectory)
    {
        var directory = RequireTaskDirectory(taskDirectory);
        if (TryFindSessionByTaskDirectory(directory, out var existingSession))
        {
            SetActive(existingSession);
            return existingSession;
        }

        var taskPath = Path.Combine(directory, TaskFileName);
        using var stream = File.OpenRead(taskPath);
        var document = _serializer.LoadAsync(stream).GetAwaiter().GetResult();
        GraphTaskAssetStore.ValidateReferencedAssets(document, directory);
        return Add(document, directory);
    }

    public bool TryGetSessionByTaskDirectory(
        string taskDirectory,
        out GraphDocumentSession? session)
    {
        var directory = NormalizeDirectory(taskDirectory);
        return TryFindSessionByTaskDirectory(directory, out session);
    }

    public async Task SaveAsync(
        GraphDocumentSession session,
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var targetPath = string.IsNullOrWhiteSpace(path)
            ? session.StoragePath
            : Path.GetFullPath(path);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new InvalidOperationException(
                GraphTaskLocalization.Get(GraphTaskLocalization.TargetTaskDirectoryRequired));
        }

        if (GraphTaskPackage.IsPackagePath(targetPath))
        {
            await SavePackageAsync(session, targetPath, cancellationToken).ConfigureAwait(false);
            return;
        }

        await SaveDirectoryAsync(session, targetPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveDirectoryAsync(
        GraphDocumentSession session,
        string taskDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var targetDirectory = Path.GetFullPath(taskDirectory);
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException(
                GraphTaskLocalization.Get(GraphTaskLocalization.TargetTaskDirectoryRequired));
        }

        Directory.CreateDirectory(targetDirectory);
        var sourceDirectory = session.WorkingDirectory ?? targetDirectory;
        var taskPath = Path.Combine(targetDirectory, TaskFileName);
        var manifestPath = Path.Combine(targetDirectory, ManifestFileName);
        var temporaryPath = $"{taskPath}.{Guid.NewGuid():N}.tmp";
        var temporaryManifestPath = $"{manifestPath}.{Guid.NewGuid():N}.tmp";
        var previousPackageWorkingDirectory = session.IsPackage ? session.WorkingDirectory : null;
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous))
            {
                await _serializer.SaveAsync(stream, session.Document, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            await using (var manifestStream = new FileStream(
                             temporaryManifestPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous))
            {
                await GraphTaskPackage.WriteManifestAsync(manifestStream, cancellationToken)
                    .ConfigureAwait(false);
                await manifestStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            await GraphTaskAssetStore.CopyReferencedAssetsAsync(
                    session.Document,
                    sourceDirectory,
                    targetDirectory,
                    cancellationToken);
            File.Move(temporaryPath, taskPath, true);
            File.Move(temporaryManifestPath, manifestPath, true);
            session.AcceptSaved(targetDirectory);
            if (previousPackageWorkingDirectory is not null)
            {
                session.ReleasePackageWorkingDirectory(previousPackageWorkingDirectory);
            }
        }
        finally
        {
            TryDeleteFile(temporaryPath);
            TryDeleteFile(temporaryManifestPath);
        }
    }

    public async Task SavePackageAsync(
        GraphDocumentSession session,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var targetPath = RequirePackagePathForSave(packagePath);
        var parentDirectory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            throw new InvalidOperationException("The package path must have a parent directory.");
        }

        Directory.CreateDirectory(parentDirectory);
        var sourceDirectory = session.WorkingDirectory ?? session.EnsureWorkingDirectory();
        var ownedSourceWorkingDirectory = !session.IsPackage && session.OwnsWorkingDirectory
            ? sourceDirectory
            : null;
        var stagingDirectory = Path.Combine(
            Path.GetTempPath(),
            "Kudo",
            "sessions",
            $"package-save-{Guid.NewGuid():N}");
        var temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        var previousPackageWorkingDirectory = session.IsPackage ? session.WorkingDirectory : null;
        try
        {
            await GraphTaskAssetStore.CopyReferencedAssetsAsync(
                    session.Document,
                    sourceDirectory,
                    stagingDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

            await using (var packageStream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var archive = new ZipArchive(packageStream, ZipArchiveMode.Create, leaveOpen: true);
                var manifestEntry = archive.CreateEntry(ManifestFileName, CompressionLevel.Fastest);
                await using (var manifestStream = manifestEntry.Open())
                {
                    await GraphTaskPackage.WriteManifestAsync(manifestStream, cancellationToken)
                        .ConfigureAwait(false);
                }

                var taskEntry = archive.CreateEntry(TaskFileName, CompressionLevel.Fastest);
                await using (var taskStream = taskEntry.Open())
                {
                    await _serializer.SaveAsync(taskStream, session.Document, cancellationToken)
                        .ConfigureAwait(false);
                }

                foreach (var relativePath in GraphTaskAssetStore.GetReferencedAssetPaths(session.Document))
                {
                    var sourcePath = GraphTaskAssetStore.ResolveAssetPath(stagingDirectory, relativePath);
                    var entry = archive.CreateEntry(
                        relativePath.Replace('\\', '/'),
                        CompressionLevel.NoCompression);
                    await using var input = new FileStream(
                        sourcePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        81920,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    await using var output = entry.Open();
                    await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                }

                archive.Dispose();
                await packageStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, targetPath, true);
            session.AcceptPackageSaved(targetPath, stagingDirectory);
            if (previousPackageWorkingDirectory is not null &&
                !string.Equals(previousPackageWorkingDirectory, stagingDirectory, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteDirectory(previousPackageWorkingDirectory);
            }

            if (ownedSourceWorkingDirectory is not null &&
                !string.Equals(ownedSourceWorkingDirectory, stagingDirectory, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteDirectory(ownedSourceWorkingDirectory);
            }
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            if (ownedSourceWorkingDirectory is not null)
            {
                TryDeleteDirectory(ownedSourceWorkingDirectory);
            }

            throw;
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    public async Task<GraphDocumentSession> DuplicateAsync(
        GraphDocumentSession source,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, source.Document, cancellationToken);
        stream.Position = 0;
        var document = await _serializer.LoadAsync(stream, cancellationToken);
        document.Id = GraphId.NewId();
        document.Title = title ?? document.Title;
        var duplicate = Add(document);
        if (source.WorkingDirectory is { } sourceDirectory)
        {
            var targetDirectory = duplicate.EnsureWorkingDirectory();
            await GraphTaskAssetStore.CopyReferencedAssetsAsync(
                    duplicate.Document,
                    sourceDirectory,
                    targetDirectory,
                    cancellationToken);
        }

        return duplicate;
    }

    public bool Close(GraphDocumentSession session)
    {
        var removed = _sessions.Remove(session);
        if (!removed)
        {
            return false;
        }

        if (ReferenceEquals(ActiveSession, session))
        {
            ActiveSession = _sessions.LastOrDefault();
        }

        session.ReleasePackageWorkingDirectory();

        SessionsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void SetActive(GraphDocumentSession session)
    {
        if (!_sessions.Contains(session))
        {
            throw new InvalidOperationException(
                GraphTaskLocalization.Get(GraphTaskLocalization.SessionNotManaged));
        }

        ActiveSession = session;
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string RequireTaskDirectory(string taskDirectory)
    {
        var directory = NormalizeDirectory(taskDirectory);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(GraphTaskLocalization.Format(
                GraphTaskLocalization.TaskDirectoryNotFound,
                directory));
        }

        var taskPath = Path.Combine(directory, TaskFileName);
        if (!File.Exists(taskPath))
        {
            throw new FileNotFoundException(
                GraphTaskLocalization.Get(GraphTaskLocalization.TaskFileNotFound),
                taskPath);
        }

        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                GraphTaskLocalization.Get(GraphTaskLocalization.TaskManifestNotFound),
                manifestPath);
        }

        using var manifestStream = File.OpenRead(manifestPath);
        GraphTaskPackage.ValidateManifestAsync(manifestStream).GetAwaiter().GetResult();

        return directory;
    }

    private static string RequirePackagePath(string packagePath)
    {
        var path = Path.GetFullPath(packagePath);
        if (!GraphTaskPackage.IsPackagePath(path))
        {
            throw new InvalidDataException($"The model package must use the {PackageExtension} extension.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                GraphTaskLocalization.Format(GraphTaskLocalization.TaskDirectoryNotFound, path),
                path);
        }

        return path;
    }

    private static string RequirePackagePathForSave(string packagePath)
    {
        var path = Path.GetFullPath(packagePath);
        if (!GraphTaskPackage.IsPackagePath(path))
        {
            throw new InvalidDataException($"The model package must use the {PackageExtension} extension.");
        }

        return path;
    }

    private bool TryFindSessionByTaskDirectory(
        string directory,
        out GraphDocumentSession session)
    {
        session = _sessions.FirstOrDefault(candidate =>
            candidate.TaskDirectory is not null &&
            DirectoryComparer.Equals(candidate.TaskDirectory, directory))!;
        return session is not null;
    }

    private bool TryFindSessionByPackagePath(
        string path,
        out GraphDocumentSession session)
    {
        session = _sessions.FirstOrDefault(candidate =>
            candidate.PackagePath is not null &&
            DirectoryComparer.Equals(candidate.PackagePath, path))!;
        return session is not null;
    }

    private static string NormalizeDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private void AddSession(GraphDocumentSession session)
    {
        var previousActiveSession = ActiveSession;
        _sessions.Add(session);
        ActiveSession = session;
        try
        {
            SessionsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            _sessions.Remove(session);
            ActiveSession = previousActiveSession;
            try
            {
                SessionsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Preserve the original add failure after giving subscribers a chance to roll back.
            }

            throw;
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
