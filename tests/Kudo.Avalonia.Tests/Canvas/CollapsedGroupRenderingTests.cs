using Shouldly;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class CollapsedGroupRenderingTests
{
    [Fact]
    public void Collapsed_group_members_are_excluded_by_node_render_pass()
    {
        var hidden = new NodeModel
        {
            Id = "hidden",
            Position = new GraphPoint(100, 100)
        };
        var visible = new NodeModel
        {
            Id = "visible",
            Position = new GraphPoint(400, 100)
        };
        var canvas = new GraphCanvas
        {
            Document = new GraphDocument
            {
                Nodes = [hidden, visible],
                Groups =
                [
                    new GroupModel
                    {
                        Id = "collapsed",
                        NodeIds = [hidden.Id],
                        IsCollapsed = true
                    }
                ]
            }
        };

        GraphCanvasNodeRenderPass.ShouldRenderNode(hidden, canvas.IsNodeVisible).ShouldBeFalse();
        GraphCanvasNodeRenderPass.ShouldRenderNode(visible, canvas.IsNodeVisible).ShouldBeTrue();
        hidden.Position.ShouldBe(new GraphPoint(100, 100));
    }
}
