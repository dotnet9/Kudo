using Avalonia;
using System;
using Avalonia.Media;
using Kudo.Themes;
using TKCode.Arc;
using TKCode.Arc.Motion;

using ReactiveUI.Avalonia;

namespace Kudo.Demo.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new FontManagerOptions { DefaultFamilyName = KudoThemeFonts.DefaultFamilyName })
            .UseArc(arc => arc
                .UseArcControls()
                .UseArcDock()
                .WithTheme(theme => theme
                    .Appearance(ArcAppearance.Light)
                    .MotionLevel(ArcMotionLevel.Reduced)))
            .UseReactiveUI(_ => { })
            .LogToTrace();
}
