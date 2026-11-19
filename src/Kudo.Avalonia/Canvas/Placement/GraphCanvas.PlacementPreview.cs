using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public GraphNodePlacementPreview UpdateNodePlacementPreview(
        GraphPoint rawPosition,
        GraphSize size,
        string? title = null,
        bool isValid = true,
        GraphNodeVisualMode visualMode = GraphNodeVisualMode.Card) =>
        _placementController.Update(rawPosition, size, title, isValid, visualMode);

    public void ClearNodePlacementPreview() =>
        _placementController.Clear();
}
