using Avalonia.Input;

namespace Kudo.Avalonia;

public enum GraphPointerButton
{
    None,
    Left,
    Middle,
    Right
}

public readonly record struct GraphPointerGesture(
    GraphPointerButton Button,
    KeyModifiers KeyModifiers = KeyModifiers.None)
{
    public static GraphPointerGesture None { get; } = new(GraphPointerButton.None);

    public bool Matches(GraphPointerButton button, KeyModifiers keyModifiers) =>
        Button != GraphPointerButton.None &&
        Button == button &&
        KeyModifiers == keyModifiers;
}

public readonly record struct GraphKeyGesture(
    Key Key,
    KeyModifiers KeyModifiers = KeyModifiers.None)
{
    public static GraphKeyGesture None { get; } = new(Key.None);

    public bool Matches(Key key, KeyModifiers keyModifiers) =>
        Key != Key.None &&
        Key == key &&
        KeyModifiers == keyModifiers;

    public static GraphKeyGesture Command(Key key, bool shift = false) =>
        new(
            key,
            (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) |
            (shift ? KeyModifiers.Shift : KeyModifiers.None));
}

public sealed class GraphSelectionGestures
{
    public GraphPointerGesture Replace { get; set; } = new(GraphPointerButton.Left);

    public GraphPointerGesture Extend { get; set; } =
        new(GraphPointerButton.Left, KeyModifiers.Shift);

    public GraphPointerGesture Subtract { get; set; } =
        new(GraphPointerButton.Left, KeyModifiers.Alt);
}

public sealed class GraphNudgeGestures
{
    public KeyModifiers SmallStepModifiers { get; set; } = KeyModifiers.None;

    public KeyModifiers LargeStepModifiers { get; set; } = KeyModifiers.Shift;

    public double SmallStep { get; set; } = 1.0;

    public double LargeStep { get; set; } = 10.0;
}

/// <summary>
/// Mutable, per-canvas mappings for GraphCanvas built-in input.
/// Set a gesture to <see cref="GraphPointerGesture.None"/> or
/// <see cref="GraphKeyGesture.None"/> to disable that operation.
/// </summary>
public sealed class GraphCanvasInputGestures
{
    public GraphSelectionGestures Selection { get; } = new();

    public GraphPointerGesture Drag { get; set; } =
        new(GraphPointerButton.Left);

    public GraphPointerGesture Edit { get; set; } =
        new(GraphPointerButton.Left);

    public GraphPointerGesture Connect { get; set; } =
        new(GraphPointerButton.Left);

    public ICollection<GraphPointerGesture> Pan { get; } =
    [
        new GraphPointerGesture(GraphPointerButton.Middle),
        new GraphPointerGesture(GraphPointerButton.Right)
    ];

    public GraphPointerGesture NodePushing { get; set; } =
        new(GraphPointerButton.Left, KeyModifiers.Control | KeyModifiers.Shift);

    public GraphPointerGesture EdgeCutting { get; set; } =
        new(GraphPointerButton.Left, KeyModifiers.Control | KeyModifiers.Alt);

    public GraphKeyGesture KeyboardPan { get; set; } = new(Key.Space);

    public GraphKeyGesture Cancel { get; set; } = new(Key.Escape);

    public ICollection<GraphKeyGesture> Delete { get; } =
    [
        new GraphKeyGesture(Key.Delete),
        new GraphKeyGesture(Key.Back)
    ];

    public GraphKeyGesture SelectAll { get; set; } = GraphKeyGesture.Command(Key.A);

    public GraphKeyGesture Group { get; set; } = GraphKeyGesture.Command(Key.G);

    public GraphKeyGesture Ungroup { get; set; } = GraphKeyGesture.Command(Key.G, shift: true);

    public GraphNudgeGestures Nudge { get; } = new();
}
