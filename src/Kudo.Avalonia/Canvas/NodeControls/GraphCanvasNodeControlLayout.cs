using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasNodeControlLayout(GraphCanvasNodeControlContext context)
{
    private HashSet<GraphNodeControlHost> _visibleHosts = [];
    private HashSet<GraphNodeControlHost> _nextVisibleHosts = [];

    public Rect GetScreenRect(NodeModel node)
    {
        var nodeRect = context.GetNodeScreenRect(node);
        var horizontalPadding = Math.Min(nodeRect.Width / 4, 8 * context.Viewport().Zoom);
        var topPadding = Math.Min(nodeRect.Height / 2, 30 * context.Viewport().Zoom);
        var bottomPadding = Math.Min(nodeRect.Height / 4, 8 * context.Viewport().Zoom);
        return new Rect(
            nodeRect.X + horizontalPadding,
            nodeRect.Y + topPadding,
            Math.Max(0, nodeRect.Width - horizontalPadding * 2),
            Math.Max(0, nodeRect.Height - topPadding - bottomPadding));
    }

    public void Update(IReadOnlyDictionary<string, GraphNodeControlHost> hosts)
    {
        if (hosts.Count == 0)
        {
            _visibleHosts.Clear();
            _nextVisibleHosts.Clear();
            return;
        }

        _nextVisibleHosts.Clear();
        var cullingBounds = context.GetViewportCullingGraphBounds();
        if (!context.IsViewportCullingEnabled())
        {
            foreach (var host in hosts.Values)
            {
                var isVisible = context.IsNodeVisible(host.Context.Node.Id);
                var rect = UpdateScreenRect(host);
                SetVisibility(host, isVisible);
                if (isVisible)
                {
                    EnsureLayoutWhenShown(host, rect);
                    _nextVisibleHosts.Add(host);
                }
            }
        }
        else if (context.Document() is { } document && cullingBounds is { } visibleBounds)
        {
            context.ElementSpatialIndex.RebuildIfNeeded(document);
            foreach (var node in context.ElementSpatialIndex.QueryNodes(visibleBounds))
            {
                if (!hosts.TryGetValue(node.Id, out var host) ||
                    !ReferenceEquals(host.Context.Node, node))
                {
                    continue;
                }

                var rect = UpdateScreenRect(host);
                SetVisibility(host, true);
                EnsureLayoutWhenShown(host, rect);
                _nextVisibleHosts.Add(host);
            }
        }

        foreach (var host in _visibleHosts)
        {
            if (!_nextVisibleHosts.Contains(host))
            {
                SetVisibility(host, false);
            }
        }

        (_visibleHosts, _nextVisibleHosts) = (_nextVisibleHosts, _visibleHosts);
        _nextVisibleHosts.Clear();
        context.NodeControlLayer.InvalidateArrange();
    }

    public void Update(
        IReadOnlyDictionary<string, GraphNodeControlHost> hosts,
        IEnumerable<string> nodeIds)
    {
        if (hosts.Count == 0)
        {
            return;
        }

        var cullingBounds = context.GetViewportCullingGraphBounds();
        foreach (var nodeId in nodeIds)
        {
            Update(hosts, nodeId, cullingBounds);
        }

        context.NodeControlLayer.InvalidateArrange();
    }

    public void Update(
        IReadOnlyDictionary<string, GraphNodeControlHost> hosts,
        string nodeId)
    {
        Update(hosts, nodeId, context.GetViewportCullingGraphBounds());
        context.NodeControlLayer.InvalidateArrange();
    }

    public void Remove(GraphNodeControlHost host)
    {
        SetVisibility(host, false);
        _visibleHosts.Remove(host);
        _nextVisibleHosts.Remove(host);
    }

    private void Update(
        IReadOnlyDictionary<string, GraphNodeControlHost> hosts,
        string nodeId,
        GraphRect? cullingBounds)
    {
        if (!hosts.TryGetValue(nodeId, out var host))
        {
            return;
        }

        var isVisible = context.IsNodeVisible(nodeId) &&
            (!context.IsViewportCullingEnabled() ||
             cullingBounds is { } bounds && Intersects(bounds, context.GetNodeGraphRect(host.Context.Node)));
        if (!isVisible)
        {
            SetVisibility(host, false);
            _visibleHosts.Remove(host);
            return;
        }

        var rect = UpdateScreenRect(host);
        SetVisibility(host, true);
        EnsureLayoutWhenShown(host, rect);
        _visibleHosts.Add(host);
    }

    private static void SetVisibility(GraphNodeControlHost host, bool isVisible)
    {
        host.IsVisible = isVisible;
        host.Context.SetControlVisible(isVisible);
    }

    private Rect UpdateScreenRect(GraphNodeControlHost host)
    {
        var node = host.Context.Node;
        var nodeRect = context.GetNodeScreenRect(node);
        var rect = GetScreenRect(node);
        Canvas.SetLeft(host, rect.X);
        Canvas.SetTop(host, rect.Y);
        host.Width = rect.Width;
        host.Height = rect.Height;
        ApplyRotation(host, node, nodeRect, rect);
        return rect;
    }

    private void EnsureLayoutWhenShown(GraphNodeControlHost host, Rect rect)
    {
        if (_visibleHosts.Contains(host))
        {
            return;
        }

        // Avalonia can retain the pre-hide Bounds for one render tick; force only hidden -> visible.
        host.Measure(rect.Size);
        host.Arrange(rect);
    }

    private static void ApplyRotation(
        Control control,
        NodeModel node,
        Rect nodeRect,
        Rect controlRect)
    {
        var rotation = GraphNodeGeometry.NormalizeRotation(node.Rotation);
        if (rotation == 0 || controlRect.Width <= 0 || controlRect.Height <= 0)
        {
            control.RenderTransform = null;
            control.RenderTransformOrigin = RelativePoint.Center;
            return;
        }

        control.RenderTransformOrigin = new RelativePoint(
            (nodeRect.Center.X - controlRect.X) / controlRect.Width,
            (nodeRect.Center.Y - controlRect.Y) / controlRect.Height,
            RelativeUnit.Relative);
        control.RenderTransform = new RotateTransform(rotation);
    }

    private static bool Intersects(GraphRect first, Rect second) =>
        first.X <= second.Right &&
        first.X + first.Width >= second.Left &&
        first.Y <= second.Bottom &&
        first.Y + first.Height >= second.Top;
}
