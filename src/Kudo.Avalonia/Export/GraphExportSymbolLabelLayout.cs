using SkiaSharp;

namespace Kudo.Avalonia.Export;

/// <summary>
/// 统一计算并绘制导出图元的外部名称和描述，保证导出范围与实际绘制使用同一套度量。
/// </summary>
internal static class GraphExportSymbolLabelLayout
{
    public static GraphExportSymbolLabels Calculate(
        NodeModel node,
        GraphRect visualBounds,
        GraphCanvasSettings canvasSettings,
        GraphExportTheme theme)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(canvasSettings);
        ArgumentNullException.ThrowIfNull(theme);
        var appearance = (node.Appearance ?? new GraphNodeAppearance()).Normalize();
        if (appearance.VisualMode != GraphNodeVisualMode.Symbol)
        {
            return GraphExportSymbolLabels.Empty;
        }

        var textAppearance = appearance.TextAppearance.Normalize();
        var showName = GraphLabelVisibilityResolver.IsNodeNameVisible(
            node,
            canvasSettings.SymbolNameVisibility,
            false,
            false);
        var showDescription = GraphLabelVisibilityResolver.IsNodeDescriptionVisible(
            node,
            canvasSettings.SymbolDescriptionVisibility,
            false,
            false);
        var name = showName && !string.IsNullOrWhiteSpace(node.Name)
            ? CreateLabel(
                node.Name,
                visualBounds,
                textAppearance.FontSize,
                textAppearance.FontWeight,
                textAppearance.Foreground,
                theme.SymbolNameColor,
                theme.FontFamily,
                NormalizeNonNegative(theme.SymbolNameOffset, 6),
                false)
            : null;
        var descriptionFontScale = NormalizePositive(theme.SymbolDescriptionFontScale, 0.86f);
        var description = showDescription && !string.IsNullOrWhiteSpace(node.Description)
            ? CreateLabel(
                node.Description,
                visualBounds,
                Math.Max(6, textAppearance.FontSize * descriptionFontScale),
                textAppearance.FontWeight,
                textAppearance.Foreground,
                theme.SymbolDescriptionColor,
                theme.FontFamily,
                NormalizeNonNegative(theme.SymbolDescriptionOffset, 6),
                true)
            : null;
        return new GraphExportSymbolLabels(name, description);
    }

    public static void Render(
        SKCanvas canvas,
        GraphExportSymbolLabels labels,
        string fontFamily)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(labels);
        if (labels.Description is { } description)
        {
            RenderLabel(canvas, description, fontFamily);
        }

        if (labels.Name is { } name)
        {
            RenderLabel(canvas, name, fontFamily);
        }
    }

    public static GraphRect Include(GraphRect bounds, GraphExportSymbolLabels labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var result = bounds;
        if (labels.Name is { } name)
        {
            result = Include(result, name.Bounds);
        }

        if (labels.Description is { } description)
        {
            result = Include(result, description.Bounds);
        }

        return result;
    }

    private static GraphExportSymbolLabel? CreateLabel(
        string text,
        GraphRect visualBounds,
        double fontSize,
        GraphFontWeight fontWeight,
        string? foreground,
        string fallbackColor,
        string fontFamily,
        float offset,
        bool placeAbove)
    {
        var normalizedSize = (float)Math.Clamp(fontSize, 6, 256);
        using var font = CreateFont(text, fontFamily, normalizedSize, fontWeight);
        using var measurePaint = new SKPaint { IsAntialias = true };
        var maximumWidth = (float)Math.Max(1, visualBounds.Width);
        var renderedText = TrimToWidth(text, maximumWidth, font, measurePaint);
        if (renderedText.Length == 0)
        {
            return null;
        }

        var textWidth = Math.Min(maximumWidth, font.MeasureText(renderedText, measurePaint));
        var metrics = font.Metrics;
        var textHeight = Math.Max(1, metrics.Descent - metrics.Ascent);
        var left = (float)(visualBounds.X + (visualBounds.Width - textWidth) / 2);
        var top = placeAbove
            ? (float)visualBounds.Y - offset - textHeight
            : (float)(visualBounds.Y + visualBounds.Height) + offset;
        var baseline = top - metrics.Ascent;
        return new GraphExportSymbolLabel(
            renderedText,
            new GraphRect(left, top, textWidth, textHeight),
            baseline,
            normalizedSize,
            fontWeight,
            ParseColor(foreground, fallbackColor));
    }

    private static void RenderLabel(
        SKCanvas canvas,
        GraphExportSymbolLabel label,
        string fontFamily)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = label.Color,
            Style = SKPaintStyle.Fill
        };
        using var font = CreateFont(label.Text, fontFamily, label.FontSize, label.FontWeight);
        var textWidth = font.MeasureText(label.Text, paint);
        canvas.DrawText(
            label.Text,
            (float)(label.Bounds.X + label.Bounds.Width / 2) - textWidth / 2,
            label.Baseline,
            SKTextAlign.Left,
            font,
            paint);
    }

    private static string TrimToWidth(
        string text,
        float maximumWidth,
        SKFont font,
        SKPaint paint)
    {
        if (font.MeasureText(text, paint) <= maximumWidth)
        {
            return text;
        }

        const string ellipsis = "…";
        if (font.MeasureText(ellipsis, paint) > maximumWidth)
        {
            return string.Empty;
        }

        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            var candidate = string.Concat(text.AsSpan(0, middle), ellipsis);
            if (font.MeasureText(candidate, paint) <= maximumWidth)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return string.Concat(text.AsSpan(0, low), ellipsis);
    }

    private static SKFont CreateFont(
        string text,
        string fontFamily,
        float size,
        GraphFontWeight weight)
    {
        var styleWeight = weight switch
        {
            GraphFontWeight.Medium => SKFontStyleWeight.Medium,
            GraphFontWeight.SemiBold => SKFontStyleWeight.SemiBold,
            GraphFontWeight.Bold => SKFontStyleWeight.Bold,
            _ => SKFontStyleWeight.Normal
        };
        var typeface = SKTypeface.FromFamilyName(
            fontFamily,
            styleWeight,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);
        if (typeface is null)
        {
            var character = text.FirstOrDefault(value => value > 127);
            typeface = character == default
                ? SKTypeface.Default
                : SKFontManager.Default.MatchCharacter(fontFamily, character) ?? SKTypeface.Default;
        }

        return new SKFont(typeface, size);
    }

    private static SKColor ParseColor(string? value, string fallback)
    {
        try
        {
            return SKColor.Parse(string.IsNullOrWhiteSpace(value) ? fallback : value);
        }
        catch (ArgumentException)
        {
            return SKColor.Parse(fallback);
        }
    }

    private static GraphRect Include(GraphRect first, GraphRect second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        var right = Math.Max(first.X + first.Width, second.X + second.Width);
        var bottom = Math.Max(first.Y + first.Height, second.Y + second.Height);
        return new GraphRect(left, top, right - left, bottom - top);
    }

    private static float NormalizePositive(float value, float fallback) =>
        float.IsFinite(value) && value > 0 ? value : fallback;

    private static float NormalizeNonNegative(float value, float fallback) =>
        float.IsFinite(value) && value >= 0 ? value : fallback;
}

internal sealed record GraphExportSymbolLabels(
    GraphExportSymbolLabel? Name,
    GraphExportSymbolLabel? Description)
{
    public static GraphExportSymbolLabels Empty { get; } = new(null, null);
}

internal sealed record GraphExportSymbolLabel(
    string Text,
    GraphRect Bounds,
    float Baseline,
    float FontSize,
    GraphFontWeight FontWeight,
    SKColor Color);
