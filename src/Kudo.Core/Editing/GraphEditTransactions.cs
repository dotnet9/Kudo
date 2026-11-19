namespace Kudo;

public enum GraphEditTransactionKind
{
    Execute,
    CommitApplied,
    Undo,
    Redo
}

public sealed record GraphEditTransactionRequest(
    GraphDocument Document,
    IGraphCommand Command,
    GraphEditTransactionKind Kind,
    GraphOperationContext? OperationContext = null)
{
    public GraphDocument Document { get; } =
        Document ?? throw new ArgumentNullException(nameof(Document));

    public IGraphCommand Command { get; } =
        Command ?? throw new ArgumentNullException(nameof(Command));

    public GraphOperationContext OperationContext { get; } =
        OperationContext ?? GraphOperationContext.Programmatic;
}

public readonly record struct GraphEditTransactionDecision(bool IsAllowed, string? Reason = null)
{
    public static GraphEditTransactionDecision Allow { get; } = new(true);

    public static GraphEditTransactionDecision Deny(string? reason = null) => new(false, reason);
}

public readonly record struct GraphEditTransactionResult(
    bool IsAccepted,
    GraphChangeSet Changes,
    string? Reason = null)
{
    public static GraphEditTransactionResult Accepted(GraphChangeSet changes) =>
        new(true, changes ?? throw new ArgumentNullException(nameof(changes)));

    public static GraphEditTransactionResult Rejected(string? reason = null) =>
        new(false, GraphChangeSet.Empty, reason);
}

/// <summary>
/// 协调 Kudo 文档命令与宿主业务事务。实现方只应在 Execute 返回接受后保留外部副作用。
/// </summary>
public interface IGraphEditCoordinator
{
    GraphEditTransactionDecision Evaluate(GraphEditTransactionRequest request);

    GraphEditTransactionResult Execute(GraphEditTransactionRequest request);
}

public sealed class DirectGraphEditCoordinator : IGraphEditCoordinator
{
    public static DirectGraphEditCoordinator Instance { get; } = new();

    private DirectGraphEditCoordinator()
    {
    }

    public GraphEditTransactionDecision Evaluate(GraphEditTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GraphEditTransactionDecision.Allow;
    }

    public GraphEditTransactionResult Execute(GraphEditTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        switch (request.Kind)
        {
            case GraphEditTransactionKind.Execute:
            case GraphEditTransactionKind.Redo:
                request.Command.Execute(request.Document);
                break;
            case GraphEditTransactionKind.CommitApplied:
                break;
            case GraphEditTransactionKind.Undo:
                request.Command.Undo(request.Document);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Kind));
        }

        return GraphEditTransactionResult.Accepted(GraphChangeSet.ForCommand(
            request.Command,
            request.Kind == GraphEditTransactionKind.Undo));
    }
}
