using Avalonia;
using Avalonia.Media;

namespace Kudo.AvaloniaCommon.Helpers;

/// <summary>
///     普通文本 ToolTip 的统一封装。
///     用 TextBlock 承载字符串提示，统一换行、最大宽度和白色前景色，绕开主题版本不匹配时字符串 ToolTip 前景色不清晰的问题。
/// </summary>
public sealed class ToolTipHelper
{
    private const double DefaultMaxWidth = 260;

    /// <summary>
    ///     要显示为 ToolTip 的文本。
    /// </summary>
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<ToolTipHelper, Control, string?>("Text");

    static ToolTipHelper()
    {
        TextProperty.Changed.AddClassHandler<Control>(OnTextChanged);
    }

    /// <summary>
    ///     工具类不需要实例化。
    /// </summary>
    private ToolTipHelper()
    {
    }

    /// <summary>
    ///     获取 ToolTip 文本。
    /// </summary>
    public static string? GetText(Control control)
    {
        return control.GetValue(TextProperty);
    }

    /// <summary>
    ///     设置 ToolTip 文本。
    /// </summary>
    public static void SetText(Control control, string? value)
    {
        control.SetValue(TextProperty, value);
    }

    private static void OnTextChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        ToolTip.SetTip(control, CreateTextTip(args.GetNewValue<string?>()));
    }

    /// <summary>
    ///     创建统一样式的 ToolTip 内容；空文本不显示提示。
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
