using Avalonia;
using Avalonia.Media;

namespace Kudo.Avalonia;

#pragma warning disable AVP1001

public partial class GraphCanvas
{
    public static readonly StyledProperty<IBrush> CanvasBackgroundBrushProperty =
        RegisterBrush(nameof(CanvasBackgroundBrush), Colors.White);
    public static readonly StyledProperty<IBrush> NodeFillBrushProperty =
        RegisterBrush(nameof(NodeFillBrush), Colors.White);
    public static readonly StyledProperty<IBrush> NodeStrokeBrushProperty =
        RegisterBrush(nameof(NodeStrokeBrush), Colors.Gray);
    public static readonly StyledProperty<IBrush> SelectedNodeStrokeBrushProperty =
        RegisterBrush(nameof(SelectedNodeStrokeBrush), Colors.Blue);
    public static readonly StyledProperty<IBrush> GroupFillBrushProperty =
        RegisterBrush(nameof(GroupFillBrush), Colors.Transparent);
    public static readonly StyledProperty<IBrush> GroupHeaderFillBrushProperty =
        RegisterBrush(nameof(GroupHeaderFillBrush), Colors.Transparent);
    public static readonly StyledProperty<IBrush> GroupStrokeBrushProperty =
        RegisterBrush(nameof(GroupStrokeBrush), Colors.Gray);
    public static readonly StyledProperty<IBrush> SelectedGroupStrokeBrushProperty =
        RegisterBrush(nameof(SelectedGroupStrokeBrush), Colors.Blue);
    public static readonly StyledProperty<IBrush> GroupTitleBrushProperty =
        RegisterBrush(nameof(GroupTitleBrush), Colors.Black);
    public static readonly StyledProperty<IBrush> AnnotationFillBrushProperty =
        RegisterBrush(nameof(AnnotationFillBrush), Colors.LightYellow);
    public static readonly StyledProperty<IBrush> AnnotationStrokeBrushProperty =
        RegisterBrush(nameof(AnnotationStrokeBrush), Colors.Goldenrod);
    public static readonly StyledProperty<IBrush> SelectedAnnotationStrokeBrushProperty =
        RegisterBrush(nameof(SelectedAnnotationStrokeBrush), Colors.Blue);
    public static readonly StyledProperty<IBrush> AnnotationTextBrushProperty =
        RegisterBrush(nameof(AnnotationTextBrush), Colors.Black);
    public static readonly StyledProperty<IBrush> RuntimeActiveBrushProperty =
        RegisterBrush(nameof(RuntimeActiveBrush), Colors.Green);
    public static readonly StyledProperty<IBrush> EdgeBrushProperty =
        RegisterBrush(nameof(EdgeBrush), Colors.Gray);
    public static readonly StyledProperty<IBrush> SelectedEdgeBrushProperty =
        RegisterBrush(nameof(SelectedEdgeBrush), Colors.Orange);
    public static readonly StyledProperty<IBrush> RuntimeSignalBrushProperty =
        RegisterBrush(nameof(RuntimeSignalBrush), Colors.Green);
    public static readonly StyledProperty<IBrush> OverlayStrokeBrushProperty =
        RegisterBrush(nameof(OverlayStrokeBrush), Colors.White);
    public static readonly StyledProperty<IBrush> RuntimeRunningNodeFillBrushProperty =
        RegisterBrush(nameof(RuntimeRunningNodeFillBrush), Colors.Green);
    public static readonly StyledProperty<IBrush> RuntimePausedNodeFillBrushProperty =
        RegisterBrush(nameof(RuntimePausedNodeFillBrush), Colors.Orange);
    public static readonly StyledProperty<IBrush> RuntimeFaultedNodeFillBrushProperty =
        RegisterBrush(nameof(RuntimeFaultedNodeFillBrush), Colors.Red);
    public static readonly StyledProperty<IBrush> EdgeLabelFillBrushProperty =
        RegisterBrush(nameof(EdgeLabelFillBrush), Colors.White);
    public static readonly StyledProperty<IBrush> EdgeLabelTextBrushProperty =
        RegisterBrush(nameof(EdgeLabelTextBrush), Colors.Black);
    public static readonly StyledProperty<IBrush> EdgeLabelStrokeBrushProperty =
        RegisterBrush(nameof(EdgeLabelStrokeBrush), Colors.Gray);
    public static readonly StyledProperty<IBrush> InputPortBrushProperty =
        RegisterBrush(nameof(InputPortBrush), Colors.Green);
    public static readonly StyledProperty<IBrush> OutputPortBrushProperty =
        RegisterBrush(nameof(OutputPortBrush), Colors.Orange);
    public static readonly StyledProperty<IBrush> BidirectionalPortBrushProperty =
        RegisterBrush(nameof(BidirectionalPortBrush), Colors.DodgerBlue);
    public static readonly StyledProperty<IBrush> ReferencePortBrushProperty =
        RegisterBrush(nameof(ReferencePortBrush), Colors.MediumPurple);
    public static readonly StyledProperty<IBrush> InvalidPortFillBrushProperty =
        RegisterBrush(nameof(InvalidPortFillBrush), Color.FromRgb(254, 226, 226));
    public static readonly StyledProperty<IBrush> InvalidPortStrokeBrushProperty =
        RegisterBrush(nameof(InvalidPortStrokeBrush), Color.FromRgb(220, 38, 38));
    public static readonly StyledProperty<double> PortGlyphRadiusProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(PortGlyphRadius), 5.5);
    public static readonly StyledProperty<double> ActivePortGlyphRadiusProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(ActivePortGlyphRadius), 7);
    public static readonly StyledProperty<double> PortCandidateRadiusProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(PortCandidateRadius), 11);
    public static readonly StyledProperty<double> PortHighlightPaddingProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(PortHighlightPadding), 7);
    public static readonly StyledProperty<double> PortLabelFontSizeProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(PortLabelFontSize), 11);
    public static readonly StyledProperty<double> PortLabelOffsetProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(PortLabelOffset), 10);
    public static readonly StyledProperty<double> SymbolNameOffsetProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(SymbolNameOffset), 6);
    public static readonly StyledProperty<double> SymbolDescriptionOffsetProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(SymbolDescriptionOffset), 6);
    public static readonly StyledProperty<double> SymbolDescriptionFontScaleProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(SymbolDescriptionFontScale), 0.86);
    public static readonly StyledProperty<double> PortDisabledOpacityProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(PortDisabledOpacity), 0.35);
    public static readonly StyledProperty<double> PortHighlightOpacityProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(PortHighlightOpacity), 0.16);
    public static readonly StyledProperty<double> InvalidPortHighlightOpacityProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(InvalidPortHighlightOpacity), 0.2);
    public static readonly StyledProperty<double> ActivePortStrokeThicknessProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(ActivePortStrokeThickness), 2);
    public static readonly StyledProperty<double> PortGlyphStrokeThicknessProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(PortGlyphStrokeThickness), 1.5);
    public static readonly StyledProperty<double> InvalidPortStrokeThicknessProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(InvalidPortStrokeThickness), 1.5);
    public static readonly StyledProperty<double> InputOutputPortGlyphOffsetFactorProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(InputOutputPortGlyphOffsetFactor), 0.42);
    public static readonly StyledProperty<double> InputOutputPortGlyphScaleProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(InputOutputPortGlyphScale), 0.72);
    public static readonly StyledProperty<double> PortChevronHalfWidthFactorProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(PortChevronHalfWidthFactor), 0.55);
    public static readonly StyledProperty<double> PortHitRadiusProperty =
        AvaloniaProperty.Register<GraphCanvas, double>(nameof(PortHitRadius), 10);
    public static readonly StyledProperty<IBrush> TitleBrushProperty =
        RegisterBrush(nameof(TitleBrush), Colors.Black);
    public static readonly StyledProperty<IBrush> DescriptionBrushProperty =
        RegisterBrush(nameof(DescriptionBrush), Colors.Gray);
    public static readonly StyledProperty<IBrush> PortTextBrushProperty =
        RegisterBrush(nameof(PortTextBrush), Colors.Gray);
    public static readonly StyledProperty<IBrush> SelectionFillBrushProperty =
        RegisterBrush(nameof(SelectionFillBrush), Colors.Transparent);
    public static readonly StyledProperty<IBrush> GridMinorBrushProperty =
        RegisterBrush(nameof(GridMinorBrush), Colors.Transparent);
    public static readonly StyledProperty<IBrush> GridMajorBrushProperty =
        RegisterBrush(nameof(GridMajorBrush), Colors.Gray);
    public static readonly StyledProperty<IBrush> AlignmentGuideBrushProperty =
        RegisterBrush(nameof(AlignmentGuideBrush), Colors.Orange);
    public static readonly StyledProperty<IBrush> PlacementPreviewFillBrushProperty =
        RegisterBrush(nameof(PlacementPreviewFillBrush), Colors.Transparent);
    public static readonly StyledProperty<IBrush> InvalidPlacementPreviewFillBrushProperty =
        RegisterBrush(nameof(InvalidPlacementPreviewFillBrush), Colors.Transparent);
    public static readonly StyledProperty<IBrush> DangerBrushProperty =
        RegisterBrush(nameof(DangerBrush), Colors.Red);

    private static readonly HashSet<AvaloniaProperty> PaletteProperties =
    [
        CanvasBackgroundBrushProperty,
        NodeFillBrushProperty,
        NodeStrokeBrushProperty,
        SelectedNodeStrokeBrushProperty,
        GroupFillBrushProperty,
        GroupHeaderFillBrushProperty,
        GroupStrokeBrushProperty,
        SelectedGroupStrokeBrushProperty,
        GroupTitleBrushProperty,
        AnnotationFillBrushProperty,
        AnnotationStrokeBrushProperty,
        SelectedAnnotationStrokeBrushProperty,
        AnnotationTextBrushProperty,
        RuntimeActiveBrushProperty,
        EdgeBrushProperty,
        SelectedEdgeBrushProperty,
        RuntimeSignalBrushProperty,
        OverlayStrokeBrushProperty,
        RuntimeRunningNodeFillBrushProperty,
        RuntimePausedNodeFillBrushProperty,
        RuntimeFaultedNodeFillBrushProperty,
        EdgeLabelFillBrushProperty,
        EdgeLabelTextBrushProperty,
        EdgeLabelStrokeBrushProperty,
        InputPortBrushProperty,
        OutputPortBrushProperty,
        BidirectionalPortBrushProperty,
        ReferencePortBrushProperty,
        InvalidPortFillBrushProperty,
        InvalidPortStrokeBrushProperty,
        PortGlyphRadiusProperty,
        ActivePortGlyphRadiusProperty,
        PortCandidateRadiusProperty,
        PortHighlightPaddingProperty,
        PortLabelFontSizeProperty,
        PortLabelOffsetProperty,
        SymbolNameOffsetProperty,
        SymbolDescriptionOffsetProperty,
        SymbolDescriptionFontScaleProperty,
        PortDisabledOpacityProperty,
        PortHighlightOpacityProperty,
        InvalidPortHighlightOpacityProperty,
        ActivePortStrokeThicknessProperty,
        PortGlyphStrokeThicknessProperty,
        InvalidPortStrokeThicknessProperty,
        InputOutputPortGlyphOffsetFactorProperty,
        InputOutputPortGlyphScaleProperty,
        PortChevronHalfWidthFactorProperty,
        PortHitRadiusProperty,
        TitleBrushProperty,
        DescriptionBrushProperty,
        PortTextBrushProperty,
        SelectionFillBrushProperty,
        GridMinorBrushProperty,
        GridMajorBrushProperty,
        AlignmentGuideBrushProperty,
        PlacementPreviewFillBrushProperty,
        InvalidPlacementPreviewFillBrushProperty,
        DangerBrushProperty
    ];

    private static StyledProperty<IBrush> RegisterBrush(string name, Color defaultColor) =>
        AvaloniaProperty.Register<GraphCanvas, IBrush>(name, GraphCanvasVisualFactory.CreateBrush(defaultColor));

    private static bool IsPaletteProperty(AvaloniaProperty property) => PaletteProperties.Contains(property);
}

#pragma warning restore AVP1001
