using Avalonia;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public static readonly StyledProperty<bool> IsEdgeLabelEditingEnabledProperty =
        AvaloniaProperty.Register<GraphCanvas, bool>(nameof(IsEdgeLabelEditingEnabled), true);

    public bool IsEdgeLabelEditingEnabled
    {
        get => GetValue(IsEdgeLabelEditingEnabledProperty);
        set => SetValue(IsEdgeLabelEditingEnabledProperty, value);
    }

    public bool IsEditingEdgeLabel => _edgeLabelEditingController.IsEditing;

    public string? EditingEdgeLabelId => _edgeLabelEditingController.EditingEdgeId;

    public bool BeginEdgeLabelEdit(string edgeId) =>
        _edgeLabelEditingController.Begin(edgeId);

    public bool CommitEdgeLabelEdit() =>
        _edgeLabelEditingController.Commit();

    public bool CancelEdgeLabelEdit() =>
        _edgeLabelEditingController.Cancel();

    public Rect? GetEdgeLabelScreenBounds(EdgeModel edge) =>
        _edgeLabelEditingController.GetScreenBounds(edge);

    public EdgeModel? HitTestEdgeLabel(Point screenPoint) =>
        _edgeLabelEditingController.HitTest(screenPoint);

    private void InitializeEdgeLabelEditor() =>
        _edgeLabelEditingController.Initialize();

    internal bool TryBeginEdgeLabelEdit(EdgeModel edge, Point screenPoint) =>
        _edgeLabelEditingController.TryBegin(edge, screenPoint);

    private void UpdateEdgeLabelEditorLayout() =>
        _edgeLabelEditingController.UpdateLayout();
}
