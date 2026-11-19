using System.ComponentModel;
using Shouldly;

namespace Kudo.Core.Tests;

public class GraphPointTests
{
    [Theory]
    [InlineData("12,24", 12, 24)]
    [InlineData("12.5, -24.25", 12.5, -24.25)]
    public void Parse_reads_invariant_axaml_literal(
        string value,
        double expectedX,
        double expectedY)
    {
        GraphPoint.Parse(value).ShouldBe(new GraphPoint(expectedX, expectedY));
        GraphPoint.TryParse(value, out var result).ShouldBeTrue();
        result.ShouldBe(new GraphPoint(expectedX, expectedY));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12,24,36")]
    [InlineData("NaN,24")]
    [InlineData("Infinity,24")]
    public void TryParse_rejects_invalid_geometry(string value)
    {
        GraphPoint.TryParse(value, out var result).ShouldBeFalse();
        result.ShouldBe(GraphPoint.Zero);
        Should.Throw<FormatException>(() => GraphPoint.Parse(value));
    }

    [Fact]
    public void Type_converter_supports_axaml_string_conversion()
    {
        var converter = TypeDescriptor.GetConverter(typeof(GraphPoint));

        converter.CanConvertFrom(typeof(string)).ShouldBeTrue();
        converter.ConvertFromInvariantString("12,24").ShouldBe(new GraphPoint(12, 24));
    }
}
