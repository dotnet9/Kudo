using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kudo;
using Kudo.Core.Serialization;

namespace Kudo.Core.Documents;

/// <summary>
/// A complete replacement snapshot for document-level state when it differs.
/// </summary>
public sealed record GraphDocumentHeader(
    string Format,
    string Id,
    string SchemaVersion,
    string Title,
    string CreatedBy,
    DateTime CreatedAt,
    string Description,
    GraphCanvasSettings CanvasSettings,
    IReadOnlyDictionary<string, object?> Properties);

/// <summary>
/// Changes to one stable-ID element collection. Upserts are complete snapshots,
/// while Order is supplied only when the target ordering differs from the source.
/// </summary>
public sealed record GraphElementDelta<T>(
    IReadOnlyList<T> Upserts,
    IReadOnlyList<string> RemovedIds,
    IReadOnlyList<string>? Order = null)
    where T : class
{
    public static GraphElementDelta<T> Empty { get; } = new([], []);

    public bool IsEmpty => Upserts.Count == 0 && RemovedIds.Count == 0 && Order is null;
}

/// <summary>
/// A stable-ID patch between two graph documents. It keeps each changed element
/// as a full snapshot and verifies the exact source content before it is applied.
/// </summary>
public sealed record GraphDocumentDelta
{
    /// <summary>
    /// The source document ID expected by <see cref="GraphDocumentDeltaApplier"/>.
    /// </summary>
    public required string SourceDocumentId { get; init; }

    /// <summary>
    /// SHA-256 of the serialized source snapshot. This prevents a delta from
    /// silently overwriting later edits to a document with the same ID.
    /// </summary>
    public required string SourceFingerprint { get; init; }

    public GraphDocumentHeader? Header { get; init; }

    public GraphElementDelta<NodeModel> Nodes { get; init; } = GraphElementDelta<NodeModel>.Empty;

    public GraphElementDelta<EdgeModel> Edges { get; init; } = GraphElementDelta<EdgeModel>.Empty;

    public GraphElementDelta<GroupModel> Groups { get; init; } = GraphElementDelta<GroupModel>.Empty;

    public GraphElementDelta<AnnotationModel> Annotations { get; init; } =
        GraphElementDelta<AnnotationModel>.Empty;

    public bool IsEmpty =>
        Header is null &&
        Nodes.IsEmpty &&
        Edges.IsEmpty &&
        Groups.IsEmpty &&
        Annotations.IsEmpty;
}

/// <summary>
/// Thrown when a delta is applied to a different document than the one it was calculated from.
/// </summary>
public sealed class GraphDocumentDeltaConflictException(string message) : InvalidOperationException(message);

/// <summary>
/// Calculates GraphDocumentDelta instances without mutating either input document.
/// </summary>
public static class GraphDocumentDeltaCalculator
{
    public static GraphDocumentDelta Calculate(GraphDocument source, GraphDocument target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var sourceHeader = CreateHeader(source);
        var targetHeader = CreateHeader(target);
        return new GraphDocumentDelta
        {
            SourceDocumentId = source.Id,
            SourceFingerprint = GraphDocumentDeltaFingerprint.Calculate(source),
            Header = HeaderEquals(sourceHeader, targetHeader) ? null : CloneHeader(targetHeader),
            Nodes = CalculateElements(
                source.Nodes,
                target.Nodes,
                static node => node.Id,
                GraphModelSnapshot.CloneNode,
                NodeEquals,
                nameof(GraphDocument.Nodes)),
            Edges = CalculateElements(
                source.Edges,
                target.Edges,
                static edge => edge.Id,
                GraphModelSnapshot.CloneEdge,
                EdgeEquals,
                nameof(GraphDocument.Edges)),
            Groups = CalculateElements(
                source.Groups,
                target.Groups,
                static group => group.Id,
                GraphModelSnapshot.CloneGroup,
                GroupEquals,
                nameof(GraphDocument.Groups)),
            Annotations = CalculateElements(
                source.Annotations,
                target.Annotations,
                static annotation => annotation.Id,
                GraphModelSnapshot.CloneAnnotation,
                AnnotationEquals,
                nameof(GraphDocument.Annotations))
        };
    }

