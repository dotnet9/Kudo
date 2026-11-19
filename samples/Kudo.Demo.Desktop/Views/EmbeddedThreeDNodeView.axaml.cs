using Avalonia;
using Avalonia.Controls;
using Kudo.Demo.Desktop.ViewModels;

namespace Kudo.Demo.Desktop.Views;

public partial class EmbeddedThreeDNodeView : UserControl
{
    public EmbeddedThreeDNodeView()
    {
        InitializeComponent();
        AttachedToVisualTree += HandleAttachedToVisualTree;
        DetachedFromVisualTree += HandleDetachedFromVisualTree;
    }

    private void HandleAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) =>
        (DataContext as EmbeddedThreeDNodeViewModel)?.Attach();

    private void HandleDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e) =>
        (DataContext as EmbeddedThreeDNodeViewModel)?.Detach();
}
