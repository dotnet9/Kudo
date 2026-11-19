using Avalonia.Media;
using Kudo.Core.Runtime;

namespace Kudo.Avalonia;

internal static class GraphRuntimeNodeBrushResolver
{
    public static IBrush GetFill(
        GraphCanvasRenderState renderState,
        in GraphCanvasRenderPalette palette,
        string nodeId)
    {
        if (!renderState.RuntimeNodeStates.TryGetValue(nodeId, out var state))
        {
            return palette.NodeFillBrush;
        }

        return state.Status switch
        {
            RuntimeStatus.Starting or RuntimeStatus.Running => palette.RuntimeRunningNodeFillBrush,
            RuntimeStatus.Paused or RuntimeStatus.Stopping => palette.RuntimePausedNodeFillBrush,
            RuntimeStatus.Faulted => palette.RuntimeFaultedNodeFillBrush,
            _ => palette.NodeFillBrush
        };
    }
}
