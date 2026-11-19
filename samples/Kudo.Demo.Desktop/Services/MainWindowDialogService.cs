using Avalonia.Controls;
using Kudo.Avalonia;
using Kudo.Core.Documents;
using TKCode.Arc;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowDialogService(TopLevel topLevel)
{
    public async Task<DocumentTitleDialogResult> RequestDocumentTitleAsync(string? currentTitle)
    {
        var editor = new TextBox
        {
            Text = currentTitle ?? string.Empty,
            PlaceholderText = L(MainWindowLanguage.TaskNamePlaceholder),
            MinWidth = 320
        };
        var result = await Dialog.ShowAsync(
            topLevel,
            L(MainWindowLanguage.RenameTask),
            editor,
            actions:
            [
                new DialogAction(L(MainWindowLanguage.DialogCancel), false),
                new DialogAction(L(MainWindowLanguage.DialogConfirm), true, ButtonVariant.Primary)
            ]);
        var accepted = result is true;
        var title = accepted && !string.IsNullOrWhiteSpace(editor.Text)
            ? editor.Text.Trim()
            : null;
        return new DocumentTitleDialogResult(accepted, title);
    }

    public async Task<bool> ConfirmDiscardChangesAsync(
        IReadOnlyCollection<GraphDocumentSession> dirtySessions,
        string title)
    {
        ArgumentNullException.ThrowIfNull(dirtySessions);
        var names = dirtySessions
            .Select(session => string.IsNullOrWhiteSpace(session.Document.Title)
                ? L(MainWindowLanguage.UntitledCanvas)
                : session.Document.Title)
            .Take(5)
            .ToArray();
        var remaining = dirtySessions.Count - names.Length;
        var list = string.Join(L(MainWindowLanguage.ListSeparator), names);
        if (remaining > 0)
        {
            list = F(MainWindowLanguage.RemainingCanvasList, list, dirtySessions.Count);
        }

        var message = dirtySessions.Count == 1
            ? F(MainWindowLanguage.DiscardSingleCanvas, list)
            : F(MainWindowLanguage.DiscardMultipleCanvases, list);
        return await Dialog.ConfirmAsync(
            topLevel,
            title,
            message,
            confirmText: L(MainWindowLanguage.DialogDiscard),
            cancelText: L(MainWindowLanguage.DialogCancel),
            isDanger: true);
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}

internal readonly record struct DocumentTitleDialogResult(bool IsAccepted, string? Title);
