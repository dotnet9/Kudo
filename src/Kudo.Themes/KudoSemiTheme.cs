using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Kudo.Themes;

public sealed class KudoSemiTheme : Styles
{
    public const string SemiXmlNamespace = "https://irihi.tech/semi";

    public const string UrsaXmlNamespace = "https://irihi.tech/ursa";

    public KudoSemiTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
