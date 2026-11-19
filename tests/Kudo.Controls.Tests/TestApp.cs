using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Kudo.Controls.Tests.TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Kudo.Controls.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class TestApplication : Application
{
    public override void Initialize()
    {
    }
}
