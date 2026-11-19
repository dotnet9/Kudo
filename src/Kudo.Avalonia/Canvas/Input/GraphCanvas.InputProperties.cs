namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public GraphInputBehaviorCollection InputBehaviors { get; } = new();

    public GraphCanvasInputGestures InputGestures { get; } = new();

    public GraphInteractionSessionStarterCollection InteractionSessionStarters { get; } = new();

    public bool IsSpacePanMode => _keyboardController.IsSpacePanMode;
}
