using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasNodeTitleEditor(GraphCanvasNodeTitleEditorContext context)
{
    private const double MinimumEditingZoom = 0.5;

    private readonly TextBox _editor = new()
    {
        Name = GraphCanvas.NodeTitleEditorPartName,
        AcceptsReturn = false,
        IsVisible = false,
        MinHeight = 0,
        Padding = new Thickness(4, 0),
        VerticalContentAlignment = VerticalAlignment.Center,
        ZIndex = int.MaxValue
    };
    private NodeModel? _editingNode;
    private bool _isEndingEdit;

    public bool IsEditing => _editingNode is not null;

    public string? EditingNodeId => _editingNode?.Id;

    public void Initialize()
    {
        _editor.AddHandler(
            InputElement.KeyDownEvent,
            HandleKeyDown,
            RoutingStrategies.Tunnel);
        _editor.AddHandler(
            InputElement.LostFocusEvent,
            HandleLostFocus,
            RoutingStrategies.Bubble);
        context.NodeControlLayer.Children.Add(_editor);
    }

    public bool CanBegin(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        return context.IsEnabled() &&
               !context.IsReadOnly() &&
               context.Viewport().Zoom >= MinimumEditingZoom &&
               context.Document() is { } document &&
               document.FindNode(nodeId) is { } node &&
               context.IsNodeVisible(node.Id) &&
               context.CanAuthorizeEdit(new GraphEditRequest(
                   GraphEditOperation.EditNodeProperties,
                   document,
                   Nodes: [node]));
    }

    public bool Begin(string nodeId)
    {
        if (!context.IsEnabled() ||
            context.IsReadOnly() ||
            context.Viewport().Zoom < MinimumEditingZoom ||
            context.Document() is not { } document ||
            document.FindNode(nodeId) is not { } node ||
            !context.IsNodeVisible(node.Id) ||
            !context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.EditNodeProperties,
                document,
                Nodes: [node])))
        {
            return false;
        }

        if (ReferenceEquals(_editingNode, node))
        {
            _editor.Focus();
            _editor.SelectAll();
            return true;
        }

        context.CommitAnnotationTextEdit();
        context.CommitEdgeLabelEdit();
        Cancel();
        _editingNode = node;
        _editor.Text = node.Name ?? string.Empty;
        _editor.IsVisible = true;
        UpdateLayout();
        _editor.Focus();
        _editor.SelectAll();
        context.InvalidateVisual();
        return true;
    }

    public bool Commit() => End(commit: true);

    public bool Cancel() => End(commit: false);

    public bool TryBegin(NodeModel node, Point screenPoint)
    {
        var appearance = (node.Appearance ?? new GraphNodeAppearance()).Normalize();
        var isSymbol = appearance.VisualMode == GraphNodeVisualMode.Symbol;
        var hitPoint = isSymbol
            ? screenPoint
            : context.InverseTransformNodeScreenPoint(node, screenPoint);
        if (!isSymbol && !UsesDefaultNodeRendering(node) ||
            context.IsLevelOfDetailEnabled() && context.Viewport().Zoom < context.LodLabelsZoomThreshold() ||
            !GraphLabelVisibilityResolver.IsNodeNameVisible(
                node,
                context.SymbolNameVisibility(),
                context.SelectedNodeIds().Contains(node.Id),
                isHovered: true) ||
            !GetTitleScreenRect(node).Contains(hitPoint))
        {
            return false;
        }

        return Begin(node.Id);
    }

    public void UpdateLayout()
    {
        if (_editingNode is not { } node)
        {
            return;
        }

        if (context.Document() is not { } document ||
            !ReferenceEquals(document.FindNode(node.Id), node) ||
            !context.IsNodeVisible(node.Id))
        {
            Cancel();
            return;
        }

        var rect = GetEditorScreenRect(node);
        Canvas.SetLeft(_editor, rect.X);
        Canvas.SetTop(_editor, rect.Y);
        _editor.Width = rect.Width;
        _editor.Height = rect.Height;
        _editor.FontSize = Math.Max(10, 14 * context.Viewport().Zoom);
        var nodeRect = context.GetNodeScreenRect(node);
        var rotation = GraphNodeGeometry.NormalizeRotation(node.Rotation);
        var isSymbol = node.Appearance?.VisualMode == GraphNodeVisualMode.Symbol;
        if (isSymbol || rotation == 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            _editor.RenderTransform = null;
            _editor.RenderTransformOrigin = RelativePoint.Center;
        }
        else
        {
            _editor.RenderTransformOrigin = new RelativePoint(
                (nodeRect.Center.X - rect.X) / rect.Width,
                (nodeRect.Center.Y - rect.Y) / rect.Height,
                RelativeUnit.Relative);
            _editor.RenderTransform = new RotateTransform(rotation);
        }

        _editor.Measure(rect.Size);
        _editor.Arrange(rect);
        context.NodeControlLayer.InvalidateArrange();
    }

    public bool IsEditorSource(object? source)
    {
        var visual = source as Visual;
        while (visual is not null && !ReferenceEquals(visual, context.Owner))
        {
            if (ReferenceEquals(visual, _editor))
            {
                return true;
            }

            visual = visual.GetVisualParent();
        }

        return false;
    }

    private bool End(bool commit)
    {
        if (_editingNode is not { } node || _isEndingEdit)
        {
            return false;
        }

        _isEndingEdit = true;
        try
        {
            var document = context.Document();
            var newName = _editor.Text;
            _editingNode = null;
            _editor.IsVisible = false;

            if (!commit ||
                document is null ||
                !ReferenceEquals(document.FindNode(node.Id), node) ||
                newName == node.Name)
            {
                context.InvalidateVisual();
                return true;
            }

            var command = new SetNodePropertiesCommand(
                node.Id,
                node.Number,
                node.Number,
                node.Name,
                newName,
                node.Description,
                node.Description,
                node.Position,
                node.Position,
                node.Rotation,
                node.Rotation,
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.EditNodeTitleCommand));
            if (!context.CanPerformEdit(new GraphEditRequest(
                    GraphEditOperation.EditNodeProperties,
                    document,
                    Nodes: [node],
                    Command: command)))
            {
                context.InvalidateVisual();
                return true;
            }

            node.Name = newName;
            return context.CommitGraphEdit(command);
        }
        finally
        {
            _isEndingEdit = false;
        }
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Commit();
            context.FocusCanvas();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Cancel();
            context.FocusCanvas();
            e.Handled = true;
        }
    }

    private void HandleLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!_isEndingEdit)
        {
            Commit();
        }
    }

    private bool UsesDefaultNodeRendering(NodeModel node) =>
        ReferenceEquals(context.NodeRenderer(), DefaultGraphNodeRenderer.Instance) ||
        context.NodeRenderer() is IGraphNodeRendererDefaultCompatibility compatibility &&
        compatibility.UsesDefaultRendering(node);

    private Rect GetTitleScreenRect(NodeModel node)
    {
        if (node.Appearance?.VisualMode == GraphNodeVisualMode.Symbol)
        {
            return GetEditorScreenRect(node);
        }

        var nodeRect = context.GetNodeScreenRect(node);
        if (node.Appearance?.VisualMode == GraphNodeVisualMode.Text)
        {
            return nodeRect.Deflate(8 * context.Viewport().Zoom);
        }

        var inset = 8 * context.Viewport().Zoom;
        return new Rect(
            nodeRect.X + inset,
            nodeRect.Y,
            Math.Max(0, nodeRect.Width - inset * 2),
            Math.Min(nodeRect.Height, 36 * context.Viewport().Zoom));
    }

    private Rect GetEditorScreenRect(NodeModel node)
    {
        if (node.Appearance?.VisualMode == GraphNodeVisualMode.Symbol)
        {
            var visualBounds = context.GetNodeVisualScreenRect(node);
            var editorHeight = Math.Max(
                24,
                node.Appearance.TextAppearance.Normalize().FontSize * context.Viewport().Zoom +
                12 * context.Viewport().Zoom);
            return new Rect(
                visualBounds.X,
                visualBounds.Bottom + context.SymbolNameOffset() * context.Viewport().Zoom,
                Math.Max(24, visualBounds.Width),
                editorHeight);
        }

        var nodeRect = context.GetNodeScreenRect(node);
        var horizontalInset = 10 * context.Viewport().Zoom;
        var verticalInset = 6 * context.Viewport().Zoom;
        if (node.Appearance?.VisualMode == GraphNodeVisualMode.Text)
        {
            var editorHeight = Math.Min(
                Math.Max(
                    24,
                    node.Appearance.TextAppearance.Normalize().FontSize * context.Viewport().Zoom +
                    12 * context.Viewport().Zoom),
                Math.Max(24, nodeRect.Height - verticalInset * 2));
            return new Rect(
                nodeRect.X + horizontalInset,
                nodeRect.Center.Y - editorHeight / 2,
                Math.Max(24, nodeRect.Width - horizontalInset * 2),
                editorHeight);
        }

        return new Rect(
            nodeRect.X + horizontalInset,
            nodeRect.Y + verticalInset,
            Math.Max(24, nodeRect.Width - horizontalInset * 2),
            Math.Min(
                Math.Max(24, 28 * context.Viewport().Zoom),
                Math.Max(0, nodeRect.Height - verticalInset * 2)));
    }
}

