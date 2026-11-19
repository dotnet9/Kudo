using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    internal PortHitResult? FindCompatibleConnectionCandidate(
        Point screenPoint,
        PortHitResult startPort)
    {
        return FindCompatibleConnectionCandidate(screenPoint, candidate =>
        {
            if (candidate.Reference == startPort.Reference)
            {
                return false;
            }

            var (source, target) = NormalizeConnection(startPort, candidate);
            return ValidateConnection(
                source,
                target,
                ResolveRelationKind(startPort.Port)).IsValid;
        });
    }

    internal PortHitResult? FindCompatibleConnectionCandidate(
        Point screenPoint,
        EdgeModel edge,
        GraphEdgeEndpoint endpoint)
    {
        return FindCompatibleConnectionCandidate(screenPoint, candidate =>
        {
            var source = endpoint == GraphEdgeEndpoint.Source ? candidate.Reference : edge.Source;
            var target = endpoint == GraphEdgeEndpoint.Target ? candidate.Reference : edge.Target;
            return (source != edge.Source || target != edge.Target) &&
                ValidateConnection(
                    source,
                    target,
                    edge.RelationKind,
                    edge.Id).IsValid;
        });
    }

    internal PortHitResult? FindCompatibleConnectionCandidate(
        Point screenPoint,
        Func<PortHitResult, bool> isCompatible)
    {
        var document = Document;
        var maximumDistance = ConnectionSnapDistance;
        if (document is null || maximumDistance <= 0)
        {
            return null;
        }

        var maximumDistanceSquared = maximumDistance * maximumDistance;
        var graphPoint = ScreenToGraph(screenPoint);
        var graphDistance = maximumDistance / Viewport.Zoom;
        _elementSpatialIndex.RebuildIfNeeded(document);
        var candidates = _elementSpatialIndex.QueryNodes(new GraphRect(
                graphPoint.X - graphDistance,
                graphPoint.Y - graphDistance,
                graphDistance * 2,
                graphDistance * 2))
            .Reverse()
            .SelectMany(node => node.Ports.Select(port =>
            {
                var position = GetPortScreenPosition(node, port);
                return new
                {
                    Hit = new PortHitResult(
                        node,
                        port,
                        new PortReference(node.Id, port.Id),
                        position),
                    Distance = GraphGeometryMath.DistanceSquared(screenPoint, position)
                };
            }))
            .Where(candidate => candidate.Distance <= maximumDistanceSquared)
            .OrderBy(candidate => candidate.Distance);

        foreach (var candidate in candidates)
        {
            if (isCompatible(candidate.Hit))
            {
                return candidate.Hit;
            }
        }

        return null;
    }

    private static (PortReference Source, PortReference Target) NormalizeConnection(
        PortHitResult first,
        PortHitResult second)
    {
        if (!PortKindSemantics.CanSendConnection(first.Port.Kind) &&
            PortKindSemantics.CanSendConnection(second.Port.Kind))
        {
            return (second.Reference, first.Reference);
        }

        if (PortKindSemantics.CanReceiveConnection(first.Port.Kind) &&
            !PortKindSemantics.CanReceiveConnection(second.Port.Kind))
        {
            return (second.Reference, first.Reference);
        }

        return (first.Reference, second.Reference);
    }

    private static GraphRelationKind ResolveRelationKind(PortModel startPort) =>
        startPort.Kind == PortKind.Reference
            ? GraphRelationKind.Reference
            : GraphRelationKind.Connection;
}
