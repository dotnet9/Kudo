using System.Xml.Linq;

namespace Kudo.Demo.Desktop.Infrastructure;

/// <summary>
///     极简 App.config（appSettings）键值读写器：替代 CodeWF.Toolkit.Files 的 AppConfigHelper。
///     文件不存在时读取返回空；写入自动创建目录与文件结构。
/// </summary>
public static class AppConfigHelper
{
    public static bool TryGet(string filePath, string key, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        value = null;
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var settings = LoadSettings(filePath);
            return settings.TryGetValue(key, out value);
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException)
        {
            return false;
        }
    }

    public static void SetOrAdd(string filePath, string key, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = File.Exists(filePath)
            ? XElement.Load(filePath)
            : new XElement("configuration");
        var appSettings = document.Element("appSettings");
        if (appSettings is null)
        {
            appSettings = new XElement("appSettings");
            document.Add(appSettings);
        }

        var entry = appSettings.Elements("add")
            .FirstOrDefault(element => (string?)element.Attribute("key") == key);
        if (entry is null)
        {
            appSettings.Add(new XElement("add",
                new XAttribute("key", key),
                new XAttribute("value", value ?? string.Empty)));
        }
        else
        {
            entry.SetAttributeValue("value", value ?? string.Empty);
        }

        document.Save(filePath);
    }

    public static bool TryGet(string filePath, string key, out bool value)
    {
        var ok = TryGet(filePath, key, out string? text);
        value = ok && bool.TryParse(text, out var parsed) && parsed;
        return ok;
    }

    public static void SetOrAdd(string filePath, string key, bool value) =>
        SetOrAdd(filePath, key, value ? bool.TrueString : bool.FalseString);

    private static Dictionary<string, string> LoadSettings(string filePath)
    {
        var document = XElement.Load(filePath);
        return document.Element("appSettings")?
            .Elements("add")
            .Select(element => (Key: (string?)element.Attribute("key"), Value: (string?)element.Attribute("value")))
            .Where(pair => pair.Key is not null)
            .ToDictionary(pair => pair.Key!, pair => pair.Value ?? string.Empty, StringComparer.Ordinal)
            ?? [];
    }
}
