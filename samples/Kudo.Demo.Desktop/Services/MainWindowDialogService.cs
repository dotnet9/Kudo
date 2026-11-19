using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Kudo.Avalonia;
using Kudo.Core.Documents;
using Ursa.Controls;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowDialogService(TopLevel topLevel, string hostId)
{
    public async Task<DocumentTitleDialogResult> RequestDocumentTitleAsync(string? currentTitle)
    {
        var editor = new TextBox
        {
            Text = currentTitle ?? string.Empty,
            PlaceholderText = L(MainWindowLanguage.TaskNamePlaceholder),
            MinWidth = 320
        };
        var result = await OverlayDialog.ShowStandardAsync(
            editor,
            null,
            hostId,
            new OverlayDialogOptions
            {
                Title = L(MainWindowLanguage.RenameTask),
                Buttons = DialogButton.OKCancel,
                TopLevelHashCode = topLevel.GetHashCode(),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            });
        var title = string.IsNullOrWhiteSpace(editor.Text) ? null : editor.Text.Trim();
        return new DocumentTitleDialogResult(result == DialogResult.OK, title);
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
        var result = await OverlayMessageBox.ShowAsync(
            message,
            title,
            hostId,
            MessageBoxIcon.Warning,
            MessageBoxButton.YesNo,
            topLevel.GetHashCode());
        return result == MessageBoxResult.Yes;
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}

internal readonly record struct DocumentTitleDialogResult(bool IsAccepted, string? Title);
