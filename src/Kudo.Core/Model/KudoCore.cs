using System.ComponentModel;
using System.Globalization;

namespace Kudo;

public static class GraphDocumentFormat
{
    public const string CurrentTaskFormat = "Kudo.Task/1";

    public const string CurrentSchemaVersion = "1.0";
}

public static class GraphNodePropertyKeys
{
    public const string ThreeD = "Kudo.ThreeD";

    /// <summary>
    /// 存储为模型背景图映射导入的任务相对资源。
    /// 属性值为对象，键是模型资源路径，值是任务资源路径。
    /// </summary>
    public const string BackgroundImageMappings = "Kudo.BackgroundImageMappings";
}

public static class GraphId
{
    public static string NewId() => Guid.NewGuid().ToString("N");
}

[TypeConverter(typeof(GraphPointConverter))]
public readonly record struct GraphPoint(double X, double Y)
{
    public static GraphPoint Zero { get; } = new(0, 0);

    public static GraphPoint Parse(string value)
    {
        if (TryParse(value, out var result))
        {
            return result;
        }

        throw new FormatException(
            $"Cannot parse '{value}' as GraphPoint. Use two finite numbers separated by a comma, for example '12,24'.");
    }

    public static bool TryParse(string? value, out GraphPoint result)
    {
        result = Zero;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !double.IsFinite(x) ||
            !double.IsFinite(y))
        {
            return false;
        }

        result = new GraphPoint(x, y);
        return true;
    }
}

public sealed class GraphPointConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value) =>
        value is string text ? GraphPoint.Parse(text) : base.ConvertFrom(context, culture, value);
}

public readonly record struct GraphSize(double Width, double Height)
{
    public static GraphSize Empty { get; } = new(0, 0);
}

public readonly record struct GraphRect(double X, double Y, double Width, double Height);

public sealed class GraphDocument
{
    public string Format { get; set; } = GraphDocumentFormat.CurrentTaskFormat;

    public string Id { get; set; } = GraphId.NewId();

    public string SchemaVersion { get; set; } = GraphDocumentFormat.CurrentSchemaVersion;

    public string Title { get; set; } = "Untitled";

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string Description { get; set; } = string.Empty;

    public GraphCanvasSettings CanvasSettings { get; set; } = GraphCanvasSettings.Default;

    public List<NodeModel> Nodes { get; set; } = [];

    public List<GroupModel> Groups { get; set; } = [];

    public List<EdgeModel> Edges { get; set; } = [];

    public List<AnnotationModel> Annotations { get; set; } = [];

    public Dictionary<string, object?> Properties { get; set; } = [];

    public NodeModel AddNode(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.DefinitionId = string.IsNullOrWhiteSpace(node.DefinitionId)
            ? node.Type
            : node.DefinitionId;
        node.Number = string.IsNullOrWhiteSpace(node.Number)
            ? GraphNodeNumberAllocator.Next(this)
            : node.Number;
        node.Name ??= node.Type;
        Nodes.Add(node);
        return node;
    }

    public void AddNodes(IEnumerable<NodeModel> nodes)
    {
        foreach (var node in MaterializeRange(nodes, nameof(nodes)))
        {
            AddNode(node);
        }
    }

    public EdgeModel AddEdge(EdgeModel edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        Edges.Add(edge);
        return edge;
    }

    public void AddEdges(IEnumerable<EdgeModel> edges) =>
        Edges.AddRange(MaterializeRange(edges, nameof(edges)));

    public GroupModel AddGroup(GroupModel group)
    {
        ArgumentNullException.ThrowIfNull(group);
        Groups.Add(group);
        return group;
    }

    public void AddGroups(IEnumerable<GroupModel> groups) =>
        Groups.AddRange(MaterializeRange(groups, nameof(groups)));

    public AnnotationModel AddAnnotation(AnnotationModel annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        Annotations.Add(annotation);
        return annotation;
    }

    public void AddAnnotations(IEnumerable<AnnotationModel> annotations) =>
        Annotations.AddRange(MaterializeRange(annotations, nameof(annotations)));

    public NodeModel? FindNode(string nodeId)
    {
        foreach (var node in Nodes)
        {
            if (node.Id == nodeId)
            {
                return node;
            }
        }

        return null;
    }

    public EdgeModel? FindEdge(string edgeId) => Edges.FirstOrDefault(edge => edge.Id == edgeId);

    public GroupModel? FindGroup(string groupId) => Groups.FirstOrDefault(group => group.Id == groupId);

    public AnnotationModel? FindAnnotation(string annotationId) =>
        Annotations.FirstOrDefault(annotation => annotation.Id == annotationId);

    public GroupModel? FindGroupContainingNode(string nodeId) => Groups.FirstOrDefault(group =>
        group.NodeIds?.Contains(nodeId, StringComparer.Ordinal) == true);

    public bool RemoveEdge(string edgeId)
    {
        var edge = Edges.FirstOrDefault(candidate => candidate.Id == edgeId);
        return edge is not null && Edges.Remove(edge);
    }

    public bool RemoveGroup(string groupId)
    {
        var group = FindGroup(groupId);
        return group is not null && Groups.Remove(group);
    }

