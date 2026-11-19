using System.Collections;
using System.Text.Json;
using Kudo;

namespace Kudo.Avalonia.Designer;

internal static class GraphNodeDefinitionValue
{
    public static PortModel ClonePort(PortModel port)
    {
        ArgumentNullException.ThrowIfNull(port);
        return new PortModel
        {
            Id = port.Id,
            Name = port.Name,
            Kind = port.Kind,
            IsVisible = port.IsVisible,
            Anchor = port.Anchor,
            IncomingDirection = port.IncomingDirection,
            OutgoingDirection = port.OutgoingDirection,
            DataType = port.DataType,
            Capacity = port.Capacity with { },
            State = port.State,
            Properties = MaterializeDictionary(port.Properties)
        };
    }

    public static Dictionary<string, object?> CloneDictionary(
        IReadOnlyDictionary<string, object?>? source) => source is null
        ? new Dictionary<string, object?>(StringComparer.Ordinal)
        : source.ToDictionary(
            pair => pair.Key,
            pair => Clone(pair.Value),
            StringComparer.Ordinal);

    public static Dictionary<string, object?> MaterializeDictionary(
        IReadOnlyDictionary<string, object?>? source) => source is null
        ? new Dictionary<string, object?>(StringComparer.Ordinal)
        : source.ToDictionary(
            pair => pair.Key,
            pair => Materialize(pair.Value),
            StringComparer.Ordinal);

    public static object? MaterializeValue(object? value) => Materialize(value);

    private static object? Clone(object? value) => value switch
    {
        null => null,
        JsonElement element => element.Clone(),
        IReadOnlyDictionary<string, object?> dictionary => CloneDictionary(dictionary),
        IDictionary dictionary => CloneNonGenericDictionary(dictionary),
        IEnumerable values when value is not string => values
            .Cast<object?>()
            .Select(Clone)
            .ToList(),
        _ => value
    };

    private static object? Materialize(object? value) => value switch
    {
        null => null,
        JsonElement element => MaterializeJsonElement(element),
        IReadOnlyDictionary<string, object?> dictionary => MaterializeDictionary(dictionary),
        IDictionary dictionary => MaterializeNonGenericDictionary(dictionary),
        IEnumerable values when value is not string => values
            .Cast<object?>()
            .Select(Materialize)
            .ToList(),
        _ => value
    };

    private static object? MaterializeJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => MaterializeJsonNumber(element),
        JsonValueKind.Array => element
            .EnumerateArray()
            .Select(MaterializeJsonElement)
            .ToList(),
        JsonValueKind.Object => element
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => MaterializeJsonElement(property.Value),
                StringComparer.Ordinal),
        _ => element.ToString()
    };

    private static object MaterializeJsonNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var integer))
        {
            return integer is >= int.MinValue and <= int.MaxValue
                ? (int)integer
                : integer;
        }

        return element.GetDouble();
    }

    private static Dictionary<string, object?> CloneNonGenericDictionary(IDictionary source)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in source)
        {
            if (entry.Key is not null)
            {
                result[entry.Key.ToString() ?? string.Empty] = Clone(entry.Value);
            }
        }

        return result;
    }

    private static Dictionary<string, object?> MaterializeNonGenericDictionary(IDictionary source)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in source)
        {
            if (entry.Key is not null)
            {
                result[entry.Key.ToString() ?? string.Empty] = Materialize(entry.Value);
            }
        }

        return result;
    }
}
