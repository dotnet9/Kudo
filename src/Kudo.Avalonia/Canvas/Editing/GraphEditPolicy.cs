namespace Kudo.Avalonia;

public readonly record struct GraphEditDecision(bool IsAllowed, string? Reason = null)
{
    public static GraphEditDecision Allow { get; } = new(true);

    public static GraphEditDecision Deny(string? reason = null) => new(false, reason);
}

public interface IGraphEditPolicy
{
    GraphEditDecision Evaluate(GraphEditRequest request);
}

public sealed class AllowAllGraphEditPolicy : IGraphEditPolicy
{
    public static AllowAllGraphEditPolicy Instance { get; } = new();

    private AllowAllGraphEditPolicy()
    {
    }

    public GraphEditDecision Evaluate(GraphEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GraphEditDecision.Allow;
    }
}
