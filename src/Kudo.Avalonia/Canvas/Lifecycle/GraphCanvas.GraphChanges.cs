using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public void RefreshGraph()
    {
        _graphChangeController.RefreshGraph();
    }

    /// <summary>
    /// 按模型变更范围同步画布缓存；不支持的范围会安全回退为全量刷新。
    /// </summary>
    public void ApplyGraphChanges(GraphChangeSet changes)
    {
        _graphChangeController.ApplyGraphChanges(changes);
    }

    internal bool CommitGraphEdit(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (Document is not { } document)
        {
            return false;
        }

        var transaction = new GraphEditTransactionRequest(
            document,
            command,
            GraphEditTransactionKind.CommitApplied,
            GraphOperationContext.User());
        GraphEditTransactionResult result;
        try
        {
            result = EditCoordinator.Execute(transaction);
        }
        catch
        {
            RollbackAppliedGraphEdit(document, command);
            throw;
        }

        if (!result.IsAccepted)
        {
            RollbackAppliedGraphEdit(document, command);
            ReportGraphEditRejection(command, result.Reason);
            return false;
        }

        var args = new GraphEditCommittedEventArgs(command);
        GraphEditCommitted?.Invoke(this, args);
        ExecuteNotificationCommand(GraphEditCommittedCommand, args);
        ApplyGraphChanges(result.Changes);
        return true;
    }

    internal void ReportGraphEditRejection(
        IGraphCommand command,
        string? reason,
        bool undo = false)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (Document is not { } document)
        {
            return;
        }

        var requests = undo
            ? GraphEditRequestFactory.CreateUndo(document, command)
            : GraphEditRequestFactory.Create(document, command);
        var request = requests.FirstOrDefault() ??
            new GraphEditRequest(
                GraphEditOperation.ExecuteCommand,
                document,
                Command: command);
        ReportGraphEditRejection(request, reason);
    }

    private void RollbackAppliedGraphEdit(GraphDocument document, IGraphCommand command)
    {
        command.Undo(document);
        ApplyGraphChanges(GraphChangeSet.ForCommand(command, undo: true));
    }
}
