// 连线属性编辑器：负责路由、样式预设、端点标记和标签提交。
using System.Globalization;
using Avalonia.Controls;
using Kudo.Avalonia;
using static Kudo.Avalonia.Designer.GraphPropertyEditorControls;
using static Kudo.Avalonia.Designer.GraphPropertyValueConverter;
using PropertiesLanguage = global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView;

namespace Kudo.Avalonia.Designer;

internal static class GraphEdgePropertyEditor
{
    public static void Show(
        StackPanel content,
        GraphPropertyEditorContext editorContext,
        EdgeModel edge,
        bool showMarkerEditors,
        Action<string> notifyEditCommitted)
    {
        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.BasicInformation)));
        content.Children.Add(CreateReadOnlyField("ID", edge.Id));
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.Start),
            $"{edge.Source.NodeId}/{edge.Source.PortId}"));
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.End),
            $"{edge.Target.NodeId}/{edge.Target.PortId}"));
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.State),
            FormatEdgeState(edge.State)));
        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.EditableProperties)));
        var labelEditor = CreateTextEditor("EdgeLabelEditor", L(PropertiesLanguage.Label), edge.Label);
        var routingEditor = CreateEnumEditor(
            "EdgeRoutingEditor",
            edge.RoutingStyle,
            FormatRoutingStyle);
        var stylePresetEditor = CreateEnumEditor(
            "EdgeStylePresetEditor",
            edge.StylePreset,
            FormatEdgeStylePreset);
        var sourceMarkerEditor = CreateEnumEditor(
            "EdgeSourceMarkerEditor",
            edge.SourceMarker,
            FormatEdgeMarker);
        var targetMarkerEditor = CreateEnumEditor(
            "EdgeTargetMarkerEditor",
            edge.TargetMarker,
            FormatEdgeMarker);
        SetEditorReadOnly(labelEditor, editorContext.IsReadOnly);
        routingEditor.IsEnabled = !editorContext.IsReadOnly;
        stylePresetEditor.IsEnabled = !editorContext.IsReadOnly;
        sourceMarkerEditor.IsEnabled = !editorContext.IsReadOnly;
        targetMarkerEditor.IsEnabled = !editorContext.IsReadOnly;
        content.Children.Add(CreateField(L(PropertiesLanguage.Label), labelEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.Routing), routingEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.StylePreset), stylePresetEditor));
        if (showMarkerEditors)
        {
            content.Children.Add(CreateField(L(PropertiesLanguage.SourceMarker), sourceMarkerEditor));
            content.Children.Add(CreateField(L(PropertiesLanguage.TargetMarker), targetMarkerEditor));
        }
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.Waypoints),
            edge.Waypoints.Count.ToString(CultureInfo.CurrentCulture)));

        void Commit() => Apply(
            editorContext,
            edge.Id,
            labelEditor,
            routingEditor,
            stylePresetEditor,
            sourceMarkerEditor,
            targetMarkerEditor,
            notifyEditCommitted);
        AttachTextCommit(labelEditor, Commit);
        AttachSelectionCommit(routingEditor, Commit);
        AttachSelectionCommit(stylePresetEditor, Commit);
        if (showMarkerEditors)
        {
            AttachSelectionCommit(sourceMarkerEditor, Commit);
            AttachSelectionCommit(targetMarkerEditor, Commit);
        }
    }

    private static void Apply(
        GraphPropertyEditorContext editorContext,
        string edgeId,
        TextBox labelEditor,
        ComboBox routingEditor,
        ComboBox stylePresetEditor,
        ComboBox sourceMarkerEditor,
        ComboBox targetMarkerEditor,
        Action<string> notifyEditCommitted)
    {
        var edge = editorContext.Session.Document.Edges
            .FirstOrDefault(candidate => candidate.Id == edgeId);
        if (edge is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.EdgeMissing));
            return;
        }

        var command = GraphPropertyCommandFactory.CreateEdgePropertiesCommand(
            edge,
            NormalizeOptionalText(labelEditor.Text),
            GetEnumValue(routingEditor, edge.RoutingStyle),
            GetEnumValue(stylePresetEditor, edge.StylePreset),
            GetEnumValue(sourceMarkerEditor, edge.SourceMarker),
            GetEnumValue(targetMarkerEditor, edge.TargetMarker));
        if (command is null || !editorContext.Execute(command))
        {
            return;
        }

        var message = F(PropertiesLanguage.EdgeUpdated, edge.Id);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static string FormatEdgeState(EdgeState state) => state switch
    {
        EdgeState.Active => L(PropertiesLanguage.EdgeActive),
        EdgeState.Highlighted => L(PropertiesLanguage.EdgeHighlighted),
        EdgeState.Invalid => L(PropertiesLanguage.EdgeInvalid),
        EdgeState.Disabled => L(PropertiesLanguage.EdgeDisabled),
        _ => L(PropertiesLanguage.EdgeNormal)
    };

    private static string FormatRoutingStyle(EdgeRoutingStyle style) => style switch
    {
        EdgeRoutingStyle.Direct => L(PropertiesLanguage.RoutingDirect),
        EdgeRoutingStyle.Bezier => L(PropertiesLanguage.RoutingBezier),
        EdgeRoutingStyle.Orthogonal => L(PropertiesLanguage.RoutingOrthogonal),
        EdgeRoutingStyle.Manual => L(PropertiesLanguage.RoutingManual),
        _ => L(PropertiesLanguage.RoutingSmartOrthogonal)
    };

    private static string FormatEdgeMarker(EdgeMarker marker) => marker switch
    {
        EdgeMarker.Arrow => L(PropertiesLanguage.MarkerArrow),
        EdgeMarker.Circle => L(PropertiesLanguage.MarkerCircle),
        EdgeMarker.Diamond => L(PropertiesLanguage.MarkerDiamond),
        _ => L(PropertiesLanguage.MarkerNone)
    };

    private static string FormatEdgeStylePreset(GraphEdgeStylePreset preset) => preset switch
    {
        GraphEdgeStylePreset.Success => L(PropertiesLanguage.StylePresetSuccess),
        GraphEdgeStylePreset.Warning => L(PropertiesLanguage.StylePresetWarning),
        GraphEdgeStylePreset.Error => L(PropertiesLanguage.StylePresetError),
        GraphEdgeStylePreset.Disabled => L(PropertiesLanguage.StylePresetDisabled),
        GraphEdgeStylePreset.ActiveFlow => L(PropertiesLanguage.StylePresetActiveFlow),
        GraphEdgeStylePreset.Highlighted => L(PropertiesLanguage.StylePresetHighlighted),
        GraphEdgeStylePreset.DataStream => L(PropertiesLanguage.StylePresetDataStream),
        GraphEdgeStylePreset.Bidirectional => L(PropertiesLanguage.StylePresetBidirectional),
        GraphEdgeStylePreset.ReverseFlow => L(PropertiesLanguage.StylePresetReverseFlow),
        GraphEdgeStylePreset.Electric => L(PropertiesLanguage.StylePresetElectric),
        GraphEdgeStylePreset.Gentle => L(PropertiesLanguage.StylePresetGentle),
        GraphEdgeStylePreset.Debug => L(PropertiesLanguage.StylePresetDebug),
        GraphEdgeStylePreset.Rainbow => L(PropertiesLanguage.StylePresetRainbow),
        GraphEdgeStylePreset.Pulse => L(PropertiesLanguage.StylePresetPulse),
        GraphEdgeStylePreset.Heartbeat => L(PropertiesLanguage.StylePresetHeartbeat),
        GraphEdgeStylePreset.Neon => L(PropertiesLanguage.StylePresetNeon),
        GraphEdgeStylePreset.Signal => L(PropertiesLanguage.StylePresetSignal),
        GraphEdgeStylePreset.Custom => L(PropertiesLanguage.StylePresetCustom),
        _ => L(PropertiesLanguage.StylePresetDefault)
    };

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
