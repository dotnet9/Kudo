using Avalonia.Markup.Xaml;

namespace Kudo.AvaloniaCommon.Views;

public partial class NoRegisterDialogView : CodeWFWindow
{
    public NoRegisterDialogView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}