    public bool RemoveAnnotation(string annotationId)
    {
        var annotation = FindAnnotation(annotationId);
        return annotation is not null && Annotations.Remove(annotation);
    }

    public bool RemoveNode(string nodeId)
    {
        var node = FindNode(nodeId);
        if (node is null)
        {
            return false;
        }

        Edges.RemoveAll(edge => edge.Source.NodeId == nodeId || edge.Target.NodeId == nodeId);
        foreach (var group in Groups)
        {
            group.NodeIds?.RemoveAll(candidate => string.Equals(candidate, nodeId, StringComparison.Ordinal));
        }

        Groups.RemoveAll(group => group.NodeIds is null || group.NodeIds.Count == 0);
        return Nodes.Remove(node);
    }

    public PortModel? FindPort(PortReference reference)
    {
        if (reference.IsEmpty)
        {
            return null;
        }

        var node = FindNode(reference.NodeId);
        if (node is null)
        {
            return null;
        }

        foreach (var port in node.Ports)
        {
            if (port.Id == reference.PortId)
            {
                return port;
            }
        }

        return null;
    }

    public ConnectionValidationResult ValidateConnection(
        PortReference source,
        PortReference target,
        GraphRelationKind relationKind = GraphRelationKind.Connection,
        IConnectionValidationRule? additionalRule = null,
        string? ignoredEdgeId = null,
        IGraphValidationTextProvider? textProvider = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var request = new ConnectionValidationRequest(
            this,
            source,
            target,
            relationKind,
            ignoredEdgeId,
            textProvider);
        var validation = DefaultConnectionValidationRule.Instance.Validate(in request);
        return validation.IsValid && additionalRule is not null
            ? additionalRule.Validate(in request)
            : validation;
    }

    public bool TryConnect(
        PortReference source,
        PortReference target,
        out EdgeModel? edge,
        out ConnectionValidationResult validation,
        GraphRelationKind relationKind = GraphRelationKind.Connection,
        IConnectionValidationRule? additionalRule = null,
        IGraphValidationTextProvider? textProvider = null)
    {
        validation = ValidateConnection(
            source,
            target,
            relationKind,
            additionalRule,
            textProvider: textProvider);
        if (!validation.IsValid)
        {
            edge = null;
            return false;
        }

        edge = AddEdge(new EdgeModel
        {
            Source = source,
            Target = target,
            RelationKind = relationKind
        });
        return true;
    }

    private static List<T> MaterializeRange<T>(IEnumerable<T> items, string paramName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(items, paramName);
        var materialized = items.ToList();
        if (materialized.Any(item => item is null))
        {
            throw new ArgumentException("The collection cannot contain null elements.", paramName);
        }

        return materialized;
    }
}

public sealed class NodeModel
{
    public string Id { get; set; } = GraphId.NewId();

    public string Type { get; set; } = "Node";

    public string DefinitionId { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Description { get; set; }

    public GraphPoint Position { get; set; } = GraphPoint.Zero;

    public GraphSize Size { get; set; } = new(160, 88);

    /// <summary>
    /// 整个节点相对于画布的顺时针旋转角度。内部复合单元角度在局部布局阶段单独处理。
    /// </summary>
    public double Rotation { get; set; }

    /// <summary>
    /// 锁定节点的几何位置，禁止移动、缩放、旋转和删除，但仍允许选择、查看与连接端口。
    /// </summary>
    public bool IsLocked { get; set; }

    public GraphNodeAppearance Appearance { get; set; } = new();

    /// <summary>
    /// 由节点类型扩展拥有的版本化业务状态；Kudo 将其作为不透明结构化数据保存。
    /// </summary>
    public GraphExtensionState? ExtensionState { get; set; }

    public List<PortModel> Ports { get; set; } = [];

    public Dictionary<string, object?> Properties { get; set; } = [];

    public NodeModel AddPort(PortModel port)
    {
        ArgumentNullException.ThrowIfNull(port);
        Ports.Add(port);
        return this;
    }
}

public sealed record GraphNodeAppearance
{
    public GraphNodeVisualMode VisualMode { get; init; } = GraphNodeVisualMode.Auto;

    public string? BackgroundImage { get; init; }

    public GraphImageStretch Stretch { get; init; } = GraphImageStretch.UniformToFill;

    public double Opacity { get; init; } = 1;

    public GraphTextAppearance TextAppearance { get; init; } = new();

    public GraphLabelVisibility? PortLabelVisibility { get; init; }

    public GraphLabelVisibility? NameVisibility { get; init; }

    public GraphLabelVisibility? DescriptionVisibility { get; init; }

    public GraphNodeAppearance Normalize() => this with
    {
        BackgroundImage = string.IsNullOrWhiteSpace(BackgroundImage)
            ? null
            : BackgroundImage.Replace('\\', '/').Trim(),
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0, 1) : 1,
        TextAppearance = (TextAppearance ?? new GraphTextAppearance()).Normalize(),
        PortLabelVisibility = NormalizeOptionalVisibility(PortLabelVisibility),
        NameVisibility = NormalizeOptionalVisibility(NameVisibility),
        DescriptionVisibility = NormalizeOptionalVisibility(DescriptionVisibility)
    };

