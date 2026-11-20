using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using Kudo.Demo.Desktop.Infrastructure;

namespace Kudo.Demo.Desktop.Services;

public sealed class DemoApplicationSettingsStore
{
    public const string SettingsPathEnvironmentVariable = "KUDO_DEMO_SETTINGS_PATH";

    private const string SettingsFileName = "Kudo.Demo.Desktop.config";
    private const string LanguageKey = "Application.Language";
    private const string ThemeKey = "Application.Theme";
    private const string RibbonExpandedKey = "Workspace.IsRibbonExpanded";
    private const string ToolVisibilityKey = "Workspace.ToolVisibility";

    public DemoApplicationSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
    }

    public string FilePath { get; }

    public static DemoApplicationSettingsStore CreateDefault()
    {
        var overriddenPath = Environment.GetEnvironmentVariable(SettingsPathEnvironmentVariable);
        return new DemoApplicationSettingsStore(
            string.IsNullOrWhiteSpace(overriddenPath)
                ? Path.Combine(Environment.CurrentDirectory, SettingsFileName)
                : overriddenPath);
    }

    public string? LoadLanguage() =>
        AppConfigHelper.TryGet(FilePath, LanguageKey, out string? language) &&
        !string.IsNullOrWhiteSpace(language)
            ? language
            : null;

    public bool TrySaveLanguage(string? cultureName)
    {
        try
        {
            AppConfigHelper.SetOrAdd(FilePath, LanguageKey, cultureName?.Trim() ?? string.Empty);
            return true;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    public string? LoadTheme() =>
        AppConfigHelper.TryGet(FilePath, ThemeKey, out string? theme) &&
        !string.IsNullOrWhiteSpace(theme)
            ? theme
            : null;

    public bool TrySaveTheme(string? themeKey)
    {
        try
        {
            AppConfigHelper.SetOrAdd(FilePath, ThemeKey, themeKey?.Trim() ?? string.Empty);
            return true;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    public DemoWorkspaceSettings LoadWorkspace(out string? warning)
    {
        warning = null;
        if (!File.Exists(FilePath))
        {
            return new DemoWorkspaceSettings();
        }

        try
        {
            var isRibbonExpanded = AppConfigHelper.TryGet(
                FilePath,
                RibbonExpandedKey,
                out bool ribbonExpanded)
                ? ribbonExpanded
                : false;
            var toolVisibility = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (AppConfigHelper.TryGet(FilePath, ToolVisibilityKey, out string? visibilityJson) &&
                !string.IsNullOrWhiteSpace(visibilityJson))
            {
                toolVisibility = JsonSerializer.Deserialize(
                        visibilityJson,
                        DemoApplicationSettingsJsonContext.Default.DictionaryStringBoolean) ??
                    new Dictionary<string, bool>(StringComparer.Ordinal);
            }

            return new DemoWorkspaceSettings
            {
                IsRibbonExpanded = isRibbonExpanded,
                ToolVisibility = new Dictionary<string, bool>(toolVisibility, StringComparer.Ordinal)
            };
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or JsonException or XmlException)
        {
            warning = exception.Message;
            return new DemoWorkspaceSettings();
        }
    }

    public bool TrySaveWorkspace(DemoWorkspaceSettings settings, out string? error)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            var toolVisibility = settings.ToolVisibility
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var visibilityJson = JsonSerializer.Serialize(
                toolVisibility,
                DemoApplicationSettingsJsonContext.Default.DictionaryStringBoolean);
            AppConfigHelper.SetOrAdd(FilePath, RibbonExpandedKey, settings.IsRibbonExpanded);
            AppConfigHelper.SetOrAdd(FilePath, ToolVisibilityKey, visibilityJson);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            error = exception.Message;
            return false;
        }
    }
}

public sealed class DemoWorkspaceSettings
{
    public bool IsRibbonExpanded { get; init; } = false;

    public Dictionary<string, bool> ToolVisibility { get; init; } = new(StringComparer.Ordinal);
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, bool>))]
internal partial class DemoApplicationSettingsJsonContext : JsonSerializerContext;
