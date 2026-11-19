using System.Globalization;
using Avalonia;
using Kudo.Avalonia;
using Kudo.Demo.Desktop.Localization;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using Semi.Avalonia;
using CommonLanguage = global::Localization.Kudo_Demo_Desktop.Application.Common;

namespace Kudo.Demo.Desktop.Services;

public sealed class DemoLocalizationService
{
    public const string CultureOverrideEnvironmentVariable = "KUDO_DEMO_CULTURE";

    private readonly DemoApplicationSettingsStore _settingsStore;
    private readonly CultureInfo _systemCulture;

    public DemoLocalizationService(
        DemoApplicationSettingsStore settingsStore,
        CultureInfo systemCulture,
        string? cultureOverride = null)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _systemCulture = systemCulture ?? throw new ArgumentNullException(nameof(systemCulture));
        Preference = ParsePreference(_settingsStore.LoadLanguage());

        if (!string.IsNullOrWhiteSpace(cultureOverride))
        {
            Preference = ParsePreference(cultureOverride);
        }
    }

    public DemoLanguagePreference Preference { get; private set; }

    public CultureInfo EffectiveCulture => ResolveCulture(Preference);

    public void Initialize(string? resourceFolder = null)
    {
        I18nManager.Instance.Register(new JsonLangPlugin
        {
            ResourceFolder = resourceFolder ?? Path.Combine(AppContext.BaseDirectory, "I18n")
        }, CultureInfo.GetCultureInfo(GraphLocalization.DefaultCultureName));
        GraphLocalization.Register(EffectiveCulture);
    }

    public IReadOnlyList<DemoLanguageOption> CreateOptions() =>
    [
        new DemoLanguageOption(
            DemoLanguagePreference.System,
            GraphLocalization.Get(CommonLanguage.FollowSystem),
            null),
        new DemoLanguageOption(
            DemoLanguagePreference.English,
            GraphLocalization.Get(CommonLanguage.English),
            GraphLocalization.DefaultCultureName),
        new DemoLanguageOption(
            DemoLanguagePreference.SimplifiedChinese,
            GraphLocalization.Get(CommonLanguage.SimplifiedChinese),
            GraphLocalization.SimplifiedChineseCultureName)
    ];

    public void SetPreference(DemoLanguagePreference preference)
    {
        Preference = preference;
        _settingsStore.TrySaveLanguage(ToStoredCulture(preference));
        I18nManager.Instance.Culture = ResolveCulture(preference);
        if (Application.Current is { } application)
        {
            ApplyControlCultures(application);
        }
    }

    public void ApplyControlCultures(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var culture = EffectiveCulture;
        SemiTheme.OverrideLocaleResources(application, culture);
    }

    private CultureInfo ResolveCulture(DemoLanguagePreference preference) => preference switch
    {
        DemoLanguagePreference.English => new CultureInfo(GraphLocalization.DefaultCultureName),
        DemoLanguagePreference.SimplifiedChinese => new CultureInfo(GraphLocalization.SimplifiedChineseCultureName),
        _ => GraphLocalization.ResolveSupportedCulture(_systemCulture)
    };

    private static DemoLanguagePreference ParsePreference(string? cultureName)
    {
        if (string.Equals(cultureName, GraphLocalization.DefaultCultureName, StringComparison.OrdinalIgnoreCase))
        {
            return DemoLanguagePreference.English;
        }

        if (string.Equals(cultureName, GraphLocalization.SimplifiedChineseCultureName, StringComparison.OrdinalIgnoreCase))
        {
            return DemoLanguagePreference.SimplifiedChinese;
        }

        return DemoLanguagePreference.System;
    }

    private static string? ToStoredCulture(DemoLanguagePreference preference) => preference switch
    {
        DemoLanguagePreference.English => GraphLocalization.DefaultCultureName,
        DemoLanguagePreference.SimplifiedChinese => GraphLocalization.SimplifiedChineseCultureName,
        _ => null
    };
}
