using Kudo.Avalonia.Export;
using Kudo.Core.Documents;

namespace Kudo.Avalonia.Designer;

/// <summary>
/// 提供不依赖 UI 对话框的文档导出和打印入口。
/// </summary>
public sealed class GraphOutputOperations
{
    private readonly GraphDocumentView _documentView;
    private readonly GraphDocument _document;
    private readonly GraphExporter _exporter;
    private readonly GraphPrintService _printService;

    internal GraphOutputOperations(
        GraphDocumentView documentView,
        GraphExporter exporter,
        GraphPrintService printService)
    {
        _documentView = documentView ?? throw new ArgumentNullException(nameof(documentView));
        _document = documentView.Session.Document;
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _printService = printService ?? throw new ArgumentNullException(nameof(printService));
    }

    public ValueTask ExportAsync(
        Stream output,
        GraphExportOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _exporter.ExportAsync(
            _document,
            output,
            ConfigureExportOptions(options),
            cancellationToken);

    public async ValueTask ExportAsync(
        string path,
        GraphExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var output = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);
        await ExportAsync(output, options, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<byte[]> ExportBytesAsync(
        GraphExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await using var output = new MemoryStream();
        await ExportAsync(output, options, cancellationToken).ConfigureAwait(false);
        return output.ToArray();
    }

    public ValueTask PrintAsync(
        IGraphPrintTarget target,
        GraphPrintOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new GraphPrintOptions();
        return _printService.PrintAsync(
            _document,
            target,
            options with { Export = ConfigureExportOptions(options.Export) },
            cancellationToken);
    }

    public IReadOnlyList<GraphValidationIssue> Validate(GraphValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        return validator.Validate(_document);
    }

    public IReadOnlyList<GraphValidationIssue> Validate() =>
        new GraphValidator([
            new GraphStructureValidationRule(new LocalizedGraphValidationTextProvider())
        ]).Validate(_document);

    private GraphExportOptions ConfigureExportOptions(GraphExportOptions? options)
    {
        options ??= new GraphExportOptions();
        return options with
        {
            BackgroundImageResolver = options.BackgroundImageResolver ??
                _documentView.Canvas.BackgroundImageResolver,
            BackgroundImagePathResolver = options.BackgroundImagePathResolver ?? ResolveTaskAssetPath
        };
    }

    private string? ResolveTaskAssetPath(string source) =>
        _documentView.Session.WorkingDirectory is { } root &&
        GraphTaskAssetStore.TryResolvePath(root, source, out var path)
            ? path
            : null;
}
