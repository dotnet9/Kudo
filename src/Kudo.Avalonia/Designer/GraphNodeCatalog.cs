using Avalonia.Input;
using Kudo;
using CatalogLanguage = global::Localization.Kudo_Avalonia.Designer.GraphNodeCatalog;

namespace Kudo.Avalonia.Designer;

public sealed record GraphNodeDefinition(
    string GroupId,
    string GroupName,
    string Name,
    string Description,
    string NodeType,
    string TemplateKey,
    string IconText)
{
    public const double MaximumSymbolDimension = 200;

    public string Id { get; init; } = NodeType;

    public GraphNodeDefinitionKind Kind { get; init; }

    public string? Resource { get; init; }

    public GraphSize DefaultSize { get; init; } = new(160, 88);

    public GraphSize? MinimumSize { get; init; }

    public GraphSize? IntrinsicImageSize { get; init; }

    public string GroupIconText { get; init; } = string.Empty;

    public string? GroupIcon { get; init; }

    public string? Icon { get; init; }

    public GraphNodeAppearance DefaultAppearance { get; init; } = new();

    public IReadOnlyList<GraphNodeBackgroundImageMapping> BackgroundImageMappings { get; init; } = [];

    public IReadOnlyList<PortModel> DefaultPorts { get; init; } = [];

    public IReadOnlyDictionary<string, object?> DefaultProperties { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    public IReadOnlyList<GraphNodePropertyDefinition> PropertyDefinitions { get; init; } = [];

    public GraphCompositeDefinition? Composite { get; init; }

    public string? ResourceDirectory { get; init; }

    public GraphSize GetEffectiveDefaultSize()
    {
        if (DefaultAppearance.VisualMode != GraphNodeVisualMode.Symbol ||
            IntrinsicImageSize is not { } imageSize ||
            !double.IsFinite(imageSize.Width) || imageSize.Width <= 0 ||
            !double.IsFinite(imageSize.Height) || imageSize.Height <= 0)
        {
            return DefaultSize;
        }

        var scale = Math.Min(1,
            Math.Min(
                DefaultSize.Width / imageSize.Width,
                DefaultSize.Height / imageSize.Height));
        scale = Math.Min(
            scale,
            MaximumSymbolDimension / Math.Max(imageSize.Width, imageSize.Height));
        return new GraphSize(imageSize.Width * scale, imageSize.Height * scale);
    }

    public string? ResolveResourcePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(ResourceDirectory))
        {
            return null;
        }

        var root = Path.GetFullPath(ResourceDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}

public sealed record GraphNodeCatalogGroup(
    string Id,
    string Name,
    IReadOnlyList<GraphNodeDefinition> Definitions,
    string IconText = "",
    string? Icon = null);

public enum GraphNodeDefinitionKind
{
    Basic,
    Composite,
    Annotation
}

public enum GraphNodePropertyValueKind
{
    String,
    Number,
    Integer,
    Boolean,
    Percent,
    Choice,
    Color
}

public enum GraphNodePropertyEditorKind
{
    Auto,
    ComboBox,
    RadioButton
}

public sealed record GraphNodePropertyOption(
    string Value,
    string DisplayName);

public sealed record GraphNodePropertyDefinition(
    string Name,
    string DisplayName,
    GraphNodePropertyValueKind ValueKind = GraphNodePropertyValueKind.String)
{
    public string GroupName { get; init; } = string.Empty;

    public object? DefaultValue { get; init; }

    public bool IsReadOnly { get; init; }

    public string? Unit { get; init; }

    public decimal? Minimum { get; init; }

    public decimal? Maximum { get; init; }

    public decimal? Step { get; init; }

    public IReadOnlyList<string> Options { get; init; } = [];

    public IReadOnlyList<GraphNodePropertyOption> ChoiceOptions { get; init; } = [];

    public GraphNodePropertyEditorKind Editor { get; init; }
}

public sealed record GraphCompositeDefinition
{
    public IReadOnlyList<GraphCompositeChildDefinition> Children { get; init; } = [];

    public IReadOnlyList<GraphCompositeConnectionDefinition> Connections { get; init; } = [];

    public IReadOnlyList<GraphCompositePortDefinition> Ports { get; init; } = [];
}

public sealed record GraphCompositeChildDefinition(
    string Id,
    string Symbol,
    GraphPoint Position)
{
    public double Rotation { get; init; }

    public GraphSize? Size { get; init; }
}

public sealed record GraphCompositeConnectionDefinition(
    string From,
    string To);

public sealed record GraphCompositePortDefinition(
    string Id,
    string MapTo)
{
    public string Name { get; init; } = string.Empty;

    public PortKind Kind { get; init; } = PortKind.InputOutput;
}

public interface IGraphNodeCatalog
{
    IReadOnlyList<GraphNodeCatalogGroup> Groups { get; }

    GraphNodeDefinition? FindById(string definitionId);

    GraphNodeDefinition? FindByNodeType(string nodeType);

    IReadOnlyList<GraphNodeDefinition> Search(string? keyword, string? groupId = null);
}

public interface IGraphNodeFactory
{
    NodeModel CreateNode(GraphNodeDefinition definition, string nodeId, GraphPoint position);
}

public sealed class GraphNodeCatalog : IGraphNodeCatalog
{
    private readonly IReadOnlyList<GraphNodeDefinition> _definitions;
    private readonly IReadOnlyDictionary<string, GraphNodeDefinition> _definitionsByType;
    private readonly IReadOnlyDictionary<string, GraphNodeDefinition> _definitionsById;

    public GraphNodeCatalog(IEnumerable<GraphNodeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var items = definitions.ToArray();
        foreach (var definition in items)
        {
            Validate(definition);
        }

        var duplicateType = items
            .GroupBy(definition => definition.NodeType, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateType is not null)
        {
            throw new ArgumentException(
                $"Node type '{duplicateType}' is duplicated in the catalog.",
                nameof(definitions));
        }

        var duplicateId = items
            .GroupBy(definition => definition.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
        {
            throw new ArgumentException(
                $"Node definition ID '{duplicateId}' is duplicated in the catalog.",
                nameof(definitions));
        }

        _definitions = items;
        _definitionsByType = items.ToDictionary(
            definition => definition.NodeType,
            StringComparer.Ordinal);
        _definitionsById = items.ToDictionary(
            definition => definition.Id,
            StringComparer.Ordinal);
        Groups = items
            .GroupBy(definition => definition.GroupId, StringComparer.Ordinal)
            .Select(group => new GraphNodeCatalogGroup(
                group.Key,
                group.Select(definition => definition.GroupName).First(),
                group.ToArray(),
                group.Select(definition => definition.GroupIconText)
                    .FirstOrDefault(icon => !string.IsNullOrWhiteSpace(icon)) ?? string.Empty,
                group.Select(definition => definition.ResolveResourcePath(definition.GroupIcon))
                    .FirstOrDefault(icon => !string.IsNullOrWhiteSpace(icon))))
            .ToArray();
    }

    public IReadOnlyList<GraphNodeCatalogGroup> Groups { get; }

    public GraphNodeDefinition? FindByNodeType(string nodeType) =>
        !string.IsNullOrWhiteSpace(nodeType) && _definitionsByType.TryGetValue(nodeType, out var definition)
            ? definition
            : null;

    public GraphNodeDefinition? FindById(string definitionId) =>
        !string.IsNullOrWhiteSpace(definitionId) && _definitionsById.TryGetValue(definitionId, out var definition)
            ? definition
            : null;

    public IReadOnlyList<GraphNodeDefinition> Search(string? keyword, string? groupId = null)
    {
        var query = string.IsNullOrWhiteSpace(groupId)
            ? _definitions
            : _definitions
                .Where(definition => string.Equals(
                    definition.GroupId,
                    groupId,
                    StringComparison.Ordinal))
                .ToArray();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return query;
        }

        var searchText = keyword.Trim();
        return query
            .Where(definition =>
                Contains(definition.Name, searchText) ||
                Contains(definition.Description, searchText) ||
                Contains(definition.NodeType, searchText) ||
                Contains(definition.GroupName, searchText))
            .ToArray();
    }

    private static bool Contains(string value, string keyword) =>
        value.Contains(keyword, StringComparison.CurrentCultureIgnoreCase);

    private static void Validate(GraphNodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireValue(definition.GroupId, nameof(definition.GroupId));
        RequireValue(definition.GroupName, nameof(definition.GroupName));
        RequireValue(definition.Id, nameof(definition.Id));
        RequireValue(definition.Name, nameof(definition.Name));
        RequireValue(definition.NodeType, nameof(definition.NodeType));
        RequireValue(definition.TemplateKey, nameof(definition.TemplateKey));
        if (!double.IsFinite(definition.DefaultSize.Width) || definition.DefaultSize.Width <= 0 ||
            !double.IsFinite(definition.DefaultSize.Height) || definition.DefaultSize.Height <= 0)
        {
            throw new ArgumentException(
                "DefaultSize must contain finite positive values.",
                nameof(definition.DefaultSize));
        }

        if (definition.MinimumSize is { } minimumSize &&
            (!double.IsFinite(minimumSize.Width) || minimumSize.Width <= 0 ||
             !double.IsFinite(minimumSize.Height) || minimumSize.Height <= 0))
        {
            throw new ArgumentException(
                "MinimumSize must contain finite positive values when provided.",
                nameof(definition.MinimumSize));
        }

        if (definition.IntrinsicImageSize is { } imageSize &&
            (!double.IsFinite(imageSize.Width) || imageSize.Width <= 0 ||
             !double.IsFinite(imageSize.Height) || imageSize.Height <= 0))
        {
            throw new ArgumentException(
                "IntrinsicImageSize must contain finite positive values when provided.",
                nameof(definition.IntrinsicImageSize));
        }

        var duplicatePortId = definition.DefaultPorts
            .Where(port => port is not null)
            .GroupBy(port => port.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicatePortId is not null)
        {
            throw new ArgumentException(
                $"Port id '{duplicatePortId}' is duplicated in node definition '{definition.Id}'.",
                nameof(definition.DefaultPorts));
        }

        foreach (var port in definition.DefaultPorts)
        {
            ArgumentNullException.ThrowIfNull(port);
            RequireValue(port.Id, nameof(port.Id));
        }

        var duplicatePropertyName = definition.PropertyDefinitions
            .Where(property => property is not null)
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicatePropertyName is not null)
        {
            throw new ArgumentException(
                $"Property name '{duplicatePropertyName}' is duplicated in node definition '{definition.Id}'.",
                nameof(definition.PropertyDefinitions));
        }

        foreach (var property in definition.PropertyDefinitions)
        {
            ArgumentNullException.ThrowIfNull(property);
            RequireValue(property.Name, nameof(property.Name));
            RequireValue(property.DisplayName, nameof(property.DisplayName));
            if (property.Minimum is { } minimum &&
                property.Maximum is { } maximum &&
                minimum > maximum)
            {
                throw new ArgumentException(
                    $"Property '{property.Name}' minimum cannot be greater than maximum.",
                    nameof(definition.PropertyDefinitions));
            }

            if (property.Step is { } step && step <= 0)
            {
                throw new ArgumentException(
                    $"Property '{property.Name}' step must be greater than zero.",
                    nameof(definition.PropertyDefinitions));
            }
        }

        var duplicateBackgroundMapping = definition.BackgroundImageMappings
            .Where(mapping => mapping is not null)
            .GroupBy(mapping => $"{mapping.PropertyName}\u001f{mapping.Value}", StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateBackgroundMapping is not null)
        {
            var mapping = duplicateBackgroundMapping.First();
            throw new ArgumentException(
                GraphLocalization.Format(
                    CatalogLanguage.DuplicateBackgroundImageMapping,
                    $"{mapping.PropertyName}={mapping.Value}",
                    definition.Id),
                nameof(definition.BackgroundImageMappings));
        }

        foreach (var mapping in definition.BackgroundImageMappings)
        {
            ArgumentNullException.ThrowIfNull(mapping);
            RequireValue(mapping.PropertyName, nameof(mapping.PropertyName));
            RequireValue(mapping.Value, nameof(mapping.Value));
            RequireValue(mapping.Resource, nameof(mapping.Resource));
        }
    }

    private static void RequireValue(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{propertyName} cannot be empty.", propertyName);
        }
    }
}

