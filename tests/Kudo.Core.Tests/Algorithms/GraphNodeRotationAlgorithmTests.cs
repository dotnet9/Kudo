using Kudo.Core.Algorithms;
using Shouldly;

namespace Kudo.Core.Tests.Algorithms;

public class GraphNodeRotationAlgorithmTests
{
    [Fact]
    public void Group_bounds_include_rotated_node_canvas_bounds()
    {
        var node = new NodeModel
        {
            Id = "node",
            Position = new GraphPoint(100, 100),
            Size = new GraphSize(100, 40),
            Rotation = 90
        };
        var group = new GroupModel { NodeIds = [node.Id], Padding = 10 };

        var bounds = GraphGroupBoundsCalculator.Calculate(group, [node]).ShouldNotBeNull();
        var nodeBounds = GraphNodeGeometry.GetCanvasBounds(node);

        bounds.X.ShouldBe(nodeBounds.X - 10, 0.0001);
        bounds.Y.ShouldBe(nodeBounds.Y - 10 - GraphGroupBoundsCalculator.HeaderHeight, 0.0001);
        bounds.Width.ShouldBe(nodeBounds.Width + 20, 0.0001);
        bounds.Height.ShouldBe(nodeBounds.Height + 20 + GraphGroupBoundsCalculator.HeaderHeight, 0.0001);
    }

    [Fact]
    public void Alignment_uses_rotated_canvas_bounds_without_changing_rotation()
    {
        var first = new NodeModel
        {
            Id = "first",
            Position = new GraphPoint(10, 20),
            Size = new GraphSize(100, 40),
            Rotation = 45
        };
        var second = new NodeModel
        {
            Id = "second",
            Position = new GraphPoint(220, 80),
            Size = new GraphSize(80, 60)
        };

        var result = GraphNodeArrangement.Align([first, second], NodeAlignment.Left);
        first.Position = result.NodePositions[first.Id];
        second.Position = result.NodePositions[second.Id];

        GraphNodeGeometry.GetCanvasBounds(first).X.ShouldBe(
            GraphNodeGeometry.GetCanvasBounds(second).X,
            0.0001);
        first.Rotation.ShouldBe(45);
    }

    [Fact]
    public void Guide_snapping_uses_rotated_canvas_bounds()
    {
        var primary = new NodeModel
        {
            Id = "primary",
            Position = GraphPoint.Zero,
            Size = new GraphSize(100, 40),
            Rotation = 90
        };
        var reference = new NodeModel
        {
            Id = "reference",
            Position = new GraphPoint(200, 0),
            Size = new GraphSize(40, 40)
        };
        var settings = GraphGridSettings.Default with
        {
            IsSnapEnabled = false,
            IsGuidesEnabled = true,
            GuideTolerance = 4
        };

        var result = GraphNodeSnapper.Snap(
            [primary],
            [reference],
            new Dictionary<string, GraphPoint> { [primary.Id] = primary.Position },
            primary.Id,
            new GraphPoint(168, 0),
            settings);

        result.NodePositions[primary.Id].X.ShouldBe(170, 0.0001);
        result.IsGuideSnapX.ShouldBeTrue();
        result.AlignmentGuides.ShouldContain(
            new GraphAlignmentGuide(GraphGuideOrientation.Vertical, 200));
    }
}
