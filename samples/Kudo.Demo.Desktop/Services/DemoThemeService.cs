using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using TKCode.Arc;

namespace Kudo.Demo.Desktop.Services;

/// <summary>
/// 品牌主题：accent 种子色 + 明暗 + 该品牌的艺术资产（横幅/页签/路条）与工作台头部配色。
/// 颜色档位交由 Arc 主题引擎按种子色（OKLCH）派生，本记录只携带引擎无法推导的品牌资产与手调 chrome。
/// </summary>
public sealed record DemoBrandTheme(
    string Key,
    string ChineseName,
    string EnglishName,
    Color AccentSeed,
    ArcAppearance Appearance,
    string TabImage,
    string TabStartImage,
    string TabEndImage,
    string TitleBarPathImage,
    Color HeaderSolid)
{
    /// <summary>按当前界面语言显示的品牌名。</summary>
    public string DisplayName => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
        ? ChineseName
        : EnglishName;
}

/// <summary>内置品牌主题注册表（键即持久化到设置文件的字符串）。</summary>
public static class DemoBrandThemes
{
    public static readonly DemoBrandTheme Emerald = new(
        "emerald", "翡翠", "Emerald",
        Color.Parse("#0E9F8A"), ArcAppearance.Light,
        "avares://Kudo.Themes/Assets/Brand/tab-current.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-start.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-end.png",
        "avares://Kudo.Themes/Assets/Brand/bg-address.png",
        Color.Parse("#0D5C4B"));

    public static readonly DemoBrandTheme Amber = new(
        "amber", "琥珀", "Amber",
        Color.Parse("#E8890C"), ArcAppearance.Light,
        "avares://Kudo.Themes/Assets/Brand/tab-current-amber.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-start-amber.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-end-amber.png",
        "avares://Kudo.Themes/Assets/Brand/bg-address-amber.png",
        Color.Parse("#96530A"));

    public static readonly DemoBrandTheme Graphite = new(
        "graphite", "石墨", "Graphite",
        Color.Parse("#A3E635"), ArcAppearance.Dark,
        "avares://Kudo.Themes/Assets/Brand/tab-current-graphite.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-start-graphite.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-end-graphite.png",
        "avares://Kudo.Themes/Assets/Brand/bg-address-graphite.png",
        Color.Parse("#1E242B"));

    public static IReadOnlyList<DemoBrandTheme> All { get; } = [Emerald, Amber, Graphite];

    public static DemoBrandTheme Resolve(string? key) =>
        All.FirstOrDefault(theme => string.Equals(theme.Key, key, StringComparison.Ordinal)) ?? Emerald;
}

/// <summary>
/// 主题服务：把品牌主题应用到 Arc 主题引擎（accent 种子 + 明暗），并把引擎无法推导的
/// 品牌资产与工作台头部配色发布到应用资源（DynamicResource 消费者自动更新）。
/// </summary>
public sealed class DemoThemeService(DemoApplicationSettingsStore settingsStore)
{
    public DemoBrandTheme Current { get; private set; } = DemoBrandThemes.Emerald;

    /// <summary>应用品牌主题并持久化。必须在 UI 线程调用。</summary>
    public void Apply(DemoBrandTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        Current = theme;
        ArcThemeManager.Current?.Apply(builder => builder
            .Accent(theme.AccentSeed)
            .Appearance(theme.Appearance));
        PublishBrandResources(theme);
        settingsStore.TrySaveTheme(theme.Key);
    }

    /// <summary>应用启动时恢复持久化的品牌主题（在创建主窗口之前调用，避免首帧闪错主题）。</summary>
    public void ApplySaved() => Apply(DemoBrandThemes.Resolve(settingsStore.LoadTheme()));

    private static void PublishBrandResources(DemoBrandTheme theme)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        // 头部为单一深档实色：标题栏与页签条连成一条连续主题带（无渐变/艺术纹理分层）。
        resources["DemoHeaderBrush"] = new SolidColorBrush(theme.HeaderSolid);
        resources["Kudo.Brand.Ribbon.Tab.SelectedBackground"] = ImageBrush(theme.TabImage);
        resources["Kudo.Brand.Ribbon.Tab.StartSelectedBackground"] = ImageBrush(theme.TabStartImage);
        resources["Kudo.Brand.Ribbon.Tab.EndSelectedBackground"] = ImageBrush(theme.TabEndImage);
        resources["Kudo.Brand.Shell.TitleBarPathBackground"] = ImageBrush(theme.TitleBarPathImage);
    }

    private static ImageBrush ImageBrush(string assetUri) => new()
    {
        Source = new Bitmap(AssetLoader.Open(new Uri(assetUri))),
        Stretch = Stretch.Fill,
    };
}
