using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Kudo;

namespace Kudo.Avalonia;

/// <summary>
/// 统一绘制图元实例的外部名称和描述；业务渲染器只负责图元本体。
/// </summary>
internal static class GraphSymbolLabelRenderer
{
    public static void Render(
        DrawingContext drawingContext,
        NodeModel node,
        Rect visualBounds,
        double zoom,
        bool showName,
        bool showDescription,
        IBrush nameBrush,
        IBrush descriptionBrush,
        double nameOffset,
        double descriptionOffset,
        double descriptionFontScale)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        ArgumentNullException.ThrowIfNull(node);
        if (visualBounds.Width <= 0 || visualBounds.Height <= 0 || zoom <= 0)
        {
            return;
        }

        var appearance = (node.Appearance ?? new GraphNodeAppearance()).Normalize();
        if (appearance.VisualMode != GraphNodeVisualMode.Symbol)
        {
            return;
        }

        var textAppearance = appearance.TextAppearance.Normalize();
        var typeface = CreateTypeface(textAppearance.FontWeight);
        if (showDescription && !string.IsNullOrWhiteSpace(node.Description))
        {
            var text = CreateText(
                node.Description,
                typeface,
                Math.Max(6, textAppearance.FontSize * descriptionFontScale) * zoom,
                ResolveTextBrush(textAppearance.Foreground, descriptionBrush),
                visualBounds.Width);
            drawingContext.DrawText(
                text,
                new Point(
                    visualBounds.Left,
                    visualBounds.Top - descriptionOffset * zoom - text.Height));
        }

        if (showName && !string.IsNullOrWhiteSpace(node.Name))
        {
            var text = CreateText(
                node.Name,
                typeface,
                textAppearance.FontSize * zoom,
                ResolveTextBrush(textAppearance.Foreground, nameBrush),
                visualBounds.Width);
            drawingContext.DrawText(
                text,
                new Point(
                    visualBounds.Left,
                    visualBounds.Bottom + nameOffset * zoom));
        }
    }

    private static FormattedText CreateText(
        string text,
        Typeface typeface,
        double fontSize,
        IBrush brush,
        double maximumWidth) => new(
        text,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        typeface,
        fontSize,
        brush)
    {
        MaxTextWidth = Math.Max(1, maximumWidth),
        TextAlignment = TextAlignment.Center,
        Trimming = TextTrimming.CharacterEllipsis
    };

    private static Typeface CreateTypeface(GraphFontWeight weight) => new(
        FontFamily.Default,
        FontStyle.Normal,
        weight switch
        {
            GraphFontWeight.Medium => FontWeight.Medium,
            GraphFontWeight.SemiBold => FontWeight.SemiBold,
            GraphFontWeight.Bold => FontWeight.Bold,
            _ => FontWeight.Normal
        });

    private static IBrush ResolveTextBrush(string? value, IBrush fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return new ImmutableSolidColorBrush(Color.Parse(value));
        }
        catch (FormatException)
        {
            return fallback;
        }
    }
}
