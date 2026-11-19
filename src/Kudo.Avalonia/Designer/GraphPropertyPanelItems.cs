// Property panel item builder: maps GraphPropertyEditorContext + GraphPropertyContext
// into a CodeWF.Avalonia.TreeDataGrid.PropertyGrid PropertyItem tree and carries
// commit semantics identical to the legacy built-in editors (command factory, two-phase preview/commit, validation rollback).
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using VAlignment = Avalonia.Layout.VerticalAlignment;
using Kudo.Avalonia;
using Kudo.Core.Documents;
using Kudo.Core.Runtime;
using Kudo.Controls.PropertyGrid;
using static Kudo.Avalonia.Designer.GraphPropertyValueConverter;
using PropertiesLanguage = global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView;

namespace Kudo.Avalonia.Designer;

/// <summary>
/// One instance per RefreshCore: builds the item collection and stores per-item commit/preview handlers.
/// </summary>
internal sealed class GraphPropertyPanelItems
{
    public const string ProviderItemId = "kudo.provider";
    public const string BackgroundImageItemId = "kudo.background-image";

    private readonly GraphPropertyEditorContext _editorContext;
    private readonly bool _showBackgroundStretch;
    private readonly bool _showEdgeMarkers;
    private readonly Action<string> _notifyEditCommitted;
    private readonly Action<GraphNodeBackgroundImageRequest> _notifyBackgroundImageRequested;
    private readonly Action _refreshPanel;

    private readonly List<PropertyItem> _items = new();
    private readonly Dictionary<string, Action<object?, object?>> _commitHandlers = new();
    private readonly Dictionary<string, Func<object?, IGraphCommand?>> _previewHandlers = new();
    private readonly Dictionary<string, object> _previewSnapshots = new();
    private readonly Dictionary<string, Control> _customEditors = new();
    private int _sequence;

    public GraphPropertyPanelItems(
        GraphPropertyEditorContext editorContext,
        GraphPropertyContext context,
        bool showBackgroundStretch,
        bool showEdgeMarkers,
        Action<string> notifyEditCommitted,
        Action<GraphNodeBackgroundImageRequest> notifyBackgroundImageRequested,
        Action refreshPanel)
    {
        _editorContext = editorContext;
        Context = context;
        _showBackgroundStretch = showBackgroundStretch;
        _showEdgeMarkers = showEdgeMarkers;
        _notifyEditCommitted = notifyEditCommitted;
        _notifyBackgroundImageRequested = notifyBackgroundImageRequested;
        _refreshPanel = refreshPanel;
    }

    public GraphPropertyContext Context { get; }

    public IReadOnlyList<PropertyItem> Items => _items;

