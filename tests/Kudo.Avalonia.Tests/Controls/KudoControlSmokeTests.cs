using Avalonia.Headless.XUnit;
using Kudo.Avalonia.Controls;
using Kudo.Avalonia.Docking;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests.Controls;

/// <summary>加载/骨架控件的构造契约（Headless UI 线程）。</summary>
public sealed class KudoControlSmokeTests
{
    [AvaloniaFact]
    public void Loading_view_defaults_to_loading_state()
    {
        var view = new GraphLoadingView();

        view.IsLoading.ShouldBeTrue();
        view.Title.ShouldBeNull();
        view.Message.ShouldBeNull();
        view.ErrorMessage.ShouldBeNull();
        view.RetryText.ShouldBeNull();
        view.RetryCommand.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Loading_host_defaults_to_show_loading()
    {
        var host = new GraphLoadingHost();

        host.IsLoading.ShouldBeFalse();
        host.LoadedContent.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Skeleton_configures_panes_grid()
    {
        var skeleton = new GraphWorkspaceSkeleton();

        skeleton.RowDefinitions.Count.ShouldBe(3);
        skeleton.ColumnDefinitions.Count.ShouldBe(5);
        skeleton.Children.Count.ShouldBe(4);
    }
}
