namespace Kudo.Benchmarks;

public sealed record GraphBenchmarkOptions(
    IReadOnlyList<int> NodeCounts,
    int WarmupCount,
    int IterationCount,
    string? OutputPath,
    bool ShowHelp = false,
    int FrameCount = 60)
{
    public const string Usage = """
        Kudo.Benchmarks

        用法：
          dotnet run -c Release --project benchmarks/Kudo.Benchmarks -- [选项]

        选项：
          --sizes 100,500,2000   节点规模（至少 5），Edge 数固定为 Node 的 2 倍
          --warmup 1             每项预热次数
          --iterations 3         每项正式测量次数
          --frames 60            连续工作负载每轮测量的帧数
          --output <path>        输出 Markdown 报告
          --smoke                CI 冒烟模式：100 Node、0 次预热、1 次测量、3 帧
          --help                 显示帮助
        """;

    public static GraphBenchmarkOptions Parse(IReadOnlyList<string> args)
    {
        var nodeCounts = new[] { 100, 500, 2000 };
        var warmupCount = 1;
        var iterationCount = 3;
        var frameCount = 60;
        string? outputPath = null;

        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--help" or "-h":
                    return new GraphBenchmarkOptions(
                        nodeCounts,
                        warmupCount,
                        iterationCount,
                        outputPath,
                        ShowHelp: true,
                        FrameCount: frameCount);
                case "--smoke":
                    nodeCounts = [100];
                    warmupCount = 0;
                    iterationCount = 1;
                    frameCount = 3;
                    outputPath = null;
                    break;
                case "--sizes":
                    nodeCounts = ParseNodeCounts(ReadValue(args, ref index, "--sizes"));
                    break;
                case "--warmup":
                    warmupCount = ParseNonNegativeInt(ReadValue(args, ref index, "--warmup"), "--warmup");
                    break;
                case "--iterations":
                    iterationCount = ParsePositiveInt(ReadValue(args, ref index, "--iterations"), "--iterations");
                    break;
                case "--frames":
                    frameCount = ParsePositiveInt(ReadValue(args, ref index, "--frames"), "--frames");
                    break;
                case "--output":
                    outputPath = ReadValue(args, ref index, "--output");
                    break;
                default:
                    throw new ArgumentException($"未知参数：{args[index]}{Environment.NewLine}{Usage}");
            }
        }

        return new GraphBenchmarkOptions(
            nodeCounts,
            warmupCount,
            iterationCount,
            outputPath,
            FrameCount: frameCount);
    }

    private static int[] ParseNodeCounts(string value)
    {
        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => ParsePositiveInt(item, "--sizes"))
            .Distinct()
            .Order()
            .ToArray();
        if (values.Length == 0 || values.Any(item => item < 5))
        {
            throw new ArgumentException("--sizes 至少需要一个不小于 5 的节点规模。");
        }

        return values;
    }

    private static int ParsePositiveInt(string value, string option) =>
        int.TryParse(value, out var result) && result > 0
            ? result
            : throw new ArgumentException($"{option} 需要正整数，实际值：{value}");

    private static int ParseNonNegativeInt(string value, string option) =>
        int.TryParse(value, out var result) && result >= 0
            ? result
            : throw new ArgumentException($"{option} 需要非负整数，实际值：{value}");

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
    {
        if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
        {
            throw new ArgumentException($"{option} 缺少参数值。");
        }

        return args[index];
    }
}
