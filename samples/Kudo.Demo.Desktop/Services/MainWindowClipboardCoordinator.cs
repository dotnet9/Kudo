using Avalonia.Controls;
using Avalonia.Input.Platform;
using Kudo;
using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowClipboardCoordinator(
    TopLevel topLevel,
    Func<GraphDocumentView?> activeViewProvider,
    Func<GraphDocumentView?, string, bool> ensureEditable,
    Action<string, string?> appendLog)
{
    private int _pasteSequence;
    private GraphClipboardContent? _sourceContent;
    private GraphDocumentView? _pasteView;
    private long _pastePointerVersion = -1;
    private GraphPoint _pasteBasePoint;
    private bool _hasPasteBasePoint;
    private string? _pasteText;

    public async Task CopySelectionAsync(bool cut)
    {
        var view = activeViewProvider();
        if (cut && !ensureEditable(view, L(MainWindowLanguage.CutSelectionAction)))
        {
            return;
        }

        var clipboard = topLevel.Clipboard;
        var content = view?.EditorContext.Edit.Copy();
        if (view is null || content is null)
        {
            appendLog(L(MainWindowLanguage.SelectToCopy), L(MainWindowLanguage.NoCopySelection));
            return;
        }

        if (clipboard is null)
        {
            appendLog(
                L(MainWindowLanguage.ClipboardUnavailable),
                L(MainWindowLanguage.ClipboardUnavailableStatus));
            return;
        }

        if (cut && !view.EditorContext.Edit.CanCut(content))
        {
            appendLog(
                view.LastMessage ?? L(MainWindowLanguage.CutCancelled),
                L(MainWindowLanguage.CutCancelled));
            return;
        }

        try
        {
            await clipboard.SetTextAsync(content.Text);
        }
        catch (Exception exception)
        {
            appendLog(
                F(MainWindowLanguage.ClipboardWriteFailed, exception.Message),
                L(MainWindowLanguage.CopyFailedStatus));
            return;
        }

        _sourceContent = content;
        _pasteSequence = 0;
        _pasteView = view;
        _pastePointerVersion = view.LastPointerGraphPointVersion;
        _hasPasteBasePoint = false;
        _pasteText = content.Text;
        var elementDescription = FormatElementDescription(content);
        if (!cut)
        {
            appendLog(
                F(MainWindowLanguage.Copied, elementDescription),
                L(MainWindowLanguage.CopyCompleted));
            return;
        }

        if (!view.EditorContext.Edit.CanCut(content))
        {
            appendLog(
                L(MainWindowLanguage.SelectionChangedCutCancelled),
                L(MainWindowLanguage.CutCancelled));
            return;
        }

        if (!view.EditorContext.Edit.Cut(content))
        {
            appendLog(
                L(MainWindowLanguage.SelectionMissingCutCancelled),
                L(MainWindowLanguage.CutCancelled));
            return;
        }

        view.SetMessage(F(MainWindowLanguage.CutStatus, elementDescription));
    }

    public async Task PasteSelectionAsync()
    {
        var view = activeViewProvider();
        var clipboard = topLevel.Clipboard;
        if (view is null || !ensureEditable(view, L(MainWindowLanguage.PasteContentAction)))
        {
            return;
        }

        if (clipboard is null)
        {
            appendLog(
                L(MainWindowLanguage.ClipboardUnavailable),
                L(MainWindowLanguage.ClipboardUnavailableStatus));
            return;
        }

        string? text;
        try
        {
            text = await clipboard.TryGetTextAsync();
        }
        catch (Exception exception)
        {
            appendLog(
                F(MainWindowLanguage.ClipboardReadFailed, exception.Message),
                L(MainWindowLanguage.PasteFailedStatus));
            return;
        }

        if (!ReferenceEquals(view, activeViewProvider()))
        {
            appendLog(
                L(MainWindowLanguage.ActiveCanvasChangedPasteCancelled),
                L(MainWindowLanguage.PasteCancelled));
            return;
        }

        var pointerVersion = view.LastPointerGraphPointVersion;
        if (!string.Equals(_pasteText, text, StringComparison.Ordinal))
        {
            _pasteText = text;
            _pasteSequence = 0;
            _hasPasteBasePoint = false;
        }

        var hasNewPointerPoint = pointerVersion != _pastePointerVersion;
        var pointerPoint = hasNewPointerPoint ? view.LastPointerGraphPoint : null;
        var basePoint = pointerPoint ?? GetFallbackPastePoint(view);
        if (!_hasPasteBasePoint ||
            !ReferenceEquals(_pasteView, view) ||
            hasNewPointerPoint)
        {
            _pasteView = view;
            _pastePointerVersion = pointerVersion;
            _pasteBasePoint = basePoint;
            _pasteSequence = 0;
            _hasPasteBasePoint = true;
        }

        var offset = 24 * Math.Min(_pasteSequence, 20);
        var targetPoint = new GraphPoint(
            _pasteBasePoint.X + offset,
            _pasteBasePoint.Y + offset);
        GraphPastePlan? plan;
        var activeCanvasChanged = false;
        try
        {
            plan = await view.EditorContext.Edit.PasteAtAsync(
                text,
                targetPoint,
                _sourceContent,
                L(MainWindowLanguage.PasteCommandName),
                isStillActive: () =>
                {
                    var isActive = ReferenceEquals(view, activeViewProvider());
                    activeCanvasChanged = !isActive;
                    return isActive;
                });
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            appendLog(
                F(MainWindowLanguage.PasteAssetsFailed, exception.Message),
                L(MainWindowLanguage.PasteFailedStatus));
            return;
        }

        if (plan is null)
        {
            appendLog(
                activeCanvasChanged
                    ? L(MainWindowLanguage.ActiveCanvasChangedPasteCancelled)
                    : L(MainWindowLanguage.NothingPasteable),
                activeCanvasChanged
                    ? L(MainWindowLanguage.PasteCancelled)
                    : L(MainWindowLanguage.NoPasteableContent));
            return;
        }

        _pasteSequence++;
        var message = F(
            MainWindowLanguage.PasteAddedSummary,
            plan.NodeCount,
            plan.EdgeCount,
            plan.GroupCount,
            plan.AnnotationCount);
        view.SetMessage(message);
    }

    private static string FormatElementDescription(GraphClipboardContent content)
    {
        var description = content.Kind switch
        {
            GraphClipboardElementKind.Node => content.NodeIds.Count == 1
                ? F(MainWindowLanguage.ClipboardNode, content.ElementId)
                : F(MainWindowLanguage.ClipboardNodes, content.NodeIds.Count),
            GraphClipboardElementKind.Annotation =>
                F(MainWindowLanguage.ClipboardAnnotation, content.ElementId),
            _ => F(MainWindowLanguage.ClipboardEdge, content.ElementId)
        };
        return content.Kind == GraphClipboardElementKind.Node && content.EdgeIds.Count > 0
            ? description + F(MainWindowLanguage.ClipboardInternalEdges, content.EdgeIds.Count)
            : description;
    }

    private static GraphPoint GetFallbackPastePoint(GraphDocumentView view)
    {
        if (view.VisibleGraphBounds is { } bounds)
        {
            return new GraphPoint(bounds.X + 24, bounds.Y + 24);
        }

        return new GraphPoint(24, 24);
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
