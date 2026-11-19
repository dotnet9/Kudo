using System.Text;
using Avalonia;

namespace Kudo.Benchmarks;

public static class BenchmarkReportWriter
{
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    public static void WriteConsoleSummary(GraphBenchmarkReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Console.WriteLine();
        Console.WriteLine("结果（逐样本统计；耗时单位 ms，分配量按 B/KB/MB/GB 显示）：");
        foreach (var result in report.Results)
        {
            Console.WriteLine(
                $"{result.NodeCount,5} N / {result.EdgeCount,5} E | {result.Workload,-28} | " +
                $"{result.SampleCount,7} samples | {result.MeanMilliseconds,10:F3} ms mean | " +
                $"{result.MedianMilliseconds,10:F3} ms median | {result.P95Milliseconds,10:F3} ms p95 | " +
                $"{FormatBytes(result.MedianAllocatedBytes),10}");
        }
    }

    public static void WriteMarkdown(GraphBenchmarkReport report, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var environment = report.Environment;
        var builder = new StringBuilder()
            .AppendLine("# Kudo 性能基线")
            .AppendLine()
            .AppendLine($"- 时间：{environment.Timestamp:yyyy-MM-dd HH:mm:ss zzz}")
            .AppendLine($"- 系统：{environment.OperatingSystem}")
            .AppendLine($"- .NET：{environment.Framework}")
            .AppendLine($"- 架构：{environment.Architecture}")
            .AppendLine($"- 处理器：{environment.Processor}")
            .AppendLine($"- 逻辑处理器：{environment.ProcessorCount}")
            .AppendLine($"- Server GC：{environment.ServerGc}")
            .AppendLine($"- 构建配置：{BuildConfiguration}")
            .AppendLine($"- Avalonia：{typeof(Application).Assembly.GetName().Version}")
            .AppendLine($"- 预热 / 测量：{report.Options.WarmupCount} / {report.Options.IterationCount}")
            .AppendLine($"- 连续工作负载每轮帧数：{report.Options.FrameCount}")
            .AppendLine()
            .AppendLine("> 结果用于同一机器、同一配置下的优化前后对比，不作为跨机器绝对性能承诺。分配量是当前线程估算值。")
            .AppendLine()
            .AppendLine("## Render 场景口径")
            .AppendLine()
            .AppendLine($"- 渲染后端：Avalonia Headless + Skia，{GraphBenchmarkAvaloniaHost.ViewportWidth:F0}×{GraphBenchmarkAvaloniaHost.ViewportHeight:F0} 固定视口，完整 Visual Tree 离屏渲染。")
            .AppendLine("- 字体与缩放：Inter，Headless 默认 RenderScaling 1.0。")
            .AppendLine("- 数据：Seed 42、Edge 数为 Node 的 2 倍、五种 RoutingStyle 等比例轮换。")
            .AppendLine("- 模式：纯即时绘制；约三分之一 input 类型节点嵌入轻量 Border + TextBlock Control。")
            .AppendLine("- 首次 Render：Setup 创建 Document、Canvas、Control Host 和未显示 Window；计时包含首次 Show、布局与冷 Canvas Render，不代表进程冷启动。")
            .AppendLine("- 连续平移 / Zoom：Setup 先完成一帧预热；之后每帧更新 Viewport 并同步完成一次 Headless Render Tick。")
            .AppendLine("- 限制：不包含桌面窗口合成、VSync、真实输入、GPU 驱动或 OpenGL Context；当前线程分配不含 Skia Native / GPU 内存。")
            .AppendLine()
            .AppendLine("| Node | Edge | Workload | Samples | Mean (ms) | Median (ms) | P95 (ms) | Median Allocated |")
            .AppendLine("|---:|---:|---|---:|---:|---:|---:|---:|");
        foreach (var result in report.Results)
        {
            builder.AppendLine(
                $"| {result.NodeCount} | {result.EdgeCount} | {result.Workload} | " +
                $"{result.SampleCount} | {result.MeanMilliseconds:F3} | {result.MedianMilliseconds:F3} | " +
                $"{result.P95Milliseconds:F3} | {FormatBytes(result.MedianAllocatedBytes)} |");
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(false));
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024L * 1024L => $"{bytes / (1024d * 1024d * 1024d):F2} GB",
        >= 1024L * 1024L => $"{bytes / (1024d * 1024d):F2} MB",
        >= 1024L => $"{bytes / 1024d:F2} KB",
        _ => $"{bytes} B"
    };
}
