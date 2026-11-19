using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Headless.XUnit;
using Avalonia;
using Kudo.Controls;
using Shouldly;
using Xunit;

namespace Kudo.Controls.Tests;

[Collection("KudoControls-sequential")]
public sealed class IconButtonTests
{
    [AvaloniaFact]
    public void Defaults_match_ghost_toolbar_usage()
    {
        var button = new IconButton();

        button.Icon.ShouldBeNull();
        button.IconPlacement.ShouldBe(IconPlacement.Left);
        button.Content.ShouldBeNull();
        button.Command.ShouldBeNull();
        button.ShouldBeAssignableTo<Button>();
    }

    [AvaloniaFact]
    public void Icon_roundtrips_geometry()
    {
        var geometry = Geometry.Parse("M 0 0 L 4 4 L 8 0");
        var button = new IconButton { Icon = geometry };

        button.Icon.ShouldBeSameAs(geometry);
    }

    [AvaloniaFact]
    public void IconPlacement_change_raises_property_changed()
    {
        var button = new IconButton();
        var received = new List<IconPlacement>();
        var observer = new ObserverProxy(received.Add);
        using var sub = button.GetObservable(IconButton.IconPlacementProperty).Subscribe(observer);

        button.IconPlacement = IconPlacement.Top;

        received.ShouldContain(IconPlacement.Top);
        received.Last().ShouldBe(IconPlacement.Top);
    }

    [AvaloniaFact]
    public void IconPlacement_roundtrips_both_orientations()
    {
        var button = new IconButton { IconPlacement = IconPlacement.Top };
        button.IconPlacement.ShouldBe(IconPlacement.Top);

        button.IconPlacement = IconPlacement.Left;
        button.IconPlacement.ShouldBe(IconPlacement.Left);
    }

    [AvaloniaFact]
    public void Flyout_and_command_are_carried_by_native_button()
    {
        var flyout = new MenuFlyout();
        var command = new TestCommand();
        var button = new IconButton
        {
            Flyout = flyout,
            Command = command,
            Content = "画板"
        };

        button.Flyout.ShouldBeSameAs(flyout);
        button.Command.ShouldBeSameAs(command);
        button.Content.ShouldBe("画板");
    }

    private sealed class ObserverProxy : IObserver<IconPlacement>
    {
        private readonly Action<IconPlacement> _onNext;

        public ObserverProxy(Action<IconPlacement> onNext) => _onNext = onNext;

        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(IconPlacement value) => _onNext(value);
    }

    private sealed class TestCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => throw new NotImplementedException();
    }
}
