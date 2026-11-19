using Avalonia.Media;

namespace Kudo.Themes;

/// <summary>
///     Kudo 公共主题提供的稳定字体入口。
/// </summary>
public static class KudoThemeFonts
{
    private const string DefaultAssetUri =
        "avares://Kudo.Themes/Assets/Fonts/NotoSansSC-6.ttf";

    /// <summary>
    ///     默认字体在主题资源字典中的键。
    /// </summary>
    public const string DefaultResourceKey = "NotoSansSC";

    /// <summary>
    ///     默认字体的显示族名。
    /// </summary>
    public const string DefaultDisplayName = "Noto Sans SC";

    /// <summary>
    ///     可直接用于 <see cref="FontManagerOptions.DefaultFamilyName" /> 的默认字体族标识。
    /// </summary>
    public const string DefaultFamilyName = DefaultAssetUri + "#" + DefaultDisplayName;

    /// <summary>
    ///     Win7 不加载内嵌字体，改用系统自带的微软雅黑。
    /// </summary>
    public const string Windows7FamilyName = "Microsoft YaHei";

    /// <summary>
    ///     根据当前操作系统选择的默认字体族标识。
    /// </summary>
    public static string PlatformDefaultFamilyName { get; } = IsWindows7()
        ? Windows7FamilyName
        : DefaultFamilyName;

    /// <summary>
    ///     默认字体族对象。
    /// </summary>
    public static FontFamily DefaultFontFamily { get; } = new(PlatformDefaultFamilyName);

    private static bool IsWindows7() =>
        OperatingSystem.IsWindowsVersionAtLeast(6, 1) &&
        !OperatingSystem.IsWindowsVersionAtLeast(6, 2);
}
