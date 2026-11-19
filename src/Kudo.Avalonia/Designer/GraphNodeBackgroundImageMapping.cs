using System.Collections;
using System.Globalization;
using System.Text.Json;
using Kudo;

namespace Kudo.Avalonia.Designer;

/// <summary>
/// 当节点属性值匹配时选择对应的背景图。
/// </summary>
public sealed record GraphNodeBackgroundImageMapping(
    string PropertyName,
    string Value,
    string Resource)
{
    public bool Matches(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Properties.TryGetValue(PropertyName, out var propertyValue) &&
               string.Equals(
                   FormatValue(propertyValue),
                   Value.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }

    public string NormalizedResource => Resource.Replace('\\', '/').Trim();

    public static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        bool boolean => boolean ? "true" : "false",
        JsonElement element => FormatJsonValue(element),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static string FormatJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => element.ToString()
    };
}

/// <summary>
/// 画布与导出共同使用的背景图解析结果。
/// </summary>
public sealed record GraphNodeBackgroundImageResolution(
    string? Resource,
    GraphNodeBackgroundImageMapping? Mapping = null)
{
    public bool IsMapped => Mapping is not null;
}

public interface IGraphNodeBackgroundImageResolver
{
    GraphNodeBackgroundImageResolution Resolve(NodeModel node);
}

/// <summary>
/// 解析节点的背景图映射，同时避免渲染逻辑与模型目录耦合。
/// </summary>
public sealed class GraphNodeBackgroundImageResolver : IGraphNodeBackgroundImageResolver
{
    private readonly Func<NodeModel, GraphNodeDefinition?> _definitionResolver;

    public GraphNodeBackgroundImageResolver(
        Func<NodeModel, GraphNodeDefinition?> definitionResolver)
    {
        _definitionResolver = definitionResolver ??
            throw new ArgumentNullException(nameof(definitionResolver));
    }

    public GraphNodeBackgroundImageResolution Resolve(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var definition = _definitionResolver(node);
        var mapping = definition?.BackgroundImageMappings
            .FirstOrDefault(item => item.Matches(node));
        if (mapping is null)
        {
            return new GraphNodeBackgroundImageResolution(
                node.Appearance?.Normalize().BackgroundImage);
        }

        var resource = ResolveImportedResource(node, mapping.NormalizedResource) ??
            mapping.NormalizedResource;
        return new GraphNodeBackgroundImageResolution(resource, mapping);
    }

    private static string? ResolveImportedResource(NodeModel node, string resource)
    {
        if (!node.Properties.TryGetValue(GraphNodePropertyKeys.BackgroundImageMappings, out var value))
        {
            return null;
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary &&
            dictionary.TryGetValue(resource, out var mapped))
        {
            return mapped?.ToString();
        }

        if (value is IDictionary nonGeneric)
        {
            foreach (DictionaryEntry entry in nonGeneric)
            {
                if (string.Equals(entry.Key?.ToString(), resource, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value?.ToString();
                }
            }
        }

        return null;
    }
}
