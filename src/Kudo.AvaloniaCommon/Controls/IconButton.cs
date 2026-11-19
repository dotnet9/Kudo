using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Kudo.AvaloniaCommon.Controls;

/// <summary>图标在图标槽中的排布方向。</summary>
public enum IconPlacement
{
    /// <summary>图标在文本左侧（水平排布）。</summary>
    Left,

    /// <summary>图标在文本上方（垂直排布，Ribbon 大命令）。</summary>
    Top
}

/// <summary>
///     Kudo 图标按钮：图标（Lucide 几何）+ 可选文本的组合按钮。继承原生 Button，保留 Command、Flyout、
///     伪类（:pointerover / :pressed / :disabled）能力；视觉由 Classes（RibbonCommand、QuickToolButton 等）定制。
/// </summary>
public class IconButton : Button
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<IconButton, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<IconPlacement> IconPlacementProperty =
        AvaloniaProperty.Register<IconButton, IconPlacement>(
            nameof(IconPlacement), IconPlacement.Left);

    /// <summary>图标几何。通常用 {lucide:LucideGeometry Kind} 提供。</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>图标相对文本的排布方向。</summary>
    public IconPlacement IconPlacement
    {
        get => GetValue(IconPlacementProperty);
        set => SetValue(IconPlacementProperty, value);
    }
}
