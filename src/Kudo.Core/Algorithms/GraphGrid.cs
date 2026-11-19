using Kudo;

namespace Kudo.Core.Algorithms;

public sealed record GraphGridRenderMetrics(
    double MinorSpacing,
    double MajorSpacing,
    int MajorLineInterval);

public static class GraphGridCalculator
{
    private const double MinimumSupportedZoom = 0.000001;
    private const double MaximumSupportedZoom = 1_000_000;
    private const double MaximumMinorPixelSpacing = 512;

    public static GraphGridRenderMetrics GetRenderMetrics(
        GraphGridSettings settings,
        double zoom,
        double minimumMinorPixelSpacing = 10)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Normalize();
        var safeZoom = double.IsFinite(zoom) && zoom > 0
            ? Math.Clamp(zoom, MinimumSupportedZoom, MaximumSupportedZoom)
            : 1;
        var minimumPixelSpacing = double.IsFinite(minimumMinorPixelSpacing) &&
                                  minimumMinorPixelSpacing is > 0 and <= MaximumMinorPixelSpacing
            ? minimumMinorPixelSpacing
            : 10;
        var multiplier = 1.0;
        var pixelSpacing = normalized.Spacing * safeZoom;
        while (pixelSpacing < minimumPixelSpacing)
        {
            multiplier *= normalized.MajorLineInterval;
            pixelSpacing = normalized.Spacing * multiplier * safeZoom;
        }

        var minorSpacing = normalized.Spacing * multiplier;
        return new GraphGridRenderMetrics(
            minorSpacing,
            minorSpacing * normalized.MajorLineInterval,
            normalized.MajorLineInterval);
    }
}

public sealed record GraphNodeSnapResult(
    IReadOnlyDictionary<string, GraphPoint> NodePositions,
    GraphPoint Delta,
    IReadOnlyList<GraphAlignmentGuide> AlignmentGuides,
    bool IsGuideSnapX,
    bool IsGuideSnapY,
    bool IsGridSnapX,
    bool IsGridSnapY);

public static class GraphNodeSnapper
{
    private const double MinimumSupportedZoom = 0.000001;
    private const double MaximumSupportedZoom = 1_000_000;

    public static GraphNodeSnapResult Snap(
        IEnumerable<NodeModel> selectedNodes,
        IEnumerable<NodeModel> referenceNodes,
        IReadOnlyDictionary<string, GraphPoint> initialPositions,
        string primaryNodeId,
        GraphPoint rawDelta,
        GraphGridSettings settings,
        double zoom = 1)
    {
        ArgumentNullException.ThrowIfNull(selectedNodes);
        ArgumentNullException.ThrowIfNull(referenceNodes);
        ArgumentNullException.ThrowIfNull(initialPositions);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryNodeId);
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Normalize();
        var selected = selectedNodes
            .Where(node => initialPositions.ContainsKey(node.Id))
            .DistinctBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
        var primary = selected.FirstOrDefault(node => node.Id == primaryNodeId) ?? selected.FirstOrDefault();
        if (primary is null)
        {
            return CreateResult(selected, initialPositions, rawDelta);
        }

        var selectedIds = selected.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var references = normalized.IsGuidesEnabled
            ? referenceNodes
                .Where(node => !selectedIds.Contains(node.Id))
                .DistinctBy(node => node.Id, StringComparer.Ordinal)
                .ToArray()
            : [];
        var safeZoom = double.IsFinite(zoom) && zoom > 0
            ? Math.Clamp(zoom, MinimumSupportedZoom, MaximumSupportedZoom)
            : 1;
        var primaryStart = initialPositions[primary.Id];
        var primaryStartBounds = GraphNodeGeometry.GetCanvasBounds(
            primaryStart,
            primary.Size,
            primary.Rotation);
        var candidateBoundsPosition = new GraphPoint(
            primaryStartBounds.X + rawDelta.X,
            primaryStartBounds.Y + rawDelta.Y);
        var tolerance = normalized.GuideTolerance / safeZoom;
        var gridTolerance = normalized.SnapTolerance / safeZoom;
        var guides = new List<GraphAlignmentGuide>(2);

        var xSnap = ResolveAxis(
            candidateBoundsPosition.X,
            primaryStartBounds.Width,
            references,
            horizontal: true,
            normalized,
            tolerance,
            gridTolerance,
            out var xGuide,
            out var isGuideSnapX,
            out var isGridSnapX);
        var ySnap = ResolveAxis(
            candidateBoundsPosition.Y,
            primaryStartBounds.Height,
            references,
            horizontal: false,
            normalized,
            tolerance,
            gridTolerance,
            out var yGuide,
            out var isGuideSnapY,
            out var isGridSnapY);

        if (xGuide is not null)
        {
            guides.Add(xGuide);
        }

        if (yGuide is not null)
        {
            guides.Add(yGuide);
        }

