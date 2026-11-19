using Kudo;

namespace Kudo.Benchmarks;

public sealed record GraphBenchmarkScenario(int NodeCount, int EdgeCount, int Seed = 42)
{
    public static GraphBenchmarkScenario Create(int nodeCount) => new(nodeCount, checked(nodeCount * 2));
}

public sealed class GraphBenchmarkDocumentFactory
{
    private static readonly EdgeRoutingStyle[] RoutingStyles =
    [
        EdgeRoutingStyle.Direct,
        EdgeRoutingStyle.Bezier,
        EdgeRoutingStyle.Orthogonal,
        EdgeRoutingStyle.Manual,
        EdgeRoutingStyle.SmartOrthogonal
    ];

    public GraphDocument Create(GraphBenchmarkScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.NodeCount < 2 || scenario.EdgeCount < scenario.NodeCount - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        var maximumEdgeCount = (long)scenario.NodeCount * (scenario.NodeCount - 1) / 2;
        if (scenario.EdgeCount > maximumEdgeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(scenario), "Edge 数超过无重复前向连接的最大容量。");
        }

        var random = new Random(scenario.Seed);
        var columns = (int)Math.Ceiling(Math.Sqrt(scenario.NodeCount * 2));
        var nodes = new List<NodeModel>(scenario.NodeCount);
        for (var index = 0; index < scenario.NodeCount; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var node = new NodeModel
            {
                Id = $"node-{index:D5}",
                Type = index % 3 == 0 ? "input" : index % 3 == 1 ? "transform" : "output",
                Name = $"节点 {index}",
                Position = new GraphPoint(
                    column * 220 + random.Next(-16, 17),
                    row * 140 + random.Next(-12, 13)),
                Size = new GraphSize(160, 80)
            }
            .AddPort(new PortModel
            {
                Id = "in",
                Name = "In",
                Kind = PortKind.Input,
                DataType = "number",
                Capacity = new PortCapacity { MaxIncoming = null }
            })
            .AddPort(new PortModel
            {
                Id = "out",
                Name = "Out",
                Kind = PortKind.Output,
                DataType = "number",
                Capacity = new PortCapacity { MaxOutgoing = null }
            });
            nodes.Add(node);
        }

        var document = new GraphDocument
        {
            Id = $"benchmark-{scenario.NodeCount}-{scenario.EdgeCount}-{scenario.Seed}",
            Title = $"Benchmark {scenario.NodeCount} Nodes"
        };
        document.AddNodes(nodes);
        var edges = new List<EdgeModel>(scenario.EdgeCount);
        var pairs = new HashSet<(int Source, int Target)>();
        for (var stride = 1; stride < scenario.NodeCount && edges.Count < scenario.EdgeCount; stride++)
        {
            for (var sourceIndex = 0;
                 sourceIndex + stride < scenario.NodeCount && edges.Count < scenario.EdgeCount;
                 sourceIndex++)
            {
                var targetIndex = sourceIndex + stride;
                if (!pairs.Add((sourceIndex, targetIndex)))
                {
                    continue;
                }

                edges.Add(CreateEdge(
                    edges.Count,
                    nodes[sourceIndex],
                    nodes[targetIndex]));
            }
        }

        if (edges.Count != scenario.EdgeCount)
        {
            throw new InvalidOperationException(
                $"基准图 Edge 数不完整：期望 {scenario.EdgeCount}，实际 {edges.Count}。");
        }

        document.AddEdges(edges);

        return document;
    }

    private static EdgeModel CreateEdge(int index, NodeModel source, NodeModel target)
    {
        var style = RoutingStyles[index % RoutingStyles.Length];
        return new EdgeModel
        {
            Id = $"edge-{index:D5}",
            Source = new PortReference(source.Id, "out"),
            Target = new PortReference(target.Id, "in"),
            RoutingStyle = style,
            Waypoints = style == EdgeRoutingStyle.Manual
                ?
                [
                    new GraphPoint(
                        (source.Position.X + target.Position.X) / 2,
                        Math.Min(source.Position.Y, target.Position.Y) - 36)
                ]
                : []
        };
    }
}
