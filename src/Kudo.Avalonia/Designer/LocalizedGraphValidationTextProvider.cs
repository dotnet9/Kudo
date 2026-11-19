using Kudo.Avalonia;

namespace Kudo.Avalonia.Designer;

internal sealed class LocalizedGraphValidationTextProvider : IGraphValidationTextProvider
{
    private const string ResourcePrefix = "Localization.Kudo_Avalonia.Validation.GraphValidationText.";

    public string Get(GraphValidationTextKey key) => GraphLocalization.Get(ResourcePrefix + key);
}
