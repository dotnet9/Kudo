using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace Kudo.Benchmarks;

internal static class GraphBenchmarkAvaloniaHost
{
    public const double ViewportWidth = 1280;
    public const double ViewportHeight = 720;

    private static readonly object SyncRoot = new();
    private static bool _initialized;
    private static int _uiThreadId;

    public static void EnsureInitialized()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                VerifyAccess();
                return;
            }

            if (Application.Current is not null)
            {
                throw new InvalidOperationException(
                    "Avalonia 已由其他宿主初始化，无法建立可复现的 Headless 基准环境。");
            }

            AppBuilder.Configure<GraphBenchmarkApplication>()
                .UseHarfBuzz()
                .UseSkia()
                .WithInterFont()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    Fps = 60,
                    ShouldRenderOnUIThread = true,
                    UseHeadlessDrawing = false
                })
                .SetupWithoutStarting();

            _uiThreadId = Environment.CurrentManagedThreadId;
            _initialized = true;
        }
    }

    public static Window CreateWindow(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureInitialized();
        VerifyAccess();
        return new Window
        {
            Width = ViewportWidth,
            Height = ViewportHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowDecorations = WindowDecorations.None,
            Content = content
        };
    }

    public static void RenderFrame()
    {
        EnsureInitialized();
        VerifyAccess();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public static void DrainJobs()
    {
        EnsureInitialized();
        VerifyAccess();
        Dispatcher.UIThread.RunJobs();
    }

    private static void VerifyAccess()
    {
        if (_uiThreadId != Environment.CurrentManagedThreadId || !Dispatcher.UIThread.CheckAccess())
        {
            throw new InvalidOperationException("Avalonia Render 基准必须在初始化 Headless 宿主的线程上运行。");
        }
    }

    private sealed class GraphBenchmarkApplication : Application;
}
