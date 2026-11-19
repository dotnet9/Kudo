using System.Windows.Input;
using Avalonia;
using Avalonia.Data;
using Kudo;
using Kudo.Core.Runtime;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public const string NodeTitleEditorPartName = "PART_NodeTitleEditor";

    public const string AnnotationTextEditorPartName = "PART_AnnotationTextEditor";

    public const string EdgeLabelEditorPartName = "PART_EdgeLabelEditor";

    public static readonly StyledProperty<GraphDocument?> DocumentProperty =
        AvaloniaProperty.Register<GraphCanvas, GraphDocument?>(nameof(Document));

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<GraphCanvas, bool>(
            nameof(IsReadOnly),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<GraphViewport> ViewportProperty =
        AvaloniaProperty.Register<GraphCanvas, GraphViewport>(
            nameof(Viewport),
            GraphViewport.Default,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: static (owner, value) =>
            {
                var canvas = (GraphCanvas)owner;
                return NormalizeViewport(value, canvas.ViewportOptions);
            });

    public static readonly StyledProperty<GraphViewportOptions> ViewportOptionsProperty =
        AvaloniaProperty.Register<GraphCanvas, GraphViewportOptions>(
            nameof(ViewportOptions),
            GraphViewportOptions.Default,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, value) => (value ?? GraphViewportOptions.Default).Normalize());

    public static readonly StyledProperty<GraphGridSettings> GridSettingsProperty =
        AvaloniaProperty.Register<GraphCanvas, GraphGridSettings>(
            nameof(GridSettings),
            GraphGridSettings.Default,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, value) => (value ?? GraphGridSettings.Default).Normalize());

    public static readonly StyledProperty<EdgeRoutingStyle> NewEdgeRoutingStyleProperty =
        AvaloniaProperty.Register<GraphCanvas, EdgeRoutingStyle>(
            nameof(NewEdgeRoutingStyle),
            EdgeRoutingStyle.Bezier);

    public static readonly StyledProperty<GraphRuntimeSnapshot?> RuntimeSnapshotProperty =
        AvaloniaProperty.Register<GraphCanvas, GraphRuntimeSnapshot?>(nameof(RuntimeSnapshot));

    public static readonly StyledProperty<IReadOnlyCollection<string>> BreakpointNodeIdsProperty =
        AvaloniaProperty.Register<GraphCanvas, IReadOnlyCollection<string>>(
            nameof(BreakpointNodeIds),
            Array.Empty<string>());

    public static readonly StyledProperty<string?> SelectedNodeIdProperty =
        AvaloniaProperty.Register<GraphCanvas, string?>(
            nameof(SelectedNodeId),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<GraphCanvas, IReadOnlyList<string>>
        SelectedNodeIdsProperty =
            AvaloniaProperty.RegisterDirect<GraphCanvas, IReadOnlyList<string>>(
                nameof(SelectedNodeIds),
                canvas => canvas.SelectedNodeIds);

    public static readonly DirectProperty<GraphCanvas, IReadOnlyList<string>>
        SelectedGroupIdsProperty =
            AvaloniaProperty.RegisterDirect<GraphCanvas, IReadOnlyList<string>>(
                nameof(SelectedGroupIds),
                canvas => canvas.SelectedGroupIds);

    public static readonly StyledProperty<string?> SelectedEdgeIdProperty =
        AvaloniaProperty.Register<GraphCanvas, string?>(
            nameof(SelectedEdgeId),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<GraphCanvas, IReadOnlyList<string>>
        SelectedEdgeIdsProperty =
            AvaloniaProperty.RegisterDirect<GraphCanvas, IReadOnlyList<string>>(
                nameof(SelectedEdgeIds),
                canvas => canvas.SelectedEdgeIds);

    public static readonly StyledProperty<ICommand?> SelectionChangedCommandProperty =
        AvaloniaProperty.Register<GraphCanvas, ICommand?>(nameof(SelectionChangedCommand));

    public static readonly StyledProperty<ICommand?> GraphEditCommittedCommandProperty =
        AvaloniaProperty.Register<GraphCanvas, ICommand?>(nameof(GraphEditCommittedCommand));

    public static readonly StyledProperty<ICommand?> GraphEditRejectedCommandProperty =
        AvaloniaProperty.Register<GraphCanvas, ICommand?>(nameof(GraphEditRejectedCommand));

    public static readonly StyledProperty<IGraphEditCoordinator> EditCoordinatorProperty =
        AvaloniaProperty.Register<GraphCanvas, IGraphEditCoordinator>(
            nameof(EditCoordinator),
            DirectGraphEditCoordinator.Instance,
            coerce: static (_, value) => value ?? DirectGraphEditCoordinator.Instance);

    public static readonly StyledProperty<ICommand?> ConnectionValidationChangedCommandProperty =
        AvaloniaProperty.Register<GraphCanvas, ICommand?>(nameof(ConnectionValidationChangedCommand));

    public static readonly StyledProperty<IConnectionValidationRule?> ConnectionValidationRuleProperty =
        AvaloniaProperty.Register<GraphCanvas, IConnectionValidationRule?>(nameof(ConnectionValidationRule));

    public static readonly StyledProperty<ConnectionValidationResult?> LastConnectionValidationProperty =
        AvaloniaProperty.Register<GraphCanvas, ConnectionValidationResult?>(nameof(LastConnectionValidation));

    public static readonly StyledProperty<bool> IsLevelOfDetailEnabledProperty =
        AvaloniaProperty.Register<GraphCanvas, bool>(nameof(IsLevelOfDetailEnabled), true);

    public static readonly StyledProperty<bool> ShowPortsProperty =
        AvaloniaProperty.Register<GraphCanvas, bool>(nameof(ShowPorts), true);

    public static readonly StyledProperty<double> LodPortsZoomThresholdProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(
            nameof(LodPortsZoomThreshold),
            0.4,
            coerce: static (_, value) => GraphCanvasLodSettings.NormalizeThreshold(value, 0.4));

    public static readonly StyledProperty<double> LodLabelsZoomThresholdProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(
            nameof(LodLabelsZoomThreshold),
            0.3,
            coerce: static (_, value) => GraphCanvasLodSettings.NormalizeThreshold(value, 0.3));

    public static readonly StyledProperty<double> LodSimplifiedZoomThresholdProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(
            nameof(LodSimplifiedZoomThreshold),
            0.4,
            coerce: static (_, value) => GraphCanvasLodSettings.NormalizeThreshold(value, 0.4));

    public static readonly StyledProperty<GraphLabelVisibility> PortLabelVisibilityProperty =
        AvaloniaProperty.Register<GraphCanvas, GraphLabelVisibility>(
            nameof(PortLabelVisibility),
            GraphLabelVisibility.Auto);

    public static readonly StyledProperty<GraphLabelVisibility> SymbolNameVisibilityProperty =
        AvaloniaProperty.Register<GraphCanvas, GraphLabelVisibility>(
            nameof(SymbolNameVisibility),
            GraphLabelVisibility.Never);

    public static readonly StyledProperty<GraphLabelVisibility> SymbolDescriptionVisibilityProperty =
        AvaloniaProperty.Register<GraphCanvas, GraphLabelVisibility>(
            nameof(SymbolDescriptionVisibility),
            GraphLabelVisibility.Never);
}