internal sealed class GraphCanvasNodeTitleEditorContext
{
    public required Visual Owner { get; init; }

    public required Canvas NodeControlLayer { get; init; }

    public required Func<GraphDocument?> Document { get; init; }

    public required Func<bool> IsEnabled { get; init; }

    public required Func<bool> IsReadOnly { get; init; }

    public required Func<GraphViewport> Viewport { get; init; }

    public required Func<bool> IsLevelOfDetailEnabled { get; init; }

    public required Func<double> LodLabelsZoomThreshold { get; init; }

    public required Func<GraphLabelVisibility> SymbolNameVisibility { get; init; }

    public required Func<IReadOnlySet<string>> SelectedNodeIds { get; init; }

    public required Func<IGraphNodeRenderer> NodeRenderer { get; init; }

    public required Func<double> SymbolNameOffset { get; init; }

    public required Func<string, bool> IsNodeVisible { get; init; }

    public required Func<GraphEditRequest, bool> CanAuthorizeEdit { get; init; }

    public required Func<GraphEditRequest, bool> CanPerformEdit { get; init; }

    public required Func<NodeModel, Rect> GetNodeScreenRect { get; init; }

    public required Func<NodeModel, Rect> GetNodeVisualScreenRect { get; init; }

    public required Func<NodeModel, Point, Point> InverseTransformNodeScreenPoint { get; init; }

    public required Func<bool> CommitAnnotationTextEdit { get; init; }

    public required Func<bool> CommitEdgeLabelEdit { get; init; }

    public required Func<IGraphCommand, bool> CommitGraphEdit { get; init; }

    public required Action InvalidateVisual { get; init; }

    public required Action FocusCanvas { get; init; }
}
