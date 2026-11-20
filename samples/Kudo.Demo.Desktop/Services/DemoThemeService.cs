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
    string HeaderImage,
    string TabImage,
    string TabStartImage,
    string TabEndImage,
    string TitleBarPathImage,
    Color HeaderFrom,
    Color HeaderMid,
    Color HeaderTo,
    Color OverlayFrom,
    Color OverlayMid,
    Color OverlayTo)
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
        "avares://Kudo.Demo.Desktop/Assets/header-background.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-start.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-end.png",
        "avares://Kudo.Themes/Assets/Brand/bg-address.png",
        Color.Parse("#0B6B58"), Color.Parse("#128F76"), Color.Parse("#56C2A8"),
        Color.Parse("#08006B58"), Color.Parse("#18004A3C"), Color.Parse("#34002620"));

    public static readonly DemoBrandTheme Amber = new(
        "amber", "琥珀", "Amber",
        Color.Parse("#E8890C"), ArcAppearance.Light,
        "avares://Kudo.Demo.Desktop/Assets/header-background-amber.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-amber.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-start-amber.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-end-amber.png",
        "avares://Kudo.Themes/Assets/Brand/bg-address-amber.png",
        Color.Parse("#A85408"), Color.Parse("#D97E1A"), Color.Parse("#F2B04A"),
        Color.Parse("#086B4A00"), Color.Parse("#184A3300"), Color.Parse("#34261B00"));

    public static readonly DemoBrandTheme Graphite = new(
        "graphite", "石墨", "Graphite",
        Color.Parse("#A3E635"), ArcAppearance.Dark,
        "avares://Kudo.Demo.Desktop/Assets/header-background-graphite.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-graphite.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-start-graphite.png",
        "avares://Kudo.Themes/Assets/Brand/tab-current-end-graphite.png",
        "avares://Kudo.Themes/Assets/Brand/bg-address-graphite.png",
        Color.Parse("#23272E"), Color.Parse("#3A424E"), Color.Parse("#4A5563"),
        Color.Parse("#08001418"), Color.Parse("#18000F14"), Color.Parse("#3400090C"));

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

        resources["DemoHeaderBrush"] = Gradient([
            (theme.HeaderFrom, 0.0),
            (theme.HeaderMid, 0.56),
            (theme.HeaderTo, 1.0),
        ], horizontal: true);
        resources["DemoHeaderImageBrush"] = ImageBrush(theme.HeaderImage);
        resources["DemoHeaderOverlayBrush"] = Gradient([
            (theme.OverlayFrom, 0.0),
            (theme.OverlayMid, 0.62),
            (theme.OverlayTo, 1.0),
        ], horizontal: false);
        resources["Kudo.Brand.Ribbon.Tab.SelectedBackground"] = ImageBrush(theme.TabImage);
        resources["Kudo.Brand.Ribbon.Tab.StartSelectedBackground"] = ImageBrush(theme.TabStartImage);
        resources["Kudo.Brand.Ribbon.Tab.EndSelectedBackground"] = ImageBrush(theme.TabEndImage);
        resources["Kudo.Brand.Shell.TitleBarPathBackground"] = ImageBrush(theme.TitleBarPathImage);
    }

    private static LinearGradientBrush Gradient(List<(Color Color, double Offset)> stops, bool horizontal) => new()
    {
        StartPoint = horizontal ? new RelativePoint(0, 0.5, RelativeUnit.Relative)
                                : new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = horizontal ? new RelativePoint(1, 0.5, RelativeUnit.Relative)
                              : new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(stops[0].Color, stops[0].Offset),
            new GradientStop(stops[1].Color, stops[1].Offset),
            new GradientStop(stops[2].Color, stops[2].Offset),
        },
    };

    private static ImageBrush ImageBrush(string assetUri) => new()
    {
        Source = new Bitmap(AssetLoader.Open(new Uri(assetUri))),
        Stretch = Stretch.Fill,
    };
}
