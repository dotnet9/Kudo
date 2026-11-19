using System.Xml.Serialization;
using Kudo;

namespace Kudo.Core.Serialization;

[XmlRoot("Task")]
public sealed class GraphTaskXmlDocument
{
    [XmlAttribute]
    public string Format { get; set; } = string.Empty;

    [XmlAttribute]
    public string SchemaVersion { get; set; } = string.Empty;

    [XmlAttribute]
    public string Id { get; set; } = string.Empty;

    [XmlAttribute]
    public string Title { get; set; } = "Untitled";

    [XmlAttribute]
    public string CreatedBy { get; set; } = string.Empty;

    [XmlAttribute]
    public DateTime CreatedAt { get; set; }

    [XmlAttribute]
    public string Description { get; set; } = string.Empty;

    [XmlElement]
    public GraphTaskCanvasSettingsXml CanvasSettings { get; set; } = new();

    [XmlArray("Nodes")]
    [XmlArrayItem("Node")]
    public List<GraphTaskNodeXml> Nodes { get; set; } = [];

    [XmlArray("Groups")]
    [XmlArrayItem("Group")]
    public List<GraphTaskGroupXml> Groups { get; set; } = [];

    [XmlArray("Edges")]
    [XmlArrayItem("Edge")]
    public List<GraphTaskEdgeXml> Edges { get; set; } = [];

    [XmlArray("Annotations")]
    [XmlArrayItem("Annotation")]
    public List<GraphTaskAnnotationXml> Annotations { get; set; } = [];

    [XmlElement]
    public GraphTaskPropertiesXml Properties { get; set; } = new();
}

public sealed class GraphTaskCanvasSettingsXml
{
    [XmlAttribute]
    public bool IsMinimapVisible { get; set; } = true;

    [XmlAttribute]
    public bool IsLevelOfDetailEnabled { get; set; } = true;

    [XmlAttribute]
    public bool ShowPorts { get; set; } = true;

    [XmlAttribute]
    public double LodPortsZoomThreshold { get; set; } = 0.4;

    [XmlAttribute]
    public double LodLabelsZoomThreshold { get; set; } = 0.3;

    [XmlAttribute]
    public double LodSimplifiedZoomThreshold { get; set; } = 0.4;

    [XmlAttribute]
    public GraphLabelVisibility PortLabelVisibility { get; set; } = GraphLabelVisibility.Auto;

    [XmlAttribute]
    public GraphLabelVisibility SymbolNameVisibility { get; set; } = GraphLabelVisibility.Never;

    [XmlAttribute]
    public GraphLabelVisibility SymbolDescriptionVisibility { get; set; } = GraphLabelVisibility.Never;

    [XmlElement]
    public GraphTaskGridSettingsXml Grid { get; set; } = new();
}

public sealed class GraphTaskGridSettingsXml
{
    [XmlAttribute]
    public bool IsVisible { get; set; } = true;

    [XmlAttribute]
    public bool IsSnapEnabled { get; set; }

    [XmlAttribute]
    public bool IsGuidesEnabled { get; set; } = true;

    [XmlAttribute]
    public double Spacing { get; set; } = 20;

    [XmlAttribute]
    public int MajorLineInterval { get; set; } = 4;

    [XmlAttribute]
    public double SnapTolerance { get; set; } = 8;

    [XmlAttribute]
    public double GuideTolerance { get; set; } = 8;
}

public sealed class GraphTaskNodeXml
{
    [XmlAttribute]
    public string Id { get; set; } = string.Empty;

    [XmlAttribute]
    public string Type { get; set; } = "Node";

    [XmlAttribute]
    public string DefinitionId { get; set; } = string.Empty;

    [XmlAttribute]
    public string Number { get; set; } = string.Empty;

    [XmlAttribute]
    public string? Name { get; set; }

    [XmlAttribute]
    public string? Description { get; set; }

    [XmlAttribute]
    public bool IsLocked { get; set; }

    [XmlAttribute]
    public double Rotation { get; set; }

    [XmlElement]
    public GraphTaskPointXml Position { get; set; } = new();

    [XmlElement]
    public GraphTaskSizeXml Size { get; set; } = new() { Width = 160, Height = 88 };

    [XmlElement]
    public GraphTaskNodeAppearanceXml Appearance { get; set; } = new();

