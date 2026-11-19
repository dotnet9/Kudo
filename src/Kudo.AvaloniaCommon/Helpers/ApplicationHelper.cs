using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Kudo.AvaloniaCommon.Helpers;

public static class ApplicationHelper
{
    public static void OptimizeRendering(this Visual visual)
    {
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.SubpixelAntialias);
        RenderOptions.SetBitmapInterpolationMode(visual, BitmapInterpolationMode.HighQuality);
        RenderOptions.SetEdgeMode(visual, EdgeMode.Antialias);
    }
}
