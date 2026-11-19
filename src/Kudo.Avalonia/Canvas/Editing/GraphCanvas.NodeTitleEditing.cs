using Avalonia;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public static readonly StyledProperty<bool> IsNodeTitleEditingEnabledProperty =
        AvaloniaProperty.Register<GraphCanvas, bool>(nameof(IsNodeTitleEditingEnabled), true);

    public bool IsNodeTitleEditingEnabled
    {
        get => GetValue(IsNodeTitleEditingEnabledProperty);
        set => SetValue(IsNodeTitleEditingEnabledProperty, value);
    }

    public bool IsEditingNodeTitle => _nodeTitleEditingController.IsEditing;

    public string? EditingNodeTitleId => _nodeTitleEditingController.EditingNodeId;

    public bool CanBeginNodeTitleEdit(string nodeId) =>
        _nodeTitleEditingController.CanBegin(nodeId);

    public bool BeginNodeTitleEdit(string nodeId) =>
        _nodeTitleEditingController.Begin(nodeId);

    public bool CommitNodeTitleEdit() =>
        _nodeTitleEditingController.Commit();

    public bool CancelNodeTitleEdit() =>
        _nodeTitleEditingController.Cancel();

    private void InitializeNodeTitleEditor() =>
        _nodeTitleEditingController.Initialize();

    internal bool TryBeginNodeTitleEdit(NodeModel node, Point screenPoint) =>
        _nodeTitleEditingController.TryBegin(node, screenPoint);

    private void UpdateNodeTitleEditorLayout() =>
        _nodeTitleEditingController.UpdateLayout();

    private bool IsNodeTitleEditorSource(object? source)
    {
        if (_nodeTitleEditingController.IsEditorSource(source))
        {
            return true;
        }

        var visual = source as Visual;
        while (visual is not null && !ReferenceEquals(visual, this))
        {
            if (_annotationTextEditingController.IsEditorSource(visual) ||
                _edgeLabelEditingController.IsEditorSource(visual))
            {
                return true;
            }

            visual = visual.GetVisualParent();
        }

        return false;
    }
}