    [XmlElement]
    public GraphTaskExtensionStateXml? ExtensionState { get; set; }

    [XmlArray("Ports")]
    [XmlArrayItem("Port")]
    public List<GraphTaskPortXml> Ports { get; set; } = [];

    [XmlElement]
    public GraphTaskPropertiesXml Properties { get; set; } = new();
}

public sealed class GraphTaskNodeAppearanceXml
{
    [XmlAttribute]
    public GraphNodeVisualMode VisualMode { get; set; } = GraphNodeVisualMode.Auto;

    [XmlAttribute]
    public string? BackgroundImage { get; set; }

    [XmlAttribute]
    public GraphImageStretch Stretch { get; set; } = GraphImageStretch.UniformToFill;

    [XmlAttribute]
    public double Opacity { get; set; } = 1;

    [XmlAttribute]
    public GraphLabelVisibility PortLabelVisibility { get; set; }

    [XmlIgnore]
    public bool PortLabelVisibilitySpecified { get; set; }

    [XmlAttribute]
    public GraphLabelVisibility NameVisibility { get; set; }

    [XmlIgnore]
    public bool NameVisibilitySpecified { get; set; }

    [XmlAttribute]
    public GraphLabelVisibility DescriptionVisibility { get; set; }

    [XmlIgnore]
    public bool DescriptionVisibilitySpecified { get; set; }

    [XmlElement]
    public GraphTaskTextAppearanceXml Text { get; set; } = new();
}

public sealed class GraphTaskTextAppearanceXml
{
    [XmlAttribute]
    public string? Foreground { get; set; }

    [XmlAttribute]
    public double FontSize { get; set; } = 14;

    [XmlAttribute]
    public GraphFontWeight FontWeight { get; set; } = GraphFontWeight.Normal;
}

public sealed class GraphTaskExtensionStateXml
{
    [XmlAttribute]
    public string SchemaId { get; set; } = string.Empty;

    [XmlAttribute]
    public int SchemaVersion { get; set; }

    [XmlElement]
    public GraphTaskValueXml? Payload { get; set; }
}

public sealed class GraphTaskPortXml
{
    [XmlAttribute]
    public string Id { get; set; } = string.Empty;

    [XmlAttribute]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute]
    public PortKind Kind { get; set; } = PortKind.Input;

    [XmlAttribute]
    public bool IsVisible { get; set; } = true;

    [XmlAttribute]
    public double X { get; set; }

    [XmlIgnore]
    public bool XSpecified { get; set; }

    [XmlAttribute]
    public double Y { get; set; }

    [XmlIgnore]
    public bool YSpecified { get; set; }

    [XmlAttribute]
    public string? DataType { get; set; }

    [XmlAttribute]
    public PortState State { get; set; } = PortState.Normal;

    [XmlElement]
    public GraphTaskPortCapacityXml? Capacity { get; set; }

    [XmlElement]
    public GraphTaskPointXml? IncomingDirection { get; set; }

    [XmlElement]
    public GraphTaskPointXml? OutgoingDirection { get; set; }

    [XmlElement]
    public GraphTaskPropertiesXml Properties { get; set; } = new();
}

public sealed class GraphTaskPortCapacityXml
{
    [XmlAttribute]
    public int MaxIncoming { get; set; }

    [XmlIgnore]
    public bool MaxIncomingSpecified { get; set; }

    [XmlAttribute]
    public int MaxOutgoing { get; set; }

    [XmlIgnore]
    public bool MaxOutgoingSpecified { get; set; }

    [XmlAttribute]
    public int MaxTotal { get; set; }

    [XmlIgnore]
    public bool MaxTotalSpecified { get; set; }
}

public sealed class GraphTaskGroupXml
{
    [XmlAttribute]
    public string Id { get; set; } = string.Empty;

    [XmlAttribute]
    public string? Title { get; set; }

    [XmlAttribute]
    public double Padding { get; set; } = 24;

    [XmlAttribute]
    public bool IsCollapsed { get; set; }

    [XmlArray("NodeIds")]
    [XmlArrayItem("NodeId")]
    public List<string> NodeIds { get; set; } = [];

    [XmlElement]
    public GraphTaskPropertiesXml Properties { get; set; } = new();
}

