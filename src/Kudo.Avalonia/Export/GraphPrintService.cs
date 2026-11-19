// 打印适配：统一生成 PDF 打印文档，再交由宿主提供的平台打印目标处理。
namespace Kudo.Avalonia.Export;

public interface IGraphPrintTarget
{
    ValueTask PrintAsync(
        Stream pdfDocument,
        string jobName,
        CancellationToken cancellationToken = default);
}

public sealed record GraphPrintOptions
{
    public string JobName { get; init; } = "Kudo 文档";

    public GraphExportOptions Export { get; init; } = new()
    {
        Format = GraphExportFormat.Pdf
    };
}

public sealed class GraphPrintService
{
    private readonly GraphExporter _exporter;

    public GraphPrintService(GraphExporter? exporter = null) =>
        _exporter = exporter ?? new GraphExporter();

    /// <summary>
    /// 生成与 PDF 导出一致的矢量打印文档；平台打印对话框和队列由宿主 Target 负责。
    /// </summary>
    public async ValueTask PrintAsync(
        GraphDocument document,
        IGraphPrintTarget target,
        GraphPrintOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(target);
        options ??= new GraphPrintOptions();
        if (string.IsNullOrWhiteSpace(options.JobName))
        {
            throw new ArgumentException("A print job name is required.", nameof(options));
        }

        var exportOptions = options.Export with { Format = GraphExportFormat.Pdf };
        await using var pdf = new MemoryStream();
        await _exporter.ExportAsync(document, pdf, exportOptions, cancellationToken)
            .ConfigureAwait(false);
        pdf.Position = 0;
        await target.PrintAsync(pdf, options.JobName, cancellationToken).ConfigureAwait(false);
    }
}
