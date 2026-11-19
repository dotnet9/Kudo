using Avalonia.Threading;
using Kudo.Core.Runtime;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasRuntimeController
{
    private static readonly TimeSpan AnimationInterval =
        TimeSpan.FromMilliseconds(1000.0 / 30);

    private readonly GraphCanvasRenderState _state;
    private readonly GraphCanvasRuntimeContext _context;
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render)
    {
        Interval = AnimationInterval
    };

    public GraphCanvasRuntimeController(
        GraphCanvasRenderState state,
        GraphCanvasRuntimeContext context)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timer.Tick += HandleTick;
    }

    public bool IsAnimationActive => _timer.IsEnabled;

    public double FlowProgress => _state.RuntimeFlowProgress;

    public GraphRuntimeNodeState? GetNodeState(string nodeId) =>
        _state.RuntimeNodeStates.GetValueOrDefault(nodeId);

    public GraphRuntimeEdgeState? GetEdgeState(string edgeId) =>
        _state.RuntimeEdgeStates.GetValueOrDefault(edgeId);

    public void SetAttachedToVisualTree(bool isAttached)
    {
        _state.IsAttachedToVisualTree = isAttached;
        UpdateAnimationState();
    }

    public void UpdateSnapshot(GraphRuntimeSnapshot? snapshot)
    {
        _state.UpdateRuntimeSnapshot(snapshot);
        if ((snapshot?.Status is null or RuntimeStatus.Stopped or RuntimeStatus.Faulted) &&
            !HasDeclarativeEdgeAnimations() &&
            !HasAnimatedNodeImages())
        {
            _state.RuntimeFlowProgress = 0;
        }

        UpdateAnimationState();
    }

    public void UpdateAnimationState()
    {
        var hasRuntimeAnimation = _context.RuntimeSnapshot()?.Status == RuntimeStatus.Running &&
                                  _state.RuntimeEdgeStates.Values.Any(state => state.IsActive);
        var shouldAnimate = _state.IsAttachedToVisualTree &&
                            (hasRuntimeAnimation ||
                             HasDeclarativeEdgeAnimations() ||
                             HasAnimatedNodeImages());
        if (shouldAnimate)
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }

            return;
        }

        _timer.Stop();
    }

    private bool HasDeclarativeEdgeAnimations() =>
        _context.Document()?.Edges.Any(edge => edge.Appearance?.IsAnimated == true) == true;

    private bool HasAnimatedNodeImages()
    {
        if (_context.ImageResolver() is not IGraphAnimatedImageResolver animatedResolver ||
            _context.Document() is not { } document)
        {
            return false;
        }

        return document.Nodes.Any(node =>
            node.Appearance?.BackgroundImage is { } source &&
            animatedResolver.IsAnimated(source));
    }

    private void HandleTick(object? sender, EventArgs e)
    {
        _state.RuntimeFlowProgress = (_state.RuntimeFlowProgress + 0.02) % 1;
        _context.InvalidateVisual();
    }
}

internal sealed class GraphCanvasRuntimeContext
{
    public required Func<GraphDocument?> Document { get; init; }

    public required Func<GraphRuntimeSnapshot?> RuntimeSnapshot { get; init; }

    public required Func<IGraphImageResolver?> ImageResolver { get; init; }

    public required Action InvalidateVisual { get; init; }
}
