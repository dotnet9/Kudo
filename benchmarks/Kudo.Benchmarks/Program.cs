using Kudo.Benchmarks;

var options = GraphBenchmarkOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine(GraphBenchmarkOptions.Usage);
    return 0;
}

Console.WriteLine(
    $"Kudo 基准开始：{string.Join(", ", options.NodeCounts)} 节点，" +
    $"预热 {options.WarmupCount} 次，测量 {options.IterationCount} 次，连续场景每轮 {options.FrameCount} 帧。");
var report = GraphBenchmarkRunner.Run(options, Console.WriteLine);
BenchmarkReportWriter.WriteConsoleSummary(report);
if (!string.IsNullOrWhiteSpace(options.OutputPath))
{
    var outputPath = Path.GetFullPath(options.OutputPath);
    BenchmarkReportWriter.WriteMarkdown(report, outputPath);
    Console.WriteLine($"基准报告已写入：{outputPath}");
}

return 0;
