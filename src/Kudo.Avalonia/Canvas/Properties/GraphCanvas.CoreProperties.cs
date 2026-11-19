using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public GraphDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public GraphGridSettings GridSettings
    {
        get => GetValue(GridSettingsProperty);
        set => SetValue(GridSettingsProperty, (value ?? GraphGridSettings.Default).Normalize());
    }
}
