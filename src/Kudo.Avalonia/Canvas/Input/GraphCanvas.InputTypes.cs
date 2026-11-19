using Avalonia;
using Avalonia.Input;
using Kudo;

namespace Kudo.Avalonia;

internal sealed record GraphInputHitSnapshot(
    NodeRotationHandleHitResult? NodeRotationHandle,
    NodeResizeHandleHitResult? NodeResizeHandle,
    AnnotationResizeHandleHitResult? AnnotationResizeHandle,
    EdgeWaypointHitResult? EdgeWaypoint,
    EdgeEndpointHitResult? EdgeEndpoint,
    PortHitResult? Port,
    NodeModel? Node,
    EdgeModel? EdgeLabel,
    EdgeModel? Edge,
    AnnotationModel? Annotation,
    GroupModel? Group);

internal readonly record struct GraphPointerPressedContext(
    GraphCanvas Canvas,
    PointerPressedEventArgs Event,
    Point Point,
    bool IsLeftButtonPressed,
    bool IsMiddleButtonPressed,
    bool IsRightButtonPressed,
    GraphInputHitSnapshot Hits)
{
    public GraphPointerButton? PressedButton => IsLeftButtonPressed
        ? GraphPointerButton.Left
        : IsMiddleButtonPressed
            ? GraphPointerButton.Middle
            : IsRightButtonPressed
                ? GraphPointerButton.Right
                : null;

    public bool Matches(GraphPointerGesture gesture) =>
        PressedButton is { } button &&
        gesture.Matches(button, Event.KeyModifiers);
}

internal interface IGraphDefaultInputProcessor
{
    bool TryProcess(in GraphPointerPressedContext context);
}

internal enum RectangleSelectionMode
{
    Replace,
    Add,
    Subtract
}