    // 属性控件允许不提供标识；Kudo 的提交、预览与编辑器映射必须使用已分配的标识。
    private static string GetItemId(PropertyItem item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Id);
        return item.Id;
    }

    /// <summary>Provider native controls (if any), hosted in a custom row via PropertyGrid.EditorFactory.</summary>
    public Control? ProviderContent { get; private set; }

    public Control? GetCustomEditor(PropertyItem item) =>
        _customEditors.TryGetValue(GetItemId(item), out var editor) ? editor : null;

    /// <summary>Cancelable value commit preview. Two-phase fields run TryPreview here.</summary>
    public bool HandlePreview(PropertyItem item, object? newValue)
    {
        if (!_previewHandlers.TryGetValue(GetItemId(item), out var preview))
        {
            return false;
        }

        var command = preview(newValue);
        if (command is null || !_editorContext.TryPreview(command))
        {
            return true; // canceled: the editor rolls back
        }

        return false;
    }

    /// <summary>Value already written to the item; applies the change to the graph document (command), rolling the item back on failure.</summary>
    public void HandleCommit(PropertyItem item, object? oldValue, object? newValue)
    {
        if (_commitHandlers.TryGetValue(GetItemId(item), out var commit))
        {
            commit(newValue, oldValue);
        }
    }

    /* ---------------- Build entry points ---------------- */

    public IReadOnlyList<PropertyItem> BuildForNode(NodeModel node, GraphNodeDefinition? definition, string? providerTitle, IReadOnlyList<Control>? providerControls)
    {
        var ro = _editorContext.IsReadOnly;

        AddCategory(L(PropertiesLanguage.BasicInformation));
        AddReadOnly("node.definitionId", L(PropertiesLanguage.DefinitionId), node.DefinitionId);
        AddText("node.number", L(PropertiesLanguage.Number), node.Number, v => CommitNodeBasics(node, number: AsString(v)), ro);
        AddText("node.name", L(PropertiesLanguage.Name), node.Name, v => CommitNodeBasics(node, name: AsString(v)), ro);
        AddText("node.description", L(PropertiesLanguage.Description), node.Description, v => CommitNodeBasics(node, description: AsString(v)), ro);

        AddCategory(L(PropertiesLanguage.XCoordinate));
        AddNumber("node.x", L(PropertiesLanguage.XCoordinate), node.Position.X, null, null, 1, null,
            v => CommitNodePosition(node, ToDouble(v)), ro || node.IsLocked);
        AddNumber("node.y", L(PropertiesLanguage.YCoordinate), node.Position.Y, null, null, 1, null,
            v => CommitNodePosition(node, y: ToDouble(v)), ro || node.IsLocked);
        // Matches legacy behavior: overall rotation is always disabled (display only) and submitted with the other basic fields.
        AddNumber("node.rotation", L(PropertiesLanguage.NodeRotation), GraphNodeGeometry.NormalizeRotation(node.Rotation), -180, 180, 1, null,
            _ => CommitNodeBasics(node), readOnly: true);

        AddCategory(L(PropertiesLanguage.Size));
        AddNumber("node.width", L(PropertiesLanguage.Width), node.Size.Width, null, null, 1, null,
            v => CommitNodeSize(node, width: ToDouble(v)), ro || node.IsLocked);
        AddNumber("node.height", L(PropertiesLanguage.Height), node.Size.Height, null, null, 1, null,
            v => CommitNodeSize(node, height: ToDouble(v)), ro || node.IsLocked);

        AddBoolean("node.locked", L(PropertiesLanguage.NodeLocked), node.IsLocked,
            v => CommitNodeLock(node, AsBool(v)), ro);

        if (definition is { PropertyDefinitions.Count: > 0 })
        {
            AddCategory(L(PropertiesLanguage.SymbolProperties));
            string? lastGroup = null;
            foreach (var property in definition.PropertyDefinitions)
            {
                if (!string.IsNullOrEmpty(property.GroupName) &&
                    !string.Equals(property.GroupName, lastGroup, StringComparison.Ordinal))
                {
                    AddCategory(property.GroupName);
                    lastGroup = property.GroupName;
                }

                AddDefinitionProperty(node, property);
            }
        }

        var isTextNode = node.Appearance.Normalize().VisualMode == GraphNodeVisualMode.Text;
        var hasPorts = node.Ports.Count > 0;

        AddCategory(L(PropertiesLanguage.Appearance));
        AddEnumChoice("node.visualMode", L(PropertiesLanguage.NodeVisualMode),
            FormatNodeVisualModeOptions(), node.Appearance.Normalize().VisualMode,
            v => CommitAppearance(node, a => a with { VisualMode = v }), ro,
            onCommitted: _refreshPanel);
        AddCustom(BackgroundImageItemId, L(PropertiesLanguage.BackgroundImage), visible: () => !isTextNode);
        AddEnumChoice("node.backgroundStretch", L(PropertiesLanguage.BackgroundStretch),
            FormatImageStretchOptions(), node.Appearance.Normalize().Stretch,
            v => CommitAppearance(node, a => a with { Stretch = v }), ro,
            visible: () => !isTextNode && _showBackgroundStretch);
        AddPercent("node.backgroundOpacity", L(PropertiesLanguage.BackgroundOpacity),
            node.Appearance.Normalize().Opacity * 100d, 0, 100,
            v => PreviewNodeBackgroundOpacity(node, ToDouble(v)),
            v => CommitNodeBackgroundOpacity(node, ToDouble(v)),
            ro, visible: () => !isTextNode);
        AddLabelVisibilityChoice("node.nameVisibility", L(PropertiesLanguage.NodeNameVisibility),
            node.Appearance.Normalize().NameVisibility,
            v => CommitAppearance(node, a => a with { NameVisibility = v }), ro);
        AddLabelVisibilityChoice("node.descriptionVisibility", L(PropertiesLanguage.NodeDescriptionVisibility),
            node.Appearance.Normalize().DescriptionVisibility,
            v => CommitAppearance(node, a => a with { DescriptionVisibility = v }), ro);
        AddLabelVisibilityChoice("node.portLabelVisibility", L(PropertiesLanguage.PortLabelVisibility),
            node.Appearance.Normalize().PortLabelVisibility,
            v => CommitAppearance(node, a => a with { PortLabelVisibility = v }), ro,
            visible: () => hasPorts);
        AddColor("node.textForeground", L(PropertiesLanguage.TextForeground),
            node.Appearance.Normalize().TextAppearance.Foreground,
            v => CommitAppearance(node, a => a with
            {
                TextAppearance = a.TextAppearance.Normalize() with { Foreground = AsString(v) }
            }), ro);
        AddNumber("node.textFontSize", L(PropertiesLanguage.TextFontSize),
            node.Appearance.Normalize().TextAppearance.FontSize, 6, 256, 1, null,
            v => CommitAppearance(node, a => a with
            {
                TextAppearance = a.TextAppearance.Normalize() with { FontSize = ToDouble(v) }
            }), ro);
        AddEnumChoice("node.textFontWeight", L(PropertiesLanguage.TextFontWeight),
            FormatFontWeightOptions(), node.Appearance.Normalize().TextAppearance.FontWeight,
            v => CommitAppearance(node, a => a with
            {
                TextAppearance = a.TextAppearance.Normalize() with { FontWeight = v }
            }), ro);

        if (providerControls is { Count: > 0 })
        {
            AddCategory(providerTitle ?? L(PropertiesLanguage.ComponentProperties));
            AddCustom(ProviderItemId, providerTitle ?? string.Empty);
        }
        return _items;
    }

    public IReadOnlyList<PropertyItem> BuildForProviderOnly(string title, IReadOnlyList<Control> controls)
    {
        AddCategory(title);
        AddCustom(ProviderItemId, title);
        ProviderContent = CombineControls(controls);
        return _items;
    }

    public IReadOnlyList<PropertyItem> BuildForEdge(EdgeModel edge, string stateText)
    {
        var ro = _editorContext.IsReadOnly;

        AddCategory(L(PropertiesLanguage.BasicInformation));
        AddReadOnly("edge.id", "ID", edge.Id);
        AddReadOnly("edge.start", L(PropertiesLanguage.Start), $"{edge.Source.NodeId}/{edge.Source.PortId}");
        AddReadOnly("edge.end", L(PropertiesLanguage.End), $"{edge.Target.NodeId}/{edge.Target.PortId}");
        AddReadOnly("edge.state", L(PropertiesLanguage.State), stateText);
        AddReadOnly("edge.waypoints", L(PropertiesLanguage.Waypoints), edge.Waypoints.Count.ToString(CultureInfo.CurrentCulture));

        AddCategory(L(PropertiesLanguage.EditableProperties));
        AddText("edge.label", L(PropertiesLanguage.Label), edge.Label, _ => CommitEdge(edge), ro);
        AddEnumChoice("edge.routing", L(PropertiesLanguage.Routing), FormatRoutingOptions(),
            edge.RoutingStyle, _ => CommitEdge(edge), ro);
        AddEnumChoice("edge.stylePreset", L(PropertiesLanguage.StylePreset), FormatStylePresetOptions(),
            edge.StylePreset, _ => CommitEdge(edge), ro);
        AddEnumChoice("edge.sourceMarker", L(PropertiesLanguage.SourceMarker), FormatMarkerOptions(),
            edge.SourceMarker, _ => CommitEdge(edge), ro, visible: () => _showEdgeMarkers);
        AddEnumChoice("edge.targetMarker", L(PropertiesLanguage.TargetMarker), FormatMarkerOptions(),
            edge.TargetMarker, _ => CommitEdge(edge), ro, visible: () => _showEdgeMarkers);
        return _items;
    }

    public IReadOnlyList<PropertyItem> BuildForAnnotation(AnnotationModel annotation)
    {
        var ro = _editorContext.IsReadOnly;

        AddCategory(L(PropertiesLanguage.BasicInformation));
        AddReadOnly("annotation.id", "ID", annotation.Id);
        AddReadOnly("annotation.size", L(PropertiesLanguage.Size),
            $"{annotation.Size.Width:0.##} × {annotation.Size.Height:0.##}");

        AddCategory(L(PropertiesLanguage.EditableProperties));
        AddMultiline("annotation.text", L(PropertiesLanguage.Text), annotation.Text,
            v => CommitAnnotation(annotation, text: AsString(v)), ro, commitOnEnter: false);
        AddNumber("annotation.x", L(PropertiesLanguage.XCoordinate), annotation.Position.X, null, null, 1, null,
            v => CommitAnnotation(annotation, x: ToDouble(v)), ro);
        AddNumber("annotation.y", L(PropertiesLanguage.YCoordinate), annotation.Position.Y, null, null, 1, null,
            v => CommitAnnotation(annotation, y: ToDouble(v)), ro);
        AddNumber("annotation.width", L(PropertiesLanguage.Width), annotation.Size.Width, null, null, 1, null,
            v => CommitAnnotation(annotation, width: ToDouble(v)), ro);
        AddNumber("annotation.height", L(PropertiesLanguage.Height), annotation.Size.Height, null, null, 1, null,
            v => CommitAnnotation(annotation, height: ToDouble(v)), ro);
        var annotationPropsItem = AddMultiline("annotation.properties", L(PropertiesLanguage.ExtensionProperties),
            FormatExtensionProperties(annotation.Properties),
            _ => { }, ro, commitOnEnter: false);
        _commitHandlers[GetItemId(annotationPropsItem)] = (newValue, oldValue) =>
        {
            if (TryParseExtensionProperties(AsString(newValue), out var props))
            {
                CommitAnnotation(annotation, properties: props);
            }
            else
            {
                _editorContext.SetMessage(L(PropertiesLanguage.InvalidAnnotationProperties));
                annotationPropsItem.Value = oldValue;
            }
        };
        AddReadOnly("annotation.propertiesHint", string.Empty, L(PropertiesLanguage.ExtensionPropertiesHint));
        return _items;
    }

    public IReadOnlyList<PropertyItem> BuildForGroup(GroupModel group)
    {
        var ro = _editorContext.IsReadOnly;

        AddCategory(L(PropertiesLanguage.BasicInformation));
        AddReadOnly("group.id", "ID", group.Id);
        AddReadOnly("group.nodes", L(PropertiesLanguage.Nodes), group.NodeIds.Count.ToString(CultureInfo.CurrentCulture));

        AddCategory(L(PropertiesLanguage.EditableProperties));
        AddText("group.title", L(PropertiesLanguage.Title), group.Title, _ => CommitGroup(group), ro);
        AddNumber("group.padding", L(PropertiesLanguage.Padding), group.Padding, 0, null, 1, null,
            v => CommitGroup(group, padding: ToDouble(v)), ro);
        AddStateChoice("group.state", L(PropertiesLanguage.State), group.IsCollapsed,
            collapsed => CommitGroup(group, collapsed: collapsed), ro);
        var groupPropsItem = AddMultiline("group.properties", L(PropertiesLanguage.ExtensionProperties),
            FormatExtensionProperties(group.Properties),
            _ => { }, ro, commitOnEnter: false);
        _commitHandlers[GetItemId(groupPropsItem)] = (newValue, oldValue) =>
        {
            if (TryParseExtensionProperties(AsString(newValue), out var props))
            {
                CommitGroup(group, properties: props);
            }
            else
            {
                _editorContext.SetMessage(L(PropertiesLanguage.InvalidExtensionProperties));
                groupPropsItem.Value = oldValue;
            }
        };
        AddReadOnly("group.propertiesHint", string.Empty, L(PropertiesLanguage.ExtensionPropertiesHint));
        return _items;
    }

    public IReadOnlyList<PropertyItem> BuildForDocument()
    {
        var session = _editorContext.Session;
        var document = session.Document;
        var ro = _editorContext.IsReadOnly;

        AddCategory(L(PropertiesLanguage.TaskInformation));
        AddReadOnly("document.id", "ID", document.Id);
        AddReadOnly("document.nodes", L(PropertiesLanguage.Nodes), document.Nodes.Count.ToString(CultureInfo.CurrentCulture));
        AddReadOnly("document.edges", L(PropertiesLanguage.Edges), document.Edges.Count.ToString(CultureInfo.CurrentCulture));
        AddReadOnly("document.annotations", L(PropertiesLanguage.Annotations), document.Annotations.Count.ToString(CultureInfo.CurrentCulture));
        AddReadOnly("document.breakpoints", L(PropertiesLanguage.Breakpoint), _editorContext.BreakpointNodeIds.Count.ToString(CultureInfo.CurrentCulture));
        AddReadOnly("document.runtimeStatus", L(PropertiesLanguage.RuntimeStatus), FormatRuntimeStatus(session.RuntimeStatus));
        AddReadOnly("document.saveStatus", L(PropertiesLanguage.SaveStatus), session.IsDirty ? L(PropertiesLanguage.Unsaved) : L(PropertiesLanguage.Saved));

        AddCategory(L(PropertiesLanguage.EditableProperties));
        AddText("document.title", L(PropertiesLanguage.TaskTitle), document.Title, _ => CommitDocument(document), ro);
        AddText("document.createdBy", L(PropertiesLanguage.CreatedBy), document.CreatedBy, _ => CommitDocument(document), ro);
        AddDateTime("document.createdAt", L(PropertiesLanguage.CreatedAt), document.CreatedAt, v => CommitDocument(document, createdAt: AsDateTime(v)), ro);
        AddText("document.description", L(PropertiesLanguage.Description), document.Description, _ => CommitDocument(document), ro);

        AddCategory(L(PropertiesLanguage.CanvasDisplay));
        var settings = _editorContext.CanvasSettings.Normalize();
        AddBoolean("canvas.gridVisible", L(PropertiesLanguage.GridVisible), settings.Grid.IsVisible, _ => CommitCanvas(document), ro);
        AddBoolean("canvas.gridSnap", L(PropertiesLanguage.GridSnapEnabled), settings.Grid.IsSnapEnabled, _ => CommitCanvas(document), ro);
        AddBoolean("canvas.guides", L(PropertiesLanguage.AlignmentGuidesEnabled), settings.Grid.IsGuidesEnabled, _ => CommitCanvas(document), ro);
        AddNumber("canvas.gridSpacing", L(PropertiesLanguage.GridSpacing), Convert.ToDouble(settings.Grid.Spacing), 1, 1000, 1, "0.##",
            v => CommitCanvas(document, gridSpacing: ToDouble(v)), ro);
        AddBoolean("canvas.minimap", L(PropertiesLanguage.MinimapVisible), settings.IsMinimapVisible, _ => CommitCanvas(document), ro);
        AddBoolean("canvas.lod", L(PropertiesLanguage.LevelOfDetailEnabled), settings.IsLevelOfDetailEnabled, _ => CommitCanvas(document), ro);
        AddBoolean("canvas.showPorts", L(PropertiesLanguage.ShowPorts), settings.ShowPorts, _ => CommitCanvas(document), ro);
        AddNumber("canvas.simplifiedThreshold", L(PropertiesLanguage.SimplifiedRenderThreshold),
            ToPercent(settings.LodSimplifiedZoomThreshold), 10, 300, 5, "0", v => CommitCanvas(document, simplified: ToDouble(v)), ro);
        AddNumber("canvas.labelThreshold", L(PropertiesLanguage.LabelVisibilityThreshold),
            ToPercent(settings.LodLabelsZoomThreshold), 10, 300, 5, "0", v => CommitCanvas(document, labels: ToDouble(v)), ro);
        AddNumber("canvas.portThreshold", L(PropertiesLanguage.PortVisibilityThreshold),
            ToPercent(settings.LodPortsZoomThreshold), 10, 300, 5, "0", v => CommitCanvas(document, ports: ToDouble(v)), ro);
        AddLabelVisibilityChoice("canvas.portLabelVisibility", L(PropertiesLanguage.PortLabelVisibility),
            settings.PortLabelVisibility, _ => CommitCanvas(document), ro);
        AddLabelVisibilityChoice("canvas.symbolNameVisibility", L(PropertiesLanguage.SymbolNameVisibility),
            settings.SymbolNameVisibility, _ => CommitCanvas(document), ro);
        AddLabelVisibilityChoice("canvas.symbolDescriptionVisibility", L(PropertiesLanguage.SymbolDescriptionVisibility),
            settings.SymbolDescriptionVisibility, _ => CommitCanvas(document), ro);
        return _items;
    }

    public IReadOnlyList<PropertyItem> BuildForNodeSelection(IReadOnlyList<NodeModel> nodes)
    {
        AddCategory(L(PropertiesLanguage.SelectionInformation));
        AddReadOnly("selection.count", L(PropertiesLanguage.SelectedComponents),
            F(PropertiesLanguage.ComponentCount, nodes.Count));
        AddReadOnly("selection.summary", string.Empty, BuildMemberSummary(
            nodes.Select(static node => string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name).ToList(),
            PropertiesLanguage.MoreNodes));
        AddReadOnly("selection.hint", string.Empty, L(PropertiesLanguage.BatchSelectionHint));
        return _items;
    }

    public IReadOnlyList<PropertyItem> BuildForGroupSelection(IReadOnlyList<GroupModel> groups)
    {
        AddCategory(L(PropertiesLanguage.SelectionInformation));
        AddReadOnly("groupSelection.count", L(PropertiesLanguage.SelectedGroups),
            F(PropertiesLanguage.GroupCount, groups.Count));
        AddReadOnly("groupSelection.summary", string.Empty, BuildMemberSummary(
            groups.Select(static group => string.IsNullOrWhiteSpace(group.Title) ? group.Id : group.Title).ToList(),
            PropertiesLanguage.MoreGroups));
        AddReadOnly("groupSelection.hint", string.Empty, L(PropertiesLanguage.BatchGroupSelectionHint));
        return _items;
    }

    public IReadOnlyList<PropertyItem> BuildForEmpty()
    {
        AddCategory(L(PropertiesLanguage.Properties));
        AddReadOnly("empty.info", string.Empty, L(PropertiesLanguage.NoActiveDocument));
        return _items;
    }

    /* ---------------- Node commits ---------------- */

    private void CommitNodeBasics(
        NodeModel node,
        string? number = null,
        string? name = null,
        string? description = null)
    {
        var command = GraphPropertyCommandFactory.CreateNodePropertiesCommand(
            node,
            number ?? node.Number,
            name ?? node.Name,
            description ?? node.Description,
            node.Position,
            node.Rotation);
        CommitCommand(command, L(PropertiesLanguage.NodeUpdated));
    }

    private void CommitNodePosition(NodeModel node, double? x = null, double? y = null)
    {
        var command = GraphPropertyCommandFactory.CreateNodePropertiesCommand(
            node,
            node.Number,
            node.Name,
            node.Description,
            new GraphPoint(x ?? node.Position.X, y ?? node.Position.Y),
            node.Rotation);
        if (command is null)
        {
            return;
        }

        if (!_editorContext.Execute(command))
        {
            _editorContext.SetMessage(L(PropertiesLanguage.InvalidCoordinates));
        }
        else
        {
            Notify(L(PropertiesLanguage.NodeUpdated));
        }
    }

    private void CommitNodeSize(NodeModel node, double? width = null, double? height = null)
    {
        var w = width ?? node.Size.Width;
        var h = height ?? node.Size.Height;
        if (!double.IsFinite(w) || !double.IsFinite(h) || w <= 0 || h <= 0)
        {
            _editorContext.SetMessage(L(PropertiesLanguage.InvalidSize));
            return;
        }

        if (node.Size.Width == w && node.Size.Height == h)
        {
            return;
        }

        var command = new ResizeNodeCommand(
            node.Id, node.Position, node.Size, node.Position, new GraphSize(w, h), L(PropertiesLanguage.Size));
        if (_editorContext.Execute(command))
        {
            Notify(L(PropertiesLanguage.NodeUpdated));
        }
    }

    private void CommitNodeLock(NodeModel node, bool isLocked)
    {
        var command = GraphPropertyCommandFactory.CreateNodeLockCommand(node, isLocked);
        if (command is not null && _editorContext.Execute(command))
        {
            Notify(L(PropertiesLanguage.NodeUpdated));
        }
    }

    private void CommitAppearance(NodeModel node, Func<GraphNodeAppearance, GraphNodeAppearance> update)
    {
        var appearance = update(node.Appearance.Normalize());
        var command = GraphPropertyCommandFactory.CreateNodeAppearanceCommand(node, appearance);
        if (command is null || !_editorContext.Execute(command))
        {
            return;
        }

        Notify(L(PropertiesLanguage.NodeAppearanceUpdated));
    }

    private IGraphCommand? PreviewNodeBackgroundOpacity(NodeModel node, double percent)
    {
        if (percent is < 0 or > 100)
        {
            _editorContext.SetMessage(L(PropertiesLanguage.InvalidBackgroundOpacity));
            return null;
        }

        if (!_previewSnapshots.ContainsKey(BackgroundOpacityKey))
        {
            _previewSnapshots[BackgroundOpacityKey] = node.Appearance.Normalize();
        }

        var old = (GraphNodeAppearance)_previewSnapshots[BackgroundOpacityKey];
        var preview = old with { Opacity = percent / 100d };
        return GraphPropertyCommandFactory.CreateNodeAppearanceCommand(node, preview);
    }

    private void CommitNodeBackgroundOpacity(NodeModel node, double percent)
    {
        if (percent is < 0 or > 100)
        {
            _editorContext.SetMessage(L(PropertiesLanguage.InvalidBackgroundOpacity));
            return;
        }

        var old = _previewSnapshots.TryGetValue(BackgroundOpacityKey, out var value)
            ? (GraphNodeAppearance)value
            : node.Appearance.Normalize();
        _previewSnapshots.Remove(BackgroundOpacityKey);
        var command = GraphPropertyCommandFactory.CreateNodeAppearanceCommand(node.Id, old, node.Appearance);
        if (command is null || !_editorContext.TryCommitPreview(command))
        {
            return;
        }

        Notify(L(PropertiesLanguage.NodeAppearanceUpdated));
    }

    private const string BackgroundOpacityKey = "node.backgroundOpacity";

    /* ---------------- Edge/annotation/group/document commits ---------------- */

    private void CommitEdge(EdgeModel edge)
    {
        var command = GraphPropertyCommandFactory.CreateEdgePropertiesCommand(
            edge,
            NormalizeOptionalText(edge.Label),
            edge.RoutingStyle,
            edge.StylePreset,
            edge.SourceMarker,
            edge.TargetMarker);
        if (command is null)
        {
            return;
        }

        if (_editorContext.Execute(command))
        {
            Notify(L(PropertiesLanguage.EdgeUpdated));
        }
        else
        {
            _editorContext.SetMessage(L(PropertiesLanguage.EdgeMissing));
        }
    }

    private void CommitAnnotation(
        AnnotationModel annotation,
        string? text = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        var nextText = text ?? annotation.Text;
        var position = new GraphPoint(x ?? annotation.Position.X, y ?? annotation.Position.Y);
        var size = new GraphSize(width ?? annotation.Size.Width, height ?? annotation.Size.Height);
        var props = properties ?? CloneDictionary(annotation.Properties);
        if (position.X < 0 || position.Y < 0 || size.Width <= 0 || size.Height <= 0)
        {
            _editorContext.SetMessage(L(PropertiesLanguage.InvalidAnnotationBounds));
            return;
        }

        var command = GraphPropertyCommandFactory.CreateAnnotationPropertiesCommand(
            annotation, nextText, position, size, props);
        if (command is null)
        {
            return;
        }

        if (_editorContext.Execute(command))
        {
            Notify(L(PropertiesLanguage.AnnotationUpdated));
        }
        else
        {
            _editorContext.SetMessage(L(PropertiesLanguage.AnnotationMissing));
        }
    }

    private void CommitGroup(
        GroupModel group,
        string? title = null,
        double? padding = null,
        bool? collapsed = null,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        var nextPadding = padding ?? group.Padding;
        if (nextPadding < 0)
        {
            _editorContext.SetMessage(L(PropertiesLanguage.InvalidGroupPadding));
            return;
        }

        var props = properties ?? CloneDictionary(group.Properties);
        var command = GraphPropertyCommandFactory.CreateGroupPropertiesCommand(
            group,
            title ?? group.Title,
            nextPadding,
            collapsed ?? group.IsCollapsed,
            props);
        if (command is null)
        {
            return;
        }

        if (_editorContext.Execute(command))
        {
            Notify(L(PropertiesLanguage.GroupUpdated));
        }
        else
        {
            _editorContext.SetMessage(L(PropertiesLanguage.GroupMissing));
        }
    }

    private void CommitDocument(
        GraphDocument document,
        DateTime? createdAt = null)
    {
        if (createdAt is null && document.CreatedAt == default)
        {
            _editorContext.SetMessage(L(PropertiesLanguage.InvalidCreatedAt));
            return;
        }

        var command = GraphPropertyCommandFactory.CreateDocumentPropertiesCommand(
            document,
            document.Title,
            document.CreatedBy,
            createdAt ?? document.CreatedAt,
            document.Description);
        if (command is null || !_editorContext.Execute(command))
        {
            return;
        }

        Notify(L(PropertiesLanguage.TaskTitleUpdated));
    }

    private void CommitCanvas(
        GraphDocument document,
        double? gridSpacing = null,
        double? simplified = null,
        double? labels = null,
        double? ports = null)
    {
        if (gridSpacing is < 1 or > 1000 ||
            simplified is < 10 or > 300 ||
            labels is < 10 or > 300 ||
            ports is < 10 or > 300)
        {
            _editorContext.SetMessage(L(PropertiesLanguage.InvalidCanvasDisplaySettings));
            return;
        }

        var settings = document.CanvasSettings.Normalize();
        var updated = settings with
        {
            Grid = settings.Grid with
            {
                IsVisible = GetCanvasBool("canvas.gridVisible", settings.Grid.IsVisible),
                IsSnapEnabled = GetCanvasBool("canvas.gridSnap", settings.Grid.IsSnapEnabled),
                IsGuidesEnabled = GetCanvasBool("canvas.guides", settings.Grid.IsGuidesEnabled),
                Spacing = gridSpacing ?? settings.Grid.Spacing
            },
            IsMinimapVisible = GetCanvasBool("canvas.minimap", settings.IsMinimapVisible),
            IsLevelOfDetailEnabled = GetCanvasBool("canvas.lod", settings.IsLevelOfDetailEnabled),
            ShowPorts = GetCanvasBool("canvas.showPorts", settings.ShowPorts),
            LodSimplifiedZoomThreshold = (simplified ?? settings.LodSimplifiedZoomThreshold * 100d) / 100d,
            LodLabelsZoomThreshold = (labels ?? settings.LodLabelsZoomThreshold * 100d) / 100d,
            LodPortsZoomThreshold = (ports ?? settings.LodPortsZoomThreshold * 100d) / 100d,
            PortLabelVisibility = GetCanvasEnum<GraphLabelVisibility>("canvas.portLabelVisibility", settings.PortLabelVisibility),
            SymbolNameVisibility = GetCanvasEnum<GraphLabelVisibility>("canvas.symbolNameVisibility", settings.SymbolNameVisibility),
            SymbolDescriptionVisibility = GetCanvasEnum<GraphLabelVisibility>("canvas.symbolDescriptionVisibility", settings.SymbolDescriptionVisibility)
        };
        var command = GraphPropertyCommandFactory.CreateCanvasSettingsCommand(document, updated);
        if (command is null || !_editorContext.Execute(command))
        {
            return;
        }

        Notify(L(PropertiesLanguage.CanvasDisplayUpdated));
    }

    private bool GetCanvasBool(string itemId, bool fallback)
    {
        var item = _items.FirstOrDefault(candidate => candidate.Id == itemId);
        return item?.Value as bool? ?? fallback;
    }

    private T GetCanvasEnum<T>(string itemId, T fallback)
        where T : struct, Enum
    {
        var item = _items.FirstOrDefault(candidate => candidate.Id == itemId);
        return item?.Value is string display && TryParseLabelVisibility(display, out var parsed)
            ? (T)(object)parsed
            : fallback;
    }

    private static bool TryParseLabelVisibility(string display, out GraphLabelVisibility value)
    {
        foreach (var candidate in Enum.GetValues<GraphLabelVisibility>())
        {
            if (FormatLabelVisibility(candidate) == display)
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }

    /* ---------------- Extension properties (symbol parameters) ---------------- */

    private void AddDefinitionProperty(NodeModel node, GraphNodePropertyDefinition property)
    {
        var ro = _editorContext.IsReadOnly || property.IsReadOnly;
        var id = $"ext.{property.Name}";
        var label = string.IsNullOrEmpty(property.Unit)
            ? property.DisplayName
            : $"{property.DisplayName} ({property.Unit})";
        var current = node.Properties.TryGetValue(property.Name, out var stored)
            ? stored
            : property.DefaultValue;

        switch (property.ValueKind)
        {
            case GraphNodePropertyValueKind.Boolean:
                AddBoolean(id, label, ToBool(current), v => ApplyExtensionProperty(node, property, AsBool(v)), ro);
                break;
            case GraphNodePropertyValueKind.Choice:
            {
                // options: value to display name
                var options = BuildChoiceOptions(property);
                var displays = options.Values.Distinct().ToArray();
                var currentText = FormatPropertyValue(current);
                var currentDisplay = options.TryGetValue(currentText, out var mapped)
                    ? mapped
                    : currentText;
                if (options.Count == 0)
                {
                    AddText(id, label, currentText, v => ApplyExtensionProperty(node, property, AsString(v)), ro, watermark: property.DisplayName);
                }
                else if (property.Editor == GraphNodePropertyEditorKind.RadioButton)
                {
                    AddRadio(id, label, displays, currentDisplay,
                        v => ApplyExtensionProperty(node, property, ResolveChoiceValue(v, options)), ro);
                }
                else
                {
                    AddChoice(id, label, displays, currentDisplay,
                        v => ApplyExtensionProperty(node, property, ResolveChoiceValue(v, options)), ro);
                }

                break;
            }
            case GraphNodePropertyValueKind.Integer:
                AddNumber(id, label, ToDouble(current),
                    AsNullableDouble(property.Minimum, -1_000_000_000d), AsNullableDouble(property.Maximum, 1_000_000_000d),
                    AsStep(property.Step, 1), "0",
                    v => ApplyExtensionProperty(node, property, (int)Math.Round(ToDouble(v), MidpointRounding.AwayFromZero)), ro);
                break;
            case GraphNodePropertyValueKind.Number:
                AddNumber(id, label, ToDouble(current),
                    AsNullableDouble(property.Minimum, null), AsNullableDouble(property.Maximum, null),
                    AsStep(property.Step, 0.1), "0.###",
                    v => ApplyExtensionProperty(node, property, ToDouble(v)), ro);
                break;
            case GraphNodePropertyValueKind.Percent:
                AddPercent(id, label, ToDouble(current),
                    AsNullableDouble(property.Minimum, 0d) ?? 0d, AsNullableDouble(property.Maximum, 100d) ?? 100d,
                    v => PreviewExtensionProperty(node, property, ToDouble(v), id),
                    v => CommitExtensionPropertyPercent(node, property, ToDouble(v), id), ro);
                break;
            case GraphNodePropertyValueKind.Color:
                AddColor(id, label, FormatPropertyValue(current) is { Length: > 0 } color ? color : null,
                    v => ApplyExtensionProperty(node, property, string.IsNullOrWhiteSpace(AsString(v)) ? null : AsString(v)), ro);
                break;
            default:
                AddText(id, label, FormatPropertyValue(current), v => ApplyExtensionProperty(node, property, AsString(v)), ro, watermark: property.DisplayName);
                break;
        }
    }

    private void ApplyExtensionProperty(NodeModel node, GraphNodePropertyDefinition property, object? value)
    {
        var target = FindNode(node.Id);
        if (target is null)
        {
            _editorContext.SetMessage(L(PropertiesLanguage.NodeMissing));
            return;
        }

        var properties = CloneDictionary(target.Properties);
        properties[property.Name] = NormalizeDefinitionValue(property, value);
        var command = GraphPropertyCommandFactory.CreateNodeExtensionPropertiesCommand(target, properties);
        if (command is null)
        {
            return;
        }

        if (_editorContext.Execute(command))
        {
            Notify(L(PropertiesLanguage.NodeSymbolPropertiesUpdated));
        }
    }

    private IGraphCommand? PreviewExtensionProperty(NodeModel node, GraphNodePropertyDefinition property, double percent, string itemId)
    {
        if (!_previewSnapshots.ContainsKey(itemId))
        {
            _previewSnapshots[itemId] = CloneDictionary(node.Properties);
        }

        var snapshot = (Dictionary<string, object?>)_previewSnapshots[itemId];
        var previewProps = CloneDictionary(snapshot);
        previewProps[property.Name] = Math.Round(ClampDefinition(property, percent), 2);
        return GraphPropertyCommandFactory.CreateNodeExtensionPropertiesCommand(
            node.Id, CloneDictionary(node.Properties), previewProps);
    }

    private void CommitExtensionPropertyPercent(NodeModel node, GraphNodePropertyDefinition property, double percent, string itemId)
    {
        var snapshot = _previewSnapshots.TryGetValue(itemId, out var value)
            ? (Dictionary<string, object?>)value
            : CloneDictionary(node.Properties);
        _previewSnapshots.Remove(itemId);
        var currentProps = CloneDictionary(node.Properties);
        currentProps[property.Name] = Math.Round(ClampDefinition(property, percent), 2);
        var command = GraphPropertyCommandFactory.CreateNodeExtensionPropertiesCommand(node.Id, snapshot, currentProps);
        if (command is null || !_editorContext.TryCommitPreview(command))
        {
            return;
        }

        Notify(L(PropertiesLanguage.NodeSymbolPropertiesUpdated));
    }

    private static double ClampDefinition(GraphNodePropertyDefinition property, double value)
    {
        var min = AsNullableDouble(property.Minimum,
            property.ValueKind == GraphNodePropertyValueKind.Percent ? 0d : double.MinValue) ?? double.MinValue;
        var max = AsNullableDouble(property.Maximum,
            property.ValueKind == GraphNodePropertyValueKind.Percent ? 100d : double.MaxValue) ?? double.MaxValue;
        return Math.Clamp(value, min, max);
    }

    private static double? AsNullableDouble(decimal? value, double? fallback)
        => value.HasValue ? decimal.ToDouble(value.Value) : fallback;

    private static double AsStep(decimal? step, double fallback)
        => step.HasValue && decimal.ToDouble(step.Value) > 0 ? decimal.ToDouble(step.Value) : fallback;

    private static object? NormalizeDefinitionValue(GraphNodePropertyDefinition property, object? value)
    {
        return property.ValueKind switch
        {
            GraphNodePropertyValueKind.Integer => Convert.ToInt32(ToDouble(value), CultureInfo.InvariantCulture),
            GraphNodePropertyValueKind.Number => ToDouble(value),
            GraphNodePropertyValueKind.Percent => ToDouble(value),
            GraphNodePropertyValueKind.Boolean => AsBool(value),
            GraphNodePropertyValueKind.Color => string.IsNullOrWhiteSpace(AsString(value)) ? null : AsString(value),
            _ => AsString(value) ?? string.Empty
        };
    }

    private NodeModel? FindNode(string nodeId)
    {
        var document = _editorContext.Session.Document;
        NodeModel? result = null;
        foreach (var node in document.Nodes)
        {
            if (node.Id == nodeId)
            {
                result = node;
                break;
            }
        }

        return result;
    }

    /* ---------------- Item building helpers ---------------- */

    private void AddCategory(string title)
    {
        _items.Add(new PropertyItem
        {
            Id = $"category.{_sequence++}",
            Kind = PropertyItemKind.Category,
            Name = title
        });
    }

    private PropertyItem AddItem(PropertyItem item)
    {
        // 空文档态（BuildForEmpty）没有编辑上下文，此时只列出只读占位项。
        if (_editorContext?.IsReadOnly == true)
        {
            item.IsReadOnly = true;
        }

        _items.Add(item);
        return item;
    }

    private PropertyItem AddReadOnly(string id, string label, string? value)
        => AddItem(new PropertyItem
        {
            Id = Unique(id), Kind = PropertyItemKind.ReadOnly, Name = label, Value = value
        });

    private PropertyItem AddText(string id, string label, string? value, Action<string?> commit,
        bool readOnly = false, string? watermark = null, bool commitOnEnter = true)
    {
        var item = AddItem(new PropertyItem
        {
            Id = Unique(id), Kind = PropertyItemKind.Text, Name = label,
            Value = value, DefaultValue = value, IsReadOnly = readOnly, Watermark = watermark,
            CommitMode = commitOnEnter ? PropertyCommitMode.FocusLoss : PropertyCommitMode.Immediate
        });
        _commitHandlers[GetItemId(item)] = (_, _) => commit(AsString(item.Value));
        return item;
    }

    private PropertyItem AddMultiline(string id, string label, string? value, Action<string?> commit,
        bool readOnly = false, bool commitOnEnter = true)
    {
        var item = AddItem(new PropertyItem
        {
            Id = Unique(id), Kind = PropertyItemKind.MultilineText, Name = label,
            Value = value, DefaultValue = value, IsReadOnly = readOnly,
            CommitMode = commitOnEnter ? PropertyCommitMode.FocusLoss : PropertyCommitMode.Immediate
        });
        _commitHandlers[GetItemId(item)] = (_, _) => commit(AsString(item.Value));
        return item;
    }

    private PropertyItem AddNumber(string id, string label, double value,
        double? minimum, double? maximum, double increment, string? format,
        Action<object?> commit, bool readOnly = false)
    {
        var item = AddItem(new PropertyItem
        {
            Id = Unique(id), Kind = PropertyItemKind.Number, Name = label,
            Value = Convert.ToDecimal(value), DefaultValue = Convert.ToDecimal(value),
            Minimum = minimum, Maximum = maximum, Increment = increment, Format = format,
            IsReadOnly = readOnly
        });
        _commitHandlers[GetItemId(item)] = (newValue, _) => commit(newValue);
        return item;
    }

    private PropertyItem AddBoolean(string id, string label, bool value, Action<object?> commit, bool readOnly = false)
    {
        var item = AddItem(new PropertyItem
        {
            Id = Unique(id), Kind = PropertyItemKind.Boolean, Name = label,
            Value = value, DefaultValue = value, IsReadOnly = readOnly
        });
        _commitHandlers[GetItemId(item)] = (newValue, _) => commit(newValue);
        return item;
    }

    private PropertyItem AddDateTime(string id, string label, DateTime value, Action<object?> commit, bool readOnly = false)
    {
        var item = AddItem(new PropertyItem
        {
            Id = Unique(id), Kind = PropertyItemKind.DateTime, Name = label,
            Value = value, DefaultValue = value, IsReadOnly = readOnly
        });
        _commitHandlers[GetItemId(item)] = (newValue, _) => commit(newValue);
        return item;
    }

    /// <summary>Enum dropdown: options map localized display to enum value.</summary>
    private void AddEnumChoice<T>(string id, string label, IReadOnlyDictionary<string, T> options, T current,
        Action<T> commit, bool readOnly = false, Action? onCommitted = null, Func<bool>? visible = null)
        where T : struct, Enum
    {
        var currentDisplay = options.FirstOrDefault(pair => EqualityComparer<T>.Default.Equals(pair.Value, current)).Key
            ?? options.Keys.FirstOrDefault() ?? string.Empty;
        AddChoiceCore(id, label, options.ToDictionary(static pair => pair.Key, static pair => (object?)pair.Value),
            currentDisplay,
            v =>
            {
                if (v is string display && options.TryGetValue(display, out var parsed))
                {
                    commit(parsed);
                }
            }, readOnly, onCommitted, visible);
    }

    private PropertyItem AddChoice(string id, string label, IReadOnlyList<string> options, string current,
        Action<string?> commit, bool readOnly = false, Action? onCommitted = null, Func<bool>? visible = null)
    {
        var item = AddChoiceCore(id, label, options.ToDictionary(static option => option, static option => (object?)option),
            options.Contains(current) ? current : options.FirstOrDefault() ?? string.Empty,
            v => commit(v), readOnly, onCommitted, visible);
        return item;
    }

    /// <summary>Group state (bool inverted to expanded/collapsed).</summary>
    private void AddStateChoice(string id, string label, bool isCollapsed,
        Action<bool> commit, bool readOnly = false)
    {
        var options = new Dictionary<string, bool>
        {
            [L(PropertiesLanguage.Expanded)] = false,
            [L(PropertiesLanguage.Collapsed)] = true
        };
        var currentDisplay = isCollapsed ? L(PropertiesLanguage.Collapsed) : L(PropertiesLanguage.Expanded);
        AddChoiceCore(id, label, options.ToDictionary(static pair => pair.Key, static pair => (object?)pair.Value),
            currentDisplay,
            v =>
            {
                if (v is string display && options.TryGetValue(display, out var collapsed))
                {
                    commit(collapsed);
                }
            }, readOnly);
    }

    private PropertyItem AddLabelVisibilityChoice(string id, string label, GraphLabelVisibility? current,
        Action<GraphLabelVisibility?> commit, bool readOnly = false, Func<bool>? visible = null)
    {
        var options = new Dictionary<string, GraphLabelVisibility?>
        {
            [L(PropertiesLanguage.UseCanvasSetting)] = null
        };
        foreach (var candidate in Enum.GetValues<GraphLabelVisibility>())
        {
            options[FormatLabelVisibility(candidate)] = candidate;
        }

        var currentDisplay = current is { } value ? FormatLabelVisibility(value) : L(PropertiesLanguage.UseCanvasSetting);
        var item = AddChoiceCore(id, label, options.ToDictionary(static pair => pair.Key, static pair => (object?)pair.Value),
            currentDisplay,
            v => commit(ParseLabelVisibility(v, current)), readOnly, visible: visible);
        return item;
    }

    private PropertyItem AddChoiceCore(string id, string label, IReadOnlyDictionary<string, object?> options,
        string currentDisplay, Action<string?> commit, bool readOnly = false,
        Action? onCommitted = null, Func<bool>? visible = null)
    {
        var optionList = options.Keys.ToArray();
        var value = options.ContainsKey(currentDisplay) ? currentDisplay : optionList.Length > 0 ? optionList[0] : string.Empty;
        var item = AddItem(new PropertyItem
        {
            Id = Unique(id), Kind = PropertyItemKind.Choice, Name = label,
            Value = value, DefaultValue = value, Options = optionList, IsReadOnly = readOnly
        });
        if (visible is not null)
        {
            item.VisibleWhen = visible;
        }

        _commitHandlers[GetItemId(item)] = (_, _) =>
        {
            commit(AsString(item.Value));
            onCommitted?.Invoke();
        };
        return item;
    }

    private PropertyItem AddRadio(string id, string label, IReadOnlyList<string> options, string current,
        Action<string?> commit, bool readOnly = false)
    {
        var item = AddItem(new PropertyItem
        {
            Id = Unique(id), Kind = PropertyItemKind.Radio, Name = label,
            Value = options.Contains(current) ? current : options.FirstOrDefault() ?? string.Empty,
            DefaultValue = current, Options = options, IsReadOnly = readOnly
        });
        _commitHandlers[GetItemId(item)] = (_, _) => commit(AsString(item.Value));
        return item;
    }

    private PropertyItem AddPercent(string id, string label, double value,
        double minimum, double maximum,
        Func<object?, IGraphCommand?> preview, Action<object?> commit,
        bool readOnly = false, Func<bool>? visible = null)
    {
        var item = AddItem(new PropertyItem
        {
            Id = Unique(id), Kind = PropertyItemKind.Percent, Name = label,
            Value = value, DefaultValue = value, Minimum = minimum, Maximum = maximum,
            IsReadOnly = readOnly
        });
        if (visible is not null)
        {
            item.VisibleWhen = visible;
        }

        _previewHandlers[GetItemId(item)] = preview;
        _commitHandlers[GetItemId(item)] = (newValue, _) => commit(newValue);
        return item;
    }

    private PropertyItem AddColor(string id, string label, string? value, Action<string?> commit, bool readOnly = false)
    {
        var item = AddItem(new PropertyItem
        {
            Id = Unique(id), Kind = PropertyItemKind.Color, Name = label,
            Value = value, DefaultValue = value, IsReadOnly = readOnly
        });
        _commitHandlers[GetItemId(item)] = (_, _) => commit(AsString(item.Value));
        return item;
    }

    private PropertyItem AddCustom(string id, string label, Func<bool>? visible = null)
    {
        var item = AddItem(new PropertyItem
        {
            Id = Unique(id), Kind = PropertyItemKind.ReadOnly, Name = label
        });
        if (visible is not null)
        {
            item.VisibleWhen = visible;
        }

        _customEditors[GetItemId(item)] = BuildCustomEditorControl(id, item);
        return item;
    }

    /// <summary>Background image row / provider native control host.</summary>
    private Control BuildCustomEditorControl(string logicalId, PropertyItem item)
    {
        if (logicalId == ProviderItemId)
        {
            return ProviderContent ?? new StackPanel();
        }

        if (logicalId == BackgroundImageItemId)
        {
            var node = Context.Node;
            var imagePath = node?.Appearance?.BackgroundImage;
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VAlignment.Center
            };
            var pathText = new TextBlock
            {
                Classes = { "pg-readonly" },
                Text = string.IsNullOrWhiteSpace(imagePath)
                    ? L(PropertiesLanguage.NoBackgroundImage)
                    : System.IO.Path.GetFileName(imagePath),
                VerticalAlignment = VAlignment.Center,
                MaxWidth = 220,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            if (!string.IsNullOrWhiteSpace(imagePath))
            {
                ToolTip.SetTip(pathText, imagePath);
            }

            var selectButton = new Button
            {
                Content = L(PropertiesLanguage.SelectBackgroundImage),
                IsEnabled = !_editorContext.IsReadOnly && node is not null,
                VerticalAlignment = VAlignment.Center
            };
            selectButton.Click += (_, _) =>
            {
                if (Context.Node is { } target)
                {
                    _notifyBackgroundImageRequested(new GraphNodeBackgroundImageRequest(_editorContext, target.Id));
                }
            };
            var clearButton = new Button
            {
                Content = L(PropertiesLanguage.ClearBackgroundImage),
                IsEnabled = !_editorContext.IsReadOnly && !string.IsNullOrWhiteSpace(imagePath),
                VerticalAlignment = VAlignment.Center
            };
            clearButton.Click += (_, _) =>
            {
                if (Context.Node is { } target)
                {
                    CommitAppearance(target, appearance => appearance with { BackgroundImage = null });
                    _refreshPanel();
                }
            };
            panel.Children.Add(pathText);
            panel.Children.Add(selectButton);
            panel.Children.Add(clearButton);
            return panel;
        }

        return new TextBlock { Text = item.Value?.ToString() ?? string.Empty };
    }

    /* ---------------- Two-phase preview/commit helpers ---------------- */

    private static Dictionary<string, object?> CloneDictionary(IReadOnlyDictionary<string, object?> source)
        => new(source);

    private static Dictionary<string, string> BuildChoiceOptions(GraphNodePropertyDefinition property)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        if (property.Options is { Count: > 0 })
        {
            foreach (var option in property.Options)
            {
                options.TryAdd(option, option);
            }
        }
        else if (property.ChoiceOptions is { Count: > 0 })
        {
            foreach (var choice in property.ChoiceOptions)
            {
                options.TryAdd(choice.Value, string.IsNullOrEmpty(choice.DisplayName) ? choice.Value : choice.DisplayName);
            }
        }

        return options;
    }

    private static string ResolveChoiceValue(string? display, Dictionary<string, string> options)
    {
        if (display is null)
        {
            return options.Keys.FirstOrDefault() ?? string.Empty;
        }

        // display is the display name; resolve back to the option value
        foreach (var pair in options)
        {
            if (pair.Value == display)
            {
                return pair.Key;
            }
        }

        return display;
    }

    /* ---------------- Enum/option formatting ---------------- */

    private static Dictionary<string, GraphNodeVisualMode> FormatNodeVisualModeOptions() => new()
    {
        [L(PropertiesLanguage.NodeVisualModeAuto)] = GraphNodeVisualMode.Auto,
        [L(PropertiesLanguage.NodeVisualModeCard)] = GraphNodeVisualMode.Card,
        [L(PropertiesLanguage.NodeVisualModeSymbol)] = GraphNodeVisualMode.Symbol,
        [L(PropertiesLanguage.NodeVisualModeText)] = GraphNodeVisualMode.Text
    };

    private static GraphNodeVisualMode ParseNodeVisualMode(string? display)
        => display is not null && FormatNodeVisualModeOptions().TryGetValue(display, out var value)
            ? value
            : GraphNodeVisualMode.Auto;

    private static Dictionary<string, GraphImageStretch> FormatImageStretchOptions() => new()
    {
        [L(PropertiesLanguage.ImageStretchNone)] = GraphImageStretch.None,
        [L(PropertiesLanguage.ImageStretchFill)] = GraphImageStretch.Fill,
        [L(PropertiesLanguage.ImageStretchUniform)] = GraphImageStretch.Uniform,
        [L(PropertiesLanguage.ImageStretchUniformToFill)] = GraphImageStretch.UniformToFill
    };

    private static GraphImageStretch ParseImageStretch(string? display)
        => display is not null && FormatImageStretchOptions().TryGetValue(display, out var value)
            ? value
            : GraphImageStretch.None;

    private static Dictionary<string, GraphFontWeight> FormatFontWeightOptions() => new()
    {
        [L(PropertiesLanguage.FontWeightNormal)] = GraphFontWeight.Normal,
        [L(PropertiesLanguage.FontWeightMedium)] = GraphFontWeight.Medium,
        [L(PropertiesLanguage.FontWeightSemiBold)] = GraphFontWeight.SemiBold,
        [L(PropertiesLanguage.FontWeightBold)] = GraphFontWeight.Bold
    };

    private static GraphFontWeight ParseFontWeight(string? display)
        => display is not null && FormatFontWeightOptions().TryGetValue(display, out var value)
            ? value
            : GraphFontWeight.Normal;

    private static Dictionary<string, EdgeRoutingStyle> FormatRoutingOptions() => new()
    {
        [L(PropertiesLanguage.RoutingDirect)] = EdgeRoutingStyle.Direct,
        [L(PropertiesLanguage.RoutingBezier)] = EdgeRoutingStyle.Bezier,
        [L(PropertiesLanguage.RoutingOrthogonal)] = EdgeRoutingStyle.Orthogonal,
        [L(PropertiesLanguage.RoutingManual)] = EdgeRoutingStyle.Manual,
        [L(PropertiesLanguage.RoutingSmartOrthogonal)] = EdgeRoutingStyle.SmartOrthogonal
    };

    private static Dictionary<string, GraphEdgeStylePreset> FormatStylePresetOptions() => new()
    {
        [L(PropertiesLanguage.StylePresetDefault)] = GraphEdgeStylePreset.Default,
        [L(PropertiesLanguage.StylePresetSuccess)] = GraphEdgeStylePreset.Success,
        [L(PropertiesLanguage.StylePresetWarning)] = GraphEdgeStylePreset.Warning,
        [L(PropertiesLanguage.StylePresetError)] = GraphEdgeStylePreset.Error,
        [L(PropertiesLanguage.StylePresetDisabled)] = GraphEdgeStylePreset.Disabled,
        [L(PropertiesLanguage.StylePresetActiveFlow)] = GraphEdgeStylePreset.ActiveFlow,
        [L(PropertiesLanguage.StylePresetHighlighted)] = GraphEdgeStylePreset.Highlighted,
        [L(PropertiesLanguage.StylePresetDataStream)] = GraphEdgeStylePreset.DataStream,
        [L(PropertiesLanguage.StylePresetBidirectional)] = GraphEdgeStylePreset.Bidirectional,
        [L(PropertiesLanguage.StylePresetReverseFlow)] = GraphEdgeStylePreset.ReverseFlow,
        [L(PropertiesLanguage.StylePresetElectric)] = GraphEdgeStylePreset.Electric,
        [L(PropertiesLanguage.StylePresetGentle)] = GraphEdgeStylePreset.Gentle,
        [L(PropertiesLanguage.StylePresetDebug)] = GraphEdgeStylePreset.Debug,
        [L(PropertiesLanguage.StylePresetRainbow)] = GraphEdgeStylePreset.Rainbow,
        [L(PropertiesLanguage.StylePresetPulse)] = GraphEdgeStylePreset.Pulse,
        [L(PropertiesLanguage.StylePresetHeartbeat)] = GraphEdgeStylePreset.Heartbeat,
        [L(PropertiesLanguage.StylePresetNeon)] = GraphEdgeStylePreset.Neon,
        [L(PropertiesLanguage.StylePresetSignal)] = GraphEdgeStylePreset.Signal,
        [L(PropertiesLanguage.StylePresetCustom)] = GraphEdgeStylePreset.Custom
    };

    private static Dictionary<string, EdgeMarker> FormatMarkerOptions() => new()
    {
        [L(PropertiesLanguage.MarkerNone)] = EdgeMarker.None,
        [L(PropertiesLanguage.MarkerArrow)] = EdgeMarker.Arrow,
        [L(PropertiesLanguage.MarkerCircle)] = EdgeMarker.Circle,
        [L(PropertiesLanguage.MarkerDiamond)] = EdgeMarker.Diamond
    };

    private static string FormatRuntimeStatus(RuntimeStatus status) => status switch
    {
        RuntimeStatus.Starting => L(PropertiesLanguage.RuntimeStarting),
        RuntimeStatus.Running => L(PropertiesLanguage.RuntimeRunning),
        RuntimeStatus.Paused => L(PropertiesLanguage.RuntimePaused),
        RuntimeStatus.Stopping => L(PropertiesLanguage.RuntimeStopping),
        RuntimeStatus.Faulted => L(PropertiesLanguage.RuntimeFaulted),
        _ => L(PropertiesLanguage.RuntimeStopped)
    };

    private static GraphLabelVisibility? ParseLabelVisibility(string? display, GraphLabelVisibility? fallback)
    {
        if (string.IsNullOrEmpty(display))
        {
            return fallback;
        }

        foreach (var candidate in Enum.GetValues<GraphLabelVisibility>())
        {
            if (FormatLabelVisibility(candidate) == display)
            {
                return candidate;
            }
        }

        return fallback;
    }

    private static string FormatLabelVisibility(GraphLabelVisibility visibility) => visibility switch
    {
        GraphLabelVisibility.Always => L(PropertiesLanguage.LabelVisibilityAlways),
        GraphLabelVisibility.SelectedOrHovered => L(PropertiesLanguage.LabelVisibilitySelectedOrHovered),
        GraphLabelVisibility.Hovered => L(PropertiesLanguage.LabelVisibilityHovered),
        GraphLabelVisibility.Never => L(PropertiesLanguage.LabelVisibilityNever),
        _ => L(PropertiesLanguage.LabelVisibilityAuto)
    };

    private static string BuildMemberSummary(IReadOnlyList<string> titles, string moreKey)
    {
        var taken = titles.Take(8).ToArray();
        var remaining = titles.Count - taken.Length;
        return remaining > 0
            ? F(moreKey, string.Join("\n", taken), remaining)
            : string.Join("\n", taken);
    }

    /* ---------------- General helpers ---------------- */

    private void Notify(string message)
    {
        _editorContext.SetMessage(message);
        _notifyEditCommitted(message);
    }

    private void CommitCommand(IGraphCommand? command, string message)
    {
        if (command is null || !_editorContext.Execute(command))
        {
            return;
        }

        Notify(message);
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);

    private string Unique(string id)
    {
        _commitHandlers.Remove(id);
        _previewHandlers.Remove(id);
        return id;
    }

    private static string AsString(object? value) => value as string ?? value?.ToString() ?? string.Empty;

    private static bool AsBool(object? value) => value as bool? ?? Equals(value, true);

    private static Control CombineControls(IReadOnlyList<Control> controls)
    {
        if (controls.Count == 1)
        {
            return controls[0];
        }

        var panel = new StackPanel { Spacing = 4 };
        foreach (var control in controls)
        {
            panel.Children.Add(control);
        }

        return panel;
    }

    private static string FormatPropertyValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => element.GetString() ?? string.Empty,
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                _ => element.GetRawText()
            };
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? string.Empty;
    }

    private static bool ToBool(object? value)
    {
        if (value is bool b)
        {
            return b;
        }

        if (value is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False } element)
        {
            return element.GetBoolean();
        }

        return bool.TryParse(value?.ToString(), out var parsed) && parsed;
    }

    private static double ToDouble(object? value) => value switch
    {
        double d => d,
        decimal m => decimal.ToDouble(m),
        IConvertible convertible => convertible.ToDouble(CultureInfo.InvariantCulture),
        _ => 0d
    };

    private static DateTime AsDateTime(object? value) => value switch
    {
        DateTime dt => dt,
        IConvertible convertible => convertible.ToDateTime(CultureInfo.InvariantCulture),
        _ => default
    };

    private static double ToPercent(double threshold) =>
        double.IsFinite(threshold) ? Math.Round(threshold * 100d, 0) : 100d;
}
