// 属性值转换器：集中处理数值、枚举和扩展 JSON 值的解析与规范化。
using System.Globalization;
using System.Text.Json;

namespace Kudo.Avalonia.Designer;

internal static class GraphPropertyValueConverter
{
    public static bool TryParseCoordinate(string? text, out double value) =>
        double.TryParse(
            text,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.CurrentCulture,
            out value) && double.IsFinite(value);

    public static bool TryParsePositiveSize(string? text, out double value) =>
        TryParseCoordinate(text, out value) && value > 0;

    public static bool TryParseNonNegativeNumber(string? text, out double value) =>
        TryParseCoordinate(text, out value) && value >= 0;

    public static bool TryParseExtensionProperties(
        string? text,
        out Dictionary<string, object?> properties)
    {
        properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var sourceLine in (text ?? string.Empty).Split('\n'))
        {
            var line = sourceLine.Trim().TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                return false;
            }

            var key = line[..separator].Trim();
            if (key.Length == 0 || properties.ContainsKey(key))
            {
                return false;
            }

            var value = line[(separator + 1)..].Trim();
            properties.Add(key, ParseExtensionPropertyValue(value));
        }

        return true;
    }

    public static string FormatExtensionProperties(
        IReadOnlyDictionary<string, object?> properties) =>
        string.Join(
            Environment.NewLine,
            properties
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={FormatExtensionPropertyValue(pair.Value)}"));

    public static string? NormalizeOptionalText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static object? ParseExtensionPropertyValue(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Null
                ? null
                : document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static string FormatExtensionPropertyValue(object? value) => value switch
    {
        null => "null",
        JsonElement element => element.GetRawText(),
        string text => JsonSerializer.Serialize(text),
        bool boolean => boolean ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => JsonSerializer.Serialize(value)
    };
}
