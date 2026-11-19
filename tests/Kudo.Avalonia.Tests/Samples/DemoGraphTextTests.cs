using Kudo.Demo;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests.Samples;

/// <summary>示例文案契约：每个键都有非空文案，且键定义自带中文描述。</summary>
public sealed class DemoGraphTextTests
{
    [Fact]
    public void English_provider_resolves_every_key_to_non_empty_text()
    {
        foreach (DemoGraphTextKey key in Enum.GetValues(typeof(DemoGraphTextKey)))
        {
            DemoGraphText.Get(null, key).ShouldNotBeNull();
            DemoGraphText.Get(null, key).ShouldNotBeEmpty($"{key} 缺少英文文案");
        }
    }

    [Fact]
    public void Every_key_declares_a_chinese_description_attribute()
    {
        var enumType = typeof(DemoGraphTextKey);
        foreach (var name in Enum.GetNames(enumType))
        {
            var member = enumType.GetMember(name)[0];
            var description = (System.ComponentModel.DescriptionAttribute?)Attribute
                .GetCustomAttribute(member, typeof(System.ComponentModel.DescriptionAttribute));

            description.ShouldNotBeNull($"{name} 缺少 Description 特性");
            description.Description.ShouldNotBeEmpty($"{name} 的中文描述为空");
        }
    }
}
