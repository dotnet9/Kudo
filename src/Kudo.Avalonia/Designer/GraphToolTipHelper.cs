// Unified text tooltip helper: hosts the string in a TextBlock with wrapping, max width,
// and white foreground, working around unclear string tooltip foreground on theme mismatches.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Kudo.Avalonia.Designer;

internal sealed class GraphToolTipHelper
{
    private const double DefaultMaxWidth = 260;

    /// <summary>
    ///     The tooltip text attached property.
    /// </summary>
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<GraphToolTipHelper, Control, string?>("Text");

    static GraphToolTipHelper()
    {
        TextProperty.Changed.AddClassHandler<Control>(OnTextChanged);
    }

    private GraphToolTipHelper()
    {
    }

    public static string? GetText(Control control)
    {
        return control.GetValue(TextProperty);
    }

    public static void SetText(Control control, string? value)
    {
        control.SetValue(TextProperty, value);
    }

    private static void OnTextChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        ToolTip.SetTip(control, CreateTextTip(args.GetNewValue<string?>()));
    }

    /// <summary>
    ///     Creates the styled tooltip content; empty text shows no tooltip.
    /// </summary>
    public static object? CreateTextTip(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? null
            : new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = DefaultMaxWidth,
                Foreground = Brushes.White
            };
    }
}
