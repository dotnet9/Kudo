namespace Kudo.Avalonia.ThreeD;

public sealed record GraphThreeDRenderOptions(
    GraphThreeDRenderMode RenderMode = GraphThreeDRenderMode.Auto,
    bool AllowSoftwareFallback = true)
{
    public const string RendererEnvironmentVariable = "KUDO_3D_RENDERER";

    public static GraphThreeDRenderOptions Default { get; } = new();

    public bool ShouldForcePlatformSoftwareRendering => RenderMode == GraphThreeDRenderMode.Software;

    public static GraphThreeDRenderOptions FromCurrentProcess() =>
        FromCommandLine(
            Environment.GetCommandLineArgs(),
            Environment.GetEnvironmentVariable(RendererEnvironmentVariable));

    public static GraphThreeDRenderOptions FromCommandLine(
        IEnumerable<string>? args,
        string? environmentRenderer = null)
    {
        var renderMode = TryReadRenderMode(args) ??
            TryParseRenderMode(environmentRenderer) ??
            GraphThreeDRenderMode.Auto;
        return new GraphThreeDRenderOptions(renderMode);
    }

    public void ApplyTo(GraphThreeDView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.PreferredRenderMode = RenderMode;
        view.AllowSoftwareFallback = AllowSoftwareFallback;
    }

    private static GraphThreeDRenderMode? TryReadRenderMode(IEnumerable<string>? args)
    {
        if (args is null)
        {
            return null;
        }

        var values = args.ToArray();
        for (var index = 0; index < values.Length; index++)
        {
            var arg = values[index];
            if (IsRendererOption(arg))
            {
                var next = index + 1 < values.Length ? values[index + 1] : null;
                return TryParseRenderMode(next);
            }

            var inlineValue = TryReadInlineValue(arg);
            if (inlineValue is not null)
            {
                return TryParseRenderMode(inlineValue);
            }
        }

        return null;
    }

    private static bool IsRendererOption(string arg) =>
        string.Equals(arg, "-type", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "--type", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "/type", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "--kudo-3d-renderer", StringComparison.OrdinalIgnoreCase);

    private static string? TryReadInlineValue(string arg)
    {
        var separators = new[] { "-type=", "--type=", "/type=", "--kudo-3d-renderer=" };
        foreach (var separator in separators)
        {
            if (arg.StartsWith(separator, StringComparison.OrdinalIgnoreCase))
            {
                return arg[separator.Length..];
            }
        }

        return null;
    }

    private static GraphThreeDRenderMode? TryParseRenderMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "auto" => GraphThreeDRenderMode.Auto,
            "gl" or "gpu" or "opengl" or "open-gl" => GraphThreeDRenderMode.OpenGl,
            "cpu" or "soft" or "software" => GraphThreeDRenderMode.Software,
            _ => null
        };
    }
}
