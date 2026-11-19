using Kudo;
using Shouldly;

namespace Kudo.Avalonia.Tests.Canvas;

public sealed class ResizeAndConnectionInputProcessorTests
{
    [Theory]
    [InlineData(PortKind.Input, false)]
    [InlineData(PortKind.Output, true)]
    [InlineData(PortKind.InputOutput, true)]
    [InlineData(PortKind.Reference, true)]
    public void CanStartConnection_RequiresSendingPort(
        PortKind kind,
        bool expected)
    {
        ResizeAndConnectionInputProcessor.CanStartConnection(new PortModel
        {
            Kind = kind
        }).ShouldBe(expected);
    }

    [Fact]
    public void CanStartConnection_RejectsUnavailableOutgoingCapacity()
    {
        ResizeAndConnectionInputProcessor.CanStartConnection(new PortModel
        {
            Kind = PortKind.Output,
            Capacity = new PortCapacity { MaxIncoming = 1, MaxOutgoing = 0 }
        }).ShouldBeFalse();
    }

    [Fact]
    public void CanStartConnection_RejectsDisabledPort()
    {
        ResizeAndConnectionInputProcessor.CanStartConnection(new PortModel
        {
            Kind = PortKind.Output,
            State = PortState.Disabled
        }).ShouldBeFalse();
    }
}
