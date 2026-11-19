using Avalonia.Controls;
using Avalonia.Threading;
using Kudo.Avalonia;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Avalonia.Docking;
using Kudo.Core.Documents;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowDraftCoordinator : IDisposable
{
    private readonly Window _window;
    private readonly GraphDocumentManager _documents;
    private readonly GraphDocumentDraftAutosaveService _draftAutosave;
    private readonly GraphDockLayoutFactory _dockFactory;
    private readonly MainWindowViewModel _viewModel;
    private readonly MainWindowDialogService _dialogService;
    private readonly Action<string, string?> _appendLog;
    private readonly CancellationTokenSource _restoreCancellation = new();
    private bool _isCloseConfirmed;
    private bool _isCloseConfirmationRunning;
    private bool _restoreStarted;
    private bool _isDisposed;

    public MainWindowDraftCoordinator(
        Window window,
        GraphDocumentManager documents,
        GraphDocumentDraftAutosaveService draftAutosave,
        GraphDockLayoutFactory dockFactory,
        MainWindowViewModel viewModel,
        MainWindowDialogService dialogService,
        Action<string, string?> appendLog)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _draftAutosave = draftAutosave ??
            throw new ArgumentNullException(nameof(draftAutosave));
        _dockFactory = dockFactory ?? throw new ArgumentNullException(nameof(dockFactory));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _dialogService = dialogService ??
            throw new ArgumentNullException(nameof(dialogService));
        _appendLog = appendLog ?? throw new ArgumentNullException(nameof(appendLog));

        _window.Opened += HandleWindowOpened;
        _window.Closing += HandleWindowClosing;
        _draftAutosave.DraftSaveFailed += HandleDraftSaveFailed;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _restoreCancellation.Cancel();
        _window.Opened -= HandleWindowOpened;
        _window.Closing -= HandleWindowClosing;
        _draftAutosave.DraftSaveFailed -= HandleDraftSaveFailed;
        _draftAutosave.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _restoreCancellation.Dispose();
    }

    private async void HandleWindowOpened(object? sender, EventArgs e)
    {
        if (_restoreStarted)
        {
            return;
        }

        _restoreStarted = true;
        try
        {
            var result = await _draftAutosave.RestoreAsync(_restoreCancellation.Token);
            foreach (var warning in result.Warnings)
            {
                _appendLog(
                    F(MainWindowLanguage.DraftRestoreWarning, warning),
                    L(MainWindowLanguage.DraftRestoreWarningStatus));
            }

            if (result.Sessions.Count == 0)
            {
                return;
            }

            _viewModel.RestoredDraftCount = result.Sessions.Count;
            _dockFactory.ActivateDocument(result.Sessions[^1]);
            _appendLog(
                F(MainWindowLanguage.DraftsRestored, result.Sessions.Count),
                F(MainWindowLanguage.DraftsRestoredStatus, result.Sessions.Count));
        }
        catch (OperationCanceledException) when (_restoreCancellation.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_restoreCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _appendLog(
                F(MainWindowLanguage.DraftRestoreFailed, exception.Message),
                L(MainWindowLanguage.DraftRestoreFailedStatus));
        }
        finally
        {
            _viewModel.IsDraftRestoreCompleted = true;
        }
    }

    private void HandleDraftSaveFailed(
        object? sender,
        GraphDocumentDraftSaveFailedEventArgs e)
    {
        void ReportFailure() => _appendLog(
            F(
                MainWindowLanguage.DraftSaveFailed,
                e.Session.Document.Title ?? L(MainWindowLanguage.UntitledCanvas),
                e.Exception.Message),
            L(MainWindowLanguage.DraftSaveFailedStatus));

        if (Dispatcher.UIThread.CheckAccess())
        {
            ReportFailure();
            return;
        }

        Dispatcher.UIThread.Post(ReportFailure);
    }

    private async void HandleWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isCloseConfirmed)
        {
            return;
        }

        var dirtySessions = _documents.Sessions.Where(session => session.IsDirty).ToArray();
        if (dirtySessions.Length == 0)
        {
            return;
        }

        e.Cancel = true;
        if (_isCloseConfirmationRunning)
        {
            return;
        }

        _isCloseConfirmationRunning = true;
        try
        {
            if (!await _dialogService.ConfirmDiscardChangesAsync(
                    dirtySessions,
                    L(MainWindowLanguage.ExitDemoDialogTitle)))
            {
                return;
            }

            foreach (var session in dirtySessions)
            {
                _draftAutosave.Discard(session);
            }

            _isCloseConfirmed = true;
            _window.Close();
        }
        finally
        {
            _isCloseConfirmationRunning = false;
        }
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
