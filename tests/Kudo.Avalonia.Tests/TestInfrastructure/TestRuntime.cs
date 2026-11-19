using Avalonia;
using Avalonia.Headless;

namespace Kudo.Avalonia.Tests.TestInfrastructure;

/// <summary>Headless 平台一次性初始化（替代尚未跟进 xunit.v3 4.x 的 Headless.XUnit 集成）。</summary>
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
