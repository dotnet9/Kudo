using System.Globalization;
using System.Reflection;
using Kudo.Core.Serialization;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;

namespace Kudo.Avalonia;

/// <summary>
/// 注册 Kudo 内嵌语言资源，统一使用宿主的全局语言与同键覆盖配置。
/// </summary>
public static class GraphLocalization
{
    public const string DefaultCultureName = "en-US";
    public const string SimplifiedChineseCultureName = "zh-CN";

    private static readonly JsonLocalizer Resources = new(typeof(GraphLocalization).Assembly, DefaultCultureName);

    /// <summary>
    /// 向全局管理器添加库资源，不替换宿主已经注册的语言插件。
    /// </summary>
    /// <param name="culture">请求的界面语言。未指定时保留宿主当前语言。</param>
    /// <param name="additionalAssemblies">包含额外内嵌 JSON 语言资源的程序集，按由内层到外层的顺序传入。</param>
    public static void Register(
        CultureInfo? culture = null,
        params Assembly[] additionalAssemblies)
    {
        I18nManager.Instance.AddResource(typeof(GraphLocalization).Assembly);
        if (additionalAssemblies.Length > 0)
        {
            I18nManager.Instance.AddResource(additionalAssemblies);
        }

        if (culture != null || I18nManager.Instance.Culture == null)
        {
            I18nManager.Instance.Culture = culture ?? CultureInfo.CurrentUICulture;
        }
        GraphTaskLocalization.Register(key => Get(
            $"Localization.Kudo_Avalonia.Serialization.GraphTask.{key}"));
    }

    /// <summary>
    /// 将系统语言或用户选择映射为 Kudo 提供的语言。
    /// </summary>
    public static CultureInfo ResolveSupportedCulture(CultureInfo? culture)
    {
        if (culture?.Name.Equals(SimplifiedChineseCultureName, StringComparison.OrdinalIgnoreCase) == true ||
            culture?.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new CultureInfo(SimplifiedChineseCultureName);
        }

        return new CultureInfo(DefaultCultureName);
    }

    /// <summary>
    /// 查询 SDK 文案；宿主同键资源优先，缺失时使用当前语言的内嵌资源并回退到英语。
    /// </summary>
    public static string Get(string key) => Resources.GetResource(key);

    /// <summary>
    /// 按当前区域格式设置填充 SDK 文案中的参数。
    /// </summary>
    public static string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