    private static GraphLabelVisibility? NormalizeOptionalVisibility(GraphLabelVisibility? value) =>
        value is null || Enum.IsDefined(value.Value) ? value : null;
}

public sealed record GraphTextAppearance
{
    private const double MinimumFontSize = 6;
    private const double MaximumFontSize = 256;

    public string? Foreground { get; init; }

    public double FontSize { get; init; } = 14;

    public GraphFontWeight FontWeight { get; init; } = GraphFontWeight.Normal;

    public GraphTextAppearance Normalize() => this with
    {
        Foreground = string.IsNullOrWhiteSpace(Foreground) ? null : Foreground.Trim(),
        FontSize = double.IsFinite(FontSize) && FontSize is >= MinimumFontSize and <= MaximumFontSize
            ? FontSize
            : 14,
        FontWeight = Enum.IsDefined(FontWeight) ? FontWeight : GraphFontWeight.Normal
    };
}

public enum GraphFontWeight
{
    [Description("常规")]
    Normal,

    [Description("中等")]
    Medium,

    [Description("半粗")]
    SemiBold,

    [Description("粗体")]
    Bold
}

public enum GraphNodeVisualMode
{
    [Description("自动")]
    Auto,

    [Description("卡片")]
    Card,

    [Description("图元")]
    Symbol,

    [Description("文字")]
    Text
}

public enum GraphImageStretch
{
    [Description("保持原始尺寸")]
    None,

    [Description("拉伸填满")]
    Fill,

    [Description("等比完整显示")]
    Uniform,

    [Description("等比填满并裁剪")]
    UniformToFill
}

public sealed class AnnotationModel
{
    public string Id { get; set; } = GraphId.NewId();

    public string? Text { get; set; }

    public GraphPoint Position { get; set; } = GraphPoint.Zero;

    public GraphSize Size { get; set; } = new(220, 120);

    public Dictionary<string, object?> Properties { get; set; } = [];
}

public sealed class GroupModel
{
    public string Id { get; set; } = GraphId.NewId();

    public string? Title { get; set; }

    public List<string> NodeIds { get; set; } = [];

    public double Padding { get; set; } = 24;

    public bool IsCollapsed { get; set; }

    public Dictionary<string, object?> Properties { get; set; } = [];
}

public sealed class PortModel
{
    private GraphPoint? _incomingDirection;
    private GraphPoint? _outgoingDirection;
    private PortCapacity _capacity = new();

    public string Id { get; set; } = GraphId.NewId();

    public string Name { get; set; } = string.Empty;

    public PortKind Kind { get; set; } = PortKind.Input;

    /// <summary>
    /// 控制端口是否参与显示和指针交互；已有连线仍可使用该端口作为路由端点。
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// 端口在节点矩形内的相对锚点，X/Y 通常取 0 到 1；为空时按方向自动排布。
    /// </summary>
    public GraphPoint? Anchor { get; set; }

    /// <summary>
    /// 连线进入端口时在节点归一化局部坐标系中的单位方向向量；无有效方向约束时为空。
    /// </summary>
    public GraphPoint? IncomingDirection
    {
        get => _incomingDirection;
        set => _incomingDirection = PortGeometry.NormalizeDirection(value);
    }

    /// <summary>
    /// 连线离开端口时在节点归一化局部坐标系中的单位方向向量；无有效方向约束时为空。
    /// </summary>
    public GraphPoint? OutgoingDirection
    {
        get => _outgoingDirection;
        set => _outgoingDirection = PortGeometry.NormalizeDirection(value);
    }

    public string? DataType { get; set; }

    public PortCapacity Capacity
    {
        get => _capacity;
        set => _capacity = (value ?? new PortCapacity()).Normalize();
    }

    public PortState State { get; set; } = PortState.Normal;

    public Dictionary<string, object?> Properties { get; set; } = [];
}

public sealed class EdgeModel : GraphObservableObject
{
    private string _id = GraphId.NewId();
    private PortReference _source = PortReference.Empty;
    private PortReference _target = PortReference.Empty;
    private GraphRelationKind _relationKind;
    private EdgeRoutingStyle _routingStyle = EdgeRoutingStyle.Bezier;
    private GraphEdgeStylePreset _stylePreset;
    private GraphEdgeAppearance _appearance = new();
    private List<GraphPoint> _waypoints = [];
    private EdgeMarker _sourceMarker;
    private EdgeMarker _targetMarker;
    private string? _label;
    private EdgeState _state = EdgeState.Normal;
    private Dictionary<string, object?> _properties = [];

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value ?? string.Empty);
    }

    public PortReference Source
    {
        get => _source;
        set => SetProperty(ref _source, value ?? PortReference.Empty);
    }

    public PortReference Target
    {
        get => _target;
        set => SetProperty(ref _target, value ?? PortReference.Empty);
    }

    public GraphRelationKind RelationKind
    {
        get => _relationKind;
        set => SetProperty(ref _relationKind, value);
    }

    public EdgeRoutingStyle RoutingStyle
    {
        get => _routingStyle;
        set => SetProperty(ref _routingStyle, value);
    }

    public GraphEdgeStylePreset StylePreset
    {
        get => _stylePreset;
        set
        {
            if (!SetProperty(ref _stylePreset, value) || value == GraphEdgeStylePreset.Custom)
            {
                return;
            }

            SetAppearance(GraphEdgeAppearance.FromPreset(value), updatePreset: false);
        }
    }

    public GraphEdgeAppearance Appearance
    {
        get => _appearance;
        set => SetAppearance(value ?? new GraphEdgeAppearance(), updatePreset: true);
    }

    public List<GraphPoint> Waypoints
    {
        get => _waypoints;
        set => SetProperty(ref _waypoints, value ?? []);
    }

    public EdgeMarker SourceMarker
    {
        get => _sourceMarker;
        set => SetProperty(ref _sourceMarker, value);
    }

    public EdgeMarker TargetMarker
    {
        get => _targetMarker;
        set => SetProperty(ref _targetMarker, value);
    }

    public string? Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public EdgeState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    public Dictionary<string, object?> Properties
    {
        get => _properties;
        set => SetProperty(ref _properties, value ?? []);
    }

    private void SetAppearance(GraphEdgeAppearance appearance, bool updatePreset)
    {
        var normalized = appearance.Normalize();
        if (!SetProperty(ref _appearance, normalized, nameof(Appearance)))
        {
            return;
        }

        if (updatePreset &&
            _stylePreset != GraphEdgeStylePreset.Custom &&
            normalized != GraphEdgeAppearance.FromPreset(_stylePreset))
        {
            _stylePreset = GraphEdgeStylePreset.Custom;
            OnPropertyChanged(nameof(StylePreset));
        }
    }
}

