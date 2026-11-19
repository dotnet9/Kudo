using System.ComponentModel;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasEdgeObserver(
    Func<GraphDocument?> documentAccessor,
    Action<EdgeModel> invalidateEdgeGeometry,
    Action invalidateDocumentLookup,
    Action invalidateOverviewDrawing,
    Action updateRuntimeAnimationState,
    Action invalidateVisual,
    Action raiseGraphChanged)
{
    private readonly HashSet<EdgeModel> _observedEdges = [];

    public void Synchronize()
    {
        var currentEdges = documentAccessor()?.Edges.ToHashSet() ?? [];
        foreach (var edge in _observedEdges.Except(currentEdges).ToArray())
        {
            edge.PropertyChanged -= HandleObservedEdgePropertyChanged;
            _observedEdges.Remove(edge);
        }

        foreach (var edge in currentEdges.Except(_observedEdges))
        {
            edge.PropertyChanged += HandleObservedEdgePropertyChanged;
            _observedEdges.Add(edge);
        }
    }

    public void Unsubscribe()
    {
        foreach (var edge in _observedEdges)
        {
            edge.PropertyChanged -= HandleObservedEdgePropertyChanged;
        }

        _observedEdges.Clear();
    }

    private void HandleObservedEdgePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not EdgeModel edge || documentAccessor()?.Edges.Contains(edge) != true)
        {
            return;
        }

        if (e.PropertyName is nameof(EdgeModel.Source) or
            nameof(EdgeModel.Target) or
            nameof(EdgeModel.RoutingStyle) or
            nameof(EdgeModel.Waypoints) or
            nameof(EdgeModel.SourceMarker) or
            nameof(EdgeModel.TargetMarker))
        {
            invalidateEdgeGeometry(edge);
            if (e.PropertyName is nameof(EdgeModel.Source) or nameof(EdgeModel.Target))
            {
                invalidateDocumentLookup();
            }
        }
        else
        {
            invalidateOverviewDrawing();
        }

        if (e.PropertyName is nameof(EdgeModel.Appearance) or nameof(EdgeModel.StylePreset))
        {
            updateRuntimeAnimationState();
        }

        invalidateVisual();
        raiseGraphChanged();
    }
}
