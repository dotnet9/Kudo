// 导出服务：创建 PNG、SVG 或 PDF 后端，并通过 Stream 异步写出完整结果。
using SkiaSharp;

namespace Kudo.Avalonia.Export;

public sealed class GraphExporter
{
    public async ValueTask ExportAsync(
        GraphDocument document,
        Stream output,
        GraphExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
        {
            throw new ArgumentException("The output stream must be writable.", nameof(output));
        }

        options ??= new GraphExportOptions();
        ValidateOptions(options);
        cancellationToken.ThrowIfCancellationRequested();
        var layout = GraphExportLayout.Create(document, options);
        await using var buffer = new MemoryStream();
        switch (options.Format)
        {
            case GraphExportFormat.Png:
                RenderPng(document, buffer, options, layout, cancellationToken);
                break;
            case GraphExportFormat.Svg:
                RenderSvg(document, buffer, options, layout, cancellationToken);
                break;
            case GraphExportFormat.Pdf:
                RenderPdf(document, buffer, options, layout, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options), options.Format, "Unsupported export format.");
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void RenderPng(
        GraphDocument document,
        Stream output,
        GraphExportOptions options,
        GraphExportLayout layout,
        CancellationToken cancellationToken)
    {
        var info = new SKImageInfo(
            layout.PixelWidth,
            layout.PixelHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ??
            throw new InvalidOperationException("Unable to create the PNG render surface.");
        Render(document, surface.Canvas, options, layout, cancellationToken);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100) ??
            throw new InvalidOperationException("Unable to encode the PNG image.");
        data.SaveTo(output);
    }

    private static void RenderSvg(
        GraphDocument document,
        Stream output,
        GraphExportOptions options,
        GraphExportLayout layout,
        CancellationToken cancellationToken)
    {
        using var canvas = SKSvgCanvas.Create(
            SKRect.Create(layout.PixelWidth, layout.PixelHeight),
            output) ?? throw new InvalidOperationException("Unable to create the SVG canvas.");
        Render(document, canvas, options, layout, cancellationToken);
        canvas.Flush();
    }

    private static void RenderPdf(
        GraphDocument document,
        Stream output,
        GraphExportOptions options,
        GraphExportLayout layout,
        CancellationToken cancellationToken)
    {
        var dpi = double.IsFinite(options.Dpi) && options.Dpi > 0 ? options.Dpi : 96;
        var pageWidth = (float)(layout.PixelWidth * 72 / dpi);
        var pageHeight = (float)(layout.PixelHeight * 72 / dpi);
        using var pdf = SKDocument.CreatePdf(output) ??
            throw new InvalidOperationException("Unable to create the PDF document.");
        var canvas = pdf.BeginPage(pageWidth, pageHeight);
        canvas.Scale(pageWidth / layout.PixelWidth, pageHeight / layout.PixelHeight);
        Render(document, canvas, options, layout, cancellationToken);
        pdf.EndPage();
        pdf.Close();
    }

    private static void Render(
        GraphDocument document,
        SKCanvas canvas,
        GraphExportOptions options,
        GraphExportLayout layout,
        CancellationToken cancellationToken)
    {
        options.Renderer.Render(new GraphExportRenderContext(
            canvas,
            document,
            layout,
            options,
            cancellationToken));
        canvas.Flush();
    }

    private static void ValidateOptions(GraphExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.Theme);
        ArgumentNullException.ThrowIfNull(options.Renderer);
        ArgumentNullException.ThrowIfNull(options.RoutingService);
        if (options.EmbeddedContentPolicy == GraphEmbeddedContentPolicy.Snapshot &&
            options.HasEmbeddedContent is not null &&
            options.EmbeddedContentSnapshot is null)
        {
            throw new ArgumentException(
                "EmbeddedContentSnapshot is required when the snapshot policy is enabled.",
                nameof(options));
        }
    }
}
