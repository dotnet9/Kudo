using Kudo.Demo;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests.Samples;

/// <summary>示例画板构造契约：节点/连线规模、唯一性与特性开关。</summary>
public sealed class SampleGraphsTests
{
    [Fact]
    public void BasicDocument_links_input_transform_output()
    {
        var document = SampleGraphs.CreateBasicDocument();

        document.Title.ShouldNotBeNullOrEmpty();
        document.Nodes.Count.ShouldBe(3);
        document.Edges.Count.ShouldBe(2);
        document.Nodes.Select(n => n.Id).ShouldBe(new[] { "input", "transform", "output" });
        document.Edges.ShouldAllBe(e => HasPorts(document, e));
    }

    [Fact]
    public void BranchingDocument_groups_inputs_and_annotates_risk_review()
    {
        var document = SampleGraphs.CreateBranchingDocument();

        document.Title.ShouldBe(DemoGraphText.English.Get(DemoGraphTextKey.OrderRiskControl));
        document.Nodes.Count.ShouldBe(5);
        document.Edges.Count.ShouldBe(5);
        document.Groups.ShouldHaveSingleItem().NodeIds.ShouldBe(new[] { "input-a", "input-b" });
        document.Annotations.ShouldHaveSingleItem();
        document.Nodes.Select(n => n.Id).ShouldBeUnique();
    }

    [Fact]
    public void RendererShowcaseDocument_flags_the_renderer_property()
    {
        var document = SampleGraphs.CreateRendererShowcaseDocument();

        document.Properties[SampleGraphs.RendererShowcaseProperty].ShouldBe(true);
        document.Nodes.Count.ShouldBe(5);
        document.Edges.Count.ShouldBe(4);
    }

    [Fact]
    public void EdgeStylesDocument_creates_a_pair_per_preset()
    {
        var document = SampleGraphs.CreateEdgeStylesDocument();

        document.Properties[SampleGraphs.EdgeStylesProperty].ShouldBe(true);
        document.Nodes.Count.ShouldBe(24);
        document.Edges.Count.ShouldBe(12);
        document.Edges.ShouldAllBe(e => HasPorts(document, e));
    }

    [Fact]
    public void ThreeDDocument_enables_the_three_d_preview()
    {
        var document = SampleGraphs.CreateThreeDDocument();

        document.Properties[SampleGraphs.ThreeDShowcaseProperty].ShouldBe(true);
        document.FindNode("transform").ShouldNotBeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(50)]
    public void LargeWorkflowDocument_creates_unique_nodes(int nodeCount)
    {
        var document = SampleGraphs.CreateLargeWorkflowDocument(nodeCount);

        document.Nodes.Count.ShouldBe(nodeCount);
        document.Nodes.Select(n => n.Id).ShouldBeUnique();
        document.Edges.Count.ShouldBeGreaterThan(0);
        document.Edges.Select(e => e.Id).ShouldBeUnique();
    }

    private static bool HasPorts(GraphDocument document, EdgeModel edge) =>
        document.Nodes.Any(n => n.Id == edge.Source.NodeId && n.Ports.Any(p => p.Id == edge.Source.PortId)) &&
        document.Nodes.Any(n => n.Id == edge.Target.NodeId && n.Ports.Any(p => p.Id == edge.Target.PortId));
}
