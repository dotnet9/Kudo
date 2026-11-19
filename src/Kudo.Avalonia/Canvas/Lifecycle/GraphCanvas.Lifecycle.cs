using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _runtimeController.SetAttachedToVisualTree(true);
        _edgeObserver.Synchronize();
        _keyboardController.Subscribe();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelActivePointerInteraction();
        CancelManualConnectionDraft();
        CancelNodeTitleEdit();
        CancelAnnotationTextEdit();
        CancelEdgeLabelEdit();
        ResetAutoPan();
        _keyboardController.Unsubscribe();
        _keyboardController.SetSpacePanMode(false);
        _runtimeController.SetAttachedToVisualTree(false);
        _edgeObserver.Unsubscribe();
        ClearNodePlacementPreview();
        InvalidateOverviewDrawing();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateNodeControlHostLayout();
        UpdateNodeTitleEditorLayout();
        UpdateAnnotationTextEditorLayout();
        UpdateEdgeLabelEditorLayout();
        UpdateAutoPanState();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty)
        {
            LastPointerGraphPoint = null;
            LastPointerGraphPointVersion++;
        }

        _propertyChangeController.Handle(change);
    }

}
