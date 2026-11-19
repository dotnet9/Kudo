using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;

namespace Kudo.Avalonia.Designer;

public static class GraphNodeCatalogFormats
{
    public const string ModelLibraryV1 = "Kudo.ModelLibrary/1";

    public const string SymbolV1 = "Kudo.Symbol/1";
}

public sealed class GraphModelLibraryFile
{
    public string Format { get; set; } = GraphNodeCatalogFormats.ModelLibraryV1;

    public List<GraphModelLibraryGroupFile> Groups { get; set; } = [];
}

public sealed class GraphModelLibraryGroupFile
{
    public string Id { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;

    public string IconText { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public List<string> Symbols { get; set; } = [];
}

public sealed class GraphSymbolDefinitionFile
{
    public string Format { get; set; } = GraphNodeCatalogFormats.SymbolV1;

    public string Id { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;

    public string DescriptionKey { get; set; } = string.Empty;

    public string NodeType { get; set; } = string.Empty;

    public string TemplateKey { get; set; } = string.Empty;

    public string IconText { get; set; } = string.Empty;

    public GraphNodeDefinitionKind Kind { get; set; }

    public string? Resource { get; set; }

    public string? Icon { get; set; }

    public GraphSize DefaultSize { get; set; } = new(160, 88);

    public GraphSize? MinimumSize { get; set; }

    public GraphNodeAppearance Appearance { get; set; } = new()
    {
        Stretch = GraphImageStretch.Uniform
    };

    public List<GraphNodeBackgroundImageMappingFile> BackgroundImageMappings { get; set; } = [];

    public object? ThreeD { get; set; }

    public List<GraphPortDefinitionFile> Ports { get; set; } = [];

    public List<GraphNodePropertyDefinitionFile> PropertyDefinitions { get; set; } = [];

    public Dictionary<string, object?> Properties { get; set; } = [];

    public GraphCompositeDefinitionFile? Composite { get; set; }
}

public sealed class GraphNodeBackgroundImageMappingFile
{
    public string Property { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Resource { get; set; } = string.Empty;
}

public sealed class GraphPortDefinitionFile
{
    public string Id { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;

    public PortKind Kind { get; set; } = PortKind.Input;

    public bool IsVisible { get; set; } = true;

    public double? X { get; set; }

    public double? Y { get; set; }

    public string? DataType { get; set; }

    public PortCapacity Capacity { get; set; } = new();

    public GraphPoint? IncomingDirection { get; set; }

    public GraphPoint? OutgoingDirection { get; set; }

    public PortState State { get; set; } = PortState.Normal;

    public Dictionary<string, object?> Properties { get; set; } = [];
}

public sealed class GraphNodePropertyDefinitionFile
{
    public string Name { get; set; } = string.Empty;

    public string DisplayKey { get; set; } = string.Empty;

    public string GroupKey { get; set; } = string.Empty;

    public GraphNodePropertyValueKind ValueKind { get; set; } = GraphNodePropertyValueKind.String;

    public GraphNodePropertyEditorKind Editor { get; set; }

    public object? DefaultValue { get; set; }

    public bool IsReadOnly { get; set; }

    public string? Unit { get; set; }

    public decimal? Minimum { get; set; }

    public decimal? Maximum { get; set; }

    public decimal? Step { get; set; }

    public List<string> Options { get; set; } = [];

    public List<GraphNodePropertyOptionFile> ChoiceOptions { get; set; } = [];
}

public sealed class GraphNodePropertyOptionFile
{
    public string Value { get; set; } = string.Empty;

    public string DisplayKey { get; set; } = string.Empty;
}

public sealed class GraphCompositeDefinitionFile
{
    public List<GraphCompositeChildDefinitionFile> Children { get; set; } = [];

    public List<GraphCompositeConnectionDefinitionFile> Connections { get; set; } = [];

    public List<GraphCompositePortDefinitionFile> Ports { get; set; } = [];
}

public sealed class GraphCompositeChildDefinitionFile
{
    public string Id { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public double Rotation { get; set; }

    public GraphSize? Size { get; set; }
}

public sealed class GraphCompositeConnectionDefinitionFile
{
    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;
}

public sealed class GraphCompositePortDefinitionFile
{
    public string Id { get; set; } = string.Empty;

    public string MapTo { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;

    public PortKind Kind { get; set; } = PortKind.InputOutput;
}

public static class GraphNodeCatalogXmlLoader
{
    public static GraphNodeCatalog Load(string path) =>
        Load(path, CancellationToken.None);

    public static Task<GraphNodeCatalog> LoadAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(path, cancellationToken), cancellationToken);

    private static GraphNodeCatalog Load(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(path);
        var root = LoadRoot(fullPath, "ModelLibrary");
        var format = ReadOptionalString(root, nameof(GraphModelLibraryFile.Format));
        return format switch
        {
            GraphNodeCatalogFormats.ModelLibraryV1 => LoadModelLibrary(
                fullPath,
                ReadModelLibrary(root),
                cancellationToken),
            _ => throw new InvalidDataException($"Unsupported model catalog format '{format}'.")
        };
    }

    private static GraphNodeCatalog LoadModelLibrary(
        string fullPath,
        GraphModelLibraryFile file,
        CancellationToken cancellationToken)
    {
        var rootDirectory = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory;
        var definitions = new List<GraphNodeDefinition>();
        foreach (var group in file.Groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var symbolPath in group.Symbols)
            {
                cancellationToken.ThrowIfCancellationRequested();
                definitions.Add(LoadSymbol(
                    rootDirectory,
                    group.Id,
                    ResolveLocalization(group.NameKey),
                    group.IconText,
                    group.Icon,
                    symbolPath,
                    cancellationToken));
            }
        }

        var catalog = new GraphNodeCatalog(definitions);
        ValidateResources(definitions, cancellationToken);
        return catalog;
    }

    private static GraphNodeDefinition LoadSymbol(
        string rootDirectory,
        string groupId,
        string groupName,
        string groupIconText,
        string? groupIcon,
        string symbolPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbolPath);
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveLibraryPath(rootDirectory, symbolPath);
        var symbol = ReadSymbol(LoadRoot(fullPath, "Symbol"));
        if (!string.Equals(symbol.Format, GraphNodeCatalogFormats.SymbolV1, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported symbol format '{symbol.Format}' in '{symbolPath}'.");
        }

        var nodeType = string.IsNullOrWhiteSpace(symbol.NodeType)
            ? symbol.Id
            : symbol.NodeType;
        var templateKey = string.IsNullOrWhiteSpace(symbol.TemplateKey)
            ? nodeType
            : symbol.TemplateKey;
        var appearance = ResolveAppearance(symbol.Appearance, symbol.Resource);
        var properties = CreateDefaultProperties(
            symbol.Properties,
            symbol.PropertyDefinitions,
            symbol.ThreeD);

        var definition = new GraphNodeDefinition(
            groupId,
            groupName,
            ResolveLocalization(symbol.NameKey),
            ResolveOptionalLocalization(symbol.DescriptionKey),
            nodeType,
            templateKey,
            symbol.IconText)
        {
            Id = symbol.Id,
            Kind = symbol.Kind,
            Resource = symbol.Resource,
            GroupIconText = groupIconText,
            GroupIcon = groupIcon,
            Icon = symbol.Icon ?? symbol.Resource,
            DefaultSize = symbol.DefaultSize,
            MinimumSize = symbol.MinimumSize,
            DefaultAppearance = appearance,
            BackgroundImageMappings = symbol.BackgroundImageMappings
                .Select(mapping => new GraphNodeBackgroundImageMapping(
                    mapping.Property,
                    mapping.Value,
                    mapping.Resource.Replace('\\', '/').Trim()))
                .ToArray(),
            DefaultPorts = symbol.Ports
                .Select(CreatePort)
                .ToArray(),
            DefaultProperties = properties,
            PropertyDefinitions = symbol.PropertyDefinitions
                .Select(CreateProperty)
                .ToArray(),
            Composite = CreateComposite(symbol.Composite),
            ResourceDirectory = rootDirectory
        };
        return definition with
        {
            IntrinsicImageSize = ResolveIntrinsicImageSize(definition)
        };
    }

    private static XElement LoadRoot(string path, string expectedName)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 100 * 1024 * 1024,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };
        using var reader = XmlReader.Create(path, settings);
        var root = XDocument.Load(reader, LoadOptions.SetLineInfo).Root
            ?? throw new InvalidDataException($"The XML file '{path}' is empty.");
        if (!string.Equals(root.Name.LocalName, expectedName, StringComparison.Ordinal) ||
            root.Name.Namespace != XNamespace.None)
        {
            throw new InvalidDataException(
                $"Expected XML root '{expectedName}' in '{path}', but found '{root.Name}'.");
        }

        return root;
    }

    private static GraphModelLibraryFile ReadModelLibrary(XElement root) =>
        new()
        {
            Format = ReadString(root, nameof(GraphModelLibraryFile.Format)),
            Groups = ReadChildren(root, "Groups", "Group")
                .Select(group => new GraphModelLibraryGroupFile
                {
                    Id = ReadString(group, nameof(GraphModelLibraryGroupFile.Id)),
                    NameKey = ReadString(group, nameof(GraphModelLibraryGroupFile.NameKey)),
                    IconText = ReadOptionalString(group, nameof(GraphModelLibraryGroupFile.IconText)) ?? string.Empty,
                    Icon = ReadOptionalString(group, nameof(GraphModelLibraryGroupFile.Icon)),
                    Symbols = ReadChildren(group, "Symbols", "Symbol")
                        .Select(symbol => ReadString(symbol, "Path"))
                        .ToList()
                })
                .ToList()
        };

    private static GraphSymbolDefinitionFile ReadSymbol(XElement root)
    {
        var defaultSize = root.Element("DefaultSize");
        return new GraphSymbolDefinitionFile
        {
            Format = ReadOptionalString(root, nameof(GraphSymbolDefinitionFile.Format))
                ?? GraphNodeCatalogFormats.SymbolV1,
            Id = ReadString(root, nameof(GraphSymbolDefinitionFile.Id)),
            NameKey = ReadString(root, nameof(GraphSymbolDefinitionFile.NameKey)),
            DescriptionKey = ReadOptionalString(root, nameof(GraphSymbolDefinitionFile.DescriptionKey)) ?? string.Empty,
            NodeType = ReadOptionalString(root, nameof(GraphSymbolDefinitionFile.NodeType)) ?? string.Empty,
            TemplateKey = ReadOptionalString(root, nameof(GraphSymbolDefinitionFile.TemplateKey)) ?? string.Empty,
            IconText = ReadOptionalString(root, nameof(GraphSymbolDefinitionFile.IconText)) ?? string.Empty,
            Kind = ReadEnum(root, nameof(GraphSymbolDefinitionFile.Kind), GraphNodeDefinitionKind.Basic),
            Resource = ReadOptionalString(root, nameof(GraphSymbolDefinitionFile.Resource)),
            Icon = ReadOptionalString(root, nameof(GraphSymbolDefinitionFile.Icon)),
            DefaultSize = defaultSize is null
                ? new GraphSize(160, 88)
                : ReadSize(defaultSize),
            MinimumSize = root.Element("MinimumSize") is { } minimumSize
                ? ReadSize(minimumSize)
                : null,
            Appearance = ReadAppearance(root.Element("Appearance")),
            BackgroundImageMappings = ReadChildren(root, "BackgroundImageMappings", "Mapping")
                .Select(ReadBackgroundImageMapping)
                .ToList(),
            ThreeD = ReadValue(root.Element("ThreeD")),
            Ports = ReadChildren(root, "Ports", "Port").Select(ReadPort).ToList(),
            PropertyDefinitions = ReadChildren(root, "PropertyDefinitions", "Property")
                .Select(ReadPropertyDefinition)
                .ToList(),
            Properties = ReadProperties(root.Element("Properties")),
            Composite = root.Element("Composite") is { } composite
                ? ReadComposite(composite)
                : null
        };
    }

    private static GraphNodeBackgroundImageMappingFile ReadBackgroundImageMapping(XElement element) =>
        new()
        {
            Property = ReadString(element, "Property"),
            Value = ReadString(element, "Value"),
            Resource = ReadString(element, "Resource")
        };

    private static GraphNodeAppearance ReadAppearance(XElement? element)
    {
        var defaults = new GraphNodeAppearance
        {
            Stretch = GraphImageStretch.Uniform
        };
        if (element is null)
        {
            return defaults;
        }

        return new GraphNodeAppearance
        {
            VisualMode = ReadEnum(element, nameof(GraphNodeAppearance.VisualMode), defaults.VisualMode),
            BackgroundImage = ReadOptionalString(element, nameof(GraphNodeAppearance.BackgroundImage)),
            Stretch = ReadEnum(element, nameof(GraphNodeAppearance.Stretch), defaults.Stretch),
            Opacity = ReadDouble(element, nameof(GraphNodeAppearance.Opacity), defaults.Opacity),
            TextAppearance = ReadTextAppearance(element.Element("TextAppearance")),
            PortLabelVisibility = ReadNullableEnum<GraphLabelVisibility>(
                element,
                nameof(GraphNodeAppearance.PortLabelVisibility)),
            NameVisibility = ReadNullableEnum<GraphLabelVisibility>(
                element,
                nameof(GraphNodeAppearance.NameVisibility)),
            DescriptionVisibility = ReadNullableEnum<GraphLabelVisibility>(
                element,
                nameof(GraphNodeAppearance.DescriptionVisibility))
        };
    }

    private static GraphTextAppearance ReadTextAppearance(XElement? element)
    {
        var defaults = new GraphTextAppearance();
        if (element is null)
        {
            return defaults;
        }

        return new GraphTextAppearance
        {
            Foreground = ReadOptionalString(element, nameof(GraphTextAppearance.Foreground)),
            FontSize = ReadDouble(element, nameof(GraphTextAppearance.FontSize), defaults.FontSize),
            FontWeight = ReadEnum(element, nameof(GraphTextAppearance.FontWeight), defaults.FontWeight)
        };
    }

    private static GraphPortDefinitionFile ReadPort(XElement element) =>
        new()
        {
            Id = ReadString(element, nameof(GraphPortDefinitionFile.Id)),
            NameKey = ReadOptionalString(element, nameof(GraphPortDefinitionFile.NameKey)) ?? string.Empty,
            Kind = ReadEnum(element, nameof(GraphPortDefinitionFile.Kind), PortKind.Input),
            IsVisible = ReadBoolean(element, nameof(GraphPortDefinitionFile.IsVisible), true),
            X = ReadNullableDouble(element, nameof(GraphPortDefinitionFile.X)),
            Y = ReadNullableDouble(element, nameof(GraphPortDefinitionFile.Y)),
            DataType = ReadOptionalString(element, nameof(GraphPortDefinitionFile.DataType)),
            Capacity = ReadCapacity(element.Element("Capacity")),
            IncomingDirection = element.Element("IncomingDirection") is { } incoming
                ? ReadPoint(incoming)
                : null,
            OutgoingDirection = element.Element("OutgoingDirection") is { } outgoing
                ? ReadPoint(outgoing)
                : null,
            State = ReadEnum(element, nameof(GraphPortDefinitionFile.State), PortState.Normal),
            Properties = ReadProperties(element.Element("Properties"))
        };

    private static PortCapacity ReadCapacity(XElement? element)
    {
        var defaults = new PortCapacity();
        return element is null
            ? defaults
            : new PortCapacity
            {
                MaxIncoming = ReadConnectionLimit(element, nameof(PortCapacity.MaxIncoming), defaults.MaxIncoming),
                MaxOutgoing = ReadConnectionLimit(element, nameof(PortCapacity.MaxOutgoing), defaults.MaxOutgoing),
                MaxTotal = ReadConnectionLimit(element, nameof(PortCapacity.MaxTotal), defaults.MaxTotal)
            };
    }

    private static GraphNodePropertyDefinitionFile ReadPropertyDefinition(XElement element)
    {
        var choiceOptions = ReadChildren(element, "Options", "Option")
            .Select(ReadPropertyOption)
            .ToList();
        return new GraphNodePropertyDefinitionFile
        {
            Name = ReadString(element, nameof(GraphNodePropertyDefinitionFile.Name)),
            DisplayKey = ReadString(element, nameof(GraphNodePropertyDefinitionFile.DisplayKey)),
            GroupKey = ReadOptionalString(element, nameof(GraphNodePropertyDefinitionFile.GroupKey)) ?? string.Empty,
            ValueKind = ReadEnum(
                element,
                nameof(GraphNodePropertyDefinitionFile.ValueKind),
                GraphNodePropertyValueKind.String),
            Editor = ReadEnum(
                element,
                nameof(GraphNodePropertyDefinitionFile.Editor),
                GraphNodePropertyEditorKind.Auto),
            DefaultValue = ReadValue(element.Element("DefaultValue")),
            IsReadOnly = ReadBoolean(element, nameof(GraphNodePropertyDefinitionFile.IsReadOnly), false),
            Unit = ReadOptionalString(element, nameof(GraphNodePropertyDefinitionFile.Unit)),
            Minimum = ReadNullableDecimal(element, nameof(GraphNodePropertyDefinitionFile.Minimum)),
            Maximum = ReadNullableDecimal(element, nameof(GraphNodePropertyDefinitionFile.Maximum)),
            Step = ReadNullableDecimal(element, nameof(GraphNodePropertyDefinitionFile.Step)),
            Options = choiceOptions.Select(option => option.Value).ToList(),
            ChoiceOptions = choiceOptions
        };
    }

    private static GraphNodePropertyOptionFile ReadPropertyOption(XElement element)
    {
        var value = ReadOptionalString(element, nameof(GraphNodePropertyOptionFile.Value)) ??
            element.Value.Trim();
        return new GraphNodePropertyOptionFile
        {
            Value = value,
            DisplayKey = ReadString(element, nameof(GraphNodePropertyOptionFile.DisplayKey))
        };
    }

    private static GraphCompositeDefinitionFile ReadComposite(XElement element) =>
        new()
        {
            Children = ReadChildren(element, "Children", "Child")
                .Select(child => new GraphCompositeChildDefinitionFile
                {
                    Id = ReadString(child, nameof(GraphCompositeChildDefinitionFile.Id)),
                    Symbol = ReadString(child, nameof(GraphCompositeChildDefinitionFile.Symbol)),
                    X = ReadDouble(child, nameof(GraphCompositeChildDefinitionFile.X), 0),
                    Y = ReadDouble(child, nameof(GraphCompositeChildDefinitionFile.Y), 0),
                    Rotation = ReadDouble(child, nameof(GraphCompositeChildDefinitionFile.Rotation), 0),
                    Size = child.Element("Size") is { } size ? ReadSize(size) : null
                })
                .ToList(),
            Connections = ReadChildren(element, "Connections", "Connection")
                .Select(connection => new GraphCompositeConnectionDefinitionFile
                {
                    From = ReadString(connection, nameof(GraphCompositeConnectionDefinitionFile.From)),
                    To = ReadString(connection, nameof(GraphCompositeConnectionDefinitionFile.To))
                })
                .ToList(),
            Ports = ReadChildren(element, "Ports", "Port")
                .Select(port => new GraphCompositePortDefinitionFile
                {
                    Id = ReadString(port, nameof(GraphCompositePortDefinitionFile.Id)),
                    MapTo = ReadString(port, nameof(GraphCompositePortDefinitionFile.MapTo)),
                    NameKey = ReadOptionalString(port, nameof(GraphCompositePortDefinitionFile.NameKey)) ?? string.Empty,
                    Kind = ReadEnum(port, nameof(GraphCompositePortDefinitionFile.Kind), PortKind.InputOutput)
                })
                .ToList()
        };

    private static Dictionary<string, object?> ReadProperties(XElement? element) =>
        element?.Elements("Property").ToDictionary(
            property => ReadString(property, "Name"),
            ReadValue,
            StringComparer.Ordinal) ?? new Dictionary<string, object?>(StringComparer.Ordinal);

    private static object? ReadValue(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var type = ReadOptionalString(element, "Type") ?? "String";
        return type.ToUpperInvariant() switch
        {
            "NULL" => null,
            "STRING" => element.Value,
            "BOOLEAN" => ParseBoolean(element.Value, element),
            "INTEGER" => ParseInteger(element.Value, element),
            "NUMBER" => ParseDouble(element.Value, element),
            "DECIMAL" => ParseDecimal(element.Value, element),
            "OBJECT" => element.Elements("Property").ToDictionary(
                property => ReadString(property, "Name"),
                ReadValue,
                StringComparer.Ordinal),
            "ARRAY" => element.Elements("Item").Select(ReadValue).ToList(),
            _ => throw InvalidValue(element, "Type", type)
        };
    }

    private static GraphSize ReadSize(XElement element) =>
        new(
            ReadDouble(element, nameof(GraphSize.Width), 0),
            ReadDouble(element, nameof(GraphSize.Height), 0));

    private static GraphPoint ReadPoint(XElement element) =>
        new(
            ReadDouble(element, nameof(GraphPoint.X), 0),
            ReadDouble(element, nameof(GraphPoint.Y), 0));

    private static IEnumerable<XElement> ReadChildren(
        XElement parent,
        string containerName,
        string itemName) =>
        parent.Element(containerName)?.Elements(itemName) ?? [];

    private static string ReadString(XElement element, string name) =>
        ReadOptionalString(element, name)
        ?? throw InvalidValue(element, name, null);

    private static string? ReadOptionalString(XElement element, string name) =>
        element.Attribute(name)?.Value;

    private static double ReadDouble(XElement element, string name, double fallback) =>
        ReadOptionalString(element, name) is { } value
            ? ParseDouble(value, element)
            : fallback;

    private static double? ReadNullableDouble(XElement element, string name) =>
        ReadOptionalString(element, name) is { } value
            ? ParseDouble(value, element)
            : null;

    private static decimal? ReadNullableDecimal(XElement element, string name) =>
        ReadOptionalString(element, name) is { } value
            ? ParseDecimal(value, element)
            : null;

    private static bool ReadBoolean(XElement element, string name, bool fallback) =>
        ReadOptionalString(element, name) is { } value
            ? ParseBoolean(value, element)
            : fallback;

    private static TEnum ReadEnum<TEnum>(XElement element, string name, TEnum fallback)
        where TEnum : struct, Enum =>
        ReadOptionalString(element, name) is { } value
            ? ParseEnum<TEnum>(value, element, name)
            : fallback;

    private static TEnum? ReadNullableEnum<TEnum>(XElement element, string name)
        where TEnum : struct, Enum =>
        ReadOptionalString(element, name) is { } value
            ? ParseEnum<TEnum>(value, element, name)
            : null;

    private static TEnum ParseEnum<TEnum>(string value, XElement element, string name)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var result) && Enum.IsDefined(result)
            ? result
            : throw InvalidValue(element, name, value);

    private static bool ParseBoolean(string value, XElement element) =>
        bool.TryParse(value, out var result)
            ? result
            : throw InvalidValue(element, "value", value);

    private static object ParseInteger(string value, XElement element)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw InvalidValue(element, "value", value);
        }

        return result is >= int.MinValue and <= int.MaxValue ? (int)result : result;
    }

