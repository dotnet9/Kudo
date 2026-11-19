using Avalonia;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public static readonly StyledProperty<bool> IsAnnotationTextEditingEnabledProperty =
        AvaloniaProperty.Register<GraphCanvas, bool>(nameof(IsAnnotationTextEditingEnabled), true);

    public bool IsAnnotationTextEditingEnabled
    {
        get => GetValue(IsAnnotationTextEditingEnabledProperty);
        set => SetValue(IsAnnotationTextEditingEnabledProperty, value);
    }

    public bool IsEditingAnnotationText => _annotationTextEditingController.IsEditing;

    public string? EditingAnnotationId => _annotationTextEditingController.EditingAnnotationId;

    public bool BeginAnnotationTextEdit(string annotationId) =>
        _annotationTextEditingController.Begin(annotationId);

    public bool CommitAnnotationTextEdit() =>
        _annotationTextEditingController.Commit();

    public bool CancelAnnotationTextEdit() =>
        _annotationTextEditingController.Cancel();

    private void InitializeAnnotationTextEditor() =>
        _annotationTextEditingController.Initialize();

    internal void UpdateAnnotationTextEditorLayout() =>
        _annotationTextEditingController.UpdateLayout();
}
