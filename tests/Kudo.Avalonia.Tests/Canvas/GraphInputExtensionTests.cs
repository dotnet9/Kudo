using Avalonia;
using Avalonia.Input;
using Shouldly;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class GraphInputExtensionTests
{
    [Fact]
    public void Default_gestures_match_the_built_in_canvas_operations()
    {
        var gestures = new GraphCanvasInputGestures();

        gestures.Selection.Replace.Matches(GraphPointerButton.Left, KeyModifiers.None)
            .ShouldBeTrue();
        gestures.Selection.Extend.Matches(GraphPointerButton.Left, KeyModifiers.Shift)
            .ShouldBeTrue();
        gestures.Selection.Subtract.Matches(GraphPointerButton.Left, KeyModifiers.Alt)
            .ShouldBeTrue();
        gestures.Drag.Matches(GraphPointerButton.Left, KeyModifiers.None).ShouldBeTrue();
        gestures.Edit.Matches(GraphPointerButton.Left, KeyModifiers.None).ShouldBeTrue();
        gestures.Connect.Matches(GraphPointerButton.Left, KeyModifiers.None).ShouldBeTrue();
        gestures.NodePushing.Matches(
                GraphPointerButton.Left,
                KeyModifiers.Control | KeyModifiers.Shift)
            .ShouldBeTrue();
        gestures.EdgeCutting.Matches(
                GraphPointerButton.Left,
                KeyModifiers.Control | KeyModifiers.Alt)
            .ShouldBeTrue();
        gestures.Pan.ShouldContain(new GraphPointerGesture(GraphPointerButton.Middle));
        gestures.Pan.ShouldContain(new GraphPointerGesture(GraphPointerButton.Right));
    }

    [Fact]
    public void Interaction_starters_are_ordered_by_priority_and_can_be_unregistered()
    {
        var starters = new GraphInteractionSessionStarterCollection();
        var calls = new List<string>();
        var first = new TestStarter("first", calls);
        var second = new TestStarter("second", calls);
        using var firstSubscription = starters.Register(first, priority: 1);
        using var secondSubscription = starters.Register(second, priority: 10);

        var session = starters.TryStart(new GraphInteractionSessionStartContext(
            new GraphCanvas(),
            null!,
            null,
            new Point(10, 20),
            new GraphPoint(10, 20),
            KeyModifiers.None,
            1,
            new GraphInteractionHitTestResult(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                null,
                null,
                null)));

        session.ShouldBeSameAs(second.Session);
        calls.ShouldBe(["second"]);

        secondSubscription.Dispose();
        starters.TryStart(new GraphInteractionSessionStartContext(
            new GraphCanvas(),
            null!,
            null,
            default,
            default,
            KeyModifiers.None,
            1,
            new GraphInteractionHitTestResult(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                null,
                null,
                null)));
        calls.ShouldBe(["second", "first"]);
    }

    private sealed class TestStarter(string name, ICollection<string> calls)
        : IGraphInteractionSessionStarter
    {
        public TestSession Session { get; } = new();

        public IGraphInteractionSession? TryStart(
            in GraphInteractionSessionStartContext context)
        {
            calls.Add(name);
            return Session;
        }
    }

    private sealed class TestSession : IGraphInteractionSession
    {
        public IPointer Pointer => null!;

        public Point StartPointerPoint => default;

        public bool AllowContextMenuOnRelease => false;

        public bool SupportsAutoPan => false;

        public void Update(Point point, KeyModifiers modifiers)
        {
        }

        public void Complete(Point point)
        {
        }

        public void CaptureLost()
        {
        }

        public void Cancel()
        {
        }
    }
}
