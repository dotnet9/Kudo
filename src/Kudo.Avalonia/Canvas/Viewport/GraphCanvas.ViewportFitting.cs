using Avalonia;
using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public bool FitToDocument(Size? viewportSize = null, double padding = 48)
        => FitToGraphBounds(
            GetDocumentGraphBounds(),
            viewportSize ?? Bounds.Size,
            padding,
            GraphViewportChangeReason.FitDocument);

    public bool FitToSelection(Size? viewportSize = null, double padding = 64)
        => FitToGraphBounds(
            GetSelectionGraphBounds(),
            viewportSize ?? Bounds.Size,
            padding,
            GraphViewportChangeReason.FitSelection);

    public Rect? GetSelectionGraphBounds()
    {
        var document = Document;
        if (document is null)
        {
            return null;
        }

        Rect? bounds = null;
        foreach (var node in document.Nodes.Where(node => IsNodeSelected(node.Id) && IsNodeVisible(node.Id)))
        {
            bounds = bounds is { } current
                ? current.Union(ToGraphRect(node))
                : ToGraphRect(node);
        }

        foreach (var group in document.Groups.Where(group => IsGroupSelected(group.Id)))
        {
            if (GetGroupGraphBounds(group) is { } groupBounds)
            {
                bounds = bounds is { } current ? current.Union(groupBounds) : groupBounds;
            }
        }

        foreach (var selectedEdge in document.Edges.Where(edge => _selectedEdgeIds.Contains(edge.Id)))
        {
            if (GetEdgeGraphPath(selectedEdge) is not { } path)
            {
                continue;
            }

            foreach (var point in path.Points.Where(point =>
                         double.IsFinite(point.X) && double.IsFinite(point.Y)))
            {
                bounds = IncludePoint(bounds, point);
            }
        }

        if (document.FindAnnotation(SelectedAnnotationId ?? string.Empty) is { } annotation &&
            IsValidAnnotationBounds(annotation))
        {
            var annotationBounds = GetAnnotationGraphRect(annotation);
            bounds = bounds is { } current
                ? current.Union(annotationBounds)
                : annotationBounds;
        }

        return bounds;
    }

    public Rect? GetDocumentGraphBounds()
    {
        var document = Document;
        if (document is null)
        {
            return null;
        }

        if (!_isDocumentGraphBoundsDirty)
        {
            return _documentGraphBoundsCache;
        }

        Rect? bounds = null;
        foreach (var node in document.Nodes.Where(node => IsNodeVisible(node.Id)))
        {
            bounds = bounds is { } current
                ? current.Union(ToGraphRect(node))
                : ToGraphRect(node);
        }

        foreach (var group in document.Groups)
        {
            if (GetGroupGraphBounds(group) is { } groupBounds)
            {
                bounds = bounds is { } current
                    ? current.Union(groupBounds)
                    : groupBounds;
            }
        }

        foreach (var annotation in document.Annotations.Where(IsValidAnnotationBounds))
        {
            var annotationBounds = GetAnnotationGraphRect(annotation);
            bounds = bounds is { } current
                ? current.Union(annotationBounds)
                : annotationBounds;
        }

        foreach (var point in document.Edges
                     .Select(_edgeGraphPathProvider)
                     .Where(path => path is not null)
                     .SelectMany(path => path!.Points)
                     .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y)))
        {
            bounds = bounds is { } current
                ? new Rect(
                    Math.Min(current.Left, point.X),
                    Math.Min(current.Top, point.Y),
                    Math.Max(current.Right, point.X) - Math.Min(current.Left, point.X),
                    Math.Max(current.Bottom, point.Y) - Math.Min(current.Top, point.Y))
                : new Rect(point.X, point.Y, 0, 0);
        }

        _documentGraphBoundsCache = bounds;
        _isDocumentGraphBoundsDirty = false;
        return bounds;
    }

    private bool FitToGraphBounds(
        Rect? graphBounds,
        Size viewportSize,
        double padding,
        GraphViewportChangeReason reason) =>
        _viewportController.Fit(graphBounds, viewportSize, padding, reason);

    private static Rect IncludePoint(Rect? bounds, GraphPoint point) =>
        bounds is { } current
            ? new Rect(
                Math.Min(current.Left, point.X),
                Math.Min(current.Top, point.Y),
                Math.Max(current.Right, point.X) - Math.Min(current.Left, point.X),
                Math.Max(current.Bottom, point.Y) - Math.Min(current.Top, point.Y))
            : new Rect(point.X, point.Y, 0, 0);
}
