using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Kudo.Avalonia;

internal static class GraphCanvasVisualFactory
{
    public static IBrush CreateBrush(Color color) => new ImmutableSolidColorBrush(color);

    public static FormattedText CreateText(string text, double fontSize, IBrush brush) =>
        new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            brush);
}