public sealed record GraphEdgeAppearance
{
    public static GraphEdgeAppearance Default { get; } = new();

    public static GraphEdgeAppearance Success { get; } = new()
    {
        StrokeColor = "#4CAF50"
    };

    public static GraphEdgeAppearance Warning { get; } = new()
    {
        StrokeColor = "#FF9800",
        DashStyle = GraphEdgeDashStyle.Dash
    };

    public static GraphEdgeAppearance Error { get; } = new()
    {
        StrokeColor = "#F44336",
        StrokeWidth = 2.5
    };

    public static GraphEdgeAppearance Disabled { get; } = new()
    {
        StrokeColor = "#9E9E9E",
        DashStyle = GraphEdgeDashStyle.Dot,
        Opacity = 0.5
    };

    public static GraphEdgeAppearance ActiveFlow { get; } = new()
    {
        StrokeColor = "#2196F3",
        DashStyle = GraphEdgeDashStyle.Dash,
        Animation = GraphEdgeAnimationKind.Flow,
        AnimationSpeed = 1.5
    };

    public static GraphEdgeAppearance Highlighted { get; } = new()
    {
        StrokeColor = "#00BCD4",
        GlowColor = "#00BCD4",
        GlowIntensity = 0.35
    };

    public static GraphEdgeAppearance DataStream { get; } = new()
    {
        StrokeColor = "#00E676",
        StrokeWidth = 2,
        DashStyle = GraphEdgeDashStyle.Dot,
        Animation = GraphEdgeAnimationKind.Flow,
        AnimationSpeed = 3
    };

    public static GraphEdgeAppearance Bidirectional { get; } = new()
    {
        StrokeColor = "#9C27B0",
        DashStyle = GraphEdgeDashStyle.DashDot,
        Animation = GraphEdgeAnimationKind.Flow,
        AnimationSpeed = 1.5,
        AnimationDirection = GraphEdgeAnimationDirection.Bidirectional
    };

    public static GraphEdgeAppearance ReverseFlow { get; } = new()
    {
        StrokeColor = "#FF5722",
        DashStyle = GraphEdgeDashStyle.Dash,
        Animation = GraphEdgeAnimationKind.Flow,
        AnimationSpeed = 1.5,
        AnimationDirection = GraphEdgeAnimationDirection.Reverse
    };

    public static GraphEdgeAppearance Electric { get; } = new()
    {
        StrokeColor = "#FFCA28",
        DashStyle = GraphEdgeDashStyle.Dot,
        Animation = GraphEdgeAnimationKind.Flow,
        AnimationSpeed = 2.5,
        GlowColor = "#FFC107",
        GlowIntensity = 0.45
    };

    public static GraphEdgeAppearance Gentle { get; } = new()
    {
        StrokeColor = "#81D4FA",
        DashStyle = GraphEdgeDashStyle.LongDash,
        Animation = GraphEdgeAnimationKind.Flow,
        AnimationSpeed = 0.5
    };

    public static GraphEdgeAppearance Debug { get; } = new()
    {
        StrokeColor = "#E91E63",
        StrokeWidth = 3,
        DashStyle = GraphEdgeDashStyle.DashDot,
        Animation = GraphEdgeAnimationKind.Flow,
        AnimationSpeed = 2
    };

    public static GraphEdgeAppearance Rainbow { get; } = new()
    {
        StrokeWidth = 2.5,
        DashStyle = GraphEdgeDashStyle.Dash,
        Animation = GraphEdgeAnimationKind.Rainbow,
        AnimationSpeed = 0.5
    };

    public static GraphEdgeAppearance Pulse { get; } = new()
    {
        StrokeColor = "#00BCD4",
        Animation = GraphEdgeAnimationKind.Pulse,
        GlowColor = "#00BCD4",
        GlowIntensity = 0.35
    };

