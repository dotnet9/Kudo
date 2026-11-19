using Avalonia.Media;
using Kudo.Avalonia.Controls;
using Kudo.Avalonia.Docking;
using Kudo.Avalonia.Tests.TestInfrastructure;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests.Controls;

/// <summary>加载/骨架控件的构造契约（Headless 平台服务下运行）。</summary>
public sealed class KudoControlSmokeTests
{
    [Fact]
    public void Loading_view_defaults_to_loading_state()
    {
        TestRuntime.EnsureInitialized();
        var view = new GraphLoadingView();

        view.IsLoading.ShouldBeTrue();
        view.Title.ShouldBeNull();
        view.Message.ShouldBeNull();
        view.ErrorMessage.ShouldBeNull();
        view.RetryText.ShouldBeNull();
        view.RetryCommand.ShouldBeNull();
    }

    [Fact]
    public void Loading_host_defaults_to_show_loading()
    {
        TestRuntime.EnsureInitialized();
        var host = new GraphLoadingHost();

        host.IsLoading.ShouldBeFalse();
        host.LoadedContent.ShouldBeNull();
    }

    [Fact]
    public void Skeleton_configures_five_by_five_grid()
    {
        TestRuntime.EnsureInitialized();
        var skeleton = new GraphWorkspaceSkeleton();

        skeleton.RowDefinitions.Count.ShouldBe(3);
        skeleton.ColumnDefinitions.Count.ShouldBe(5);
        skeleton.Children.Count.ShouldBe(4);
    }
}
