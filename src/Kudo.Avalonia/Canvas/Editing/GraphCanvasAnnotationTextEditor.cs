using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasAnnotationTextEditor(GraphCanvasAnnotationTextEditorContext context)
{
    private const double MinimumEditingZoom = 0.35;

    private readonly TextBox _editor = new()
    {
        Name = GraphCanvas.AnnotationTextEditorPartName,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        IsVisible = false,
        MinHeight = 0,
        Padding = new Thickness(6),
        VerticalContentAlignment = VerticalAlignment.Top,
        ZIndex = int.MaxValue
    };
    private AnnotationModel? _editingAnnotation;
    private bool _isEndingEdit;

    public bool IsEditing => _editingAnnotation is not null;

    public string? EditingAnnotationId => _editingAnnotation?.Id;

    public void Initialize()
    {
        _editor.AddHandler(
            InputElement.KeyDownEvent,
            HandleKeyDown,
            RoutingStrategies.Tunnel);
        _editor.AddHandler(
            InputElement.LostFocusEvent,
            HandleLostFocus,
            RoutingStrategies.Bubble);
        context.NodeControlLayer.Children.Add(_editor);
    }

    public bool Begin(string annotationId)
    {
        if (!context.IsEnabled() ||
            context.IsReadOnly() ||
            context.Viewport().Zoom < MinimumEditingZoom ||
            context.Document() is not { } document ||
            document.FindAnnotation(annotationId) is not { } annotation ||
            !context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.EditAnnotationText,
                document,
                Annotation: annotation)))
        {
            return false;
        }

        if (ReferenceEquals(_editingAnnotation, annotation))
        {
            _editor.Focus();
            return true;
        }

        context.CommitNodeTitleEdit();
        context.CommitEdgeLabelEdit();
        Cancel();
        context.SetSelectedAnnotation(annotation.Id);
        _editingAnnotation = annotation;
        _editor.Text = annotation.Text ?? string.Empty;
        _editor.IsVisible = true;
        UpdateLayout();
        _editor.Focus();
        _editor.CaretIndex = _editor.Text?.Length ?? 0;
        context.InvalidateVisual();
        return true;
    }

    public bool Commit() => End(commit: true);

    public bool Cancel() => End(commit: false);

    public void UpdateLayout()
    {
        if (_editingAnnotation is not { } annotation)
        {
            return;
        }

        if (context.Document() is not { } document ||
            !ReferenceEquals(document.FindAnnotation(annotation.Id), annotation))
        {
            Cancel();
            return;
        }

        var annotationRect = context.GetAnnotationScreenRect(annotation);
        var inset = Math.Min(
            8 * context.Viewport().Zoom,
            Math.Min(annotationRect.Width, annotationRect.Height) / 4);
        var rect = new Rect(
            annotationRect.X + inset,
            annotationRect.Y + inset,
            Math.Max(24, annotationRect.Width - inset * 2),
            Math.Max(24, annotationRect.Height - inset * 2));
        Canvas.SetLeft(_editor, rect.X);
        Canvas.SetTop(_editor, rect.Y);
        _editor.Width = rect.Width;
        _editor.Height = rect.Height;
        _editor.FontSize = Math.Max(10, 13 * context.Viewport().Zoom);
        _editor.Measure(rect.Size);
        _editor.Arrange(rect);
        context.NodeControlLayer.InvalidateArrange();
    }

    public bool IsEditingAnnotation(AnnotationModel annotation) =>
        ReferenceEquals(_editingAnnotation, annotation);

    public bool IsEditorSource(object? source)
    {
        var visual = source as Visual;
        while (visual is not null && !ReferenceEquals(visual, context.Owner))
        {
            if (ReferenceEquals(visual, _editor))
            {
                return true;
            }

            visual = visual.GetVisualParent();
        }

        return false;
    }

    private bool End(bool commit)
    {
        if (_editingAnnotation is not { } annotation || _isEndingEdit)
        {
            return false;
        }

        _isEndingEdit = true;
        try
        {
            var document = context.Document();
            var newText = _editor.Text;
            _editingAnnotation = null;
            _editor.IsVisible = false;

            if (!commit ||
                document is null ||
                !ReferenceEquals(document.FindAnnotation(annotation.Id), annotation) ||
                newText == annotation.Text)
            {
                context.InvalidateVisual();
                return true;
            }

            var command = new SetAnnotationTextCommand(
                annotation.Id,
                annotation.Text,
                newText,
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.EditAnnotationTextCommand));
            if (!context.CanPerformEdit(new GraphEditRequest(
                    GraphEditOperation.EditAnnotationText,
                    document,
                    Annotation: annotation,
                    Command: command)))
            {
                context.InvalidateVisual();
                return true;
            }

            annotation.Text = newText;
            return context.CommitGraphEdit(command);
        }
        finally
        {
            _isEndingEdit = false;
        }
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancel();
            context.FocusCanvas();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Commit();
            context.FocusCanvas();
            e.Handled = true;
        }
    }

    private void HandleLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!_isEndingEdit)
        {
            Commit();
        }
    }
}

internal sealed class GraphCanvasAnnotationTextEditorContext
{
    public required Visual Owner { get; init; }

    public required Canvas NodeControlLayer { get; init; }

    public required Func<GraphDocument?> Document { get; init; }

    public required Func<bool> IsEnabled { get; init; }

    public required Func<bool> IsReadOnly { get; init; }

    public required Func<GraphViewport> Viewport { get; init; }

    public required Func<GraphEditRequest, bool> CanPerformEdit { get; init; }

    public required Func<bool> CommitNodeTitleEdit { get; init; }

    public required Func<bool> CommitEdgeLabelEdit { get; init; }

    public required Action<string> SetSelectedAnnotation { get; init; }

    public required Func<AnnotationModel, Rect> GetAnnotationScreenRect { get; init; }

    public required Func<IGraphCommand, bool> CommitGraphEdit { get; init; }

    public required Action InvalidateVisual { get; init; }

    public required Action FocusCanvas { get; init; }
}