    public static GraphEdgeAppearance Heartbeat { get; } = new()
    {
        StrokeColor = "#E53935",
        StrokeWidth = 2.5,
        Animation = GraphEdgeAnimationKind.Pulse,
        AnimationSpeed = 1.5
    };

    public static GraphEdgeAppearance Neon { get; } = new()
    {
        StrokeColor = "#00FFFF",
        DashStyle = GraphEdgeDashStyle.Dot,
        Animation = GraphEdgeAnimationKind.Flow,
        AnimationSpeed = 2,
        GlowColor = "#00FFFF",
        GlowIntensity = 0.6
    };

    public static GraphEdgeAppearance Signal { get; } = new()
    {
        StrokeColor = "#76FF03",
        StrokeWidth = 1.5,
        DashStyle = GraphEdgeDashStyle.Dot,
        Animation = GraphEdgeAnimationKind.Signal,
        AnimationSpeed = 4
    };

    public string? StrokeColor { get; init; }

    public double? StrokeWidth { get; init; }

    public GraphEdgeDashStyle DashStyle { get; init; }

    public IReadOnlyList<double> CustomDashPattern { get; init; } = [];

    public double Opacity { get; init; } = 1;

    public GraphEdgeAnimationKind Animation { get; init; }

    public double AnimationSpeed { get; init; } = 1;

    public GraphEdgeAnimationDirection AnimationDirection { get; init; }

    public double GlowIntensity { get; init; }

    public string? GlowColor { get; init; }

    public bool IsAnimated => Animation != GraphEdgeAnimationKind.None && Opacity > 0;

    public bool RequiresAnimationOverlay => IsAnimated && Animation == GraphEdgeAnimationKind.Signal;

    public bool IsDefault =>
        string.IsNullOrWhiteSpace(StrokeColor) &&
        StrokeWidth is null &&
        DashStyle == GraphEdgeDashStyle.Solid &&
        (CustomDashPattern?.Count ?? 0) == 0 &&
        Opacity == 1 &&
        Animation == GraphEdgeAnimationKind.None &&
        AnimationSpeed == 1 &&
        AnimationDirection == GraphEdgeAnimationDirection.Forward &&
        GlowIntensity == 0 &&
        string.IsNullOrWhiteSpace(GlowColor);

    public static GraphEdgeAppearance FromPreset(GraphEdgeStylePreset preset) => preset switch
    {
        GraphEdgeStylePreset.Success => Success,
        GraphEdgeStylePreset.Warning => Warning,
        GraphEdgeStylePreset.Error => Error,
        GraphEdgeStylePreset.Disabled => Disabled,
        GraphEdgeStylePreset.ActiveFlow => ActiveFlow,
        GraphEdgeStylePreset.Highlighted => Highlighted,
        GraphEdgeStylePreset.DataStream => DataStream,
        GraphEdgeStylePreset.Bidirectional => Bidirectional,
        GraphEdgeStylePreset.ReverseFlow => ReverseFlow,
        GraphEdgeStylePreset.Electric => Electric,
        GraphEdgeStylePreset.Gentle => Gentle,
        GraphEdgeStylePreset.Debug => Debug,
        GraphEdgeStylePreset.Rainbow => Rainbow,
        GraphEdgeStylePreset.Pulse => Pulse,
        GraphEdgeStylePreset.Heartbeat => Heartbeat,
        GraphEdgeStylePreset.Neon => Neon,
        GraphEdgeStylePreset.Signal => Signal,
        _ => Default
    };

    public GraphEdgeAppearance WithColor(string? color) => this with { StrokeColor = color };

    public GraphEdgeAppearance WithWidth(double? width) => this with { StrokeWidth = width };

    public GraphEdgeAppearance WithDash(
        GraphEdgeDashStyle dashStyle,
        IReadOnlyList<double>? customPattern = null) => this with
        {
            DashStyle = dashStyle,
            CustomDashPattern = customPattern ?? []
        };

    public GraphEdgeAppearance WithAnimation(
        GraphEdgeAnimationKind animation,
        double speed = 1,
        GraphEdgeAnimationDirection direction = GraphEdgeAnimationDirection.Forward) => this with
        {
            Animation = animation,
            AnimationSpeed = speed,
            AnimationDirection = direction
        };

    public GraphEdgeAppearance WithGlow(double intensity, string? color = null) => this with
    {
        GlowIntensity = intensity,
        GlowColor = color
    };

    public GraphEdgeAppearance WithOpacity(double opacity) => this with { Opacity = opacity };

    public GraphEdgeAppearance Normalize() => this with
    {
        StrokeColor = NormalizeColor(StrokeColor),
        StrokeWidth = StrokeWidth is { } width && double.IsFinite(width) && width > 0
            ? Math.Clamp(width, 0.5, 16)
            : null,
        CustomDashPattern = (CustomDashPattern ?? [])
            .Where(value => double.IsFinite(value) && value > 0)
            .Take(16)
            .Select(value => Math.Clamp(value, 0.25, 100))
            .ToArray(),
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0, 1) : 1,
        AnimationSpeed = double.IsFinite(AnimationSpeed)
            ? Math.Clamp(AnimationSpeed, 0.1, 4)
            : 1,
        GlowIntensity = double.IsFinite(GlowIntensity)
            ? Math.Clamp(GlowIntensity, 0, 1)
            : 0,
        GlowColor = NormalizeColor(GlowColor)
    };

    private static string? NormalizeColor(string? color) =>
        string.IsNullOrWhiteSpace(color) ? null : color.Trim();
}

