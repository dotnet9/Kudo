using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using Kudo;

namespace Kudo.Core.Serialization;

public sealed record GraphSerializationOptions(bool WriteIndented = true);

public sealed class GraphSerializationException : Exception
{
    public GraphSerializationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class GraphTaskXmlSerializer
{
    private const long MaximumDocumentCharacters = 100L * 1024 * 1024;
    private static readonly XmlSerializer Serializer = CreateSerializer();
    private readonly GraphSerializationOptions _options;

    public GraphTaskXmlSerializer(GraphSerializationOptions? options = null) =>
        _options = options ?? new GraphSerializationOptions();

    public string Serialize(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var stream = new MemoryStream();
        Serialize(stream, ToXml(document));
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public GraphDocument Deserialize(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return Deserialize(stream);
    }

    public async Task SaveAsync(
        Stream stream,
        GraphDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        await using var buffer = new MemoryStream();
        Serialize(buffer, ToXml(document));
        buffer.Position = 0;
        await buffer.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GraphDocument> LoadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        return Deserialize(buffer);
    }

    private void Serialize(Stream stream, GraphTaskXmlDocument payload)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = _options.WriteIndented,
            OmitXmlDeclaration = false,
            CloseOutput = false
        };
        var namespaces = new XmlSerializerNamespaces();
        namespaces.Add(string.Empty, string.Empty);
        using var writer = XmlWriter.Create(stream, settings);
        Serializer.Serialize(writer, payload, namespaces);
        writer.Flush();
    }

