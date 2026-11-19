namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    internal void ApplyNodeSelection(
        IReadOnlyCollection<string> nodeIds,
        string? primaryNodeId,
        bool clearEdgeSelection) =>
        _selectionController.ApplyNodeSelection(nodeIds, primaryNodeId, clearEdgeSelection);

    internal void ApplyEdgeSelection(
        IReadOnlyCollection<string> edgeIds,
        string? primaryEdgeId,
        bool clearOtherSelection) =>
        _selectionController.ApplyEdgeSelection(edgeIds, primaryEdgeId, clearOtherSelection);

    private void RaiseSelectionChanged() =>
        _selectionController.RaiseSelectionChanged();

    private bool UpdateSelectionSnapshots() =>
        _selectionController.UpdateSelectionSnapshots();

    private void NotifySelectionChanged() =>
        NotifySelectionChanged(new GraphSelectionChangedEventArgs(
            SelectedNodeIds,
            SelectedGroupIds,
            SelectedNodeId,
            SelectedEdgeIds,
            SelectedEdgeId,
            SelectedAnnotationId));

    private void NotifySelectionChanged(GraphSelectionChangedEventArgs args)
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        ExecuteNotificationCommand(SelectionChangedCommand, args);
    }

    private static void ExecuteNotificationCommand(System.Windows.Input.ICommand? command, object parameter)
    {
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    internal IReadOnlyList<string> GetDocumentOrderedNodeIds(IEnumerable<string> nodeIds) =>
        _selectionController.GetDocumentOrderedNodeIds(nodeIds);

    internal IReadOnlyList<string> GetDocumentOrderedEdgeIds(IEnumerable<string> edgeIds) =>
        _selectionController.GetDocumentOrderedEdgeIds(edgeIds);
}
