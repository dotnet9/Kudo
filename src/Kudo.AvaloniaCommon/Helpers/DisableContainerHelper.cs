using Avalonia;
using Ursa.Controls;

namespace Kudo.AvaloniaCommon.Helpers;

/// <summary>
///     Ursa DisableContainer 的文本提示兼容封装。
///     将字符串转换成统一的 ToolTip 内容，避免第三方控件直接传递字符串时前景色不清晰。
/// </summary>
public sealed class DisableContainerHelper
{
    /// <summary>
    ///     DisableContainer 禁用状态下显示的提示文本。
    /// </summary>
    public static readonly AttachedProperty<string?> DisabledTipProperty =
        AvaloniaProperty.RegisterAttached<DisableContainerHelper, DisableContainer, string?>("DisabledTip");

    static DisableContainerHelper()
    {
        DisabledTipProperty.Changed.AddClassHandler<DisableContainer>(OnDisabledTipChanged);
    }

    private DisableContainerHelper()
    {
    }

    /// <summary>
    ///     获取禁用提示文本。
    /// </summary>
    public static string? GetDisabledTip(DisableContainer container)
    {
        return container.GetValue(DisabledTipProperty);
    }

    /// <summary>
    ///     设置禁用提示文本。
    /// </summary>
    public static void SetDisabledTip(DisableContainer container, string? value)
    {
        container.SetValue(DisabledTipProperty, value);
    }

    private static void OnDisabledTipChanged(DisableContainer container, AvaloniaPropertyChangedEventArgs args)
    {
        container.SetCurrentValue(
            DisableContainer.DisabledTipProperty,
            ToolTipHelper.CreateTextTip(args.GetNewValue<string?>()));
    }
}
