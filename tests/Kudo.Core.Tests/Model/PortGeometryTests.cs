using Shouldly;

namespace Kudo.Core.Tests;

public class PortGeometryTests
{
    [Fact]
    public void Intrinsic_local_direction_roundtrips_at_its_intrinsic_size()
    {
        var intrinsicSize = new GraphSize(200, 100);

        var normalized = PortGeometry.ToNormalizedLocalDirection(
            new GraphPoint(3, 4),
            intrinsicSize);
        var restored = PortGeometry.ToNodeLocalDirection(normalized, intrinsicSize);

        restored.ShouldNotBeNull();
        restored.Value.X.ShouldBe(0.6, 0.000001);
        restored.Value.Y.ShouldBe(0.8, 0.000001);
    }

    [Fact]
    public void Normalized_local_direction_follows_non_uniform_node_resize_before_rotation()
    {
        var normalized = PortGeometry.ToNormalizedLocalDirection(
            new GraphPoint(1, 1),
            new GraphSize(200, 100));
        var node = new NodeModel
        {
            Size = new GraphSize(400, 50),
            Rotation = 90
        };

        var local = PortGeometry.ToNodeLocalDirection(normalized, node.Size);
        var canvas = PortGeometry.ToCanvasDirection(node, normalized);

        local.ShouldNotBeNull();
        local.Value.X.ShouldBe(0.9701425, 0.000001);
        local.Value.Y.ShouldBe(0.2425356, 0.000001);
        canvas.ShouldNotBeNull();
        canvas.Value.X.ShouldBe(-0.2425356, 0.000001);
        canvas.Value.Y.ShouldBe(0.9701425, 0.000001);
    }

    [Theory]
    [InlineData(1, 0, 320, 40, 0, 1, 0)]
    [InlineData(0, -1, 40, 320, 0, 0, -1)]
    [InlineData(1, 0, 320, 40, 90, 0, 1)]
    [InlineData(0, -1, 40, 320, 90, 1, 0)]
    public void Axis_aligned_directions_are_unchanged_by_node_aspect_ratio(
        double x,
        double y,
        double width,
        double height,
        double rotation,
        double expectedX,
        double expectedY)
    {
        var direction = PortGeometry.ToCanvasDirection(
            new NodeModel
            {
                Size = new GraphSize(width, height),
                Rotation = rotation
            },
            new GraphPoint(x, y));

        direction.ShouldNotBeNull();
        direction.Value.X.ShouldBe(expectedX, 0.000001);
        direction.Value.Y.ShouldBe(expectedY, 0.000001);
    }

    [Fact]
    public void Direction_conversion_rejects_invalid_sizes()
    {
        PortGeometry.ToNormalizedLocalDirection(
                new GraphPoint(1, 0),
                new GraphSize(0, 100))
            .ShouldBeNull();
        PortGeometry.ToNodeLocalDirection(
                new GraphPoint(1, 0),
                new GraphSize(100, double.NaN))
            .ShouldBeNull();
    }
}
