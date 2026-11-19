using Avalonia.Media;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public IBrush CanvasBackgroundBrush { get => GetValue(CanvasBackgroundBrushProperty); set => SetValue(CanvasBackgroundBrushProperty, value); }
    public IBrush NodeFillBrush { get => GetValue(NodeFillBrushProperty); set => SetValue(NodeFillBrushProperty, value); }
    public IBrush NodeStrokeBrush { get => GetValue(NodeStrokeBrushProperty); set => SetValue(NodeStrokeBrushProperty, value); }
    public IBrush SelectedNodeStrokeBrush { get => GetValue(SelectedNodeStrokeBrushProperty); set => SetValue(SelectedNodeStrokeBrushProperty, value); }
    public IBrush GroupFillBrush { get => GetValue(GroupFillBrushProperty); set => SetValue(GroupFillBrushProperty, value); }
    public IBrush GroupHeaderFillBrush { get => GetValue(GroupHeaderFillBrushProperty); set => SetValue(GroupHeaderFillBrushProperty, value); }
    public IBrush GroupStrokeBrush { get => GetValue(GroupStrokeBrushProperty); set => SetValue(GroupStrokeBrushProperty, value); }
    public IBrush SelectedGroupStrokeBrush { get => GetValue(SelectedGroupStrokeBrushProperty); set => SetValue(SelectedGroupStrokeBrushProperty, value); }
    public IBrush GroupTitleBrush { get => GetValue(GroupTitleBrushProperty); set => SetValue(GroupTitleBrushProperty, value); }
    public IBrush AnnotationFillBrush { get => GetValue(AnnotationFillBrushProperty); set => SetValue(AnnotationFillBrushProperty, value); }
    public IBrush AnnotationStrokeBrush { get => GetValue(AnnotationStrokeBrushProperty); set => SetValue(AnnotationStrokeBrushProperty, value); }
    public IBrush SelectedAnnotationStrokeBrush { get => GetValue(SelectedAnnotationStrokeBrushProperty); set => SetValue(SelectedAnnotationStrokeBrushProperty, value); }
    public IBrush AnnotationTextBrush { get => GetValue(AnnotationTextBrushProperty); set => SetValue(AnnotationTextBrushProperty, value); }
    public IBrush RuntimeActiveBrush { get => GetValue(RuntimeActiveBrushProperty); set => SetValue(RuntimeActiveBrushProperty, value); }
    public IBrush EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }
    public IBrush SelectedEdgeBrush { get => GetValue(SelectedEdgeBrushProperty); set => SetValue(SelectedEdgeBrushProperty, value); }
    public IBrush RuntimeSignalBrush { get => GetValue(RuntimeSignalBrushProperty); set => SetValue(RuntimeSignalBrushProperty, value); }
    public IBrush OverlayStrokeBrush { get => GetValue(OverlayStrokeBrushProperty); set => SetValue(OverlayStrokeBrushProperty, value); }
    public IBrush RuntimeRunningNodeFillBrush { get => GetValue(RuntimeRunningNodeFillBrushProperty); set => SetValue(RuntimeRunningNodeFillBrushProperty, value); }
    public IBrush RuntimePausedNodeFillBrush { get => GetValue(RuntimePausedNodeFillBrushProperty); set => SetValue(RuntimePausedNodeFillBrushProperty, value); }
    public IBrush RuntimeFaultedNodeFillBrush { get => GetValue(RuntimeFaultedNodeFillBrushProperty); set => SetValue(RuntimeFaultedNodeFillBrushProperty, value); }
    public IBrush EdgeLabelFillBrush { get => GetValue(EdgeLabelFillBrushProperty); set => SetValue(EdgeLabelFillBrushProperty, value); }
    public IBrush EdgeLabelTextBrush { get => GetValue(EdgeLabelTextBrushProperty); set => SetValue(EdgeLabelTextBrushProperty, value); }
    public IBrush EdgeLabelStrokeBrush { get => GetValue(EdgeLabelStrokeBrushProperty); set => SetValue(EdgeLabelStrokeBrushProperty, value); }
    public IBrush InputPortBrush { get => GetValue(InputPortBrushProperty); set => SetValue(InputPortBrushProperty, value); }
    public IBrush OutputPortBrush { get => GetValue(OutputPortBrushProperty); set => SetValue(OutputPortBrushProperty, value); }
    public IBrush BidirectionalPortBrush { get => GetValue(BidirectionalPortBrushProperty); set => SetValue(BidirectionalPortBrushProperty, value); }
    public IBrush ReferencePortBrush { get => GetValue(ReferencePortBrushProperty); set => SetValue(ReferencePortBrushProperty, value); }
    public IBrush InvalidPortFillBrush { get => GetValue(InvalidPortFillBrushProperty); set => SetValue(InvalidPortFillBrushProperty, value); }
    public IBrush InvalidPortStrokeBrush { get => GetValue(InvalidPortStrokeBrushProperty); set => SetValue(InvalidPortStrokeBrushProperty, value); }
    public double PortGlyphRadius { get => GetValue(PortGlyphRadiusProperty); set => SetValue(PortGlyphRadiusProperty, value); }
    public double ActivePortGlyphRadius { get => GetValue(ActivePortGlyphRadiusProperty); set => SetValue(ActivePortGlyphRadiusProperty, value); }
    public double PortCandidateRadius { get => GetValue(PortCandidateRadiusProperty); set => SetValue(PortCandidateRadiusProperty, value); }
    public double PortHighlightPadding { get => GetValue(PortHighlightPaddingProperty); set => SetValue(PortHighlightPaddingProperty, value); }
    public double PortLabelFontSize { get => GetValue(PortLabelFontSizeProperty); set => SetValue(PortLabelFontSizeProperty, value); }
    public double PortLabelOffset { get => GetValue(PortLabelOffsetProperty); set => SetValue(PortLabelOffsetProperty, value); }
    public double SymbolNameOffset { get => GetValue(SymbolNameOffsetProperty); set => SetValue(SymbolNameOffsetProperty, value); }
    public double SymbolDescriptionOffset { get => GetValue(SymbolDescriptionOffsetProperty); set => SetValue(SymbolDescriptionOffsetProperty, value); }
    public double SymbolDescriptionFontScale { get => GetValue(SymbolDescriptionFontScaleProperty); set => SetValue(SymbolDescriptionFontScaleProperty, value); }
    public double PortDisabledOpacity { get => GetValue(PortDisabledOpacityProperty); set => SetValue(PortDisabledOpacityProperty, value); }
    public double PortHighlightOpacity { get => GetValue(PortHighlightOpacityProperty); set => SetValue(PortHighlightOpacityProperty, value); }
    public double InvalidPortHighlightOpacity { get => GetValue(InvalidPortHighlightOpacityProperty); set => SetValue(InvalidPortHighlightOpacityProperty, value); }
    public double ActivePortStrokeThickness { get => GetValue(ActivePortStrokeThicknessProperty); set => SetValue(ActivePortStrokeThicknessProperty, value); }
    public double PortGlyphStrokeThickness { get => GetValue(PortGlyphStrokeThicknessProperty); set => SetValue(PortGlyphStrokeThicknessProperty, value); }
    public double InvalidPortStrokeThickness { get => GetValue(InvalidPortStrokeThicknessProperty); set => SetValue(InvalidPortStrokeThicknessProperty, value); }
    public double InputOutputPortGlyphOffsetFactor { get => GetValue(InputOutputPortGlyphOffsetFactorProperty); set => SetValue(InputOutputPortGlyphOffsetFactorProperty, value); }
    public double InputOutputPortGlyphScale { get => GetValue(InputOutputPortGlyphScaleProperty); set => SetValue(InputOutputPortGlyphScaleProperty, value); }
    public double PortChevronHalfWidthFactor { get => GetValue(PortChevronHalfWidthFactorProperty); set => SetValue(PortChevronHalfWidthFactorProperty, value); }
    public double PortHitRadius { get => GetValue(PortHitRadiusProperty); set => SetValue(PortHitRadiusProperty, value); }
    public IBrush TitleBrush { get => GetValue(TitleBrushProperty); set => SetValue(TitleBrushProperty, value); }
    public IBrush DescriptionBrush { get => GetValue(DescriptionBrushProperty); set => SetValue(DescriptionBrushProperty, value); }
    public IBrush PortTextBrush { get => GetValue(PortTextBrushProperty); set => SetValue(PortTextBrushProperty, value); }
    public IBrush SelectionFillBrush { get => GetValue(SelectionFillBrushProperty); set => SetValue(SelectionFillBrushProperty, value); }
    public IBrush GridMinorBrush { get => GetValue(GridMinorBrushProperty); set => SetValue(GridMinorBrushProperty, value); }
    public IBrush GridMajorBrush { get => GetValue(GridMajorBrushProperty); set => SetValue(GridMajorBrushProperty, value); }
    public IBrush AlignmentGuideBrush { get => GetValue(AlignmentGuideBrushProperty); set => SetValue(AlignmentGuideBrushProperty, value); }
    public IBrush PlacementPreviewFillBrush { get => GetValue(PlacementPreviewFillBrushProperty); set => SetValue(PlacementPreviewFillBrushProperty, value); }
    public IBrush InvalidPlacementPreviewFillBrush { get => GetValue(InvalidPlacementPreviewFillBrushProperty); set => SetValue(InvalidPlacementPreviewFillBrushProperty, value); }
    public IBrush DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
}
