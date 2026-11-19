using Avalonia.Controls;
using Kudo.Controls;

namespace Kudo.Demo.Desktop.Views;

public partial class MainRibbonView : UserControl
{
    public MainRibbonView()
    {
        InitializeComponent();
    }

    /// <summary>Ribbon 外壳（页签条 + 命令区宿主）。</summary>
    public Kudo.Controls.KudoRibbon Ribbon => this.GetControl<Kudo.Controls.KudoRibbon>("RibbonControl");
}
