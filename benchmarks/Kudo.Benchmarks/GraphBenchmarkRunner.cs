using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using Avalonia;
using Kudo.Avalonia;
using Kudo.Core.Serialization;

namespace Kudo.Benchmarks;

public sealed record GraphBenchmarkEnvironment(
    DateTimeOffset Timestamp,
    string OperatingSystem,
    string Framework,
    string Architecture,
    string Processor,
    int ProcessorCount,
    bool ServerGc);

public sealed record GraphBenchmarkResult(
    int NodeCount,
    int EdgeCount,
    string Workload,
    int SampleCount,
    double MeanMilliseconds,
    double MedianMilliseconds,
    double P95Milliseconds,
    long MedianAllocatedBytes,
    long Checksum);

public sealed record GraphBenchmarkReport(
    GraphBenchmarkEnvironment Environment,
    GraphBenchmarkOptions Options,
    IReadOnlyList<GraphBenchmarkResult> Results);

public delegate long GraphBenchmarkFrameOperation(int frameIndex);

public sealed record GraphBenchmarkWorkload
{
    public GraphBenchmarkWorkload(string name, Func<long> operation, Action? setup = null)
        : this(name, _ => operation(), isContinuous: false, setup)
    {
        ArgumentNullException.ThrowIfNull(operation);
    }

    public GraphBenchmarkWorkload(string name, GraphBenchmarkFrameOperation frameOperation, Action? setup = null)
        : this(name, frameOperation, isContinuous: true, setup)
    {
    }

    private GraphBenchmarkWorkload(
        string name,
        GraphBenchmarkFrameOperation operation,
        bool isContinuous,
        Action? setup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(operation);
        Name = name;
        Operation = operation;
        IsContinuous = isContinuous;
        Setup = setup;
    }

    public string Name { get; }

    public GraphBenchmarkFrameOperation Operation { get; }

    public Action? Setup { get; }

    public bool IsContinuous { get; }

    public int GetSamplesPerIteration(GraphBenchmarkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!IsContinuous)
        {
            return 1;
        }

        if (options.FrameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(GraphBenchmarkOptions.FrameCount),
                "连续工作负载帧数必须大于 0。");
        }

        return options.FrameCount;
    }
}

public static class GraphBenchmarkRunner
{
    private static long _checksumSink;

    public static GraphBenchmarkReport Run(GraphBenchmarkOptions options, Action<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var results = new List<GraphBenchmarkResult>();
        var factory = new GraphBenchmarkDocumentFactory();
        foreach (var nodeCount in options.NodeCounts)
        {
            var scenario = GraphBenchmarkScenario.Create(nodeCount);
            var context = new GraphBenchmarkContext(factory, scenario);
            progress?.Invoke($"场景：{scenario.NodeCount} Node / {scenario.EdgeCount} Edge");
            foreach (var workload in context.CreateWorkloads())
            {
                progress?.Invoke($"  测量：{workload.Name}");
                results.Add(Measure(scenario, workload, options));
            }

            using var renderSuite = new GraphRenderBenchmarkSuite(factory, scenario, options.FrameCount);
            foreach (var renderCase in renderSuite.Cases)
            {
                var workload = CreateRenderWorkload(renderCase);
                progress?.Invoke($"  测量：{workload.Name}");
                results.Add(Measure(scenario, workload, options));
            }
        }

        return new GraphBenchmarkReport(CreateEnvironment(), options, results.AsReadOnly());
    }

    private static GraphBenchmarkWorkload CreateRenderWorkload(GraphRenderBenchmarkCase renderCase)
    {
        ArgumentNullException.ThrowIfNull(renderCase);
        return renderCase.FrameCount == 1
            ? new GraphBenchmarkWorkload(renderCase.Name, renderCase.RenderFrame, renderCase.Setup)
            : new GraphBenchmarkWorkload(renderCase.Name, _ => renderCase.RenderFrame(), renderCase.Setup);
    }

