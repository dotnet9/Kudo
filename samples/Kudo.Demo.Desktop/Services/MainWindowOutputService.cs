using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Kudo;
using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using Kudo.Avalonia.Export;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

/// <summary>
/// Demo 宿主只负责文件选择与结果反馈，导出、打印和验证均委托给 SDK EditorContext。
/// </summary>
internal sealed class MainWindowOutputService(
    TopLevel topLevel,
    Func<GraphDocumentView?> activeViewProvider,
    Func<GraphValidator> validatorFactory,
    IGraphNodeExportRendererResolver nodeRendererResolver,
    Action showValidation,
    Action<string, string?> appendLog)
{
    public async Task ExportAsync(GraphExportFormat format)
    {
        var view = activeViewProvider();
        if (view is null)
        {
            return;
        }

        var extension = format.ToString().ToLowerInvariant();
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = F(MainWindowLanguage.ExportDialogTitle, FormatName(format)),
            SuggestedFileName = CreateSuggestedFileName(view, extension),
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices = [CreateFileType(format)]
        });
        if (file is null)
        {
            return;
        }

        try
        {
            await using var output = await file.OpenWriteAsync();
            PrepareForOverwrite(output);
            await view.EditorContext.Output.ExportAsync(
                output,
                new GraphExportOptions
                {
                    Format = format,
                    NodeRendererResolver = nodeRendererResolver,
                    NodeGeometryProvider = view.Canvas.NodeGeometryProvider
                });
            appendLog(
                F(MainWindowLanguage.ExportCompletedDetails, file.Name, FormatName(format)),
                F(MainWindowLanguage.ExportCompleted, FormatName(format)));
        }
        catch (Exception exception)
        {
            appendLog(
                F(MainWindowLanguage.ExportFailed, exception.Message),
                L(MainWindowLanguage.ExportFailedStatus));
        }
    }

    public async Task PrintAsync()
    {
        var view = activeViewProvider();
        if (view is null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L(MainWindowLanguage.PrintDialogTitle),
            SuggestedFileName = CreateSuggestedFileName(view, "pdf"),
            DefaultExtension = "pdf",
            ShowOverwritePrompt = true,
            FileTypeChoices = [CreateFileType(GraphExportFormat.Pdf)]
        });
        if (file is null)
        {
            return;
        }

        try
        {
            await view.EditorContext.Output.PrintAsync(
                new StorageFilePrintTarget(file),
                new GraphPrintOptions
                {
                    JobName = string.IsNullOrWhiteSpace(view.Session.Document.Title)
                        ? L(MainWindowLanguage.UntitledCanvas)
                        : view.Session.Document.Title,
                    Export = new GraphExportOptions
                    {
                        Format = GraphExportFormat.Pdf,
                        NodeRendererResolver = nodeRendererResolver,
                        NodeGeometryProvider = view.Canvas.NodeGeometryProvider
                    }
                });
            appendLog(
                F(MainWindowLanguage.PrintCompletedDetails, file.Name),
                L(MainWindowLanguage.PrintCompleted));
        }
        catch (Exception exception)
        {
            appendLog(
                F(MainWindowLanguage.PrintFailed, exception.Message),
                L(MainWindowLanguage.PrintFailedStatus));
        }
    }

    public void Validate()
    {
        var view = activeViewProvider();
        if (view is null)
        {
            return;
        }

        var issues = view.EditorContext.Output.Validate(validatorFactory());
        showValidation();
        var errors = issues.Count(issue => issue.Severity == ValidationSeverity.Error);
        var warnings = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        appendLog(
            F(MainWindowLanguage.ValidationCompletedDetails, issues.Count, errors, warnings),
            issues.Count == 0
                ? L(MainWindowLanguage.ValidationPassed)
                : F(MainWindowLanguage.ValidationIssueCount, issues.Count));
    }

    private static string CreateSuggestedFileName(GraphDocumentView view, string extension)
    {
        var title = string.IsNullOrWhiteSpace(view.Session.Document.Title)
            ? "Kudo"
            : string.Concat(view.Session.Document.Title.Select(
                character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return $"{title}.{extension}";
    }

    private static FilePickerFileType CreateFileType(GraphExportFormat format) => new(FormatName(format))
    {
        Patterns = [$"*.{format.ToString().ToLowerInvariant()}"]
    };

    private static string FormatName(GraphExportFormat format) => format switch
    {
        GraphExportFormat.Png => L(MainWindowLanguage.PngImage),
        GraphExportFormat.Svg => L(MainWindowLanguage.SvgImage),
        GraphExportFormat.Pdf => L(MainWindowLanguage.PdfDocument),
        _ => format.ToString()
    };

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);

    private static void PrepareForOverwrite(Stream output)
    {
        if (!output.CanSeek)
        {
            return;
        }

        output.SetLength(0);
        output.Position = 0;
    }

    private sealed class StorageFilePrintTarget(IStorageFile file) : IGraphPrintTarget
    {
        public async ValueTask PrintAsync(
            Stream pdfDocument,
            string jobName,
            CancellationToken cancellationToken = default)
        {
            await using var output = await file.OpenWriteAsync();
            PrepareForOverwrite(output);
            await pdfDocument.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
    }
}