public enum GraphEdgeStylePreset
{
    [Description("默认样式")]
    Default,
    [Description("成功")]
    Success,
    [Description("警告")]
    Warning,
    [Description("错误")]
    Error,
    [Description("禁用")]
    Disabled,
    [Description("活动流动")]
    ActiveFlow,
    [Description("高亮")]
    Highlighted,
    [Description("数据流")]
    DataStream,
    [Description("双向流动")]
    Bidirectional,
    [Description("反向流动")]
    ReverseFlow,
    [Description("电流效果")]
    Electric,
    [Description("柔和流动")]
    Gentle,
    [Description("调试样式")]
    Debug,
    [Description("彩虹效果")]
    Rainbow,
    [Description("脉冲效果")]
    Pulse,
    [Description("心跳效果")]
    Heartbeat,
    [Description("霓虹效果")]
    Neon,
    [Description("信号效果")]
    Signal,
    [Description("自定义样式")]
    Custom
}

public enum GraphEdgeDashStyle
{
    [Description("实线")]
    Solid,
    [Description("虚线")]
    Dash,
    [Description("点线")]
    Dot,
    [Description("点划线")]
    DashDot,
    [Description("长虚线")]
    LongDash,
    [Description("自定义虚线")]
    Custom
}

public enum GraphEdgeAnimationKind
{
    [Description("无动画")]
    None,
    [Description("流动动画")]
    Flow,
    [Description("脉冲动画")]
    Pulse,
    [Description("彩虹动画")]
    Rainbow,
    [Description("信号动画")]
    Signal
}

public enum GraphEdgeAnimationDirection
{
    [Description("正向")]
    Forward,
    [Description("反向")]
    Reverse,
    [Description("双向")]
    Bidirectional
}

public sealed record PortReference(string NodeId, string PortId)
{
    public static PortReference Empty { get; } = new(string.Empty, string.Empty);

    public bool IsEmpty => string.IsNullOrWhiteSpace(NodeId) || string.IsNullOrWhiteSpace(PortId);
}

public enum PortKind
{
    [Description("输入端口")]
    Input,
    [Description("输出端口")]
    Output,
    [Description("输入输出端口")]
    InputOutput,
    [Description("引用端口")]
    Reference
}

public static class PortKindSemantics
{
    public static bool CanReceiveConnection(PortKind kind) =>
        kind is PortKind.Input or PortKind.InputOutput;

    public static bool CanSendConnection(PortKind kind) =>
        kind is PortKind.Output or PortKind.InputOutput;

    public static bool CanReceiveReference(PortKind kind) => kind == PortKind.Reference;

    public static bool CanSendReference(PortKind kind) => kind == PortKind.Reference;
}

public sealed record PortCapacity
{
    /// <summary>
    /// 允许进入此端口的最大关系数；为空表示不限，0 表示禁止。
    /// </summary>
    public int? MaxIncoming { get; init; } = 1;

    /// <summary>
    /// 允许从此端口发出的最大关系数；为空表示不限，0 表示禁止。
    /// </summary>
    public int? MaxOutgoing { get; init; } = 1;

    /// <summary>
    /// 此端口允许承载的最大关系总数；为空表示不限，0 表示禁止。
    /// </summary>
    public int? MaxTotal { get; init; }

    public PortCapacity Normalize() => this with
    {
        MaxIncoming = NormalizeLimit(MaxIncoming),
        MaxOutgoing = NormalizeLimit(MaxOutgoing),
        MaxTotal = NormalizeLimit(MaxTotal)
    };

    private static int? NormalizeLimit(int? value) => value is < 0 ? 0 : value;
}

public static class PortGeometry
{
    private const double DirectionEpsilon = 1e-12;

    /// <summary>
    /// 将固有局部几何中的方向转换为节点归一化局部坐标方向。
    /// </summary>
    public static GraphPoint? ToNormalizedLocalDirection(
        GraphPoint? localDirection,
        GraphSize intrinsicSize)
    {
        if (localDirection is not { } direction || !IsFinitePositive(intrinsicSize))
        {
            return null;
        }

        return NormalizeDirection(new GraphPoint(
            direction.X / intrinsicSize.Width,
            direction.Y / intrinsicSize.Height));
    }

    /// <summary>
    /// 将节点归一化局部坐标方向按当前节点尺寸转换为局部视觉方向。
    /// </summary>
    public static GraphPoint? ToNodeLocalDirection(
        GraphPoint? normalizedLocalDirection,
        GraphSize nodeSize)
    {
        if (normalizedLocalDirection is not { } direction || !IsFinitePositive(nodeSize))
        {
            return null;
        }

        return NormalizeDirection(new GraphPoint(
            direction.X * nodeSize.Width,
            direction.Y * nodeSize.Height));
    }

