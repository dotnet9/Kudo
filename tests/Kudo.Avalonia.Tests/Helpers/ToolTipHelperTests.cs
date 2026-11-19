using Avalonia.Controls;
using Avalonia.Media;
using Kudo.Avalonia.Helpers;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests;

/// <summary>ToolTipHelper：附加属性回读与提示内容构造契约。</summary>
public sealed class ToolTipHelperTests
{
    [Fact]
    public void SetText_roundtrips_via_attached_property()
    {
        var control = new Border();

        ToolTipHelper.SetText(control, "画板提示");

        ToolTipHelper.GetText(control).ShouldBe("画板提示");
    }

    [Fact]
    public void CreateTextTip_returns_wrapped_text_block_for_content()
    {
        var tip = ToolTipHelper.CreateTextTip("只读模式下不可编辑") as TextBlock;

        tip.ShouldNotBeNull();
        tip.Text.ShouldBe("只读模式下不可编辑");
        tip.TextWrapping.ShouldBe(TextWrapping.Wrap);
        tip.MaxWidth.ShouldBe(260);
        tip.Foreground.ShouldBe(Brushes.White);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateTextTip_returns_null_for_blank_text(string? text)
    {
        ToolTipHelper.CreateTextTip(text).ShouldBeNull();
    }
}
