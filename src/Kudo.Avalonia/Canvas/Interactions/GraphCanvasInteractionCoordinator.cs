// 画布交互协调器：集中管理活动会话和 Pointer Capture，确保同一时刻只有一个手势。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Kudo.Avalonia;

public interface IGraphInteractionSession
{
    IPointer Pointer { get; }

    Point StartPointerPoint { get; }

    bool AllowContextMenuOnRelease { get; }

    bool SupportsAutoPan { get; }

    void Update(Point point, KeyModifiers modifiers);

    void Complete(Point point);

    void CaptureLost();

    void Cancel();
}

internal sealed class GraphCanvasInteractionCoordinator(
    GraphCanvasState state,
    Control captureTarget)
{
    private readonly GraphCanvasState _state =
        state ?? throw new ArgumentNullException(nameof(state));

    public IGraphInteractionSession? ActiveSession => _state.ActiveInteraction;

    public bool IsActive => ActiveSession is not null;

    public void Start(IGraphInteractionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (ActiveSession is not null)
        {
            throw new InvalidOperationException("The active interaction must end before another one starts.");
        }

        _state.ActiveInteraction = session;
        session.Pointer.Capture(captureTarget);
    }

    public void End(IPointer? pointer)
    {
        _state.ActiveInteraction = null;
        pointer?.Capture(null);
    }
}
