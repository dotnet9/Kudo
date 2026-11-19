using Kudo.Demo.Desktop.Services;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests.Services;

/// <summary>应用设置存储契约：语言偏好往返与默认工作区。</summary>
public sealed class DemoApplicationSettingsStoreTests
{
    private static DemoApplicationSettingsStore CreateStore()
    {
        var path = Path.Combine(Path.GetTempPath(), "kudo-tests", Guid.NewGuid().ToString("N"), "settings.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new DemoApplicationSettingsStore(path);
    }

    [Fact]
    public void Language_roundtrips()
    {
        var store = CreateStore();

        store.LoadLanguage().ShouldBeNull();

        store.TrySaveLanguage("zh-CN").ShouldBeTrue();
        store.LoadLanguage().ShouldBe("zh-CN");

        store.TrySaveLanguage("en-US").ShouldBeTrue();
        store.LoadLanguage().ShouldBe("en-US");
    }

    [Fact]
    public void Blank_language_clears_saved_value()
    {
        var store = CreateStore();

        store.TrySaveLanguage("zh-CN").ShouldBeTrue();
        store.TrySaveLanguage("   ").ShouldBeTrue();

        store.LoadLanguage().ShouldBeNull();
    }

    [Fact]
    public void Missing_workspace_file_loads_defaults_without_warning()
    {
        var store = CreateStore();

        var settings = store.LoadWorkspace(out var warning);

        warning.ShouldBeNull();
        settings.ShouldNotBeNull();
    }
}
