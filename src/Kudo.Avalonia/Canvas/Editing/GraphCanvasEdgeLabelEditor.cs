using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasEdgeLabelEditor(GraphCanvasEdgeLabelEditorContext context)
{
    private const double MinimumEditingZoom = 0.35;

    private readonly TextBox _editor = new()
    {
        Name = GraphCanvas.EdgeLabelEditorPartName,
        AcceptsReturn = false,
        IsVisible = false,
        MinHeight = 0,
        Padding = new Thickness(5, 0),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        ZIndex = int.MaxValue
    };
    private EdgeModel? _editingEdge;
    private bool _isEndingEdit;

    public bool IsEditing => _editingEdge is not null;

    public string? EditingEdgeId => _editingEdge?.Id;

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

    public bool Begin(string edgeId)
    {
        if (!context.IsEnabled() ||
            context.IsReadOnly() ||
            context.Viewport().Zoom < MinimumEditingZoom ||
            context.Document() is not { } document ||
            document.Edges.FirstOrDefault(candidate => candidate.Id == edgeId) is not { } edge ||
            !IsDefaultLabelVisible(edge) ||
            !context.CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.EditEdgeProperties,
                document,
                Edge: edge)) ||
            GetScreenBounds(edge) is null)
        {
            return false;
        }

        if (ReferenceEquals(_editingEdge, edge))
        {
            _editor.Focus();
            _editor.SelectAll();
            return true;
        }

        context.CommitNodeTitleEdit();
        context.CommitAnnotationTextEdit();
        Cancel();
        context.ClearSelection();
        context.SetSelectedEdgeId(edge.Id);
        _editingEdge = edge;
        _editor.Text = edge.Label ?? string.Empty;
        _editor.IsVisible = true;
        UpdateLayout();
        _editor.Focus();
        _editor.SelectAll();
        context.InvalidateVisual();
        return true;
    }

    public bool Commit() => End(commit: true);

    public bool Cancel() => End(commit: false);

    public Rect? GetScreenBounds(EdgeModel edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        var position = context.GetEdgeLabelScreenPosition(edge);
        return position is { } labelPosition
            ? DefaultGraphEdgeRenderer.GetLabelBounds(edge.Label, labelPosition, context.Viewport().Zoom)
            : null;
    }

    public EdgeModel? HitTest(Point screenPoint)
    {
        if (context.Document() is not { } document ||
            !ReferenceEquals(context.EdgeRenderer(), DefaultGraphEdgeRenderer.Instance) ||
            context.IsLevelOfDetailEnabled() && context.Viewport().Zoom < context.LodLabelsZoomThreshold())
        {
            return null;
        }

        return document.Edges
            .AsEnumerable()
            .Reverse()
            .FirstOrDefault(edge => GetScreenBounds(edge)?.Contains(screenPoint) == true);
    }

    public bool TryBegin(EdgeModel edge, Point screenPoint) =>
        GetScreenBounds(edge)?.Contains(screenPoint) == true &&
        Begin(edge.Id);

    public void UpdateLayout()
    {
        if (_editingEdge is not { } edge)
        {
            return;
        }

        if (context.Document() is not { } document ||
            !ReferenceEquals(document.Edges.FirstOrDefault(candidate => candidate.Id == edge.Id), edge) ||
            context.Viewport().Zoom < MinimumEditingZoom ||
            !IsDefaultLabelVisible(edge) ||
            GetScreenBounds(edge) is not { } labelBounds)
        {
            Cancel();
            return;
        }

        var width = Math.Max(80, labelBounds.Width + 8);
        var height = Math.Max(24, labelBounds.Height);
        var rect = new Rect(
            labelBounds.Center.X - width / 2,
            labelBounds.Center.Y - height / 2,
            width,
            height);
        Canvas.SetLeft(_editor, rect.X);
        Canvas.SetTop(_editor, rect.Y);
        _editor.Width = rect.Width;
        _editor.Height = rect.Height;
        _editor.FontSize = Math.Max(9, 12 * context.Viewport().Zoom);
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
        if (_editingEdge is not { } edge || _isEndingEdit)
        {
            return false;
        }

        _isEndingEdit = true;
        try
        {
            var document = context.Document();
            var newLabel = _editor.Text;
            _editingEdge = null;
            _editor.IsVisible = false;

            if (!commit ||
                document is null ||
                !ReferenceEquals(document.Edges.FirstOrDefault(candidate => candidate.Id == edge.Id), edge) ||
                newLabel == edge.Label)
            {
                context.InvalidateVisual();
                return true;
            }

            var command = new SetEdgePropertiesCommand(
                edge.Id,
                edge.Label,
                newLabel,
                edge.RoutingStyle,
                edge.RoutingStyle,
                edge.SourceMarker,
                edge.SourceMarker,
                edge.TargetMarker,
                edge.TargetMarker,
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.EditEdgeLabelCommand));
            if (!context.CanPerformEdit(new GraphEditRequest(
                    GraphEditOperation.EditEdgeProperties,
                    document,
                    Edge: edge,
                    Command: command)))
            {
                context.InvalidateVisual();
                return true;
            }

            edge.Label = newLabel;
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

    private bool IsDefaultLabelVisible(EdgeModel edge) =>
        ReferenceEquals(context.EdgeRenderer(), DefaultGraphEdgeRenderer.Instance) &&
        !string.IsNullOrWhiteSpace(edge.Label) &&
        (!context.IsLevelOfDetailEnabled() ||
         context.Viewport().Zoom >= context.LodLabelsZoomThreshold());
}

internal sealed class GraphCanvasEdgeLabelEditorContext
{
    public required Visual Owner { get; init; }

    public required Canvas NodeControlLayer { get; init; }

    public required Func<GraphDocument?> Document { get; init; }

    public required Func<bool> IsEnabled { get; init; }

    public required Func<bool> IsReadOnly { get; init; }

    public required Func<GraphViewport> Viewport { get; init; }

    public required Func<bool> IsLevelOfDetailEnabled { get; init; }

    public required Func<double> LodLabelsZoomThreshold { get; init; }

    public required Func<IGraphEdgeRenderer> EdgeRenderer { get; init; }

    public required Func<EdgeModel, Point?> GetEdgeLabelScreenPosition { get; init; }

    public required Func<GraphEditRequest, bool> CanPerformEdit { get; init; }

    public required Func<bool> CommitNodeTitleEdit { get; init; }

    public required Func<bool> CommitAnnotationTextEdit { get; init; }

    public required Action ClearSelection { get; init; }

    public required Action<string?> SetSelectedEdgeId { get; init; }

    public required Func<IGraphCommand, bool> CommitGraphEdit { get; init; }

    public required Action InvalidateVisual { get; init; }

    public required Action FocusCanvas { get; init; }
}
