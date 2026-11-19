using Kudo;

namespace Kudo.Avalonia;

internal sealed class LocalizedConnectionValidationTextProvider : IGraphValidationTextProvider
{
    private const string ResourcePrefix = "Localization.Kudo_Avalonia.Validation.GraphValidationText.";

    public string Get(GraphValidationTextKey key) => GraphLocalization.Get(ResourcePrefix + key);
}
