namespace Kudo.Demo.Desktop.Services;

public static class DemoModelNodeTypes
{
    public const string Valve = "Industrial.Valve";

    public const string CheckValve = "Industrial.CheckValve";

    public const string SafetyValve = "Industrial.SafetyValve";

    public const string Pump = "Industrial.Pump";

    public const string PipeElbow = "Industrial.PipeElbow";

    public const string PipeTee = "Industrial.PipeTee";

    public const string PipeJunction = "Industrial.PipeJunction";

    public const string PipeLoop = "Industrial.PipeLoop";

    public const string Instrument = "Industrial.Instrument";

    public const string FlowArrow = "Industrial.FlowArrow";

    public const string TextLabel = "Industrial.TextLabel";
}

public static class DemoModelCatalogText
{
    private const string Prefix = "Localization.Kudo_Demo_Desktop.Models.ModelCatalog.";

    public const string BasicSymbols = Prefix + nameof(BasicSymbols);

    public const string Piping = Prefix + nameof(Piping);

    public const string Instrumentation = Prefix + nameof(Instrumentation);

    public const string Pump = Prefix + nameof(Pump);
}