    private static GraphDocument Deserialize(Stream stream)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumDocumentCharacters,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            CloseInput = false
        };

        try
        {
            using var reader = XmlReader.Create(stream, settings);
            var payload = Serializer.Deserialize(reader) as GraphTaskXmlDocument
                ?? throw new GraphSerializationException(GraphTaskLocalization.Get(GraphTaskLocalization.EmptyPayload));
            return FromXml(payload);
        }
        catch (GraphSerializationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or XmlException)
        {
            throw new GraphSerializationException(
                GraphTaskLocalization.Get(GraphTaskLocalization.InvalidXml),
                exception);
        }
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskXmlDocument))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskCanvasSettingsXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskGridSettingsXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskNodeXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskNodeAppearanceXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskTextAppearanceXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskExtensionStateXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskPortXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskPortCapacityXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskGroupXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskEdgeXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskPortReferenceXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskEdgeAppearanceXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskAnnotationXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskPointXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskSizeXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskPropertiesXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GraphTaskValueXml))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<GraphTaskNodeXml>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<GraphTaskPortXml>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<GraphTaskGroupXml>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<GraphTaskEdgeXml>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<GraphTaskAnnotationXml>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<GraphTaskPointXml>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<GraphTaskValueXml>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<double>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<string>))]
    private static XmlSerializer CreateSerializer() => new(typeof(GraphTaskXmlDocument));

    private static GraphTaskXmlDocument ToXml(GraphDocument document)
    {
        var normalized = Normalize(document);
        return new GraphTaskXmlDocument
        {
            Format = normalized.Format,
            SchemaVersion = normalized.SchemaVersion,
            Id = normalized.Id,
            Title = normalized.Title,
            CreatedBy = normalized.CreatedBy,
            CreatedAt = normalized.CreatedAt,
            Description = normalized.Description,
            CanvasSettings = ToXml(normalized.CanvasSettings),
            Nodes = normalized.Nodes.Select(ToXml).ToList(),
            Groups = normalized.Groups.Select(ToXml).ToList(),
            Edges = normalized.Edges.Select(ToXml).ToList(),
            Annotations = normalized.Annotations.Select(ToXml).ToList(),
            Properties = ToXml(normalized.Properties)
        };
    }

    private static GraphTaskCanvasSettingsXml ToXml(GraphCanvasSettings settings) => new()
    {
        IsMinimapVisible = settings.IsMinimapVisible,
        IsLevelOfDetailEnabled = settings.IsLevelOfDetailEnabled,
        ShowPorts = settings.ShowPorts,
        LodPortsZoomThreshold = settings.LodPortsZoomThreshold,
        LodLabelsZoomThreshold = settings.LodLabelsZoomThreshold,
        LodSimplifiedZoomThreshold = settings.LodSimplifiedZoomThreshold,
        PortLabelVisibility = settings.PortLabelVisibility,
        SymbolNameVisibility = settings.SymbolNameVisibility,
        SymbolDescriptionVisibility = settings.SymbolDescriptionVisibility,
        Grid = new GraphTaskGridSettingsXml
        {
            IsVisible = settings.Grid.IsVisible,
            IsSnapEnabled = settings.Grid.IsSnapEnabled,
            IsGuidesEnabled = settings.Grid.IsGuidesEnabled,
            Spacing = settings.Grid.Spacing,
            MajorLineInterval = settings.Grid.MajorLineInterval,
            SnapTolerance = settings.Grid.SnapTolerance,
            GuideTolerance = settings.Grid.GuideTolerance
        }
    };

    private static GraphTaskNodeXml ToXml(NodeModel node) => new()
    {
        Id = node.Id,
        Type = node.Type,
        DefinitionId = node.DefinitionId,
        Number = node.Number,
        Name = node.Name,
        Description = node.Description,
        IsLocked = node.IsLocked,
        Position = ToXml(node.Position),
        Size = ToXml(node.Size),
        Rotation = node.Rotation,
        Appearance = ToXml(node.Appearance),
        ExtensionState = node.ExtensionState is null ? null : ToXml(node.ExtensionState),
        Ports = node.Ports.Select(ToXml).ToList(),
        Properties = ToXml(node.Properties)
    };

    private static GraphTaskNodeAppearanceXml ToXml(GraphNodeAppearance appearance) => new()
    {
        VisualMode = appearance.VisualMode,
        BackgroundImage = appearance.BackgroundImage,
        Stretch = appearance.Stretch,
        Opacity = appearance.Opacity,
        PortLabelVisibility = appearance.PortLabelVisibility ?? default,
        PortLabelVisibilitySpecified = appearance.PortLabelVisibility.HasValue,
        NameVisibility = appearance.NameVisibility ?? default,
        NameVisibilitySpecified = appearance.NameVisibility.HasValue,
        DescriptionVisibility = appearance.DescriptionVisibility ?? default,
        DescriptionVisibilitySpecified = appearance.DescriptionVisibility.HasValue,
        Text = new GraphTaskTextAppearanceXml
        {
            Foreground = appearance.TextAppearance.Foreground,
            FontSize = appearance.TextAppearance.FontSize,
            FontWeight = appearance.TextAppearance.FontWeight
        }
    };

    private static GraphTaskExtensionStateXml ToXml(GraphExtensionState state) => new()
    {
        SchemaId = state.SchemaId,
        SchemaVersion = state.SchemaVersion,
        Payload = ToXmlValue(null, NormalizeJsonElement(state.Payload))
    };

    private static GraphTaskPortXml ToXml(PortModel port) => new()
    {
        Id = port.Id,
        Name = port.Name,
        Kind = port.Kind,
        IsVisible = port.IsVisible,
        X = port.Anchor?.X ?? default,
        XSpecified = port.Anchor.HasValue,
        Y = port.Anchor?.Y ?? default,
        YSpecified = port.Anchor.HasValue,
        IncomingDirection = port.IncomingDirection is { } incoming ? ToXml(incoming) : null,
        OutgoingDirection = port.OutgoingDirection is { } outgoing ? ToXml(outgoing) : null,
        DataType = port.DataType,
        Capacity = new GraphTaskPortCapacityXml
        {
            MaxIncoming = port.Capacity.MaxIncoming ?? default,
            MaxIncomingSpecified = port.Capacity.MaxIncoming.HasValue,
            MaxOutgoing = port.Capacity.MaxOutgoing ?? default,
            MaxOutgoingSpecified = port.Capacity.MaxOutgoing.HasValue,
            MaxTotal = port.Capacity.MaxTotal ?? default,
            MaxTotalSpecified = port.Capacity.MaxTotal.HasValue
        },
        State = port.State,
        Properties = ToXml(port.Properties)
    };

    private static GraphTaskGroupXml ToXml(GroupModel group) => new()
    {
        Id = group.Id,
        Title = group.Title,
        NodeIds = group.NodeIds.ToList(),
        Padding = group.Padding,
        IsCollapsed = group.IsCollapsed,
        Properties = ToXml(group.Properties)
    };

    private static GraphTaskEdgeXml ToXml(EdgeModel edge) => new()
    {
        Id = edge.Id,
        Source = ToXml(edge.Source),
        Target = ToXml(edge.Target),
        RelationKind = edge.RelationKind,
        RoutingStyle = edge.RoutingStyle,
        StylePreset = edge.StylePreset,
        SourceMarker = edge.SourceMarker,
        TargetMarker = edge.TargetMarker,
        Label = edge.Label,
        State = edge.State,
        Appearance = ToXml(edge.Appearance),
        Waypoints = edge.Waypoints.Select(ToXml).ToList(),
        Properties = ToXml(edge.Properties)
    };

    private static GraphTaskPortReferenceXml ToXml(PortReference reference) => new()
    {
        NodeId = reference.NodeId,
        PortId = reference.PortId
    };

    private static GraphTaskEdgeAppearanceXml ToXml(GraphEdgeAppearance appearance) => new()
    {
        StrokeColor = appearance.StrokeColor,
        StrokeWidth = appearance.StrokeWidth ?? default,
        StrokeWidthSpecified = appearance.StrokeWidth.HasValue,
        DashStyle = appearance.DashStyle,
        CustomDashPattern = appearance.CustomDashPattern.ToList(),
        Opacity = appearance.Opacity,
        Animation = appearance.Animation,
        AnimationSpeed = appearance.AnimationSpeed,
        AnimationDirection = appearance.AnimationDirection,
        GlowIntensity = appearance.GlowIntensity,
        GlowColor = appearance.GlowColor
    };

    private static GraphTaskAnnotationXml ToXml(AnnotationModel annotation) => new()
    {
        Id = annotation.Id,
        Text = annotation.Text,
        Position = ToXml(annotation.Position),
        Size = ToXml(annotation.Size),
        Properties = ToXml(annotation.Properties)
    };

    private static GraphTaskPointXml ToXml(GraphPoint point) => new() { X = point.X, Y = point.Y };

    private static GraphTaskSizeXml ToXml(GraphSize size) => new()
    {
        Width = size.Width,
        Height = size.Height
    };

    private static GraphTaskPropertiesXml ToXml(Dictionary<string, object?> properties) => new()
    {
        Items = properties.Select(property => ToXmlValue(property.Key, property.Value)).ToList()
    };

    private static GraphTaskValueXml ToXmlValue(string? name, object? value)
    {
        value = NormalizePropertyValue(value);
        return value switch
        {
            null => new GraphTaskValueXml { Name = name, Type = GraphTaskValueKind.Null },
            string text => Scalar(name, GraphTaskValueKind.String, text),
            bool boolean => Scalar(name, GraphTaskValueKind.Boolean, XmlConvert.ToString(boolean)),
            byte number => Scalar(name, GraphTaskValueKind.Integer, XmlConvert.ToString(number)),
            sbyte number => Scalar(name, GraphTaskValueKind.Integer, XmlConvert.ToString(number)),
            short number => Scalar(name, GraphTaskValueKind.Integer, XmlConvert.ToString(number)),
            ushort number => Scalar(name, GraphTaskValueKind.Integer, XmlConvert.ToString(number)),
            int number => Scalar(name, GraphTaskValueKind.Integer, XmlConvert.ToString(number)),
            uint number when number <= int.MaxValue =>
                Scalar(name, GraphTaskValueKind.Integer, XmlConvert.ToString(number)),
            uint number => Scalar(name, GraphTaskValueKind.Long, XmlConvert.ToString(number)),
            long number => Scalar(name, GraphTaskValueKind.Long, XmlConvert.ToString(number)),
            ulong number when number <= long.MaxValue =>
                Scalar(name, GraphTaskValueKind.Long, XmlConvert.ToString(number)),
            ulong number => Scalar(name, GraphTaskValueKind.Decimal, number.ToString(CultureInfo.InvariantCulture)),
            float number => Scalar(name, GraphTaskValueKind.Double, XmlConvert.ToString(number)),
            double number => Scalar(name, GraphTaskValueKind.Double, XmlConvert.ToString(number)),
            decimal number => Scalar(name, GraphTaskValueKind.Decimal, XmlConvert.ToString(number)),
            Dictionary<string, object?> dictionary => new GraphTaskValueXml
            {
                Name = name,
                Type = GraphTaskValueKind.Object,
                Properties = dictionary.Select(property => ToXmlValue(property.Key, property.Value)).ToList()
            },
            IEnumerable<KeyValuePair<string, object?>> pairs => new GraphTaskValueXml
            {
                Name = name,
                Type = GraphTaskValueKind.Object,
                Properties = pairs.Select(property => ToXmlValue(property.Key, property.Value)).ToList()
            },
            IEnumerable values => new GraphTaskValueXml
            {
                Name = name,
                Type = GraphTaskValueKind.Array,
                Items = values.Cast<object?>().Select(value => ToXmlValue(null, value)).ToList()
            },
            _ => throw new GraphSerializationException(GraphTaskLocalization.Format(
                GraphTaskLocalization.UnsupportedPropertyType,
                value.GetType().FullName ?? value.GetType().Name))
        };
    }

    private static GraphTaskValueXml Scalar(string? name, GraphTaskValueKind type, string? text) => new()
    {
        Name = name,
        Type = type,
        Text = text
    };

    private static GraphDocument FromXml(GraphTaskXmlDocument payload)
    {
        ValidateEnvelope(payload.Format, payload.SchemaVersion);
        var document = new GraphDocument
        {
            Format = payload.Format,
            SchemaVersion = payload.SchemaVersion,
            Id = payload.Id,
            Title = payload.Title,
            CreatedBy = payload.CreatedBy,
            CreatedAt = payload.CreatedAt,
            Description = payload.Description,
            CanvasSettings = FromXml(payload.CanvasSettings),
            Nodes = (payload.Nodes ?? []).Select(FromXml).ToList(),
            Groups = (payload.Groups ?? []).Select(FromXml).ToList(),
            Edges = (payload.Edges ?? []).Select(FromXml).ToList(),
            Annotations = (payload.Annotations ?? []).Select(FromXml).ToList(),
            Properties = FromXml(payload.Properties)
        };
        return Normalize(document);
    }

    private static GraphCanvasSettings FromXml(GraphTaskCanvasSettingsXml? settings)
    {
        settings ??= new GraphTaskCanvasSettingsXml();
        var grid = settings.Grid ?? new GraphTaskGridSettingsXml();
        return new GraphCanvasSettings
        {
            IsMinimapVisible = settings.IsMinimapVisible,
            IsLevelOfDetailEnabled = settings.IsLevelOfDetailEnabled,
            ShowPorts = settings.ShowPorts,
            LodPortsZoomThreshold = settings.LodPortsZoomThreshold,
            LodLabelsZoomThreshold = settings.LodLabelsZoomThreshold,
            LodSimplifiedZoomThreshold = settings.LodSimplifiedZoomThreshold,
            PortLabelVisibility = settings.PortLabelVisibility,
            SymbolNameVisibility = settings.SymbolNameVisibility,
            SymbolDescriptionVisibility = settings.SymbolDescriptionVisibility,
            Grid = new GraphGridSettings
            {
                IsVisible = grid.IsVisible,
                IsSnapEnabled = grid.IsSnapEnabled,
                IsGuidesEnabled = grid.IsGuidesEnabled,
                Spacing = grid.Spacing,
                MajorLineInterval = grid.MajorLineInterval,
                SnapTolerance = grid.SnapTolerance,
                GuideTolerance = grid.GuideTolerance
            }
        };
    }

    private static NodeModel FromXml(GraphTaskNodeXml node) => new()
    {
        Id = node.Id,
        Type = node.Type,
        DefinitionId = node.DefinitionId,
        Number = node.Number,
        Name = node.Name,
        Description = node.Description,
        IsLocked = node.IsLocked,
        Position = FromXml(node.Position),
        Size = FromXml(node.Size),
        Rotation = node.Rotation,
        Appearance = FromXml(node.Appearance),
        ExtensionState = node.ExtensionState is null ? null : FromXml(node.ExtensionState, node.Id),
        Ports = (node.Ports ?? []).Select(FromXml).ToList(),
        Properties = FromXml(node.Properties)
    };

    private static GraphNodeAppearance FromXml(GraphTaskNodeAppearanceXml? appearance)
    {
        appearance ??= new GraphTaskNodeAppearanceXml();
        var text = appearance.Text ?? new GraphTaskTextAppearanceXml();
        return new GraphNodeAppearance
        {
            VisualMode = appearance.VisualMode,
            BackgroundImage = appearance.BackgroundImage,
            Stretch = appearance.Stretch,
            Opacity = appearance.Opacity,
            PortLabelVisibility = appearance.PortLabelVisibilitySpecified
                ? appearance.PortLabelVisibility
                : null,
            NameVisibility = appearance.NameVisibilitySpecified ? appearance.NameVisibility : null,
            DescriptionVisibility = appearance.DescriptionVisibilitySpecified
                ? appearance.DescriptionVisibility
                : null,
            TextAppearance = new GraphTextAppearance
            {
                Foreground = text.Foreground,
                FontSize = text.FontSize,
                FontWeight = text.FontWeight
            }
        };
    }

    private static GraphExtensionState FromXml(GraphTaskExtensionStateXml state, string nodeId)
    {
        if (state.Payload is null)
        {
            throw new GraphSerializationException(GraphTaskLocalization.Format(
                GraphTaskLocalization.ExtensionPayloadRequired,
                nodeId));
        }

        return new GraphExtensionState
        {
            SchemaId = state.SchemaId,
            SchemaVersion = state.SchemaVersion,
            Payload = ToJsonElement(FromXmlValue(state.Payload))
        };
    }

    private static PortModel FromXml(GraphTaskPortXml port) => new()
    {
        Id = port.Id,
        Name = port.Name,
        Kind = port.Kind,
        IsVisible = port.IsVisible,
        Anchor = port.XSpecified && port.YSpecified ? new GraphPoint(port.X, port.Y) : null,
        IncomingDirection = port.IncomingDirection is null ? null : FromXml(port.IncomingDirection),
        OutgoingDirection = port.OutgoingDirection is null ? null : FromXml(port.OutgoingDirection),
        DataType = port.DataType,
        Capacity = FromXml(port.Capacity),
        State = port.State,
        Properties = FromXml(port.Properties)
    };

    private static GroupModel FromXml(GraphTaskGroupXml group) => new()
    {
        Id = group.Id,
        Title = group.Title,
        NodeIds = group.NodeIds ?? [],
        Padding = group.Padding,
        IsCollapsed = group.IsCollapsed,
        Properties = FromXml(group.Properties)
    };

    private static PortCapacity FromXml(GraphTaskPortCapacityXml? capacity) => capacity is null
        ? new PortCapacity()
        : new PortCapacity
        {
            MaxIncoming = capacity.MaxIncomingSpecified ? capacity.MaxIncoming : null,
            MaxOutgoing = capacity.MaxOutgoingSpecified ? capacity.MaxOutgoing : null,
            MaxTotal = capacity.MaxTotalSpecified ? capacity.MaxTotal : null
        };

    private static EdgeModel FromXml(GraphTaskEdgeXml edge) => new()
    {
        Id = edge.Id,
        Source = FromXml(edge.Source),
        Target = FromXml(edge.Target),
        RelationKind = edge.RelationKind,
        RoutingStyle = edge.RoutingStyle,
        StylePreset = edge.StylePreset,
        SourceMarker = edge.SourceMarker,
        TargetMarker = edge.TargetMarker,
        Label = edge.Label,
        State = edge.State,
        Appearance = FromXml(edge.Appearance),
        Waypoints = (edge.Waypoints ?? []).Select(FromXml).ToList(),
        Properties = FromXml(edge.Properties)
    };

    private static PortReference FromXml(GraphTaskPortReferenceXml? reference) => reference is null
        ? PortReference.Empty
        : new PortReference(reference.NodeId, reference.PortId);

    private static GraphEdgeAppearance FromXml(GraphTaskEdgeAppearanceXml? appearance)
    {
        appearance ??= new GraphTaskEdgeAppearanceXml();
        return new GraphEdgeAppearance
        {
            StrokeColor = appearance.StrokeColor,
            StrokeWidth = appearance.StrokeWidthSpecified ? appearance.StrokeWidth : null,
            DashStyle = appearance.DashStyle,
            CustomDashPattern = appearance.CustomDashPattern ?? [],
            Opacity = appearance.Opacity,
            Animation = appearance.Animation,
            AnimationSpeed = appearance.AnimationSpeed,
            AnimationDirection = appearance.AnimationDirection,
            GlowIntensity = appearance.GlowIntensity,
            GlowColor = appearance.GlowColor
        };
    }

    private static AnnotationModel FromXml(GraphTaskAnnotationXml annotation) => new()
    {
        Id = annotation.Id,
        Text = annotation.Text,
        Position = FromXml(annotation.Position),
        Size = FromXml(annotation.Size),
        Properties = FromXml(annotation.Properties)
    };

    private static GraphPoint FromXml(GraphTaskPointXml? point) =>
        point is null ? GraphPoint.Zero : new GraphPoint(point.X, point.Y);

    private static GraphSize FromXml(GraphTaskSizeXml? size) =>
        size is null ? GraphSize.Empty : new GraphSize(size.Width, size.Height);

    private static Dictionary<string, object?> FromXml(GraphTaskPropertiesXml? properties)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in properties?.Items ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                throw new GraphSerializationException(
                    GraphTaskLocalization.Get(GraphTaskLocalization.PropertyNameRequired));
            }

            result[item.Name] = FromXmlValue(item);
        }

        return result;
    }

    private static object? FromXmlValue(GraphTaskValueXml value)
    {
        try
        {
            return value.Type switch
            {
                GraphTaskValueKind.Null => null,
                GraphTaskValueKind.String => value.Text ?? string.Empty,
                GraphTaskValueKind.Boolean => XmlConvert.ToBoolean(value.Text ?? string.Empty),
                GraphTaskValueKind.Integer => XmlConvert.ToInt32(value.Text ?? string.Empty),
                GraphTaskValueKind.Long => XmlConvert.ToInt64(value.Text ?? string.Empty),
                GraphTaskValueKind.Double => XmlConvert.ToDouble(value.Text ?? string.Empty),
                GraphTaskValueKind.Decimal => XmlConvert.ToDecimal(value.Text ?? string.Empty),
                GraphTaskValueKind.Object => FromXmlObject(value.Properties),
                GraphTaskValueKind.Array => (value.Items ?? []).Select(FromXmlValue).ToList(),
                _ => throw new FormatException()
            };
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            throw new GraphSerializationException(
                GraphTaskLocalization.Format(
                    GraphTaskLocalization.InvalidPropertyValue,
                    value.Name ?? string.Empty,
                    value.Type),
                exception);
        }
    }

    private static Dictionary<string, object?> FromXmlObject(List<GraphTaskValueXml>? properties)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in properties ?? [])
        {
            if (string.IsNullOrWhiteSpace(property.Name))
            {
                throw new GraphSerializationException(
                    GraphTaskLocalization.Get(GraphTaskLocalization.PropertyNameRequired));
            }

            result[property.Name] = FromXmlValue(property);
        }

        return result;
    }

    private static void ValidateEnvelope(string? format, string? schemaVersion)
    {
        if (!string.Equals(format, GraphDocumentFormat.CurrentTaskFormat, StringComparison.Ordinal))
        {
            throw new GraphSerializationException(GraphTaskLocalization.Format(
                GraphTaskLocalization.UnsupportedFormat,
                format));
        }

        if (!string.Equals(schemaVersion, GraphDocumentFormat.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new GraphSerializationException(GraphTaskLocalization.Format(
                GraphTaskLocalization.UnsupportedSchemaVersion,
                schemaVersion));
        }
    }

    private static GraphDocument Normalize(GraphDocument document)
    {
        ValidateEnvelope(document.Format, document.SchemaVersion);
        document.Nodes ??= [];
        document.Groups ??= [];
        document.Edges ??= [];
        document.Annotations ??= [];
        document.Properties ??= [];
        document.CanvasSettings = (document.CanvasSettings ?? GraphCanvasSettings.Default).Normalize();
        NormalizeProperties(document.Properties);
        foreach (var node in document.Nodes)
        {
            node.Appearance = (node.Appearance ?? new GraphNodeAppearance()).Normalize();
            node.ExtensionState = NormalizeExtensionState(node.ExtensionState, node.Id);
            node.Ports ??= [];
            node.Properties ??= [];
            NormalizeProperties(node.Properties);
            foreach (var port in node.Ports)
            {
                port.Capacity = (port.Capacity ?? new PortCapacity()).Normalize();
                port.IncomingDirection = PortGeometry.NormalizeDirection(port.IncomingDirection);
                port.OutgoingDirection = PortGeometry.NormalizeDirection(port.OutgoingDirection);
                port.Properties ??= [];
                NormalizeProperties(port.Properties);
            }
        }

        foreach (var group in document.Groups)
        {
            group.NodeIds ??= [];
            group.Properties ??= [];
            NormalizeProperties(group.Properties);
        }

        foreach (var edge in document.Edges)
        {
            edge.Appearance = (edge.Appearance ?? new GraphEdgeAppearance()).Normalize();
            edge.Waypoints ??= [];
            edge.Properties ??= [];
            NormalizeProperties(edge.Properties);
        }

        foreach (var annotation in document.Annotations)
        {
            annotation.Properties ??= [];
            NormalizeProperties(annotation.Properties);
        }

        ValidateDocument(document);
        return document;
    }

    private static GraphExtensionState? NormalizeExtensionState(GraphExtensionState? state, string nodeId)
    {
        if (state is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(state.SchemaId))
        {
            throw new GraphSerializationException(GraphTaskLocalization.Format(
                GraphTaskLocalization.ExtensionSchemaIdRequired,
                nodeId));
        }

        if (state.SchemaVersion < 1)
        {
            throw new GraphSerializationException(GraphTaskLocalization.Format(
                GraphTaskLocalization.ExtensionSchemaVersionRequired,
                nodeId));
        }

        if (state.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new GraphSerializationException(GraphTaskLocalization.Format(
                GraphTaskLocalization.ExtensionPayloadRequired,
                nodeId));
        }

        return state with { SchemaId = state.SchemaId.Trim(), Payload = state.Payload.Clone() };
    }

    private static void NormalizeProperties(Dictionary<string, object?> properties)
    {
        foreach (var key in properties.Keys.ToArray())
        {
            properties[key] = NormalizePropertyValue(properties[key]);
        }
    }

    private static object? NormalizePropertyValue(object? value) => value switch
    {
        JsonElement element => NormalizeJsonElement(element),
        Dictionary<string, object?> dictionary => NormalizePropertyDictionary(dictionary),
        IEnumerable<KeyValuePair<string, object?>> pairs => NormalizePropertyDictionary(
            new Dictionary<string, object?>(pairs, StringComparer.Ordinal)),
        _ => value
    };

    private static Dictionary<string, object?> NormalizePropertyDictionary(
        Dictionary<string, object?> dictionary)
    {
        NormalizeProperties(dictionary);
        return dictionary;
    }

    private static object? NormalizeJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => NormalizeJsonNumber(element),
        JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => NormalizeJsonElement(property.Value),
            StringComparer.Ordinal),
        _ => element.ToString()
    };

    private static object NormalizeJsonNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var integer))
        {
            return integer is >= int.MinValue and <= int.MaxValue ? (int)integer : integer;
        }

        return element.TryGetDecimal(out var decimalValue) ? decimalValue : element.GetDouble();
    }

    private static void ValidateDocument(GraphDocument document)
    {
        foreach (var node in document.Nodes)
        {
            var appearance = node.Appearance.Normalize();
            if (appearance.BackgroundImage is { } backgroundImage && !IsTaskAssetPath(backgroundImage))
            {
                throw new GraphSerializationException(GraphTaskLocalization.Format(
                    GraphTaskLocalization.InvalidNodeAssetPath,
                    node.Id,
                    backgroundImage));
            }

            if (node.Properties.TryGetValue(
                    GraphNodePropertyKeys.BackgroundImageMappings,
                    out var mappingValue) &&
                mappingValue is IReadOnlyDictionary<string, object?> mappings)
            {
                foreach (var mappedAsset in mappings.Values.OfType<string>())
                {
                    if (!IsTaskAssetPath(mappedAsset))
                    {
                        throw new GraphSerializationException(GraphTaskLocalization.Format(
                            GraphTaskLocalization.InvalidNodeAssetPath,
                            node.Id,
                            mappedAsset));
                    }
                }
            }
        }
    }

    private static bool IsTaskAssetPath(string path)
    {
        if (Path.IsPathRooted(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = path.Split('/');
        return segments.Length > 1 && segments.All(segment =>
            !string.IsNullOrWhiteSpace(segment) && segment is not "." and not "..");
    }

    private static JsonElement ToJsonElement(object? value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteJsonValue(writer, value);
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case Dictionary<string, object?> dictionary:
                writer.WriteStartObject();
                foreach (var property in dictionary)
                {
                    writer.WritePropertyName(property.Key);
                    WriteJsonValue(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case IEnumerable values:
                writer.WriteStartArray();
                foreach (var item in values)
                {
                    WriteJsonValue(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                throw new GraphSerializationException(GraphTaskLocalization.Format(
                    GraphTaskLocalization.UnsupportedPropertyType,
                    value.GetType().FullName ?? value.GetType().Name));
        }
    }
}
