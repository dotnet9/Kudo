using Kudo.Avalonia;

namespace Kudo.Demo.Desktop.Localization;

internal sealed class LocalizedDemoGraphTextProvider : IDemoGraphTextProvider
{
    private const string ResourcePrefix = "Localization.Kudo_Demo_Desktop.Data.GraphData.";

    public string Get(DemoGraphTextKey key) => GraphLocalization.Get(ResourcePrefix + key);
}
