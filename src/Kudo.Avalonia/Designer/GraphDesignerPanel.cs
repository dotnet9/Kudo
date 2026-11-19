using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Kudo.Avalonia.Designer;

public abstract class GraphDesignerPanel : UserControl
{
    public static readonly StyledProperty<IBrush> PanelBrushProperty =
        RegisterBrush(nameof(PanelBrush), Brushes.White);

    public static readonly StyledProperty<IBrush> ElevatedBrushProperty =
        RegisterBrush(nameof(ElevatedBrush), Brushes.White);

    public static readonly StyledProperty<IBrush> DividerBrushProperty =
        RegisterBrush(nameof(DividerBrush), Brushes.Gray);

    public static readonly StyledProperty<IBrush> PrimaryTextBrushProperty =
        RegisterBrush(nameof(PrimaryTextBrush), Brushes.Black);

    public static readonly StyledProperty<IBrush> SecondaryTextBrushProperty =
        RegisterBrush(nameof(SecondaryTextBrush), Brushes.Gray);

    public static readonly StyledProperty<IBrush> AccentBrushProperty =
        RegisterBrush(nameof(AccentBrush), Brushes.Blue);

    public static readonly StyledProperty<IBrush> SuccessBrushProperty =
        RegisterBrush(nameof(SuccessBrush), Brushes.Green);

    public static readonly StyledProperty<IBrush> WarningBrushProperty =
        RegisterBrush(nameof(WarningBrush), Brushes.Orange);

    public static readonly StyledProperty<IBrush> ErrorBrushProperty =
        RegisterBrush(nameof(ErrorBrush), Brushes.Red);

    public IBrush PanelBrush
    {
        get => GetValue(PanelBrushProperty);
        set => SetValue(PanelBrushProperty, value);
    }

    public IBrush ElevatedBrush
    {
        get => GetValue(ElevatedBrushProperty);
        set => SetValue(ElevatedBrushProperty, value);
    }

    public IBrush DividerBrush
    {
        get => GetValue(DividerBrushProperty);
        set => SetValue(DividerBrushProperty, value);
    }

    public IBrush PrimaryTextBrush
    {
        get => GetValue(PrimaryTextBrushProperty);
        set => SetValue(PrimaryTextBrushProperty, value);
    }

    public IBrush SecondaryTextBrush
    {
        get => GetValue(SecondaryTextBrushProperty);
        set => SetValue(SecondaryTextBrushProperty, value);
    }

    public IBrush AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public IBrush SuccessBrush
    {
        get => GetValue(SuccessBrushProperty);
        set => SetValue(SuccessBrushProperty, value);
    }

    public IBrush WarningBrush
    {
        get => GetValue(WarningBrushProperty);
        set => SetValue(WarningBrushProperty, value);
    }

    public IBrush ErrorBrush
    {
        get => GetValue(ErrorBrushProperty);
        set => SetValue(ErrorBrushProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PanelBrushProperty ||
            change.Property == ElevatedBrushProperty ||
            change.Property == DividerBrushProperty ||
            change.Property == PrimaryTextBrushProperty ||
            change.Property == SecondaryTextBrushProperty ||
            change.Property == AccentBrushProperty ||
            change.Property == SuccessBrushProperty ||
            change.Property == WarningBrushProperty ||
            change.Property == ErrorBrushProperty)
        {
            OnPaletteChanged();
        }
    }

    protected virtual void OnPaletteChanged()
    {
    }

#pragma warning disable AVP1001 // Registration is invoked only by the static property initializers above.
    private static StyledProperty<IBrush> RegisterBrush(string name, IBrush fallback) =>
        AvaloniaProperty.Register<GraphDesignerPanel, IBrush>(name, fallback);
#pragma warning restore AVP1001
}
