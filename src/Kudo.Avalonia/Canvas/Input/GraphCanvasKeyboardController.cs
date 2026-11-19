using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasKeyboardController(
    Control owner,
    Func<bool> isPanningProvider,
    Func<Key, KeyModifiers, bool> isKeyboardPanGestureProvider,
    Func<Key, bool> isKeyboardPanKeyProvider,
    Func<bool> isReadOnlyProvider,
    Func<Point, bool> rotationHandleProvider,
    Func<Point, GraphNodeResizeHandle?> resizeHandleProvider,
    Func<Point, bool> portProvider) : IDisposable
{
    private readonly Dictionary<GraphNodeResizeHandle, Cursor> _resizeCursors = [];
    private TopLevel? _inputTopLevel;
    private Cursor? _panReadyCursor;
    private Cursor? _panningCursor;
    private Cursor? _rotationCursor;
    private Cursor? _portCursor;

    public bool IsSpacePanMode { get; private set; }

    public void Subscribe()
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (ReferenceEquals(_inputTopLevel, topLevel))
        {
            return;
        }

        Unsubscribe();
        _inputTopLevel = topLevel;
        _inputTopLevel?.AddHandler(
            InputElement.KeyDownEvent,
            HandleTopLevelKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _inputTopLevel?.AddHandler(
            InputElement.KeyUpEvent,
            HandleTopLevelKeyUp,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    public void Unsubscribe()
    {
        if (_inputTopLevel is null)
        {
            return;
        }

        _inputTopLevel.RemoveHandler(InputElement.KeyDownEvent, HandleTopLevelKeyDown);
        _inputTopLevel.RemoveHandler(InputElement.KeyUpEvent, HandleTopLevelKeyUp);
        _inputTopLevel = null;
    }

    public void SetSpacePanMode(bool value)
    {
        if (IsSpacePanMode == value)
        {
            return;
        }

        IsSpacePanMode = value;
        UpdatePointerCursor();
    }

    public void UpdatePointerCursor(Point? pointerPosition = null)
    {
        if (isPanningProvider())
        {
            owner.Cursor = _panningCursor ??= new Cursor(StandardCursorType.SizeAll);
            return;
        }

        if (IsSpacePanMode)
        {
            owner.Cursor = _panReadyCursor ??= new Cursor(StandardCursorType.Hand);
            return;
        }

        var handle = !isReadOnlyProvider() && pointerPosition is { } point
            ? resizeHandleProvider(point)
            : null;
        if (!isReadOnlyProvider() && pointerPosition is { } rotationPoint &&
            rotationHandleProvider(rotationPoint))
        {
            owner.Cursor = GetRotationCursor();
            return;
        }

        if (!isReadOnlyProvider() && pointerPosition is { } portPoint &&
            portProvider(portPoint))
        {
            owner.Cursor = _portCursor ??= new Cursor(StandardCursorType.Hand);
            return;
        }

        owner.Cursor = handle is { } resizeHandle
            ? GetResizeCursor(resizeHandle)
            : null;
    }

    public void Dispose()
    {
        Unsubscribe();
        IsSpacePanMode = false;
        owner.Cursor = null;
        foreach (var cursor in _resizeCursors.Values)
        {
            cursor.Dispose();
        }

        _resizeCursors.Clear();
        _panReadyCursor?.Dispose();
        _panningCursor?.Dispose();
        _rotationCursor?.Dispose();
        _portCursor?.Dispose();
        _panReadyCursor = null;
        _panningCursor = null;
        _rotationCursor = null;
        _portCursor = null;
    }

    private void HandleTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (isKeyboardPanGestureProvider(e.Key, e.KeyModifiers) && !IsTextInputSource(e.Source))
        {
            SetSpacePanMode(true);
        }
    }

    private void HandleTopLevelKeyUp(object? sender, KeyEventArgs e)
    {
        if (isKeyboardPanKeyProvider(e.Key))
        {
            SetSpacePanMode(false);
        }
    }

    public Cursor GetResizeCursor(GraphNodeResizeHandle handle)
    {
        if (_resizeCursors.TryGetValue(handle, out var cursor))
        {
            return cursor;
        }

        var cursorType = handle switch
        {
            GraphNodeResizeHandle.TopLeft => StandardCursorType.TopLeftCorner,
            GraphNodeResizeHandle.Top => StandardCursorType.TopSide,
            GraphNodeResizeHandle.TopRight => StandardCursorType.TopRightCorner,
            GraphNodeResizeHandle.Right => StandardCursorType.RightSide,
            GraphNodeResizeHandle.BottomRight => StandardCursorType.BottomRightCorner,
            GraphNodeResizeHandle.Bottom => StandardCursorType.BottomSide,
            GraphNodeResizeHandle.BottomLeft => StandardCursorType.BottomLeftCorner,
            GraphNodeResizeHandle.Left => StandardCursorType.LeftSide,
            _ => StandardCursorType.Arrow
        };
        cursor = new Cursor(cursorType);
        _resizeCursors.Add(handle, cursor);
        return cursor;
    }

    public Cursor GetRotationCursor() =>
        _rotationCursor ??= new Cursor(StandardCursorType.Hand);

    private static bool IsTextInputSource(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TextBox)
            {
                return true;
            }
        }

        return false;
    }
}
