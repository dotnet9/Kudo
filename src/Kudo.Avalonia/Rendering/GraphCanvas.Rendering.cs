using Avalonia.Media;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _renderer.Render(context);
    }

    private GraphCanvasRenderPalette CreateRenderPalette() =>
        new()
        {
            CanvasBackgroundBrush = CanvasBackgroundBrush,
            NodeFillBrush = NodeFillBrush,
            RuntimeRunningNodeFillBrush = RuntimeRunningNodeFillBrush,
            RuntimePausedNodeFillBrush = RuntimePausedNodeFillBrush,
            RuntimeFaultedNodeFillBrush = RuntimeFaultedNodeFillBrush,
            GroupFillBrush = GroupFillBrush,
            GroupHeaderFillBrush = GroupHeaderFillBrush,
            GroupTitleBrush = GroupTitleBrush,
            InputPortBrush = InputPortBrush,
            OutputPortBrush = OutputPortBrush,
            BidirectionalPortBrush = BidirectionalPortBrush,
            ReferencePortBrush = ReferencePortBrush,
            InvalidPortFillBrush = InvalidPortFillBrush,
            InvalidPortStrokeBrush = InvalidPortStrokeBrush,
            PortGlyphRadius = PortGlyphRadius,
            ActivePortGlyphRadius = ActivePortGlyphRadius,
            PortCandidateRadius = PortCandidateRadius,
            PortHighlightPadding = PortHighlightPadding,
            PortLabelFontSize = PortLabelFontSize,
            PortLabelOffset = PortLabelOffset,
            SymbolNameOffset = SymbolNameOffset,
            SymbolDescriptionOffset = SymbolDescriptionOffset,
            SymbolDescriptionFontScale = SymbolDescriptionFontScale,
            PortDisabledOpacity = PortDisabledOpacity,
            PortHighlightOpacity = PortHighlightOpacity,
            InvalidPortHighlightOpacity = InvalidPortHighlightOpacity,
            ActivePortStrokeThickness = ActivePortStrokeThickness,
            PortGlyphStrokeThickness = PortGlyphStrokeThickness,
            InvalidPortStrokeThickness = InvalidPortStrokeThickness,
            InputOutputPortGlyphOffsetFactor = InputOutputPortGlyphOffsetFactor,
            InputOutputPortGlyphScale = InputOutputPortGlyphScale,
            PortChevronHalfWidthFactor = PortChevronHalfWidthFactor,
            TitleBrush = TitleBrush,
            DescriptionBrush = DescriptionBrush,
            PortTextBrush = PortTextBrush,
            BreakpointBrush = BreakpointBrush,
            RuntimeSignalBrush = RuntimeSignalBrush,
            EdgeLabelFillBrush = EdgeLabelFillBrush,
            EdgeLabelTextBrush = EdgeLabelTextBrush,
            GridMinorPen = GridMinorPen,
            GridMajorPen = GridMajorPen,
            NodeStrokePen = NodeStrokePen,
            SelectedNodeStrokePen = SelectedNodeStrokePen,
            RuntimeActiveNodeStrokePen = RuntimeActiveNodeStrokePen,
            GroupStrokePen = GroupStrokePen,
            SelectedGroupStrokePen = SelectedGroupStrokePen,
            EdgePen = EdgePen,
            SelectedEdgePen = SelectedEdgePen,
            RuntimeActiveEdgePen = RuntimeActiveEdgePen,
            RuntimeSignalStrokePen = RuntimeSignalStrokePen,
            EdgeLabelStrokePen = EdgeLabelStrokePen,
            PortStrokePen = PortStrokePen,
            BreakpointStrokePen = BreakpointStrokePen,
            EdgeCuttingPreviewPen = EdgeCuttingPreviewPen
        };
}