public sealed class GraphTaskEdgeXml
{
    [XmlAttribute]
    public string Id { get; set; } = string.Empty;

    [XmlAttribute]
    public GraphRelationKind RelationKind { get; set; } = GraphRelationKind.Connection;

    [XmlAttribute]
    public EdgeRoutingStyle RoutingStyle { get; set; } = EdgeRoutingStyle.Bezier;

    [XmlAttribute]
    public GraphEdgeStylePreset StylePreset { get; set; } = GraphEdgeStylePreset.Default;

    [XmlAttribute]
    public EdgeMarker SourceMarker { get; set; } = EdgeMarker.None;

    [XmlAttribute]
    public EdgeMarker TargetMarker { get; set; } = EdgeMarker.None;

    [XmlAttribute]
    public string? Label { get; set; }

    [XmlAttribute]
    public EdgeState State { get; set; } = EdgeState.Normal;

    [XmlElement]
    public GraphTaskPortReferenceXml Source { get; set; } = new();

    [XmlElement]
    public GraphTaskPortReferenceXml Target { get; set; } = new();

    [XmlElement]
    public GraphTaskEdgeAppearanceXml Appearance { get; set; } = new();

    [XmlArray("Waypoints")]
    [XmlArrayItem("Point")]
    public List<GraphTaskPointXml> Waypoints { get; set; } = [];

    [XmlElement]
    public GraphTaskPropertiesXml Properties { get; set; } = new();
}

public sealed class GraphTaskPortReferenceXml
{
    [XmlAttribute]
    public string NodeId { get; set; } = string.Empty;

    [XmlAttribute]
    public string PortId { get; set; } = string.Empty;
}

public sealed class GraphTaskEdgeAppearanceXml
{
    [XmlAttribute]
    public string? StrokeColor { get; set; }

    [XmlAttribute]
    public double StrokeWidth { get; set; }

    [XmlIgnore]
    public bool StrokeWidthSpecified { get; set; }

    [XmlAttribute]
    public GraphEdgeDashStyle DashStyle { get; set; }

    [XmlArray("CustomDashPattern")]
    [XmlArrayItem("Value")]
    public List<double> CustomDashPattern { get; set; } = [];

    [XmlAttribute]
    public double Opacity { get; set; } = 1;

    [XmlAttribute]
    public GraphEdgeAnimationKind Animation { get; set; }

    [XmlAttribute]
    public double AnimationSpeed { get; set; } = 1;

    [XmlAttribute]
    public GraphEdgeAnimationDirection AnimationDirection { get; set; }

    [XmlAttribute]
    public double GlowIntensity { get; set; }

    [XmlAttribute]
    public string? GlowColor { get; set; }
}

public sealed class GraphTaskAnnotationXml
{
    [XmlAttribute]
    public string Id { get; set; } = string.Empty;

    [XmlAttribute]
    public string? Text { get; set; }

    [XmlElement]
    public GraphTaskPointXml Position { get; set; } = new();

    [XmlElement]
    public GraphTaskSizeXml Size { get; set; } = new() { Width = 220, Height = 120 };

    [XmlElement]
    public GraphTaskPropertiesXml Properties { get; set; } = new();
}

public sealed class GraphTaskPointXml
{
    [XmlAttribute]
    public double X { get; set; }

    [XmlAttribute]
    public double Y { get; set; }
}

public sealed class GraphTaskSizeXml
{
    [XmlAttribute]
    public double Width { get; set; }

    [XmlAttribute]
    public double Height { get; set; }
}

public sealed class GraphTaskPropertiesXml
{
    [XmlElement("Property")]
    public List<GraphTaskValueXml> Items { get; set; } = [];
}

public sealed class GraphTaskValueXml
{
    [XmlAttribute]
    public string? Name { get; set; }

    [XmlAttribute]
    public GraphTaskValueKind Type { get; set; } = GraphTaskValueKind.String;

    [XmlText]
    public string? Text { get; set; }

    [XmlElement("Property")]
    public List<GraphTaskValueXml> Properties { get; set; } = [];

    [XmlElement("Item")]
    public List<GraphTaskValueXml> Items { get; set; } = [];
}

public enum GraphTaskValueKind
{
    Null,
    String,
    Boolean,
    Integer,
    Long,
    Double,
    Decimal,
    Object,
    Array
}
