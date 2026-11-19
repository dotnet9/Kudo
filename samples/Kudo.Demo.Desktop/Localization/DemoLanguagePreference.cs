using System.ComponentModel;

namespace Kudo.Demo.Desktop.Localization;

public enum DemoLanguagePreference
{
    [Description("跟随系统")]
    System,
    [Description("英语")]
    English,
    [Description("简体中文")]
    SimplifiedChinese
}

public sealed record DemoLanguageOption(
    DemoLanguagePreference Preference,
    string DisplayName,
    string? CultureName);
