using Avalonia.Input;

namespace Kudo.Avalonia;

internal sealed class ResizeAndConnectionInputProcessor : IGraphDefaultInputProcessor
{
    public static ResizeAndConnectionInputProcessor Instance { get; } = new();

    public bool TryProcess(in GraphPointerPressedContext context)
    {
        var canvas = context.Canvas;
        var hits = context.Hits;
        if (!canvas.IsReadOnly && canvas.IsEditGesture(context) && hits.NodeRotationHandle is { } nodeRotation)
        {
            canvas.StartNodeRotation(context.Point, nodeRotation, context.Event.Pointer);
            return true;
        }

        if (!canvas.IsReadOnly &&
            canvas.IsEditGesture(context) &&
            hits.NodeResizeHandle is { } nodeResize &&
            (!canvas.IsConnectGesture(context) || !CanStartConnection(hits.Port?.Port)))
        {
            canvas.StartNodeResize(context.Point, nodeResize, context.Event.Pointer);
            return true;
        }

        if (!canvas.IsReadOnly && canvas.IsEditGesture(context) && hits.AnnotationResizeHandle is { } annotationResize)
        {
            canvas.StartAnnotationResize(context.Point, annotationResize, context.Event.Pointer);
            return true;
        }

        if (!canvas.IsReadOnly && canvas.IsEditGesture(context) && hits.EdgeWaypoint is { } waypoint)
        {
            if (context.Event.ClickCount >= 2)
            {
                canvas.RemoveSelectedEdgeWaypoint(waypoint.Index);
            }
            else
            {
                canvas.StartEdgeWaypointMove(waypoint, context.Event.Pointer);
            }

            return true;
        }

        if (!canvas.IsReadOnly && canvas.IsEditGesture(context) && hits.EdgeEndpoint is { } endpoint)
        {
            canvas.StartEdgeReconnection(endpoint, context.Event.Pointer);
            return true;
        }

        if (!canvas.IsReadOnly &&
            canvas.IsConnectGesture(context) &&
            hits.Port is { } port &&
            CanStartConnection(port.Port))
        {
            canvas.SetSelectedNodes([port.Node.Id], port.Node.Id);
            canvas.StartConnection(context.Point, port, context.Event.Pointer);
            return true;
        }

        return false;
    }

    internal static bool CanStartConnection(PortModel? port) =>
        port is not null &&
        (PortKindSemantics.CanSendConnection(port.Kind) ||
         PortKindSemantics.CanSendReference(port.Kind)) &&
        port.Capacity.MaxOutgoing != 0 &&
        port.Capacity.MaxTotal != 0 &&
        port.State != PortState.Disabled;
}
