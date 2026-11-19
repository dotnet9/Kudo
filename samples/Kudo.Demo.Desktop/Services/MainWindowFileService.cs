using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Kudo.Avalonia;
using Kudo.Core.Documents;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowFileService(
    TopLevel topLevel,
    GraphDocumentManager documents,
    Func<GraphDocumentSession?> activeSessionProvider,
    Action<GraphDocumentSession> activateDocument,
    Action<string, string?> appendLog,
    Action updateWorkspace)
{
    public async Task OpenGraphAsync()
    {
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = L(MainWindowLanguage.OpenGraphDialogTitle),
            AllowMultiple = false
        });
        var folder = folders.FirstOrDefault();
        var taskDirectory = GetLocalPath(folder);
        if (taskDirectory is null)
        {
            return;
        }

        await OpenTaskDirectoryAsync(taskDirectory, folder!.Name);
    }

    public async Task OpenTaskDirectoryAsync(
        string taskDirectory,
        string? fallbackTitle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskDirectory);
        try
        {
            var session = await documents.OpenAsync(taskDirectory);
            activateDocument(session);
            appendLog(
                F(
                    MainWindowLanguage.DocumentOpened,
                    session.Document.Title ?? fallbackTitle ?? Path.GetFileName(taskDirectory)),
                L(MainWindowLanguage.DocumentOpenedStatus));
        }
        catch (Exception exception)
        {
            appendLog(
                F(MainWindowLanguage.OpenFailed, exception.Message),
                L(MainWindowLanguage.OpenFailedStatus));
        }
    }

    public async Task SaveActiveGraphAsync(bool forcePicker)
    {
        var session = activeSessionProvider();
        if (session is null)
        {
            appendLog(L(MainWindowLanguage.NoCanvasToSave), L(MainWindowLanguage.NoActiveCanvas));
            return;
        }

        try
        {
            if (!forcePicker &&
                !string.IsNullOrWhiteSpace(session.TaskDirectory) &&
                Directory.Exists(session.TaskDirectory))
            {
                await documents.SaveAsync(session);
                ReportSaved(session);
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = forcePicker
                    ? L(MainWindowLanguage.SaveAsDialogTitle)
                    : L(MainWindowLanguage.SaveDialogTitle),
                AllowMultiple = false
            });
            var targetDirectory = GetLocalPath(folders.FirstOrDefault());
            if (targetDirectory is null)
            {
                return;
            }

            await documents.SaveAsync(session, targetDirectory);
            ReportSaved(session);
        }
        catch (Exception exception)
        {
            appendLog(
                F(MainWindowLanguage.SaveFailed, exception.Message),
                L(MainWindowLanguage.SaveFailedStatus));
        }
    }

    private void ReportSaved(GraphDocumentSession session)
    {
        appendLog(
            F(MainWindowLanguage.DocumentSaved, session.Document.Title),
            L(MainWindowLanguage.SaveCompleted));
        updateWorkspace();
    }

    private static string? GetLocalPath(IStorageFolder? folder) =>
        folder?.Path.IsFile == true ? folder.Path.LocalPath : null;

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