    private static GraphElementDelta<T> CalculateElements<T>(
        IEnumerable<T>? source,
        IEnumerable<T>? target,
        Func<T, string?> getId,
        Func<T, T> clone,
        Func<T, T, bool> equals,
        string collectionName)
        where T : class
    {
        var sourceItems = (source ?? []).ToArray();
        var targetItems = (target ?? []).ToArray();
        var sourceById = Index(sourceItems, getId, collectionName);
        var targetById = Index(targetItems, getId, collectionName);
        var upserts = targetItems
            .Where(item =>
            {
                var id = getId(item)!;
                return !sourceById.TryGetValue(id, out var previous) || !equals(previous, item);
            })
            .Select(clone)
            .ToArray();
        var removedIds = sourceItems
            .Select(getId)
            .Where(id => id is not null && !targetById.ContainsKey(id))
            .Select(id => id!)
            .ToArray();
        var sourceOrder = sourceItems.Select(item => getId(item)!).ToArray();
        var targetOrder = targetItems.Select(item => getId(item)!).ToArray();

        return new GraphElementDelta<T>(
            upserts,
            removedIds,
            sourceOrder.SequenceEqual(targetOrder, StringComparer.Ordinal) ? null : targetOrder);
    }

    private static Dictionary<string, T> Index<T>(
        IEnumerable<T> items,
        Func<T, string?> getId,
        string collectionName)
        where T : class
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is null)
            {
                throw new ArgumentException($"{collectionName} cannot contain null elements.");
            }

            var id = getId(item);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"Every element in {collectionName} requires a non-empty ID.");
            }

            if (!result.TryAdd(id, item))
            {
                throw new ArgumentException($"Element ID '{id}' is duplicated in {collectionName}.");
            }
        }

        return result;
    }

    private static GraphDocumentHeader CreateHeader(GraphDocument document) => new(
        document.Format,
        document.Id,
        document.SchemaVersion,
        document.Title,
        document.CreatedBy,
        document.CreatedAt,
        document.Description,
        GraphModelSnapshot.CloneCanvasSettings(document.CanvasSettings),
        GraphModelSnapshot.CloneProperties(document.Properties));

    private static GraphDocumentHeader CloneHeader(GraphDocumentHeader header) => new(
        header.Format,
        header.Id,
        header.SchemaVersion,
        header.Title,
        header.CreatedBy,
        header.CreatedAt,
        header.Description,
        GraphModelSnapshot.CloneCanvasSettings(header.CanvasSettings),
        GraphModelSnapshot.CloneProperties(header.Properties));

    private static bool HeaderEquals(GraphDocumentHeader left, GraphDocumentHeader right) =>
        string.Equals(left.Format, right.Format, StringComparison.Ordinal) &&
        string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
        string.Equals(left.SchemaVersion, right.SchemaVersion, StringComparison.Ordinal) &&
        string.Equals(left.Title, right.Title, StringComparison.Ordinal) &&
        string.Equals(left.CreatedBy, right.CreatedBy, StringComparison.Ordinal) &&
        left.CreatedAt == right.CreatedAt &&
        string.Equals(left.Description, right.Description, StringComparison.Ordinal) &&
        Equals(left.CanvasSettings, right.CanvasSettings) &&
        PropertiesEqual(left.Properties, right.Properties);

    private static bool NodeEquals(NodeModel left, NodeModel right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
        string.Equals(left.Type, right.Type, StringComparison.Ordinal) &&
        string.Equals(left.DefinitionId, right.DefinitionId, StringComparison.Ordinal) &&
        string.Equals(left.Number, right.Number, StringComparison.Ordinal) &&
        string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
        string.Equals(left.Description, right.Description, StringComparison.Ordinal) &&
        left.Position == right.Position &&
        left.Size == right.Size &&
        left.Rotation == right.Rotation &&
        left.IsLocked == right.IsLocked &&
        Equals(left.Appearance, right.Appearance) &&
        ExtensionStateEquals(left.ExtensionState, right.ExtensionState) &&
        SequenceEqual(left.Ports, right.Ports, PortEquals) &&
        PropertiesEqual(left.Properties, right.Properties);

    private static bool PortEquals(PortModel left, PortModel right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
        string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
        left.Kind == right.Kind &&
        left.IsVisible == right.IsVisible &&
        left.Anchor == right.Anchor &&
        left.IncomingDirection == right.IncomingDirection &&
        left.OutgoingDirection == right.OutgoingDirection &&
        string.Equals(left.DataType, right.DataType, StringComparison.Ordinal) &&
        Equals(left.Capacity, right.Capacity) &&
        left.State == right.State &&
        PropertiesEqual(left.Properties, right.Properties);

    private static bool EdgeEquals(EdgeModel left, EdgeModel right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
        left.Source == right.Source &&
        left.Target == right.Target &&
        left.RelationKind == right.RelationKind &&
        left.RoutingStyle == right.RoutingStyle &&
        left.StylePreset == right.StylePreset &&
        EdgeAppearanceEquals(left.Appearance, right.Appearance) &&
        SequenceEqual(left.Waypoints, right.Waypoints, static (first, second) => first == second) &&
        left.SourceMarker == right.SourceMarker &&
        left.TargetMarker == right.TargetMarker &&
        string.Equals(left.Label, right.Label, StringComparison.Ordinal) &&
        left.State == right.State &&
        PropertiesEqual(left.Properties, right.Properties);

    private static bool GroupEquals(GroupModel left, GroupModel right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
        string.Equals(left.Title, right.Title, StringComparison.Ordinal) &&
        SequenceEqual(left.NodeIds, right.NodeIds, static (first, second) =>
            string.Equals(first, second, StringComparison.Ordinal)) &&
        left.Padding == right.Padding &&
        left.IsCollapsed == right.IsCollapsed &&
        PropertiesEqual(left.Properties, right.Properties);

    private static bool AnnotationEquals(AnnotationModel left, AnnotationModel right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
        string.Equals(left.Text, right.Text, StringComparison.Ordinal) &&
        left.Position == right.Position &&
        left.Size == right.Size &&
        PropertiesEqual(left.Properties, right.Properties);

    private static bool ExtensionStateEquals(GraphExtensionState? left, GraphExtensionState? right) =>
        ReferenceEquals(left, right) ||
        (left is not null &&
         right is not null &&
         string.Equals(left.SchemaId, right.SchemaId, StringComparison.Ordinal) &&
         left.SchemaVersion == right.SchemaVersion &&
         JsonElementsEqual(left.Payload, right.Payload));

    private static bool EdgeAppearanceEquals(GraphEdgeAppearance? left, GraphEdgeAppearance? right) =>
        ReferenceEquals(left, right) ||
        (left is not null &&
         right is not null &&
         string.Equals(left.StrokeColor, right.StrokeColor, StringComparison.Ordinal) &&
         left.StrokeWidth == right.StrokeWidth &&
         left.DashStyle == right.DashStyle &&
         SequenceEqual(left.CustomDashPattern, right.CustomDashPattern, static (first, second) => first == second) &&
         left.Opacity == right.Opacity &&
         left.Animation == right.Animation &&
         left.AnimationSpeed == right.AnimationSpeed &&
         left.AnimationDirection == right.AnimationDirection &&
         left.GlowIntensity == right.GlowIntensity &&
         string.Equals(left.GlowColor, right.GlowColor, StringComparison.Ordinal));

    private static bool PropertiesEqual(
        IReadOnlyDictionary<string, object?>? left,
        IReadOnlyDictionary<string, object?>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || !PropertyValueEquals(pair.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PropertyValueEquals(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left is JsonElement leftJson && right is JsonElement rightJson)
        {
            return JsonElementsEqual(leftJson, rightJson);
        }

        if (left is IReadOnlyDictionary<string, object?> leftDictionary &&
            right is IReadOnlyDictionary<string, object?> rightDictionary)
        {
            return PropertiesEqual(leftDictionary, rightDictionary);
        }

        if (left is IDictionary leftDictionaryValue && right is IDictionary rightDictionaryValue)
        {
            return DictionaryEquals(leftDictionaryValue, rightDictionaryValue);
        }

        if (left is IEnumerable leftValues && right is IEnumerable rightValues &&
            left is not string && right is not string)
        {
            return EnumerableEquals(leftValues, rightValues);
        }

        return left.Equals(right);
    }

    private static bool JsonElementsEqual(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var leftProperties = left.EnumerateObject().ToArray();
                var rightProperties = right.EnumerateObject().ToArray();
                if (leftProperties.Length != rightProperties.Length)
                {
                    return false;
                }

                foreach (var property in leftProperties)
                {
                    if (!right.TryGetProperty(property.Name, out var rightValue) ||
                        !JsonElementsEqual(property.Value, rightValue))
                    {
                        return false;
                    }
                }

                return true;
            }
            case JsonValueKind.Array:
            {
                var leftValues = left.EnumerateArray().GetEnumerator();
                var rightValues = right.EnumerateArray().GetEnumerator();
                try
                {
                    while (leftValues.MoveNext())
                    {
                        if (!rightValues.MoveNext() ||
                            !JsonElementsEqual(leftValues.Current, rightValues.Current))
                        {
                            return false;
                        }
                    }

                    return !rightValues.MoveNext();
                }
                finally
                {
                    leftValues.Dispose();
                    rightValues.Dispose();
                }
            }
            case JsonValueKind.String:
                return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                return left.TryGetDecimal(out var leftDecimal) &&
                    right.TryGetDecimal(out var rightDecimal)
                    ? leftDecimal == rightDecimal
                    : string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            default:
                return string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
        }
    }

    private static bool DictionaryEquals(IDictionary left, IDictionary right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (DictionaryEntry pair in left)
        {
            if (pair.Key is null || !right.Contains(pair.Key) ||
                !PropertyValueEquals(pair.Value, right[pair.Key]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EnumerableEquals(IEnumerable left, IEnumerable right)
    {
        var leftEnumerator = left.GetEnumerator();
        var rightEnumerator = right.GetEnumerator();
        try
        {
            while (leftEnumerator.MoveNext())
            {
                if (!rightEnumerator.MoveNext() ||
                    !PropertyValueEquals(leftEnumerator.Current, rightEnumerator.Current))
                {
                    return false;
                }
            }

            return !rightEnumerator.MoveNext();
        }
        finally
        {
            (leftEnumerator as IDisposable)?.Dispose();
            (rightEnumerator as IDisposable)?.Dispose();
        }
    }

    private static bool SequenceEqual<T>(
        IEnumerable<T>? left,
        IEnumerable<T>? right,
        Func<T, T, bool> equals) =>
        (left ?? []).SequenceEqual(right ?? [], new DelegateEqualityComparer<T>(equals));

    private sealed class DelegateEqualityComparer<T>(Func<T, T, bool> equals) : IEqualityComparer<T>
    {
        public bool Equals(T? x, T? y) => x is null ? y is null : y is not null && equals(x, y);

        public int GetHashCode(T obj) => 0;
    }
}

/// <summary>
/// Applies GraphDocumentDelta instances to cloned source documents.
/// </summary>
public static class GraphDocumentDeltaApplier
{
    public static GraphDocument Apply(GraphDocument source, GraphDocumentDelta delta)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(delta);
        if (!string.Equals(source.Id, delta.SourceDocumentId, StringComparison.Ordinal))
        {
            throw new GraphDocumentDeltaConflictException(
                $"The delta was calculated for document '{delta.SourceDocumentId}', not '{source.Id}'.");
        }

        if (!string.Equals(
                GraphDocumentDeltaFingerprint.Calculate(source),
                delta.SourceFingerprint,
                StringComparison.Ordinal))
        {
            throw new GraphDocumentDeltaConflictException(
                "The source document has changed since the delta was calculated.");
        }

        var result = GraphModelSnapshot.CloneDocument(source);
        if (delta.Header is { } header)
        {
            result.Format = header.Format;
            result.Id = header.Id;
            result.SchemaVersion = header.SchemaVersion;
            result.Title = header.Title;
            result.CreatedBy = header.CreatedBy;
            result.CreatedAt = header.CreatedAt;
            result.Description = header.Description;
            result.CanvasSettings = GraphModelSnapshot.CloneCanvasSettings(header.CanvasSettings);
            result.Properties = GraphModelSnapshot.CloneProperties(header.Properties);
        }

        result.Nodes = ApplyElements(
            result.Nodes,
            delta.Nodes,
            static node => node.Id,
            GraphModelSnapshot.CloneNode,
            nameof(GraphDocument.Nodes));
        result.Edges = ApplyElements(
            result.Edges,
            delta.Edges,
            static edge => edge.Id,
            GraphModelSnapshot.CloneEdge,
            nameof(GraphDocument.Edges));
        result.Groups = ApplyElements(
            result.Groups,
            delta.Groups,
            static group => group.Id,
            GraphModelSnapshot.CloneGroup,
            nameof(GraphDocument.Groups));
        result.Annotations = ApplyElements(
            result.Annotations,
            delta.Annotations,
            static annotation => annotation.Id,
            GraphModelSnapshot.CloneAnnotation,
            nameof(GraphDocument.Annotations));
        return result;
    }

    private static List<T> ApplyElements<T>(
        IEnumerable<T>? source,
        GraphElementDelta<T> delta,
        Func<T, string?> getId,
        Func<T, T> clone,
        string collectionName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(delta);
        var sourceItems = (source ?? []).ToArray();
        var byId = Index(sourceItems, getId, collectionName);
        var order = sourceItems.Select(item => getId(item)!).ToList();
        var removed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in delta.RemovedIds)
        {
            ValidateId(id, collectionName);
            if (!removed.Add(id))
            {
                throw new ArgumentException($"Element ID '{id}' is removed more than once from {collectionName}.");
            }

            if (!byId.Remove(id))
            {
                throw new GraphDocumentDeltaConflictException(
                    $"Element ID '{id}' no longer exists in {collectionName}.");
            }

            order.Remove(id);
        }

        var upsertIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in delta.Upserts)
        {
            if (item is null)
            {
                throw new ArgumentException($"{collectionName} upserts cannot contain null elements.");
            }

            var id = getId(item);
            ValidateId(id, collectionName);
            if (!upsertIds.Add(id!))
            {
                throw new ArgumentException($"Element ID '{id}' is upserted more than once in {collectionName}.");
            }

            if (!byId.ContainsKey(id!))
            {
                order.Add(id!);
            }

            byId[id!] = clone(item);
        }

        if (delta.Order is { } targetOrder)
        {
            order = ValidateOrder(targetOrder, byId, collectionName);
        }

        return order.Select(id => byId[id]).ToList();
    }

    private static Dictionary<string, T> Index<T>(
        IEnumerable<T> items,
        Func<T, string?> getId,
        string collectionName)
        where T : class
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is null)
            {
                throw new ArgumentException($"{collectionName} cannot contain null elements.");
            }

            var id = getId(item);
            ValidateId(id, collectionName);
            if (!result.TryAdd(id!, item))
            {
                throw new ArgumentException($"Element ID '{id}' is duplicated in {collectionName}.");
            }
        }

        return result;
    }

    private static List<string> ValidateOrder<T>(
        IReadOnlyList<string> order,
        IReadOnlyDictionary<string, T> elements,
        string collectionName)
        where T : class
    {
        if (order.Count != elements.Count)
        {
            throw new ArgumentException($"The {collectionName} order does not contain every current element.");
        }

        var result = new List<string>(order.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in order)
        {
            ValidateId(id, collectionName);
            if (!ids.Add(id) || !elements.ContainsKey(id))
            {
                throw new ArgumentException($"The {collectionName} order contains an invalid element ID '{id}'.");
            }

            result.Add(id);
        }

        return result;
    }

    private static void ValidateId(string? id, string collectionName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException($"Every element in {collectionName} requires a non-empty ID.");
        }
    }
}

internal static class GraphDocumentDeltaFingerprint
{
    private static readonly GraphTaskXmlSerializer Serializer = new(new GraphSerializationOptions(false));

    public static string Calculate(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var snapshot = GraphModelSnapshot.CloneDocument(document);
        var xml = Serializer.Serialize(snapshot);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xml)));
    }
}
