using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using Kudo.Core.Documents;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowNodeAssetService(
    TopLevel topLevel,
    GraphDocumentManager documents,
    Func<GraphDocumentSession, GraphDocumentView?> documentViewProvider,
    Func<GraphDocumentView?, string, bool> ensureEditable,
    Action<string, string?> appendLog)
{
    public async Task SelectBackgroundImageAsync(GraphNodeBackgroundImageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var session = request.EditorContext.Session;
        var view = documentViewProvider(session);
        if (!ensureEditable(view, L(MainWindowLanguage.EditNodeAppearanceAction)))
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = GraphLocalization.Get(
                global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.SelectBackgroundImage),
            AllowMultiple = false,
            FileTypeFilter = [CreateImageFileType()]
        });
        var sourcePath = GetLocalPath(files.FirstOrDefault());
        if (sourcePath is null)
        {
            return;
        }

        try
        {
            if (!documents.Sessions.Contains(session))
            {
                return;
            }

            var node = session.Document.FindNode(request.NodeId);
            if (node is null)
            {
                return;
            }

            var authorizationCommand = new SetNodeAppearanceCommand(
                node.Id,
                node.Appearance,
                node.Appearance,
                GraphLocalization.Get(
                    global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.EditNodeAppearanceCommand));
            if (!view!.TryAuthorizeCommand(authorizationCommand))
            {
                return;
            }

            var relativePath = await new GraphTaskAssetStore(session)
                .ImportImageAsync(sourcePath);
            var command = GraphPropertyCommandFactory.CreateNodeAppearanceCommand(
                node,
                node.Appearance with { BackgroundImage = relativePath });
            if (command is null || !view.TryExecuteCommand(command))
            {
                return;
            }

            var message = GraphLocalization.Format(
                global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView.NodeAppearanceUpdated,
                node.Id);
            view.SetMessage(message);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            appendLog(
                F(MainWindowLanguage.BackgroundImageImportFailed, exception.Message),
                L(MainWindowLanguage.BackgroundImageImportFailedStatus));
        }
    }

    private static FilePickerFileType CreateImageFileType() => new(L(MainWindowLanguage.ImageFileType))
    {
        Patterns = ["*.svg", "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"],
        MimeTypes = ["image/svg+xml", "image/png", "image/jpeg", "image/webp", "image/bmp"]
    };

    private static string? GetLocalPath(IStorageFile? file) =>
        file?.Path.IsFile == true ? file.Path.LocalPath : null;

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
