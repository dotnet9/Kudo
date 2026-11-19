using Avalonia.Media;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private IPen NodeStrokePen => new Pen(NodeStrokeBrush, 1);
    private IPen SelectedNodeStrokePen => new Pen(SelectedNodeStrokeBrush, 2);
    private IPen GroupStrokePen => new Pen(GroupStrokeBrush, 1.25);
    private IPen SelectedGroupStrokePen => new Pen(SelectedGroupStrokeBrush, 2.25);
    private IPen RuntimeActiveNodeStrokePen => new Pen(RuntimeActiveBrush, 2);
    private IPen EdgePen => new Pen(EdgeBrush, 2);
    private IPen SelectedEdgePen => new Pen(SelectedEdgeBrush, 3);
    private IPen RuntimeActiveEdgePen => new Pen(RuntimeActiveBrush, 2.5);
    private IPen RuntimeSignalStrokePen => new Pen(OverlayStrokeBrush, 1);
    private IPen EdgeLabelStrokePen => new Pen(EdgeLabelStrokeBrush, 1);
    private IPen PortStrokePen => new Pen(OverlayStrokeBrush, 1);
    private IBrush SelectionRectangleFillBrush => SelectionFillBrush;
    private IPen SelectionRectanglePen => new Pen(SelectedNodeStrokeBrush, 1);
    private IPen GridMinorPen => new Pen(GridMinorBrush, 1);
    private IPen GridMajorPen => new Pen(GridMajorBrush, 1);
    private IPen AlignmentGuidePen => new Pen(AlignmentGuideBrush, 1.5);
    private IPen PlacementPreviewPen => new Pen(SelectedNodeStrokeBrush, 2);
    private IPen InvalidPlacementPreviewPen => new Pen(DangerBrush, 2);
    private IPen EdgeCuttingPreviewPen => new Pen(DangerBrush, 3);
    private IPen EdgeCuttingLinePen => new Pen(DangerBrush, 2);
    private IPen NodePushAreaPen => new Pen(SelectedNodeStrokeBrush, 2);
    private IBrush BreakpointBrush => DangerBrush;
    private IPen BreakpointStrokePen => new Pen(OverlayStrokeBrush, 1);
}
