using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public GraphViewport Viewport
    {
        get => GetValue(ViewportProperty);
        set => SetViewportCore(value, GraphViewportChangeReason.External);
    }

    public GraphViewportOptions ViewportOptions
    {
        get => GetValue(ViewportOptionsProperty);
        set => SetValue(ViewportOptionsProperty, value);
    }

    public Rect? VisibleGraphBounds => GetViewportGraphBounds();

    public bool IsViewportCullingEnabled
    {
        get => _isViewportCullingEnabled;
        set
        {
            if (_isViewportCullingEnabled == value)
            {
                return;
            }

            _isViewportCullingEnabled = value;
            UpdateNodeControlHostLayout();
            InvalidateVisual();
        }
    }

    public double ViewportCullingMargin
    {
        get => _viewportCullingMargin;
        set
        {
            var normalized = double.IsFinite(value) && value >= 0
                ? value
                : DefaultViewportCullingMargin;
            if (_viewportCullingMargin == normalized)
            {
                return;
            }

            _viewportCullingMargin = normalized;
            UpdateNodeControlHostLayout();
            InvalidateVisual();
        }
    }
}