    /// <summary>
    /// 将节点归一化局部坐标方向按当前尺寸和旋转转换为画布方向。
    /// </summary>
    public static GraphPoint? ToCanvasDirection(
        NodeModel node,
        GraphPoint? normalizedLocalDirection)
    {
        ArgumentNullException.ThrowIfNull(node);
        var localDirection = ToNodeLocalDirection(normalizedLocalDirection, node.Size);
        return localDirection is { } direction
            ? NormalizeDirection(GraphNodeGeometry.RotateVector(direction, node.Rotation))
            : null;
    }

    public static GraphPoint? NormalizeDirection(GraphPoint? direction)
    {
        if (direction is not { } value ||
            !double.IsFinite(value.X) ||
            !double.IsFinite(value.Y))
        {
            return null;
        }

        var length = Math.Sqrt(value.X * value.X + value.Y * value.Y);
        return length <= DirectionEpsilon || !double.IsFinite(length)
            ? null
            : new GraphPoint(value.X / length, value.Y / length);
    }

    private static bool IsFinitePositive(GraphSize size) =>
        double.IsFinite(size.Width) && size.Width > 0 &&
        double.IsFinite(size.Height) && size.Height > 0;
}

public enum GraphRelationKind
{
    [Description("连接")]
    Connection,
    [Description("引用")]
    Reference
}

public enum PortState
{
    [Description("正常")]
    Normal,
    [Description("高亮")]
    Highlighted,
    [Description("无效")]
    Invalid,
    [Description("禁用")]
    Disabled
}

public enum EdgeRoutingStyle
{
    [Description("直线")]
    Direct,
    [Description("贝塞尔曲线")]
    Bezier,
    [Description("正交折线")]
    Orthogonal,
    [Description("手动途经点")]
    Manual,
    [Description("智能避障正交线")]
    SmartOrthogonal
}

public enum EdgeMarker
{
    [Description("无标记")]
    None,
    [Description("箭头")]
    Arrow,
    [Description("圆形")]
    Circle,
    [Description("菱形")]
    Diamond
}

public enum EdgeState
{
    [Description("正常")]
    Normal,
    [Description("活动")]
    Active,
    [Description("高亮")]
    Highlighted,
    [Description("无效")]
    Invalid,
    [Description("禁用")]
    Disabled
}

public sealed record GraphViewport(GraphPoint Offset, double Zoom)
{
    public static GraphViewport Default { get; } = new(GraphPoint.Zero, 1.0);
}

public sealed record PropertyDescriptor(
    string Name,
    string DisplayName,
    Type ValueType,
    object? DefaultValue = null,
    bool IsReadOnly = false);

public interface IGraphValidationRule
{
    IEnumerable<GraphValidationIssue> Validate(GraphDocument document);
}

public sealed record GraphValidationIssue(
    ValidationSeverity Severity,
    string Message,
    string? ElementId = null,
    string? Code = null);

public enum ValidationSeverity
{
    [Description("提示")]
    Info,
    [Description("警告")]
    Warning,
    [Description("错误")]
    Error
}

public readonly record struct ConnectionValidationRequest(
    GraphDocument Document,
    PortReference Source,
    PortReference Target,
    GraphRelationKind RelationKind = GraphRelationKind.Connection,
    string? IgnoredEdgeId = null,
    IGraphValidationTextProvider? TextProvider = null);

public readonly record struct ConnectionValidationResult(
    bool IsValid,
    string? Message = null,
    string? Code = null)
{
    public static ConnectionValidationResult Valid { get; } = new(true);

    public static ConnectionValidationResult Invalid(string message, string? code = null) =>
        new(false, message, code);
}

public interface IConnectionValidationRule
{
    ConnectionValidationResult Validate(in ConnectionValidationRequest request);
}

/// <summary>
/// 按注册顺序执行连接规则，遇到首个拒绝结果立即停止。
/// </summary>
public sealed class CompositeConnectionValidationRule : IConnectionValidationRule
{
    private readonly IConnectionValidationRule[] _rules;

    public CompositeConnectionValidationRule(IEnumerable<IConnectionValidationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToArray();
        if (Array.Exists(_rules, static rule => rule is null))
        {
            throw new ArgumentException(null, nameof(rules));
        }
    }

    public CompositeConnectionValidationRule(params IConnectionValidationRule[] rules)
        : this((IEnumerable<IConnectionValidationRule>)rules)
    {
    }

    public ConnectionValidationResult Validate(in ConnectionValidationRequest request)
    {
        foreach (var rule in _rules)
        {
            var validation = rule.Validate(in request);
            if (!validation.IsValid)
            {
                return validation;
            }
        }

        return ConnectionValidationResult.Valid;
    }
}

public sealed class DefaultConnectionValidationRule : IConnectionValidationRule
{
    public static DefaultConnectionValidationRule Instance { get; } = new();