        var snappedDelta = new GraphPoint(
            xSnap - primaryStartBounds.X,
            ySnap - primaryStartBounds.Y);
        var positions = selected.ToDictionary(
            node => node.Id,
            node => new GraphPoint(
                initialPositions[node.Id].X + snappedDelta.X,
                initialPositions[node.Id].Y + snappedDelta.Y),
            StringComparer.Ordinal);
        return new GraphNodeSnapResult(
            positions,
            snappedDelta,
            guides,
            isGuideSnapX,
            isGuideSnapY,
            isGridSnapX,
            isGridSnapY);
    }

    private static double ResolveAxis(
        double candidatePosition,
        double primarySize,
        IReadOnlyCollection<NodeModel> references,
        bool horizontal,
        GraphGridSettings settings,
        double tolerance,
        double gridTolerance,
        out GraphAlignmentGuide? guide,
        out bool isGuideSnap,
        out bool isGridSnap)
    {
        var primaryFeatures = GetFeatures(candidatePosition, primarySize);
        var bestGuide = FindGuide(primaryFeatures, references, horizontal, tolerance);
        if (bestGuide is not null)
        {
            guide = bestGuide.Value.Guide;
            isGuideSnap = true;
            isGridSnap = false;
            return candidatePosition + bestGuide.Value.Offset;
        }

        guide = null;
        isGuideSnap = false;
        if (!settings.IsSnapEnabled)
        {
            isGridSnap = false;
            return candidatePosition;
        }

        var nearest = Math.Round(
            candidatePosition / settings.Spacing,
            MidpointRounding.AwayFromZero) * settings.Spacing;
        if (Math.Abs(nearest - candidatePosition) <= gridTolerance)
        {
            isGridSnap = true;
            return nearest;
        }

        isGridSnap = false;
        return candidatePosition;
    }

    private static GuideCandidate? FindGuide(
        IReadOnlyList<double> primaryFeatures,
        IReadOnlyCollection<NodeModel> references,
        bool horizontal,
        double tolerance)
    {
        if (tolerance <= 0)
        {
            return null;
        }

        GuideCandidate? best = null;
        foreach (var reference in references)
        {
            var bounds = GraphNodeGeometry.GetCanvasBounds(reference);
            var referencePosition = horizontal ? bounds.X : bounds.Y;
            var referenceSize = horizontal ? bounds.Width : bounds.Height;
            var referenceFeatures = GetFeatures(referencePosition, referenceSize);
            for (var primaryIndex = 0; primaryIndex < primaryFeatures.Count; primaryIndex++)
            {
                for (var referenceIndex = 0; referenceIndex < referenceFeatures.Count; referenceIndex++)
                {
                    var offset = referenceFeatures[referenceIndex] - primaryFeatures[primaryIndex];
                    var distance = Math.Abs(offset);
                    var candidate = new GuideCandidate(
                        distance,
                        offset,
                        new GraphAlignmentGuide(
                            horizontal
                                ? GraphGuideOrientation.Vertical
                                : GraphGuideOrientation.Horizontal,
                            referenceFeatures[referenceIndex]),
                        reference.Id,
                        primaryIndex,
                        referenceIndex);
                    if (distance > tolerance || !IsBetter(candidate, best))
                    {
                        continue;
                    }

                    best = candidate;
                }
            }
        }

        return best;
    }

    private static bool IsBetter(GuideCandidate candidate, GuideCandidate? current)
    {
        if (current is null)
        {
            return true;
        }

        var comparison = candidate.Distance.CompareTo(current.Value.Distance);
        if (comparison != 0)
        {
            return comparison < 0;
        }

        comparison = string.Compare(candidate.ReferenceNodeId, current.Value.ReferenceNodeId, StringComparison.Ordinal);
        if (comparison != 0)
        {
            return comparison < 0;
        }

        comparison = candidate.PrimaryFeatureIndex.CompareTo(current.Value.PrimaryFeatureIndex);
        return comparison != 0
            ? comparison < 0
            : candidate.ReferenceFeatureIndex < current.Value.ReferenceFeatureIndex;
    }

    private static IReadOnlyList<double> GetFeatures(double position, double size) =>
    [
        position,
        position + size / 2,
        position + size
    ];

    private static GraphNodeSnapResult CreateResult(
        IReadOnlyCollection<NodeModel> selected,
        IReadOnlyDictionary<string, GraphPoint> initialPositions,
        GraphPoint rawDelta) =>
        new(
            selected.ToDictionary(
                node => node.Id,
                node => new GraphPoint(
                    initialPositions[node.Id].X + rawDelta.X,
                    initialPositions[node.Id].Y + rawDelta.Y),
                StringComparer.Ordinal),
            rawDelta,
            [],
            false,
            false,
            false,
            false);

    private readonly record struct GuideCandidate(
        double Distance,
        double Offset,
        GraphAlignmentGuide Guide,
        string ReferenceNodeId,
        int PrimaryFeatureIndex,
        int ReferenceFeatureIndex);
}
