namespace Kudo.Core.Documents;

public sealed class GraphDocumentSessionEditCoordinator(GraphDocumentSession session)
    : IGraphEditCoordinator
{
    public GraphDocumentSession Session { get; } =
        session ?? throw new ArgumentNullException(nameof(session));

    public GraphEditTransactionDecision Evaluate(GraphEditTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ReferenceEquals(request.Document, Session.Document))
        {
            return GraphEditTransactionDecision.Deny();
        }

        return request.Kind switch
        {
            GraphEditTransactionKind.Undo =>
                ReferenceEquals(Session.History.UndoCommand, request.Command)
                    ? GraphEditTransactionDecision.Allow
                    : GraphEditTransactionDecision.Deny(),
            GraphEditTransactionKind.Redo =>
                ReferenceEquals(Session.History.RedoCommand, request.Command)
                    ? GraphEditTransactionDecision.Allow
                    : GraphEditTransactionDecision.Deny(),
            _ => GraphEditTransactionDecision.Allow
        };
    }

    public GraphEditTransactionResult Execute(GraphEditTransactionRequest request)
    {
        var decision = Evaluate(request);
        if (!decision.IsAllowed)
        {
            return GraphEditTransactionResult.Rejected(decision.Reason);
        }

        GraphChangeSet changes;
        switch (request.Kind)
        {
            case GraphEditTransactionKind.Execute:
                changes = Session.ExecuteCommand(request.Command, request.OperationContext);
                break;
            case GraphEditTransactionKind.CommitApplied:
                changes = Session.RecordExecutedCommand(request.Command, request.OperationContext);
                break;
            case GraphEditTransactionKind.Undo:
                if (!Session.Undo())
                {
                    return GraphEditTransactionResult.Rejected();
                }

                changes = Session.LastChangeSet;
                break;
            case GraphEditTransactionKind.Redo:
                if (!Session.Redo())
                {
                    return GraphEditTransactionResult.Rejected();
                }

                changes = Session.LastChangeSet;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Kind));
        }

        return GraphEditTransactionResult.Accepted(changes);
    }
}
