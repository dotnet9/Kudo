using Avalonia.Input;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsNodeTitleEditorSource(e.Source))
        {
            return;
        }

        if (PreventsDefaultInput(new GraphInputBehaviorContext(
                this,
                GraphInputEventKind.KeyDown,
                e.Source,
                default,
                e.KeyModifiers,
                Key: e.Key)))
        {
            if (InputGestures.KeyboardPan.Key == e.Key)
            {
                _keyboardController.SetSpacePanMode(false);
            }

            e.Handled = true;
            return;
        }

        if (InputGestures.Cancel.Matches(e.Key, e.KeyModifiers) &&
            FindNodeControlHost(e.Source) is not null)
        {
            e.Handled = true;
            return;
        }

        if (InputGestures.KeyboardPan.Matches(e.Key, e.KeyModifiers) &&
            ReferenceEquals(e.Source, this))
        {
            _keyboardController.SetSpacePanMode(true);
            e.Handled = true;
        }
        else if (InputGestures.Ungroup.Matches(e.Key, e.KeyModifiers))
        {
            var changed = UngroupSelectedNodes();
            e.Handled = changed;
        }
        else if (InputGestures.Group.Matches(e.Key, e.KeyModifiers))
        {
            e.Handled = GroupSelectedNodes();
        }
        else if (InputGestures.SelectAll.Matches(e.Key, e.KeyModifiers))
        {
            SelectAllNodes();
            e.Handled = true;
        }
        else if (ReferenceEquals(e.Source, this) &&
                 TryGetNodeNudgeOffset(e.Key, e.KeyModifiers, out var offset) &&
                 NudgeSelectedNodes(offset))
        {
            e.Handled = true;
        }
        else if (InputGestures.Cancel.Matches(e.Key, e.KeyModifiers))
        {
            if (!CancelActivePointerInteraction() &&
                !CancelManualConnectionDraft())
            {
                ClearSelection();
            }

            e.Handled = true;
        }
        else if (InputGestures.Delete.Any(gesture => gesture.Matches(e.Key, e.KeyModifiers)) &&
                 DeleteSelection())
        {
            e.Handled = true;
        }
    }

    private bool TryGetNodeNudgeOffset(
        Key key,
        KeyModifiers modifiers,
        out GraphPoint offset)
    {
        offset = GraphPoint.Zero;
        var nudge = InputGestures.Nudge;
        var distance = modifiers == nudge.SmallStepModifiers
            ? nudge.SmallStep
            : modifiers == nudge.LargeStepModifiers
                ? nudge.LargeStep
                : 0;
        if (!double.IsFinite(distance) || distance <= 0)
        {
            return false;
        }

        offset = key switch
        {
            Key.Left => new GraphPoint(-distance, 0),
            Key.Right => new GraphPoint(distance, 0),
            Key.Up => new GraphPoint(0, -distance),
            Key.Down => new GraphPoint(0, distance),
            _ => GraphPoint.Zero
        };
        return offset != GraphPoint.Zero;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (PreventsDefaultInput(new GraphInputBehaviorContext(
                this,
                GraphInputEventKind.KeyUp,
                e.Source,
                default,
                e.KeyModifiers,
                Key: e.Key)))
        {
            if (InputGestures.KeyboardPan.Key == e.Key)
            {
                _keyboardController.SetSpacePanMode(false);
            }

            e.Handled = true;
            return;
        }

        if (InputGestures.KeyboardPan.Key != e.Key || e.Key == Key.None)
        {
            return;
        }

        _keyboardController.SetSpacePanMode(false);
        if (ReferenceEquals(e.Source, this))
        {
            e.Handled = true;
        }
    }
}