    public ConnectionValidationResult Validate(in ConnectionValidationRequest request)
    {
        var document = request.Document;
        var source = request.Source;
        var target = request.Target;
        var sourceNode = document.FindNode(source.NodeId);
        var targetNode = document.FindNode(target.NodeId);
        var sourcePort = FindPort(sourceNode, source.PortId);
        var targetPort = FindPort(targetNode, target.PortId);

        if (sourceNode is null || targetNode is null || sourcePort is null || targetPort is null)
        {
            return Invalid(request, GraphValidationTextKey.MissingConnectionEndpoint, GraphValidationCodes.MissingEdgeEndpoint);
        }

        if (sourceNode.Id == targetNode.Id)
        {
            return Invalid(request, GraphValidationTextKey.ConnectionSelfConnection, GraphValidationCodes.SelfConnection);
        }

        if (!Enum.IsDefined(request.RelationKind))
        {
            return Invalid(request, GraphValidationTextKey.InvalidConnectionRelationKind, GraphValidationCodes.InvalidRelationKind);
        }

        if (!ArePortKindsCompatible(request.RelationKind, sourcePort.Kind, targetPort.Kind))
        {
            return Invalid(
                request,
                GraphValidationTextKey.ConnectionPortKindsIncompatible,
                GraphValidationCodes.IncompatiblePortKinds);
        }

        if (sourcePort.State == PortState.Disabled || targetPort.State == PortState.Disabled)
        {
            return Invalid(request, GraphValidationTextKey.ConnectionPortDisabled, GraphValidationCodes.ConnectionDisabled);
        }

        if (request.RelationKind == GraphRelationKind.Connection &&
            !AreDataTypesCompatible(sourcePort.DataType, targetPort.DataType))
        {
            return Invalid(
                request,
                GraphValidationTextKey.ConnectionDataTypesIncompatible,
                GraphValidationCodes.IncompatibleDataType);
        }

        var sourceOutgoing = 0;
        var sourceIncoming = 0;
        var targetOutgoing = 0;
        var targetIncoming = 0;
        var ignoredEdgeId = request.IgnoredEdgeId;
        var hasIgnoredEdge = !string.IsNullOrWhiteSpace(ignoredEdgeId);
        foreach (var edge in document.Edges)
        {
            if (hasIgnoredEdge && string.Equals(edge.Id, ignoredEdgeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (edge.Source == source &&
                edge.Target == target &&
                edge.RelationKind == request.RelationKind)
            {
                return Invalid(request, GraphValidationTextKey.ConnectionDuplicate, GraphValidationCodes.DuplicateConnection);
            }

            if (edge.Source == source)
            {
                sourceOutgoing++;
            }

            if (edge.Target == source)
            {
                sourceIncoming++;
            }

            if (edge.Source == target)
            {
                targetOutgoing++;
            }

            if (edge.Target == target)
            {
                targetIncoming++;
            }
        }

        if (WouldExceedCapacity(
                sourcePort.Capacity,
                sourceOutgoing,
                sourceIncoming,
                outgoing: true))
        {
            return Invalid(
                request,
                GraphValidationTextKey.SourceConnectionLimitExceeded,
                GraphValidationCodes.ConnectionLimitExceeded);
        }

        if (WouldExceedCapacity(
                targetPort.Capacity,
                targetOutgoing,
                targetIncoming,
                outgoing: false))
        {
            return Invalid(
                request,
                GraphValidationTextKey.TargetConnectionLimitExceeded,
                GraphValidationCodes.ConnectionLimitExceeded);
        }

        return ConnectionValidationResult.Valid;
    }

    internal static bool ArePortKindsCompatible(
        GraphRelationKind relationKind,
        PortKind sourceKind,
        PortKind targetKind) => relationKind switch
    {
        GraphRelationKind.Connection =>
            PortKindSemantics.CanSendConnection(sourceKind) &&
            PortKindSemantics.CanReceiveConnection(targetKind),
        GraphRelationKind.Reference =>
            PortKindSemantics.CanSendReference(sourceKind) &&
            PortKindSemantics.CanReceiveReference(targetKind),
        _ => false
    };

    internal static bool WouldExceedCapacity(
        PortCapacity capacity,
        int outgoingCount,
        int incomingCount,
        bool outgoing)
    {
        var normalized = (capacity ?? new PortCapacity()).Normalize();
        var directionalCount = outgoing ? outgoingCount : incomingCount;
        var directionalLimit = outgoing ? normalized.MaxOutgoing : normalized.MaxIncoming;
        return HasReachedLimit(directionalCount, directionalLimit) ||
               HasReachedLimit(outgoingCount + incomingCount, normalized.MaxTotal);
    }

    private static bool HasReachedLimit(int count, int? limit) => limit is { } value && count >= value;

    private static PortModel? FindPort(NodeModel? node, string portId)
    {
        if (node is null)
        {
            return null;
        }

        foreach (var port in node.Ports)
        {
            if (port.Id == portId)
            {
                return port;
            }
        }

        return null;
    }

    private static ConnectionValidationResult Invalid(
        in ConnectionValidationRequest request,
        GraphValidationTextKey key,
        string code) =>
        ConnectionValidationResult.Invalid(GraphValidationText.Get(request.TextProvider, key), code);

    private static bool AreDataTypesCompatible(string? sourceDataType, string? targetDataType)
    {
        if (string.IsNullOrWhiteSpace(sourceDataType) || string.IsNullOrWhiteSpace(targetDataType))
        {
            return true;
        }

        return string.Equals(sourceDataType, targetDataType, StringComparison.OrdinalIgnoreCase);
    }
}

public interface IGraphCommand
{
    string Name { get; }

    void Execute(GraphDocument document);

    void Undo(GraphDocument document);
}
