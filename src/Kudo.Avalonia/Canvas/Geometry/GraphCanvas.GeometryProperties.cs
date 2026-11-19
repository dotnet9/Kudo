using Kudo;
using Kudo.Core.Algorithms;
using Kudo.Avalonia.Designer;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    /// <summary>
    /// Resolves graph-space port positions used by routing, hit testing and connection previews.
    /// </summary>
    public IGraphPortPositionProvider PortPositionProvider
    {
        get => _portPositionProvider;
        set
        {
            var provider = value ?? throw new ArgumentNullException(nameof(value));
            if (ReferenceEquals(_portPositionProvider, provider))
            {
                return;
            }

            _portPositionProvider = provider;
            ClearEdgeRoutingCache();
            InvalidateVisual();
        }
    }

    public IGraphNodeGeometryProvider NodeGeometryProvider
    {
        get => _nodeGeometryProvider;
        set
        {
            var provider = value ?? throw new ArgumentNullException(nameof(value));
            if (ReferenceEquals(_nodeGeometryProvider, provider))
            {
                return;
            }

            _nodeGeometryProvider = provider;
            _elementSpatialIndex.GeometryProvider = provider;
            _elementSpatialIndex.Invalidate();
            ClearEdgeRoutingCache();
            InvalidateVisual();
        }
    }

    internal void RefreshNodeGeometryProvider()
    {
        _elementSpatialIndex.Invalidate();
        ClearEdgeRoutingCache();
        InvalidateVisual();
    }

    public GraphSize MinimumNodeSize
    {
        get => _minimumNodeSize;
        set => _minimumNodeSize = GraphNodeResizeCalculator.NormalizeMinimumSize(value);
    }

    /// <summary>
    /// Resolves per-node minimum size. Returning null falls back to <see cref="MinimumNodeSize" />.
    /// </summary>
    public Func<NodeModel, GraphSize?>? MinimumNodeSizeResolver { get; set; }
}