public static class GraphNodeDragDrop
{
    public const string DefinitionFormatName = "Kudo.NodeDefinition";

    public const string NodeTypeFormatName = "Kudo.NodeType";

    public static DataFormat<GraphNodeDefinition> DefinitionFormat { get; } =
        DataFormat.CreateInProcessFormat<GraphNodeDefinition>(DefinitionFormatName);

    public static DataFormat<string> NodeTypeFormat { get; } =
        DataFormat.CreateStringApplicationFormat(NodeTypeFormatName);

    public static DataTransfer CreateData(GraphNodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(DefinitionFormat, definition));
        data.Add(DataTransferItem.Create(NodeTypeFormat, definition.NodeType));
        return data;
    }

    public static bool TryGetDefinition(
        IDataTransfer? dataTransfer,
        out GraphNodeDefinition? definition)
    {
        if (dataTransfer is not null)
        {
            foreach (var item in dataTransfer.Items)
            {
                if (item.TryGetRaw(DefinitionFormat) is GraphNodeDefinition value)
                {
                    definition = value;
                    return true;
                }
            }
        }

        definition = null;
        return false;
    }

    public static bool TryGetNodeType(IDataTransfer? dataTransfer, out string nodeType)
    {
        if (dataTransfer is not null)
        {
            foreach (var item in dataTransfer.Items)
            {
                if (item.TryGetRaw(NodeTypeFormat) is string value && !string.IsNullOrWhiteSpace(value))
                {
                    nodeType = value;
                    return true;
                }
            }
        }

        nodeType = string.Empty;
        return false;
    }
}
