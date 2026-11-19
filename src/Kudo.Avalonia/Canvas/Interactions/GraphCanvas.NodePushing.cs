using System.ComponentModel;
using Avalonia;
using Avalonia.Input;

namespace Kudo.Avalonia;

public enum GraphNodePushAxis
{
    [Description("水平方向推移")]
    Horizontal,
    [Description("垂直方向推移")]
    Vertical
}

public partial class GraphCanvas
{
    public static readonly StyledProperty<bool> IsNodePushingEnabledProperty =
        AvaloniaProperty.Register<GraphCanvas, bool>(nameof(IsNodePushingEnabled), true);

    public bool IsNodePushingEnabled
    {
        get => GetValue(IsNodePushingEnabledProperty);
        set => SetValue(IsNodePushingEnabledProperty, value);
    }

    public bool IsPushingNodes => _interactionSession is NodePushingInteractionSession;

    public int PushNodes(GraphPoint boundary, GraphNodePushAxis axis, double amount)
        => _nodePushController.Push(boundary, axis, amount);

    internal void StartNodePushing(Point point, IPointer pointer) =>
        StartInteraction(new NodePushingInteractionSession(this, pointer, point));

    internal IReadOnlyList<NodeModel> GetNodePushCandidates(
        GraphPoint boundary,
        GraphNodePushAxis axis)
        => _nodePushController.GetCandidates(boundary, axis);
}
