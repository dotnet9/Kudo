using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kudo.Core.Documents;
using Shouldly;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class GraphRulerTests
{
    [Fact]
    public void Origin_label_visibility_can_be_disabled_without_removing_the_tick()
    {
        var canvas = new GraphCanvas { Viewport = GraphViewport.Default };
        var ruler = new GraphRuler { TargetCanvas = canvas };

        var visibleOriginTick = ruler.CalculateTicks(new Size(200, 22))
            .Single(tick => tick.GraphValue == 0);

        visibleOriginTick.Label.ShouldBe("0");

        ruler.IsOriginLabelVisible = false;

        var originTick = ruler.CalculateTicks(new Size(200, 22))
            .Single(tick => tick.GraphValue == 0);

        originTick.IsMajor.ShouldBeTrue();
        originTick.Label.ShouldBeNull();
    }

    [Fact]
    public void Document_ruler_corner_tracks_the_effective_ruler_appearance()
    {
        using var view = new Designer.GraphDocumentView(
            new GraphDocumentSession(new GraphDocument()));
        var root = view.Content.ShouldBeOfType<Border>();
        var host = root.Child.ShouldBeOfType<Grid>();
        var corner = host.Children.OfType<Border>().Single();
        var origin = corner.Child.ShouldBeOfType<TextBlock>();
        var background = Brushes.AliceBlue;
        var border = Brushes.CadetBlue;
        var foreground = Brushes.DarkSlateBlue;

        view.HorizontalRuler.Background = background;
        view.HorizontalRuler.BorderBrush = border;
        view.HorizontalRuler.Foreground = foreground;
        view.HorizontalRuler.FontSize = 11;

        corner.Background.ShouldBeSameAs(background);
        corner.BorderBrush.ShouldBeSameAs(border);
        origin.Foreground.ShouldBeSameAs(foreground);
        origin.FontSize.ShouldBe(11);
    }
}
