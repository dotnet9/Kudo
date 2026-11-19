using Kudo;
using Kudo.Core.Documents;

namespace Kudo.Avalonia.Designer;

public sealed class GraphEditOperations
{
    private static readonly GraphPoint DefaultDuplicateOffset = new(24, 24);
    private readonly GraphDocumentView _view;
    private readonly GraphClipboardService _clipboard = new();

    internal GraphEditOperations(GraphDocumentView view) => _view = view;

    public bool CanUndo => _view.CanUndo;

    public bool CanRedo => _view.CanRedo;

    public bool CanDelete => _view.Canvas.CanDeleteSelection();

    public bool CanGroup => _view.Canvas.CanGroupSelectedNodes();

    public bool CanUngroup => _view.Canvas.CanUngroupSelectedNodes();

    public bool CanToggleGroup => _view.Canvas.CanToggleSelectedGroupCollapsed();

    public bool Undo() => _view.Undo();

    public bool Redo() => _view.Redo();

    public bool Delete() => _view.Canvas.DeleteSelection();

    public bool SetSelectedNodesLocked(bool value) =>
        _view.Canvas.SetSelectedNodesLocked(value);

    public bool ToggleSelectedNodesLocked() =>
        _view.Canvas.ToggleSelectedNodesLocked();

    /// <summary>
    /// Moves the selected nodes to the highest layer while preserving their relative order.
    /// The operation is recorded in the document history and can be undone.
    /// </summary>
    public bool BringSelectedNodesToFront(string? commandName = null) =>
        SetSelectedNodesBoundary(
            toFront: true,
            commandName ?? L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.BringSelectedNodesToFrontCommand));

    /// <summary>
    /// Moves the selected nodes to the lowest layer while preserving their relative order.
    /// The operation is recorded in the document history and can be undone.
    /// </summary>
    public bool SendSelectedNodesToBack(string? commandName = null) =>
        SetSelectedNodesBoundary(
            toFront: false,
            commandName ?? L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.SendSelectedNodesToBackCommand));

    /// <summary>
    /// Serializes the current selection for a host-provided system clipboard.
    /// </summary>
    public GraphClipboardContent? Copy()
    {
        var canvas = _view.Canvas;
        var content = _clipboard.SerializeSubgraphSelection(
            _view.Session.Document,
            canvas.SelectedNodeIds,
            canvas.SelectedEdgeIds,
            canvas.SelectedAnnotationId is { } annotationId ? [annotationId] : []);
        return content is null
            ? null
            : content with { SourceWorkingDirectory = _view.Session.WorkingDirectory };
    }

    /// <summary>
    /// Returns whether the captured selection is still current and may be removed.
    /// </summary>
    public bool CanCut(GraphClipboardContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return IsSelectionUnchanged(content) && _view.Canvas.CanDeleteSelection();
    }

    /// <summary>
    /// Removes a selection previously returned by <see cref="Copy"/>.
    /// The host should place the captured text on the system clipboard before calling this method.
    /// </summary>
    public bool Cut(GraphClipboardContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return IsSelectionUnchanged(content) && _view.Canvas.DeleteSelection();
    }

    /// <summary>
    /// Pastes graph content, copies referenced task assets when the source is known,
    /// executes the command, and selects the newly created elements.
    /// </summary>
    public async Task<GraphPastePlan?> PasteAsync(
        string? text,
        GraphPoint offset,
        GraphClipboardContent? sourceContent = null,
        string? commandName = null,
        CancellationToken cancellationToken = default,
        Func<bool>? isStillActive = null)
    {
        commandName = string.IsNullOrWhiteSpace(commandName)
            ? L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.PasteSelectionCommand)
            : commandName;
        if (!_clipboard.TryCreatePastePlan(
                _view.Session.Document,
                text,
                offset,
                out var plan,
                commandName,
                _view.ConnectionValidationRule,
                GraphCanvas.ConnectionValidationTextProvider) ||
            plan is null)
        {
            return null;
        }

        return await ExecutePastePlanAsync(
            text,
            plan,
            sourceContent,
            cancellationToken,
            isStillActive,
            GraphOperationContext.User(GraphOperationInteractions.Paste));
    }

    /// <summary>
    /// Pastes graph content with its copied top-left point aligned to the target.
    /// </summary>
    public async Task<GraphPastePlan?> PasteAtAsync(
        string? text,
        GraphPoint targetPoint,
        GraphClipboardContent? sourceContent = null,
        string? commandName = null,
        CancellationToken cancellationToken = default,
        Func<bool>? isStillActive = null)
    {
        commandName = string.IsNullOrWhiteSpace(commandName)
            ? L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.PasteSelectionCommand)
            : commandName;
        if (!_clipboard.TryCreatePastePlanAt(
                _view.Session.Document,
                text,
                targetPoint,
                out var plan,
                commandName,
                _view.ConnectionValidationRule,
                GraphCanvas.ConnectionValidationTextProvider) ||
            plan is null)
        {
            return null;
        }

        return await ExecutePastePlanAsync(
            text,
            plan,
            sourceContent,
            cancellationToken,
            isStillActive,
            GraphOperationContext.User(GraphOperationInteractions.Paste));
    }

    private async Task<GraphPastePlan?> ExecutePastePlanAsync(
        string? text,
        GraphPastePlan plan,
        GraphClipboardContent? sourceContent,
        CancellationToken cancellationToken,
        Func<bool>? isStillActive,
        GraphOperationContext operationContext)
    {
        if (!_view.TryAuthorizeCommand(plan.Command))
        {
            return null;
        }

        if (isStillActive is not null && !isStillActive())
        {
            return null;
        }

        await CopyAssetsAsync(text, sourceContent, cancellationToken);
        if (isStillActive is not null && !isStillActive())
        {
            return null;
        }

        if (!_view.TryExecuteCommand(plan.Command, operationContext))
        {
            return null;
        }

        ApplySelection(plan);
        return plan;
    }

    public bool Duplicate(
        GraphPoint? offset = null,
        string? commandName = null)
    {
        commandName = string.IsNullOrWhiteSpace(commandName)
            ? L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.DuplicateSelectionCommand)
            : commandName;
        var canvas = _view.Canvas;
        var content = _clipboard.SerializeSubgraphSelection(
            _view.Session.Document,
            canvas.SelectedNodeIds,
            canvas.SelectedEdgeIds,
            canvas.SelectedAnnotationId is { } annotationId ? [annotationId] : []);
        if (content is null ||
            !_clipboard.TryCreatePastePlan(
                _view.Session.Document,
                content.Text,
                offset ?? DefaultDuplicateOffset,
                out var plan,
                commandName,
                _view.ConnectionValidationRule,
                GraphCanvas.ConnectionValidationTextProvider) ||
            plan is null ||
            !_view.TryExecuteCommand(
                plan.Command,
                GraphOperationContext.User(GraphOperationInteractions.Duplicate)))
        {
            return false;
        }

        ApplySelection(plan);
        return true;
    }

    public bool Group(string? title = null) => _view.Canvas.GroupSelectedNodes(title);

    public bool Ungroup() => _view.Canvas.UngroupSelectedNodes();

    public bool ToggleGroup() => _view.Canvas.ToggleSelectedGroupCollapsed();

    private bool SetSelectedNodesBoundary(bool toFront, string commandName)
    {
        if (_view.IsReadOnly)
        {
            return false;
        }

        var document = _view.Session.Document;
        var selectedIds = _view.Canvas.SelectedNodeIds.ToHashSet(StringComparer.Ordinal);
        var selectedOrder = document.Nodes
            .Where(node => selectedIds.Contains(node.Id))
            .Select(node => node.Id)
            .ToArray();
        if (selectedOrder.Length == 0)
        {
            return false;
        }

        var oldOrder = document.Nodes.Select(node => node.Id).ToArray();
        var unselectedOrder = oldOrder
            .Where(nodeId => !selectedIds.Contains(nodeId))
            .ToArray();
        var newOrder = toFront
            ? unselectedOrder.Concat(selectedOrder).ToArray()
            : selectedOrder.Concat(unselectedOrder).ToArray();
        if (oldOrder.SequenceEqual(newOrder, StringComparer.Ordinal))
        {
            return false;
        }

        var command = new SetNodeOrderCommand(
            oldOrder,
            newOrder,
            selectedOrder,
            commandName);
        return _view.TryExecuteCommand(command);
    }

    private async Task CopyAssetsAsync(
        string? text,
        GraphClipboardContent? sourceContent,
        CancellationToken cancellationToken)
    {
        if (sourceContent is not
            {
                SourceWorkingDirectory: { Length: > 0 } sourceDirectory,
                AssetPaths.Count: > 0
            } ||
            !string.Equals(sourceContent.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        var targetDirectory = _view.Session.EnsureWorkingDirectory();
        if (string.Equals(
                Path.GetFullPath(sourceDirectory),
                Path.GetFullPath(targetDirectory),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await GraphTaskAssetStore.CopyAssetsAsync(
            sourceDirectory,
            targetDirectory,
            sourceContent.AssetPaths,
            cancellationToken);
    }

    private bool IsSelectionUnchanged(GraphClipboardContent content)
    {
        var canvas = _view.Canvas;
        return content.Kind switch
        {
            GraphClipboardElementKind.Node =>
                content.NodeIds.Count == canvas.SelectedNodeIds.Count &&
                content.NodeIds.ToHashSet(StringComparer.Ordinal).SetEquals(canvas.SelectedNodeIds),
            GraphClipboardElementKind.Annotation =>
                content.AnnotationIds.Count == 1 &&
                canvas.SelectedAnnotationId == content.ElementId,
            GraphClipboardElementKind.Edge =>
                content.EdgeIds.Count == canvas.SelectedEdgeIds.Count &&
                content.EdgeIds.ToHashSet(StringComparer.Ordinal).SetEquals(canvas.SelectedEdgeIds),
            _ => false
        };
    }

    private void ApplySelection(GraphPastePlan plan)
    {
        var canvas = _view.Canvas;
        if (plan.SelectedNodeIds.Count > 0)
        {
            canvas.SetSelectedNodes(plan.SelectedNodeIds, plan.SelectedNodeId);
            return;
        }

        canvas.ClearSelection();
        if (plan.SelectedEdgeIds.Count > 0)
        {
            canvas.SetSelectedEdges(plan.SelectedEdgeIds, plan.SelectedEdgeId);
        }
        else if (plan.SelectedAnnotationId is { } annotationId)
        {
            canvas.SetSelectedAnnotation(annotationId);
        }
    }

    private static string L(string key) => GraphLocalization.Get(key);
}
