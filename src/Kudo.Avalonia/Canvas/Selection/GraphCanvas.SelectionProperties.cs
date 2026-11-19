using System.Windows.Input;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public string? SelectedNodeId
    {
        get => GetValue(SelectedNodeIdProperty);
        set
        {
            if (!_isUpdatingNodeSelection)
            {
                _selectionController.SetSelectedNodes(value is null ? [] : [value], value);
                return;
            }

            SetValue(SelectedNodeIdProperty, value);
        }
    }

    public string? SelectedEdgeId
    {
        get => GetValue(SelectedEdgeIdProperty);
        set
        {
            if (!_isUpdatingEdgeSelection)
            {
                _selectionController.SetSelectedEdges(value is null ? [] : [value], value);
                return;
            }

            SetValue(SelectedEdgeIdProperty, value);
        }
    }

    public IReadOnlyList<string> SelectedNodeIds => _canvasState.SelectedNodeIdsSnapshot;

    public IReadOnlyList<string> SelectedEdgeIds => _canvasState.SelectedEdgeIdsSnapshot;

    public IReadOnlyList<string> SelectedGroupIds => _canvasState.SelectedGroupIdsSnapshot;

    public ICommand? SelectionChangedCommand
    {
        get => GetValue(SelectionChangedCommandProperty);
        set => SetValue(SelectionChangedCommandProperty, value);
    }

    public IReadOnlyList<GraphAlignmentGuide> ActiveAlignmentGuides
    {
        get => _activeAlignmentGuides;
        internal set => _activeAlignmentGuides = value;
    }
}
