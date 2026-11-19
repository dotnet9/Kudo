using Avalonia;
using Avalonia.Input;
using Ursa.Controls;

namespace Kudo.AvaloniaCommon.Helpers;

/// <summary>
///     Ursa 禁用遮罩提示的兼容封装。
///     Ursa 的 DisabledTip 会原样传给 ToolTip.Tip；这里先把字符串包装成统一的白色换行文本，避免 Avalonia 与 Semi/Ursa 版本不一致时提示文字颜色不清晰。
/// </summary>
public sealed class DisabledAdornerHelper
{
    /// <summary>
    ///     是否启用 Ursa 的禁用遮罩。
    /// </summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<DisabledAdornerHelper, InputElement, bool>("IsEnabled");

    /// <summary>
    ///     禁用状态下显示的提示文本。
    /// </summary>
    public static readonly AttachedProperty<string?> DisabledTipProperty =
        AvaloniaProperty.RegisterAttached<DisabledAdornerHelper, InputElement, string?>("DisabledTip");

    static DisabledAdornerHelper()
    {
        IsEnabledProperty.Changed.AddClassHandler<InputElement>(OnIsEnabledChanged);
        DisabledTipProperty.Changed.AddClassHandler<InputElement>(OnDisabledTipChanged);
    }

    /// <summary>
    ///     工具类不需要实例化。
    /// </summary>
    private DisabledAdornerHelper()
    {
    }

    /// <summary>
    ///     获取是否启用禁用遮罩。
    /// </summary>
    public static bool GetIsEnabled(InputElement element)
    {
        return element.GetValue(IsEnabledProperty);
    }

    /// <summary>
    ///     设置是否启用禁用遮罩。
    /// </summary>
    public static void SetIsEnabled(InputElement element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    ///     获取禁用状态下显示的提示文本。
    /// </summary>
    public static string? GetDisabledTip(InputElement element)
    {
        return element.GetValue(DisabledTipProperty);
    }

    /// <summary>
    ///     设置禁用状态下显示的提示文本。
    /// </summary>
    public static void SetDisabledTip(InputElement element, string? value)
    {
        element.SetValue(DisabledTipProperty, value);
    }

    private static void OnIsEnabledChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        DisabledAdorner.SetIsEnabled(element, args.GetNewValue<bool>());
    }

    private static void OnDisabledTipChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        DisabledAdorner.SetDisabledTip(element, ToolTipHelper.CreateTextTip(args.GetNewValue<string?>()));
    }
}
