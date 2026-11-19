using Avalonia;
using Avalonia.Headless;

namespace Kudo.Controls.Tests;

/// <summary>
///     Headless 平台一次性初始化：为几何解析、模板度量等提供平台服务，
///     不依赖 Avalonia.Headless.XUnit（其发现器尚未跟进 xunit.v3 4.x）。
/// </summary>
public static class TestRuntime
{
    private static int _initialized;

    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();
    }
}

public sealed class TestApp : Application
{
    public override void Initialize()
    {
    }
}
