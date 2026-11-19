using Avalonia.Input;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsNodeTitleEditorSource(e.Source))
        {
            return;
        }

        CommitNodeTitleEdit();
        CommitAnnotationTextEdit();
        CommitEdgeLabelEdit();
        var point = e.GetPosition(this);
        var current = e.GetCurrentPoint(this);
        if (PreventsDefaultInput(new GraphInputBehaviorContext(
                this,
                GraphInputEventKind.PointerPressed,
                e.Source,
                point,
                e.KeyModifiers,
                current.Properties.PointerUpdateKind,
                ClickCount: e.ClickCount)))
        {
            CancelActivePointerInteraction();
            e.Handled = true;
            return;
        }

        var graphPoint = ScreenToGraph(point);
        if (double.IsFinite(graphPoint.X) && double.IsFinite(graphPoint.Y))
        {
            LastPointerGraphPoint = graphPoint;
            LastPointerGraphPointVersion++;
        }

        Focus();

        if (FindNodeControlHost(e.Source) is not null)
        {
            return;
        }

        if (TryStartRegisteredInteraction(e, point, current))
        {
            e.Handled = true;
            return;
        }

        if (GetPressedButton(current) is { } pressedButton &&
            InputGestures.Connect.Matches(pressedButton, e.KeyModifiers) &&
            TryStartManualConnectionSegment(point, e.Pointer))
        {
            e.Handled = true;
            return;
        }

        if (_keyboardController.IsSpacePanMode &&
            GetPressedButton(current) is { } panButton &&
            InputGestures.Drag.Matches(panButton, e.KeyModifiers))
        {
            StartPan(point, e.Pointer, openContextMenuOnRelease: false);
            e.Handled = true;
            return;
        }

        if (ProcessDefaultPointerPressed(e, point, current))
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsNodeTitleEditorSource(e.Source))
        {
            return;
        }

        var point = e.GetPosition(this);
        var current = e.GetCurrentPoint(this);
        if (PreventsDefaultInput(new GraphInputBehaviorContext(
                this,
                GraphInputEventKind.PointerMoved,
                e.Source,
                point,
                e.KeyModifiers,
                current.Properties.PointerUpdateKind)))
        {
            CancelActivePointerInteraction();
            e.Handled = true;
            return;
        }

        UpdateHoveredPort(point);
        if (!IsPointerInteracting)
        {
            UpdateManualConnectionDraftHover(point);
            _keyboardController.UpdatePointerCursor(point);
            return;
        }

        UpdateActivePointerInteraction(point, e.KeyModifiers);
        UpdateAutoPanPointer(point, e.KeyModifiers);
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        var current = e.GetCurrentPoint(this);
        if (PreventsDefaultInput(new GraphInputBehaviorContext(
                this,
                GraphInputEventKind.PointerExited,
                e.Source,
                e.GetPosition(this),
                e.KeyModifiers,
                current.Properties.PointerUpdateKind)))
        {
            CancelActivePointerInteraction();
            e.Handled = true;
            return;
        }

        if (!IsPointerInteracting && !_keyboardController.IsSpacePanMode)
        {
            Cursor = null;
        }

        UpdateHoveredPort(null);
        UpdateManualConnectionDraftHover(null);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (IsNodeTitleEditorSource(e.Source))
        {
            base.OnPointerReleased(e);
            return;
        }

        var point = e.GetPosition(this);
        var current = e.GetCurrentPoint(this);
        if (PreventsDefaultInput(new GraphInputBehaviorContext(
                this,
                GraphInputEventKind.PointerReleased,
                e.Source,
                point,
                e.KeyModifiers,
                current.Properties.PointerUpdateKind)))
        {
            CancelActivePointerInteraction();
            e.Handled = true;
            base.OnPointerReleased(e);
            return;
        }

        var wasInteracting = IsPointerInteracting;
        var allowContextMenu = _interactionSession?.AllowContextMenuOnRelease == true;
        CompleteActivePointerInteraction(point);
        EndPointerInteraction(e.Pointer);
        UpdateHoveredPort(point);
        UpdateManualConnectionDraftHover(point);
        _keyboardController.UpdatePointerCursor(point);
        if (wasInteracting && !allowContextMenu)
        {
            e.Handled = true;
        }

        base.OnPointerReleased(e);
        e.Handled = wasInteracting || e.Handled;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (IsNodeTitleEditorSource(e.Source))
        {
            return;
        }

        CompleteActivePointerInteractionAfterCaptureLost();
        EndPointerInteraction(null);
        UpdateHoveredPort(null);
        UpdateManualConnectionDraftHover(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (IsNodeTitleEditorSource(e.Source))
        {
            return;
        }

        var pointer = e.GetPosition(this);
        var current = e.GetCurrentPoint(this);
        if (PreventsDefaultInput(new GraphInputBehaviorContext(
                this,
                GraphInputEventKind.PointerWheelChanged,
                e.Source,
                pointer,
                e.KeyModifiers,
                current.Properties.PointerUpdateKind,
                e.Delta)))
        {
            e.Handled = true;
            return;
        }

        var zoomFactor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        ZoomBy(zoomFactor, pointer);
        e.Handled = true;
    }
}
