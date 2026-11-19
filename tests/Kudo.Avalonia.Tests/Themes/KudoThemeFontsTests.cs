using Kudo.Themes;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests.Themes;

/// <summary>主题字体入口契约：默认字体与资源键常量保持稳定。</summary>
public sealed class KudoThemeFontsTests
{
    [Fact]
    public void Default_font_family_points_to_embedded_noto_sans()
    {
        KudoThemeFonts.DefaultFamilyName.ShouldNotBeNull();
        KudoThemeFonts.DefaultFamilyName.ShouldContain("Noto");
        KudoThemeFonts.DefaultResourceKey.ShouldBe("NotoSansSC");
    }

    [Fact]
    public void Windows7_fallback_font_is_preserved()
    {
        KudoThemeFonts.Windows7FamilyName.ShouldBe("Microsoft YaHei");
    }
}
