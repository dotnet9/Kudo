using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Kudo.Controls;

/// <summary>KudoRibbon 样式类（编译期 XAML，AOT 兼容；KudoTheme include 链自动挂载）。</summary>
public sealed class KudoRibbonStyles : Styles
{
    public KudoRibbonStyles()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
