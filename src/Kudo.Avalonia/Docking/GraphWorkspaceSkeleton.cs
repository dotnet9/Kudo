using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;

namespace Kudo.Avalonia.Docking;

/// <summary>
/// Provides a lightweight placeholder that mirrors the standard Kudo dock workspace.
/// </summary>
public sealed class GraphWorkspaceSkeleton : Grid
{
    public GraphWorkspaceSkeleton()
    {
        IsHitTestVisible = false;
        Margin = new Thickness(8, 6, 8, 8);
        ColumnDefinitions = new ColumnDefinitions("220,6,*,6,260");
        RowDefinitions = new RowDefinitions("*,6,160");

        var left = new Grid
        {
            RowDefinitions = new RowDefinitions("2*,6,*")
        };
        Add(left, CreatePane(), 0, 0);
        Add(left, CreatePane(), 2, 0);

        Add(this, left, 0, 0);
        Add(this, CreatePane(), 0, 2);
        Add(this, CreatePane(), 0, 4);

        var diagnostics = CreatePane();
        SetRow(diagnostics, 2);
        SetColumn(diagnostics, 2);
        SetColumnSpan(diagnostics, 3);
        Children.Add(diagnostics);
    }

    private static Control CreatePane() =>
        new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(0x1E, 0x64, 0x74, 0x8B)),
            Opacity = 0.5
        };

    private static void Add(
        Grid grid,
        Control control,
        int row,
        int column)
    {
        SetRow(control, row);
        SetColumn(control, column);
        grid.Children.Add(control);
    }
}