    private static double ParseDouble(string value, XElement element) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) &&
        double.IsFinite(result)
            ? result
            : throw InvalidValue(element, "value", value);

    private static decimal ParseDecimal(string value, XElement element) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw InvalidValue(element, "value", value);

    private static int? ReadConnectionLimit(XElement element, string name, int? fallback)
    {
        if (ReadOptionalString(element, name) is not { } value)
        {
            return fallback;
        }

        if (string.Equals(value, "Unlimited", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw InvalidValue(element, name, value);
    }

    private static InvalidDataException InvalidValue(XElement element, string name, string? value)
    {
        var line = (element as IXmlLineInfo).HasLineInfo()
            ? $" at line {((IXmlLineInfo)element).LineNumber}"
            : string.Empty;
        return new InvalidDataException(
            $"Invalid XML {name} '{value ?? "<missing>"}' on element '{element.Name.LocalName}'{line}.");
    }

    private static void ValidateResources(
        IEnumerable<GraphNodeDefinition> definitions,
        CancellationToken cancellationToken)
    {
        foreach (var definition in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResource(definition, definition.Icon, "icon");
            ValidateResource(definition, definition.GroupIcon, "group icon");
            ValidateResource(definition, definition.Resource, "resource");
            ValidateResource(definition, GetThreeDResource(definition), "3D resource");
            ValidateResource(
                definition,
                definition.DefaultAppearance.BackgroundImage,
                "background image");
            foreach (var mapping in definition.BackgroundImageMappings)
            {
                ValidateResource(definition, mapping.Resource, "background image mapping");
            }
        }
    }

    private static void ValidateResource(
        GraphNodeDefinition definition,
        string? relativePath,
        string resourceName)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        string? path;
        try
        {
            path = definition.ResolveResourcePath(relativePath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidDataException(
                $"Model '{definition.Id}' uses invalid {resourceName} path '{relativePath}'.",
                exception);
        }

        if (path is null)
        {
            throw new InvalidDataException(
                $"Model '{definition.Id}' uses invalid {resourceName} path '{relativePath}'.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Model '{definition.Id}' references missing {resourceName} '{relativePath}'.",
                path);
        }
    }

    private static PortModel CreatePort(GraphPortDefinitionFile port)
    {
        ArgumentNullException.ThrowIfNull(port);
        return new PortModel
        {
            Id = port.Id,
            Name = ResolveOptionalLocalization(port.NameKey),
            Kind = port.Kind,
            IsVisible = port.IsVisible,
            Anchor = port.X is { } x && port.Y is { } y
                ? new GraphPoint(x, y)
                : null,
            IncomingDirection = port.IncomingDirection,
            OutgoingDirection = port.OutgoingDirection,
            DataType = port.DataType,
            Capacity = (port.Capacity ?? new PortCapacity()).Normalize(),
            State = port.State,
            Properties = GraphNodeDefinitionValue.CloneDictionary(port.Properties)
        };
    }

    private static GraphNodeAppearance ResolveAppearance(
        GraphNodeAppearance? appearance,
        string? resource)
    {
        var normalized = (appearance ?? new GraphNodeAppearance()).Normalize();
        var hasResource = !string.IsNullOrWhiteSpace(resource);
        var backgroundImage = normalized.BackgroundImage is null && hasResource
            ? resource!.Trim().Replace('\\', '/')
            : normalized.BackgroundImage;
        var visualMode = normalized.VisualMode == GraphNodeVisualMode.Auto
            ? hasResource
                ? GraphNodeVisualMode.Symbol
                : GraphNodeVisualMode.Card
            : normalized.VisualMode;
        return normalized with
        {
            VisualMode = visualMode,
            BackgroundImage = backgroundImage,
            Stretch = normalized.Stretch
        };
    }

    private static GraphSize? ResolveIntrinsicImageSize(GraphNodeDefinition definition)
    {
        if (definition.DefaultAppearance.VisualMode != GraphNodeVisualMode.Symbol ||
            definition.ResolveResourcePath(definition.DefaultAppearance.BackgroundImage) is not { } path)
        {
            return null;
        }

        try
        {
            return string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase)
                ? ReadSvgSize(path)
                : ReadBitmapSize(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or XmlException or ArgumentException)
        {
            return null;
        }
    }

    private static GraphSize? ReadBitmapSize(string path)
    {
        using var codec = SKCodec.Create(path);
        return codec is not null && codec.Info.Width > 0 && codec.Info.Height > 0
            ? new GraphSize(codec.Info.Width, codec.Info.Height)
            : null;
    }

    private static GraphSize? ReadSvgSize(string path)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(path, settings);
        var root = XDocument.Load(reader).Root;
        if (root is null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (root.Attribute("viewBox")?.Value is { } viewBox)
        {
            var values = viewBox.Replace(',', ' ')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 4 &&
                TryParsePositive(values[2], out var width) &&
                TryParsePositive(values[3], out var height))
            {
                return new GraphSize(width, height);
            }
        }

        return TryParseSvgLength(root.Attribute("width")?.Value, out var fallbackWidth) &&
               TryParseSvgLength(root.Attribute("height")?.Value, out var fallbackHeight)
            ? new GraphSize(fallbackWidth, fallbackHeight)
            : null;
    }

    private static bool TryParseSvgLength(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value) || value.Contains('%'))
        {
            return false;
        }

        var text = value.Trim();
        var length = 0;
        while (length < text.Length &&
               (char.IsDigit(text[length]) || text[length] is '+' or '-' or '.' or 'e' or 'E'))
        {
            length++;
        }

        return length > 0 && TryParsePositive(text[..length], out result);
    }

    private static bool TryParsePositive(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
        double.IsFinite(result) &&
        result > 0;

    private static IReadOnlyDictionary<string, object?> CreateDefaultProperties(
        IReadOnlyDictionary<string, object?> properties,
        IEnumerable<GraphNodePropertyDefinitionFile> propertyDefinitions,
        object? threeD)
    {
        var result = GraphNodeDefinitionValue.CloneDictionary(properties);
        if (HasValue(threeD))
        {
            result[GraphNodePropertyKeys.ThreeD] = GraphNodeDefinitionValue.MaterializeValue(threeD);
        }

        foreach (var definition in propertyDefinitions)
        {
            if (!string.IsNullOrWhiteSpace(definition.Name) &&
                definition.DefaultValue is not null &&
                !result.ContainsKey(definition.Name))
            {
                result[definition.Name] = definition.DefaultValue;
            }
        }

        return result;
    }

    private static bool HasValue(object? value) => value is not null;

    private static string? GetThreeDResource(GraphNodeDefinition definition)
    {
        if (!definition.DefaultProperties.TryGetValue(GraphNodePropertyKeys.ThreeD, out var threeD))
        {
            return null;
        }

        return TryGetStringProperty(threeD, "Resource", out var resource)
            ? resource
            : null;
    }

    private static bool TryGetStringProperty(
        object? source,
        string propertyName,
        out string? value)
    {
        if (source is IReadOnlyDictionary<string, object?> dictionary)
        {
            foreach (var pair in dictionary)
            {
                if (string.Equals(pair.Key, propertyName, StringComparison.OrdinalIgnoreCase) &&
                    pair.Value is string text &&
                    !string.IsNullOrWhiteSpace(text))
                {
                    value = text;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    private static GraphNodePropertyDefinition CreateProperty(GraphNodePropertyDefinitionFile property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return new GraphNodePropertyDefinition(
            property.Name,
            ResolveLocalization(property.DisplayKey),
            property.ValueKind)
        {
            GroupName = ResolveOptionalLocalization(property.GroupKey),
            DefaultValue = property.DefaultValue,
            IsReadOnly = property.IsReadOnly,
            Unit = property.Unit,
            Minimum = property.Minimum,
            Maximum = property.Maximum,
            Step = property.Step,
            Options = property.Options.ToArray(),
            ChoiceOptions = property.ChoiceOptions
                .Select(option => new GraphNodePropertyOption(
                    option.Value,
                    ResolveLocalization(option.DisplayKey)))
                .ToArray(),
            Editor = property.Editor
        };
    }

    private static GraphCompositeDefinition? CreateComposite(GraphCompositeDefinitionFile? composite)
    {
        if (composite is null)
        {
            return null;
        }

        return new GraphCompositeDefinition
        {
            Children = composite.Children
                .Select(child => new GraphCompositeChildDefinition(
                    child.Id,
                    child.Symbol,
                    new GraphPoint(child.X, child.Y))
                {
                    Rotation = child.Rotation,
                    Size = child.Size
                })
                .ToArray(),
            Connections = composite.Connections
                .Select(connection => new GraphCompositeConnectionDefinition(
                    connection.From,
                    connection.To))
                .ToArray(),
            Ports = composite.Ports
                .Select(port => new GraphCompositePortDefinition(
                    port.Id,
                    port.MapTo)
                {
                    Name = ResolveOptionalLocalization(port.NameKey),
                    Kind = port.Kind
                })
            .ToArray()
        };
    }

    private static string ResolveLocalization(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return GraphLocalization.Get(key);
    }

    private static string ResolveOptionalLocalization(string? key) =>
        string.IsNullOrWhiteSpace(key) ? string.Empty : GraphLocalization.Get(key);

    private static string ResolveLibraryPath(string rootDirectory, string relativePath)
    {
        var root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Model library path '{relativePath}' points outside the library directory.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Model library references missing symbol '{relativePath}'.",
                path);
        }

        return path;
    }
}
