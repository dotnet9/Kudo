using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    internal void InvalidateNodeGeometry(string nodeId)
    {
        _changedGeometryNodeIds.Clear();
        _changedGeometryNodeIds.Add(nodeId);
        InvalidateNodeGeometryCore();
    }

    internal void InvalidateNodeGeometry(IEnumerable<string> nodeIds)
    {
        _changedGeometryNodeIds.Clear();
        _changedGeometryNodeIds.UnionWith(nodeIds);
        InvalidateNodeGeometryCore();
    }

    private void InvalidateNodeGeometryCore()
    {
        var document = Document;
        if (document is null || _changedGeometryNodeIds.Count == 0)
        {
            _elementSpatialIndex.Invalidate();
            ClearEdgeRoutingCache();
            InvalidateDocumentGraphBounds();
            InvalidateOverviewDrawing();
            NodeGeometryChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!_elementSpatialIndex.TryUpdateNodeGeometry(document, _changedGeometryNodeIds))
        {
            _elementSpatialIndex.Invalidate();
        }

        if (!TryPrepareIncrementalRoutingInvalidation(document, out var changedObstacleRegions))
        {
            InvalidateAllSmartEdgeGeometry(document);
            InvalidateDocumentGraphBounds();
            InvalidateOverviewDrawing();
            NodeGeometryChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        CollectAffectedGeometryEdges(document, changedObstacleRegions);
        if (!_edgeSpatialIndex.TryUpdateEdges(
                document.Edges,
                _affectedGeometryEdges,
                _edgeGraphPathProvider))
        {
            _edgeSpatialIndex.Invalidate();
        }

        InvalidateDocumentGraphBounds();
        InvalidateOverviewDrawing();
        NodeGeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TryPrepareIncrementalRoutingInvalidation(
        GraphDocument document,
        out IReadOnlyList<GraphRect> changedObstacleRegions)
    {
        changedObstacleRegions = [];
        if (EdgeObstacleProvider is not GraphCanvasEdgeObstacleProvider ||
            _defaultEdgeObstacleSnapshot is not { } previousSnapshot ||
            _defaultEdgeObstacleSnapshotRevision != _smartEdgeObstacleRevision ||
            !ReferenceEquals(previousSnapshot.Document, document))
        {
            return false;
        }

        var regions = new List<GraphRect>(_changedGeometryNodeIds.Count);
        foreach (var nodeId in _changedGeometryNodeIds)
        {
            var node = document.FindNode(nodeId);
            if (node is null)
            {
                return false;
            }

            var changedGroup = false;
            foreach (var group in document.Groups)
            {
                if (!(group.NodeIds ?? []).Contains(nodeId, StringComparer.Ordinal) ||
                    !group.IsCollapsed)
                {
                    continue;
                }

                if (!previousSnapshot.TryGetGroupBounds(group.Id, out var oldBounds) ||
                    GraphGroupBoundsCalculator.Calculate(document, group) is not { } newBounds ||
                    !TryAddChangedRegions(regions, oldBounds, newBounds))
                {
                    return false;
                }

                changedGroup = true;
            }

            if (changedGroup)
            {
                continue;
            }

            if (!previousSnapshot.TryGetNodeBounds(nodeId, out var previousBounds) ||
                !TryAddChangedRegions(
                    regions,
                    previousBounds,
                    GraphNodeGeometry.GetCanvasBounds(node)))
            {
                return false;
            }
        }

        _defaultEdgeObstacleSnapshot = GraphCanvasEdgeObstacleProvider.Default.CreateSnapshot(document);
        _defaultEdgeObstacleSnapshotRevision = _smartEdgeObstacleRevision;
        changedObstacleRegions = regions;
        return true;
    }

    private void CollectAffectedGeometryEdges(
        GraphDocument document,
        IReadOnlyList<GraphRect> changedObstacleRegions)
    {
        CollectAffectedEdgeEndpointNodeIds(document);

        _affectedGeometryEdges.Clear();
        foreach (var edge in document.Edges)
        {
            if (_affectedEdgeEndpointNodeIds.Contains(edge.Source.NodeId) ||
                _affectedEdgeEndpointNodeIds.Contains(edge.Target.NodeId))
            {
                _affectedGeometryEdges.Add(edge);
            }
        }

        _edgePathCache.Remove(_affectedGeometryEdges);
        foreach (var edge in _edgePathCache.RemoveSmartOrthogonalIntersecting(changedObstacleRegions))
        {
            if (!_affectedGeometryEdges.Contains(edge))
            {
                _affectedGeometryEdges.Add(edge);
            }
        }
    }

    private void InvalidateAllSmartEdgeGeometry(GraphDocument document)
    {
        _smartEdgeObstacleRevision++;
        _defaultEdgeObstacleSnapshot = null;
        _defaultEdgeObstacleSnapshotRevision = -1;
        CollectAffectedEdgeEndpointNodeIds(document);
        _affectedGeometryEdges.Clear();
        foreach (var edge in document.Edges)
        {
            if (edge.RoutingStyle == EdgeRoutingStyle.SmartOrthogonal ||
                _affectedEdgeEndpointNodeIds.Contains(edge.Source.NodeId) ||
                _affectedEdgeEndpointNodeIds.Contains(edge.Target.NodeId))
            {
                _affectedGeometryEdges.Add(edge);
            }
        }

        _edgePathCache.Remove(_affectedGeometryEdges);
        if (!_edgeSpatialIndex.TryUpdateEdges(
                document.Edges,
                _affectedGeometryEdges,
                _edgeGraphPathProvider))
        {
            _edgeSpatialIndex.Invalidate();
        }
    }

    private void CollectAffectedEdgeEndpointNodeIds(GraphDocument document)
    {
        _affectedEdgeEndpointNodeIds.Clear();
        _affectedEdgeEndpointNodeIds.UnionWith(_changedGeometryNodeIds);
        foreach (var group in document.Groups)
        {
            if (group.IsCollapsed &&
                (group.NodeIds ?? []).Any(_changedGeometryNodeIds.Contains))
            {
                _affectedEdgeEndpointNodeIds.UnionWith(group.NodeIds ?? []);
            }
        }
    }

    private double GetSmartObstaclePadding()
    {
        var padding = EdgeRoutingOptions.SmartOrthogonal.ObstaclePadding;
        return double.IsFinite(padding) && padding >= 0 ? padding : 0;
    }

    private bool TryAddChangedRegions(
        ICollection<GraphRect> regions,
        GraphRect previous,
        GraphRect current)
    {
        if (!IsFinite(previous) || !IsFinite(current))
        {
            return false;
        }

        var padding = GetSmartObstaclePadding();
        regions.Add(Inflate(previous, padding));
        if (current != previous)
        {
            regions.Add(Inflate(current, padding));
        }

        return true;
    }

    private static GraphRect Inflate(GraphRect bounds, double padding) => new(
        bounds.X - padding,
        bounds.Y - padding,
        bounds.Width + padding * 2,
        bounds.Height + padding * 2);

    private static bool IsFinite(GraphRect bounds) =>
        double.IsFinite(bounds.X) &&
        double.IsFinite(bounds.Y) &&
        double.IsFinite(bounds.Width) &&
        bounds.Width >= 0 &&
        double.IsFinite(bounds.Height) &&
        bounds.Height >= 0;

    internal void InvalidateDocumentGraphBounds()
    {
        _documentGraphBoundsCache = null;
        _isDocumentGraphBoundsDirty = true;
    }

    private void InvalidateOverviewDrawing()
    {
        _renderer.InvalidateOverview();
    }
}