    public static GraphBenchmarkResult Measure(
        GraphBenchmarkScenario scenario,
        GraphBenchmarkWorkload workload,
        GraphBenchmarkOptions options)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(options);
        if (options.WarmupCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(GraphBenchmarkOptions.WarmupCount),
                "预热次数不能小于 0。");
        }

        if (options.IterationCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(GraphBenchmarkOptions.IterationCount),
                "测量次数必须大于 0。");
        }

        var samplesPerIteration = workload.GetSamplesPerIteration(options);
        for (var index = 0; index < options.WarmupCount; index++)
        {
            workload.Setup?.Invoke();
            for (var sampleIndex = 0; sampleIndex < samplesPerIteration; sampleIndex++)
            {
                Consume(workload.Operation(sampleIndex));
            }
        }

        var sampleCount = checked(options.IterationCount * samplesPerIteration);
        var durations = new double[sampleCount];
        var allocations = new long[sampleCount];
        long checksum = 0;
        var resultIndex = 0;
        for (var iterationIndex = 0; iterationIndex < options.IterationCount; iterationIndex++)
        {
            workload.Setup?.Invoke();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            for (var sampleIndex = 0; sampleIndex < samplesPerIteration; sampleIndex++)
            {
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                checksum = workload.Operation(sampleIndex);
                durations[resultIndex] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                allocations[resultIndex] = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                Consume(checksum);
                resultIndex++;
            }
        }

        Array.Sort(durations);
        Array.Sort(allocations);
        return new GraphBenchmarkResult(
            scenario.NodeCount,
            scenario.EdgeCount,
            workload.Name,
            sampleCount,
            durations.Average(),
            Percentile(durations, 0.5),
            Percentile(durations, 0.95),
            (long)Percentile(allocations, 0.5),
            checksum);
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(sortedValues.Count * percentile) - 1, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }

    private static double Percentile(IReadOnlyList<long> sortedValues, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(sortedValues.Count * percentile) - 1, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }

    private static void Consume(long value) => Volatile.Write(ref _checksumSink, value);

    private static GraphBenchmarkEnvironment CreateEnvironment() => new(
        DateTimeOffset.Now,
        RuntimeInformation.OSDescription,
        RuntimeInformation.FrameworkDescription,
        RuntimeInformation.ProcessArchitecture.ToString(),
        Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown",
        Environment.ProcessorCount,
        GCSettings.IsServerGC);

    private sealed class GraphBenchmarkContext
    {
        private readonly GraphBenchmarkDocumentFactory _factory;
        private readonly GraphBenchmarkScenario _scenario;
        private readonly GraphDocument _document;
        private readonly GraphTaskXmlSerializer _serializer = new(new GraphSerializationOptions(false));
        private readonly string _documentText;
        private readonly GraphCanvas _canvas;
        private readonly GraphValidator _validator = new([new GraphStructureValidationRule()]);
        private readonly Point _firstNodePoint;
        private readonly Point _outsideGraphPoint = new(-100_000, -100_000);

        public GraphBenchmarkContext(GraphBenchmarkDocumentFactory factory, GraphBenchmarkScenario scenario)
        {
            _factory = factory;
            _scenario = scenario;
            _document = factory.Create(scenario);
            _documentText = _serializer.Serialize(_document);
            _canvas = new GraphCanvas { Document = _document };
            var firstNode = _document.Nodes[0];
            _firstNodePoint = new Point(
                firstNode.Position.X + firstNode.Size.Width / 2,
                firstNode.Position.Y + firstNode.Size.Height / 2);
        }

        public IReadOnlyList<GraphBenchmarkWorkload> CreateWorkloads() =>
        [
            new("生成 Document", GenerateDocument),
            new("JSON 序列化", Serialize),
            new("JSON 反序列化", Deserialize),
            new("结构校验", Validate),
            new("冷刷新全部 Edge 路径", RouteAllEdgesCold),
            new("复用全部 Edge 路径缓存", RouteAllEdgesCached, WarmEdgePathCache),
            new("计算文档范围（缓存已预热）", CalculateDocumentBounds, WarmEdgePathCache),
            new("重建 Node/Group 空间索引", HitTestFirstNode, _canvas.RefreshGraph),
            new("节点命中（空间索引已预热）", HitTestFirstNode, WarmElementSpatialIndex),
            new("重建 Edge 空间索引（路径已缓存）", HitTestMissingEdge, PrepareEdgeSpatialIndexRebuild),
            new("Edge 未命中（空间索引已预热）", HitTestMissingEdge, WarmEdgeSpatialIndex),
            new("刷新单节点关联 Edge", RefreshAffectedEdges)
        ];

        private long GenerateDocument()
        {
            var document = _factory.Create(_scenario);
            return document.Nodes.Count + document.Edges.Count;
        }

        private long Serialize() => _serializer.Serialize(_document).Length;

        private long Deserialize()
        {
            var document = _serializer.Deserialize(_documentText);
            return document.Nodes.Count + document.Edges.Count;
        }

        private long Validate() => _validator.Validate(_document).Count;

        private long RouteAllEdgesCold()
        {
            _canvas.InvalidateEdgeRoutingCache();
            return RouteAllEdgesCached();
        }

        private long RouteAllEdgesCached()
        {
            long pointCount = 0;
            foreach (var edge in _document.Edges)
            {
                pointCount += _canvas.GetEdgeGraphPath(edge)?.Points.Count ?? 0;
            }

            return pointCount;
        }

        private long CalculateDocumentBounds()
        {
            var bounds = _canvas.GetDocumentGraphBounds();
            return bounds is { } value ? (long)(value.Width + value.Height) : 0;
        }

        private long HitTestFirstNode() => _canvas.HitTestNode(_firstNodePoint)?.Id.Length ?? 0;

        private long HitTestMissingEdge() => _canvas.HitTestEdge(_outsideGraphPoint)?.Id.Length ?? 0;

        private void WarmEdgePathCache() => _ = RouteAllEdgesCached();

        private void WarmElementSpatialIndex()
        {
            _canvas.RefreshGraph();
            _ = HitTestFirstNode();
        }

        private void PrepareEdgeSpatialIndexRebuild()
        {
            _canvas.InvalidateEdgeRoutingCache();
            WarmEdgePathCache();
        }

        private void WarmEdgeSpatialIndex()
        {
            PrepareEdgeSpatialIndexRebuild();
            _ = HitTestMissingEdge();
        }

        private long RefreshAffectedEdges()
        {
            var node = _document.Nodes[0];
            var original = node.Position;
            node.Position = new GraphPoint(original.X + 1, original.Y + 1);
            try
            {
                long pointCount = 0;
                foreach (var edge in _document.Edges.Where(edge =>
                             edge.Source.NodeId == node.Id || edge.Target.NodeId == node.Id))
                {
                    pointCount += _canvas.GetEdgeGraphPath(edge)?.Points.Count ?? 0;
                }

                return pointCount;
            }
            finally
            {
                node.Position = original;
            }
        }
    }
}

