namespace Kudo.Core.Documents;

public sealed class GraphDocumentDraftSaveFailedEventArgs(
    GraphDocumentSession session,
    Exception exception) : EventArgs
{
    public GraphDocumentSession Session { get; } =
        session ?? throw new ArgumentNullException(nameof(session));

    public Exception Exception { get; } =
        exception ?? throw new ArgumentNullException(nameof(exception));
}

public sealed record GraphDocumentDraftRestoreResult(
    IReadOnlyList<GraphDocumentSession> Sessions,
    IReadOnlyList<string> Warnings);

public sealed class GraphDocumentDraftAutosaveService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly GraphDocumentManager _manager;
    private readonly GraphDocumentDraftStore _store;
    private readonly TimeSpan _delay;
    private readonly HashSet<GraphDocumentSession> _subscribedSessions = [];
    private readonly Dictionary<GraphDocumentSession, PendingSave> _pendingSaves = [];
    private bool _isDisposed;

    public GraphDocumentDraftAutosaveService(
        GraphDocumentManager manager,
        GraphDocumentDraftStore store,
        TimeSpan? delay = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _delay = delay ?? TimeSpan.FromSeconds(2);
        if (_delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        _manager.SessionsChanged += HandleSessionsChanged;
        SynchronizeSessions();
    }

    public event Action<GraphDocumentSession>? DraftSaved;

    public event EventHandler<GraphDocumentDraftSaveFailedEventArgs>? DraftSaveFailed;

    public async Task<GraphDocumentDraftRestoreResult> RestoreAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var result = await _store.LoadAsync(cancellationToken);
        ThrowIfDisposed();
        var restored = new List<GraphDocumentSession>();
        foreach (var draft in result.Drafts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_manager.Sessions.Any(session => session.SessionId == draft.SessionId))
            {
                continue;
            }

            var restoredWorkingDirectory = Path.Combine(
                Path.GetTempPath(),
                "Kudo",
                "sessions",
                $"{draft.SessionId}-restore-{Guid.NewGuid():N}");
            var session = _manager.Add(
                draft.Document,
                draft.TaskDirectory,
                draft.SessionId,
                restoredWorkingDirectory);
            if (Directory.Exists(Path.Combine(draft.DraftDirectory, GraphTaskAssetStore.AssetsDirectoryName)))
            {
                await GraphTaskAssetStore.CopyAssetsAsync(
                        draft.DraftDirectory,
                        session.EnsureWorkingDirectory(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            session.MarkDirty();
            restored.Add(session);
        }

        return new GraphDocumentDraftRestoreResult(restored, result.Warnings);
    }

    public async Task FlushAsync(
        GraphDocumentSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ThrowIfDisposed();
        CancelPendingSave(session);
        if (!session.IsDirty)
        {
            DeleteDraft(session);
            return;
        }

        await SaveDraftAsync(session, cancellationToken).ConfigureAwait(false);
    }

    public async Task FlushAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        foreach (var session in _manager.Sessions.ToArray())
        {
            await FlushAsync(session, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Discard(GraphDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ThrowIfDisposed();
        CancelPendingSave(session);
        DeleteDraft(session);
    }

    public async ValueTask DisposeAsync()
    {
        PendingSave[] pendingSaves;
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _manager.SessionsChanged -= HandleSessionsChanged;
            foreach (var session in _subscribedSessions)
            {
                session.ContentChanged -= HandleSessionContentChanged;
                session.DirtyStateChanged -= HandleSessionDirtyStateChanged;
            }

            _subscribedSessions.Clear();
            pendingSaves = _pendingSaves.Values.ToArray();
            foreach (var pending in pendingSaves)
            {
                pending.Cancellation.Cancel();
            }

            _pendingSaves.Clear();
        }

        try
        {
            await Task.WhenAll(pendingSaves.Select(pending => pending.Task)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            foreach (var pending in pendingSaves)
            {
                pending.Cancellation.Dispose();
            }
        }
    }

    private void HandleSessionsChanged(object? sender, EventArgs e) => SynchronizeSessions();

    private void HandleSessionContentChanged(object? sender, EventArgs e)
    {
        if (sender is GraphDocumentSession session && session.IsDirty)
        {
            ScheduleSave(session);
        }
    }

    private void HandleSessionDirtyStateChanged(object? sender, EventArgs e)
    {
        if (sender is not GraphDocumentSession session || session.IsDirty)
        {
            return;
        }

        CancelPendingSave(session);
        DeleteDraft(session);
    }

    private void SynchronizeSessions()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            var current = _manager.Sessions.ToHashSet();
            foreach (var session in _subscribedSessions.Where(session => !current.Contains(session)).ToArray())
            {
                session.ContentChanged -= HandleSessionContentChanged;
                session.DirtyStateChanged -= HandleSessionDirtyStateChanged;
                CancelPendingSaveLocked(session);
                _subscribedSessions.Remove(session);
            }

            foreach (var session in current.Where(session => !_subscribedSessions.Contains(session)))
            {
                session.ContentChanged += HandleSessionContentChanged;
                session.DirtyStateChanged += HandleSessionDirtyStateChanged;
                _subscribedSessions.Add(session);
            }
        }
    }

    private void ScheduleSave(GraphDocumentSession session)
    {
        lock (_gate)
        {
            if (_isDisposed || !_subscribedSessions.Contains(session))
            {
                return;
            }

            CancelPendingSaveLocked(session);
            var cancellation = new CancellationTokenSource();
            var task = SaveAfterDelayAsync(session, cancellation.Token);
            _pendingSaves[session] = new PendingSave(cancellation, task);
        }
    }

    private async Task SaveAfterDelayAsync(
        GraphDocumentSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            if (session.IsDirty)
            {
                await SaveDraftAsync(session, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
            {
                if (_pendingSaves.TryGetValue(session, out var pending) &&
                    pending.Cancellation.Token == cancellationToken)
                {
                    _pendingSaves.Remove(session);
                    pending.Cancellation.Dispose();
                }
            }
        }
    }

    private async Task SaveDraftAsync(
        GraphDocumentSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            await _store.SaveAsync(session, cancellationToken).ConfigureAwait(false);
            DraftSaved?.Invoke(session);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            DraftSaveFailed?.Invoke(
                this,
                new GraphDocumentDraftSaveFailedEventArgs(session, exception));
        }
    }

    private void DeleteDraft(GraphDocumentSession session)
    {
        try
        {
            _store.Delete(session);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DraftSaveFailed?.Invoke(
                this,
                new GraphDocumentDraftSaveFailedEventArgs(session, exception));
        }
    }

    private void CancelPendingSave(GraphDocumentSession session)
    {
        lock (_gate)
        {
            CancelPendingSaveLocked(session);
        }
    }

    private void CancelPendingSaveLocked(GraphDocumentSession session)
    {
        if (_pendingSaves.Remove(session, out var pending))
        {
            pending.Cancellation.Cancel();
            _ = pending.Task.ContinueWith(
                _ => pending.Cancellation.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    private sealed record PendingSave(CancellationTokenSource Cancellation, Task Task);
}
