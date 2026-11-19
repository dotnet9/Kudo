using Kudo.Avalonia;
using Kudo.Avalonia.Docking;
using Kudo.Core.Documents;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowDocumentService(
    GraphDocumentManager documents,
    MainWindowDocumentHost documentHost,
    GraphDockLayoutFactory dockFactory,
    MainWindowDialogService dialogService,
    GraphDocumentDraftAutosaveService draftAutosave,
    Action<string, string?> appendLog)
{
    public void Activate(GraphDocumentSession session)
    {
        if (documentHost.TryGetView(session, out var view) && view is not null)
        {
            view.Canvas.ClearSelection();
        }

        dockFactory.ActivateDocument(session);
    }

    public async Task RenameAsync(GraphDocumentSession session)
    {
        if (!documents.Sessions.Contains(session) ||
            !documentHost.TryGetView(session, out var view) ||
            view is null)
        {
            return;
        }

        var authorizationCommand = new SetDocumentTitleCommand(
            session.Document.Id,
            session.Document.Title,
            session.Document.Title,
            L(MainWindowLanguage.RenameTask));
        if (!view.TryAuthorizeCommand(authorizationCommand))
        {
            return;
        }

        var result = await dialogService.RequestDocumentTitleAsync(session.Document.Title);
        if (!result.IsAccepted || result.Title == session.Document.Title)
        {
            return;
        }

        var previousTitle = session.Document.Title;
        if (!view.TryExecuteCommand(new SetDocumentTitleCommand(
                session.Document.Id,
                previousTitle,
                result.Title,
                L(MainWindowLanguage.RenameTask))))
        {
            return;
        }
    }

    public async Task DuplicateAsync(GraphDocumentSession session)
    {
        if (!documents.Sessions.Contains(session))
        {
            return;
        }

        try
        {
            var copy = await documents.DuplicateAsync(
                session,
                CreateCopyTitle(session.Document.Title));
            dockFactory.ActivateDocument(copy);
            appendLog(
                F(
                    MainWindowLanguage.TaskDuplicated,
                    session.Document.Title ?? L(MainWindowLanguage.UntitledCanvas)),
                L(MainWindowLanguage.TaskDuplicateCompleted));
        }
        catch (Exception exception)
        {
            appendLog(
                F(MainWindowLanguage.TaskDuplicateFailed, exception.Message),
                L(MainWindowLanguage.TaskDuplicateFailedStatus));
        }
    }

    public Task CloseActiveAsync()
    {
        if (dockFactory.ActiveDocument is { } document)
        {
            dockFactory.CloseDockable(document);
        }

        return Task.CompletedTask;
    }

    public async Task CloseAsync(GraphDocumentSession session)
    {
        if (!documents.Sessions.Contains(session))
        {
            return;
        }

        if (session.IsDirty && !await dialogService.ConfirmDiscardChangesAsync(
                [session],
                L(MainWindowLanguage.CloseCanvasDialogTitle)))
        {
            return;
        }

        var title = session.Document.Title ?? L(MainWindowLanguage.UntitledCanvas);
        draftAutosave.Discard(session);
        documents.Close(session);
        appendLog(
            F(MainWindowLanguage.CanvasClosed, title),
            L(MainWindowLanguage.CanvasClosedStatus));
    }

    private string CreateCopyTitle(string? sourceTitle)
    {
        var baseTitle = string.IsNullOrWhiteSpace(sourceTitle)
            ? L(MainWindowLanguage.UntitledCanvas)
            : sourceTitle;
        var existingTitles = documents.Sessions
            .Select(candidate => candidate.Document.Title)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateTitle = F(MainWindowLanguage.CopyTitle, baseTitle);
        for (var sequence = 2; existingTitles.Contains(candidateTitle); sequence++)
        {
            candidateTitle = F(MainWindowLanguage.NumberedCopyTitle, baseTitle, sequence);
        }

        return candidateTitle;
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
