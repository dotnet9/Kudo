// 命令历史：保存每次执行前后的修订号，并通过 GraphDocumentEditor 统一发布模型变更。
using Kudo;

namespace Kudo.Core.Documents;

public sealed class GraphCommandHistory
{
    private readonly List<HistoryEntry> _undoEntries = [];
    private readonly List<HistoryEntry> _redoEntries = [];
    private long _nextRevision;

    public GraphCommandHistory(int capacity = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    public int Capacity { get; }

    public long CurrentRevision { get; private set; }

    public bool CanUndo => _undoEntries.Count > 0;

    public bool CanRedo => _redoEntries.Count > 0;

    public IGraphCommand? UndoCommand => _undoEntries.LastOrDefault()?.Command;

    public IGraphCommand? RedoCommand => _redoEntries.LastOrDefault()?.Command;

    public string? UndoName => UndoCommand?.Name;

    public string? RedoName => RedoCommand?.Name;

    public event EventHandler? Changed;

    /// <summary>
    /// Raised after a successful undo or redo. Consumers can use this event for history
    /// descriptions without depending on a particular UI command path.
    /// </summary>
    public event EventHandler<GraphHistoryOperationChangedEventArgs>? OperationChanged;

    public void Execute(
        GraphDocument document,
        IGraphCommand command,
        GraphOperationContext? operationContext = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(command);
        command.Execute(document);
        RecordExecuted(command, operationContext);
    }

    public GraphChangeSet Execute(
        GraphDocumentEditor editor,
        IGraphCommand command,
        GraphOperationContext? operationContext = null)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(command);
        var changes = editor.Execute(command);
        RecordExecuted(command, operationContext, changes);
        return changes;
    }

    public void RecordExecuted(
        IGraphCommand command,
        GraphOperationContext? operationContext = null)
    {
        RecordExecuted(command, operationContext, changes: null);
    }

    private void RecordExecuted(
        IGraphCommand command,
        GraphOperationContext? operationContext,
        GraphChangeSet? changes)
    {
        ArgumentNullException.ThrowIfNull(command);
        var context = operationContext ?? GraphOperationContext.Programmatic;
        var entry = new HistoryEntry(
            command,
            context,
            CurrentRevision,
            ++_nextRevision);
        _undoEntries.Add(entry);
        if (_undoEntries.Count > Capacity)
        {
            _undoEntries.RemoveAt(0);
        }

        _redoEntries.Clear();
        CurrentRevision = entry.AfterRevision;
        Changed?.Invoke(this, EventArgs.Empty);
        PublishOperation(
            GraphDocumentEditDirection.Execute,
            entry,
            changes,
            entry.BeforeRevision,
            entry.AfterRevision);
    }

    public bool Undo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!CanUndo)
        {
            return false;
        }

        var index = _undoEntries.Count - 1;
        var entry = _undoEntries[index];
        entry.Command.Undo(document);
        _undoEntries.RemoveAt(index);
        _redoEntries.Add(entry);
        CurrentRevision = entry.BeforeRevision;
        Changed?.Invoke(this, EventArgs.Empty);
        PublishOperation(
            GraphDocumentEditDirection.Undo,
            entry,
            changes: null,
            entry.AfterRevision,
            entry.BeforeRevision,
            undo: true);
        return true;
    }

    public bool Undo(GraphDocumentEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (!CanUndo)
        {
            return false;
        }

        var index = _undoEntries.Count - 1;
        var entry = _undoEntries[index];
        var changes = editor.Undo(entry.Command);
        _undoEntries.RemoveAt(index);
        _redoEntries.Add(entry);
        CurrentRevision = entry.BeforeRevision;
        Changed?.Invoke(this, EventArgs.Empty);
        PublishOperation(
            GraphDocumentEditDirection.Undo,
            entry,
            changes,
            entry.AfterRevision,
            entry.BeforeRevision,
            undo: true);
        return true;
    }

    public bool Redo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!CanRedo)
        {
            return false;
        }

        var index = _redoEntries.Count - 1;
        var entry = _redoEntries[index];
        entry.Command.Execute(document);
        _redoEntries.RemoveAt(index);
        _undoEntries.Add(entry);
        CurrentRevision = entry.AfterRevision;
        Changed?.Invoke(this, EventArgs.Empty);
        PublishOperation(
            GraphDocumentEditDirection.Redo,
            entry,
            changes: null,
            entry.BeforeRevision,
            entry.AfterRevision);
        return true;
    }

    public bool Redo(GraphDocumentEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (!CanRedo)
        {
            return false;
        }

        var index = _redoEntries.Count - 1;
        var entry = _redoEntries[index];
        var changes = editor.Redo(entry.Command);
        _redoEntries.RemoveAt(index);
        _undoEntries.Add(entry);
        CurrentRevision = entry.AfterRevision;
        Changed?.Invoke(this, EventArgs.Empty);
        PublishOperation(
            GraphDocumentEditDirection.Redo,
            entry,
            changes,
            entry.BeforeRevision,
            entry.AfterRevision);
        return true;
    }

    public void Clear()
    {
        if (!CanUndo && !CanRedo)
        {
            return;
        }

        _undoEntries.Clear();
        _redoEntries.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void PublishOperation(
        GraphDocumentEditDirection direction,
        HistoryEntry entry,
        GraphChangeSet? changes,
        long beforeRevision,
        long afterRevision,
        bool undo = false)
    {
        if (OperationChanged is not { } operationChanged)
        {
            return;
        }

        operationChanged(
            this,
            new GraphHistoryOperationChangedEventArgs(
                direction,
                entry.Command,
                changes ?? GraphChangeSet.ForCommand(entry.Command, undo),
                beforeRevision,
                afterRevision,
                entry.OperationContext));
    }

    private sealed record HistoryEntry(
        IGraphCommand Command,
        GraphOperationContext OperationContext,
        long BeforeRevision,
        long AfterRevision);
}
