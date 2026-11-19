namespace Kudo.Avalonia;

public sealed class GraphEditRejectedEventArgs(
    GraphEditRequest request,
    GraphEditDecision decision) : EventArgs
{
    public GraphEditRequest Request { get; } = request;

    public GraphEditDecision Decision { get; } = decision;
}
