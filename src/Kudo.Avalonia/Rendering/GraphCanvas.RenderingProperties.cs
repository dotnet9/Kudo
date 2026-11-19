using Kudo;
using Kudo.Avalonia.Designer;
using Kudo.Core.Runtime;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public bool IsLevelOfDetailEnabled
    {
        get => GetValue(IsLevelOfDetailEnabledProperty);
        set => SetValue(IsLevelOfDetailEnabledProperty, value);
    }

    public double LodPortsZoomThreshold
    {
        get => GetValue(LodPortsZoomThresholdProperty);
        set => SetValue(LodPortsZoomThresholdProperty, GraphCanvasLodSettings.NormalizeThreshold(value, 0.4));
    }

    public double LodLabelsZoomThreshold
    {
        get => GetValue(LodLabelsZoomThresholdProperty);
        set => SetValue(LodLabelsZoomThresholdProperty, GraphCanvasLodSettings.NormalizeThreshold(value, 0.3));
    }

    public double LodSimplifiedZoomThreshold
    {
        get => GetValue(LodSimplifiedZoomThresholdProperty);
        set => SetValue(LodSimplifiedZoomThresholdProperty, GraphCanvasLodSettings.NormalizeThreshold(value, 0.4));
    }

    public GraphLabelVisibility PortLabelVisibility
    {
        get => GetValue(PortLabelVisibilityProperty);
        set => SetValue(PortLabelVisibilityProperty, value);
    }

    public GraphLabelVisibility SymbolNameVisibility
    {
        get => GetValue(SymbolNameVisibilityProperty);
        set => SetValue(SymbolNameVisibilityProperty, value);
    }

    public GraphLabelVisibility SymbolDescriptionVisibility
    {
        get => GetValue(SymbolDescriptionVisibilityProperty);
        set => SetValue(SymbolDescriptionVisibilityProperty, value);
    }

    public GraphRuntimeSnapshot? RuntimeSnapshot
    {
        get => GetValue(RuntimeSnapshotProperty);
        set => SetValue(RuntimeSnapshotProperty, value);
    }

    public IReadOnlyCollection<string> BreakpointNodeIds
    {
        get => GetValue(BreakpointNodeIdsProperty);
        set => SetValue(BreakpointNodeIdsProperty, value ?? Array.Empty<string>());
    }

    public bool IsRuntimeAnimationActive => _runtimeController.IsAnimationActive;

    public double RuntimeFlowProgress => _runtimeController.FlowProgress;

    public IGraphBackgroundRenderer BackgroundRenderer
    {
        get => _backgroundRenderer;
        set
        {
            _backgroundRenderer = value ?? throw new ArgumentNullException(nameof(value));
            InvalidateVisual();
        }
    }

    public IGraphImageResolver? ImageResolver
    {
        get => _imageResolver;
        set
        {
            if (ReferenceEquals(_imageResolver, value))
            {
                return;
            }

            _imageResolver = value;
            InvalidateOverviewDrawing();
            UpdateRuntimeAnimationState();
            InvalidateVisual();
        }
    }

    public bool ShowPorts
    {
        get => GetValue(ShowPortsProperty);
        set => SetValue(ShowPortsProperty, value);
    }

    public IGraphNodeBackgroundImageResolver? BackgroundImageResolver
    {
        get => _backgroundImageResolver;
        set
        {
            if (ReferenceEquals(_backgroundImageResolver, value))
            {
                return;
            }

            _backgroundImageResolver = value;
            InvalidateOverviewDrawing();
            InvalidateVisual();
        }
    }

    public IGraphNodeRenderer NodeRenderer
    {
        get => _nodeRenderer;
        set
        {
            _nodeRenderer = value ?? throw new ArgumentNullException(nameof(value));
            InvalidateVisual();
        }
    }

    public IGraphPortRenderer PortRenderer
    {
        get => _portRenderer;
        set
        {
            _portRenderer = value ?? throw new ArgumentNullException(nameof(value));
            InvalidateVisual();
        }
    }

    public IGraphEdgeRenderer EdgeRenderer
    {
        get => _edgeRenderer;
        set
        {
            _edgeRenderer = value ?? throw new ArgumentNullException(nameof(value));
            UpdateEdgeLabelEditorLayout();
            InvalidateVisual();
        }
    }

    public IGraphGroupRenderer GroupRenderer
    {
        get => _groupRenderer;
        set
        {
            _groupRenderer = value ?? throw new ArgumentNullException(nameof(value));
            InvalidateVisual();
        }
    }
}
