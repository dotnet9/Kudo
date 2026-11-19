using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Kudo.Controls.Tests;

namespace Kudo.Controls.Tests.TestInfrastructure;

/// <summary>
///     Headless 测试运行时：专用 STA 线程初始化 Headless 平台并循环泵 Dispatcher 任务，
///     测试经 Run(action) 在该 UI 线程上执行控件操作。
///     （不依赖 Avalonia.Headless.XUnit——其发现器尚未跟进 xunit.v3 4.x。）
/// </summary>
public static class HeadlessTestRuntime
{
    private static Thread? _uiThread;
    private static readonly object Gate = new();

    /// <summary>在 Headless UI 线程上同步执行操作。</summary>
    public static void Run(Action action)
    {
        EnsureInitialized();
        var done = new ManualResetEventSlim();
        Exception? error = null;
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                done.Set();
            }
        });
        done.Wait(TimeSpan.FromSeconds(30));
        if (error is not null)
        {
            throw error;
        }
    }

    public static void EnsureInitialized()
    {
        if (_uiThread is not null)
        {
            return;
        }

        lock (Gate)
        {
            if (_uiThread is not null)
            {
                return;
            }

            _uiThread = new Thread(() =>
            {
                AppBuilder.Configure<TestApp>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
                while (true)
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(10);
                }
            });
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.IsBackground = true;
            _uiThread.Start();
            Thread.Sleep(100);
        }
    }
}
