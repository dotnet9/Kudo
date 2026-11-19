using System.Collections;
using System.Globalization;
using System.Text.Json;
using Kudo;

namespace Kudo.Avalonia.ThreeD;

public sealed class GraphThreeDSceneBuilder
{
    private GraphThreeDColor _color = GraphThreeDColor.Accent;
    private GraphThreeDShapeKind _shapeKind = GraphThreeDShapeKind.Cube;
    private double _zoom = 1.0;
    private int _shapeCount = 1;
    private bool _autoRotate = true;
    private double _pitch = 24;
    private double _yaw = 34;
    private IReadOnlyList<GraphThreeDNode> _nodes = [];
    private IReadOnlyList<GraphThreeDEdge> _edges = [];
    private IReadOnlyList<string> _selectedNodeIds = [];
    private IReadOnlyList<string> _selectedEdgeIds = [];

    public GraphThreeDColor Color => _color;

    public GraphThreeDShapeKind ShapeKind => _shapeKind;

    public double Zoom => _zoom;

    public int ShapeCount => _shapeCount;

    public bool AutoRotate => _autoRotate;

    public double Pitch => _pitch;

    public double Yaw => _yaw;

    public IReadOnlyList<GraphThreeDNode> Nodes => _nodes;

    public IReadOnlyList<GraphThreeDEdge> Edges => _edges;

    public IReadOnlyList<string> SelectedNodeIds => _selectedNodeIds;

    public IReadOnlyList<string> SelectedEdgeIds => _selectedEdgeIds;

    public static GraphThreeDScene FromDocument(GraphDocument? document) =>
        FromDocument(document, [], []);

    public static GraphThreeDScene FromDocument(
        GraphDocument? document,
        IEnumerable<string>? selectedNodeIds,
        IEnumerable<string>? selectedEdgeIds)
    {
        if (document is null || document.Nodes.Count == 0)
        {
            return GraphThreeDScene.Default with
            {
                Color = new GraphThreeDColor(100, 116, 139),
                AutoRotate = false
            };
        }

        var hasFanOut = document.Edges
            .GroupBy(edge => edge.Source, PortReferenceComparer.Instance)
            .Any(group => group.Count() > 1);
        var hasBranching = hasFanOut || document.Edges.Count > Math.Max(1, document.Nodes.Count - 1);
        var color = hasBranching
            ? new GraphThreeDColor(14, 165, 233)
            : new GraphThreeDColor(22, 163, 74);
        var selectedIds = CreateSelectedNodeSet(selectedNodeIds);
        var selectedEdgeIdSet = CreateSelectedEdgeSet(selectedEdgeIds);
        var nodes = CreateNodes(document, selectedIds);
        var nodeIds = nodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var edges = document.Edges
            .Where(edge => nodeIds.Contains(edge.Source.NodeId) && nodeIds.Contains(edge.Target.NodeId))
            .Select(edge => new GraphThreeDEdge
            {
                Id = edge.Id,
                SourceNodeId = edge.Source.NodeId,
                TargetNodeId = edge.Target.NodeId,
                IsSelected = selectedEdgeIdSet.Contains(edge.Id)
            })
            .ToArray();
        var selectedNodeIdsInScene = nodes
            .Where(node => node.IsSelected)
            .Select(node => node.Id)
            .ToArray();
        var selectedEdgeIdsInScene = edges
            .Where(edge => edge.IsSelected)
            .Select(edge => edge.Id)
            .ToArray();

        return new GraphThreeDScene
        {
            ShapeKind = hasBranching ? GraphThreeDShapeKind.Pyramid : GraphThreeDShapeKind.Cube,
            ShapeCount = Math.Clamp(nodes.Count, 1, 9),
            Zoom = Math.Clamp(0.8 + document.Edges.Count * 0.08, 0.75, 1.65),
            Color = color,
            AutoRotate = true,
            Nodes = nodes,
            Edges = edges,
            SelectedNodeIds = selectedNodeIdsInScene,
            SelectedEdgeIds = selectedEdgeIdsInScene
        }.Normalize();
    }

    public GraphThreeDSceneBuilder WithColor(GraphThreeDColor color)
    {
        _color = color;
        return this;
    }

    public GraphThreeDSceneBuilder WithShape(GraphThreeDShapeKind shapeKind)
    {
        _shapeKind = shapeKind;
        return this;
    }

    public GraphThreeDSceneBuilder WithShape(string? shapeName)
    {
        if (Enum.TryParse<GraphThreeDShapeKind>(shapeName, true, out var shapeKind))
        {
            _shapeKind = shapeKind;
        }

        return this;
    }

    public GraphThreeDSceneBuilder WithZoom(double zoom)
    {
        _zoom = double.IsFinite(zoom) ? zoom : 1.0;
        return this;
    }

    public GraphThreeDSceneBuilder WithShapeCount(int shapeCount)
    {
        _shapeCount = shapeCount;
        return this;
    }

    public GraphThreeDSceneBuilder WithAutoRotate(bool autoRotate)
    {
        _autoRotate = autoRotate;
        return this;
    }

    public GraphThreeDSceneBuilder WithCamera(double pitch, double yaw)
    {
        _pitch = pitch;
        _yaw = yaw;
        return this;
    }

    public GraphThreeDSceneBuilder WithNodes(IEnumerable<GraphThreeDNode>? nodes)
    {
        _nodes = nodes?.ToArray() ?? [];
        return this;
    }

    public GraphThreeDSceneBuilder WithEdges(IEnumerable<GraphThreeDEdge>? edges)
    {
        _edges = edges?.ToArray() ?? [];
        return this;
    }

    public GraphThreeDSceneBuilder WithSelection(
        IEnumerable<string>? selectedNodeIds,
        IEnumerable<string>? selectedEdgeIds)
    {
        _selectedNodeIds = selectedNodeIds?
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        _selectedEdgeIds = selectedEdgeIds?
            .Where(edgeId => !string.IsNullOrWhiteSpace(edgeId))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        return this;
    }

    public GraphThreeDScene Build() =>
        new GraphThreeDScene
        {
            Color = _color,
            ShapeKind = _shapeKind,
            Zoom = _zoom,
            ShapeCount = _shapeCount,
            AutoRotate = _autoRotate,
            Pitch = _pitch,
            Yaw = _yaw,
            Nodes = _nodes,
            Edges = _edges,
            SelectedNodeIds = _selectedNodeIds,
            SelectedEdgeIds = _selectedEdgeIds
        }.Normalize();

    private static IReadOnlyList<GraphThreeDNode> CreateNodes(
        GraphDocument document,
        IReadOnlySet<string> selectedNodeIds)
    {
        var bounds = GetDocumentBounds(document);
        var scale = 5.5 / Math.Max(1, Math.Max(bounds.Width, bounds.Height));
        var centerX = bounds.X + bounds.Width / 2;
        var centerY = bounds.Y + bounds.Height / 2;
        var nodes = new List<GraphThreeDNode>();
        foreach (var node in document.Nodes)
        {
            var configuration = GraphThreeDNodeConfiguration.From(node);
            if (configuration?.Enabled == false)
            {
                continue;
            }

            var position = NormalizePoint(node.Position);
            var size = NormalizeSize(node.Size);
            var nodeCenterX = position.X + size.Width / 2;
            var nodeCenterY = position.Y + size.Height / 2;
            var connectionCount = document.Edges.Count(edge =>
                string.Equals(edge.Source.NodeId, node.Id, StringComparison.Ordinal) ||
                string.Equals(edge.Target.NodeId, node.Id, StringComparison.Ordinal));
            var derivedPosition = new GraphThreeDPoint(
                (nodeCenterX - centerX) * scale,
                Math.Clamp(0.08 + connectionCount * 0.06, 0.08, 0.68),
                (nodeCenterY - centerY) * scale);
            var derivedSize = new GraphThreeDSize(
                Math.Clamp(size.Width * scale * 1.18, 0.65, 1.85),
                Math.Clamp(size.Height * scale * 1.18, 0.32, 1.15),
                Math.Clamp((node.Ports.Count + 1) * 0.12, 0.28, 0.78));

            nodes.Add(new GraphThreeDNode
            {
                Id = node.Id,
                Title = node.Name,
                Type = node.Type,
                Position = configuration?.Position ?? derivedPosition,
                Size = configuration?.Size ?? derivedSize,
                ShapeKind = configuration?.ShapeKind,
                Color = configuration?.Color ?? CreateNodeColor(node),
                IsSelected = selectedNodeIds.Contains(node.Id)
            });
        }

        return nodes;
    }

    private static IReadOnlySet<string> CreateSelectedNodeSet(IEnumerable<string>? selectedNodeIds) =>
        selectedNodeIds?
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .ToHashSet(StringComparer.Ordinal) ??
        new HashSet<string>(StringComparer.Ordinal);

    private static IReadOnlySet<string> CreateSelectedEdgeSet(IEnumerable<string>? selectedEdgeIds) =>
        selectedEdgeIds?
            .Where(edgeId => !string.IsNullOrWhiteSpace(edgeId))
            .ToHashSet(StringComparer.Ordinal) ??
        new HashSet<string>(StringComparer.Ordinal);

    private static GraphRect GetDocumentBounds(GraphDocument document)
    {
        var nodeBounds = document.Nodes.Select(GraphNodeGeometry.GetCanvasBounds).ToArray();
        var minX = nodeBounds.Min(bounds => bounds.X);
        var minY = nodeBounds.Min(bounds => bounds.Y);
        var maxX = nodeBounds.Max(bounds => bounds.X + bounds.Width);
        var maxY = nodeBounds.Max(bounds => bounds.Y + bounds.Height);
        return new GraphRect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
    }

    private static GraphPoint NormalizePoint(GraphPoint point) =>
        new(
            double.IsFinite(point.X) ? point.X : 0,
            double.IsFinite(point.Y) ? point.Y : 0);

    private static GraphSize NormalizeSize(GraphSize size) =>
        new(
            double.IsFinite(size.Width) && size.Width > 0 ? size.Width : 160,
            double.IsFinite(size.Height) && size.Height > 0 ? size.Height : 88);

    private static GraphThreeDColor CreateNodeColor(NodeModel node) =>
        node.Type.ToLowerInvariant() switch
        {
            "input" => new GraphThreeDColor(22, 163, 74),
            "output" => new GraphThreeDColor(217, 119, 6),
            "transform" => new GraphThreeDColor(35, 120, 213),
            _ => new GraphThreeDColor(100, 116, 139)
        };

    private sealed record GraphThreeDNodeConfiguration(
        bool Enabled,
        GraphThreeDShapeKind? ShapeKind,
        GraphThreeDPoint? Position,
        GraphThreeDSize? Size,
        GraphThreeDColor? Color)
    {
        public static GraphThreeDNodeConfiguration? From(NodeModel node)
        {
            if (!node.Properties.TryGetValue(GraphNodePropertyKeys.ThreeD, out var source) ||
                source is null ||
                IsJsonNull(source))
            {
                return null;
            }

            return new GraphThreeDNodeConfiguration(
                ReadBoolean(source, "Enabled") ?? true,
                ReadShape(source, "Shape"),
                ReadPoint(source, "Position"),
                ReadSize(source, "Size"),
                ReadColor(source, "Color"));
        }

        private static bool IsJsonNull(object value) =>
            value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };

        private static GraphThreeDShapeKind? ReadShape(object source, string propertyName)
        {
            if (!TryGetProperty(source, propertyName, out var value) ||
                ReadStringValue(value) is not { } text)
            {
                return null;
            }

            return Enum.TryParse<GraphThreeDShapeKind>(text, true, out var shapeKind)
                ? shapeKind
                : null;
        }

        private static GraphThreeDPoint? ReadPoint(object source, string propertyName)
        {
            if (!TryGetProperty(source, propertyName, out var value))
            {
                return null;
            }

            var x = ReadDouble(value, "X");
            var y = ReadDouble(value, "Y");
            var z = ReadDouble(value, "Z");
            return x is { } xValue && y is { } yValue && z is { } zValue
                ? new GraphThreeDPoint(xValue, yValue, zValue).Normalize()
                : null;
        }

        private static GraphThreeDSize? ReadSize(object source, string propertyName)
        {
            if (!TryGetProperty(source, propertyName, out var value))
            {
                return null;
            }

            var width = ReadDouble(value, "Width");
            var height = ReadDouble(value, "Height");
            var depth = ReadDouble(value, "Depth");
            return width is { } widthValue &&
                   height is { } heightValue &&
                   depth is { } depthValue
                ? new GraphThreeDSize(widthValue, heightValue, depthValue).Normalize()
                : null;
        }

        private static GraphThreeDColor? ReadColor(object source, string propertyName)
        {
            if (!TryGetProperty(source, propertyName, out var value))
            {
                return null;
            }

            if (ReadStringValue(value) is { } text &&
                TryParseColor(text, out var color))
            {
                return color;
            }

            var r = ReadByte(value, "R");
            var g = ReadByte(value, "G");
            var b = ReadByte(value, "B");
            var a = ReadByte(value, "A") ?? 255;
            return r is { } red && g is { } green && b is { } blue
                ? new GraphThreeDColor(red, green, blue, a)
                : null;
        }

        private static bool? ReadBoolean(object? source, string propertyName) =>
            TryGetProperty(source, propertyName, out var value)
                ? ReadBooleanValue(value)
                : null;

        private static double? ReadDouble(object? source, string propertyName) =>
            TryGetProperty(source, propertyName, out var value)
                ? ReadDoubleValue(value)
                : null;

        private static byte? ReadByte(object? source, string propertyName)
        {
            var number = ReadDouble(source, propertyName);
            return number is { } value && double.IsFinite(value)
                ? (byte)Math.Clamp(Math.Round(value), byte.MinValue, byte.MaxValue)
                : null;
        }

        private static bool TryGetProperty(object? source, string name, out object? value)
        {
            if (source is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in element.EnumerateObject())
                    {
                        if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            value = property.Value;
                            return true;
                        }
                    }
                }
            }
            else if (source is IReadOnlyDictionary<string, object?> dictionary)
            {
                foreach (var pair in dictionary)
                {
                    if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = pair.Value;
                        return true;
                    }
                }
            }
            else if (source is IDictionary nonGeneric)
            {
                foreach (DictionaryEntry entry in nonGeneric)
                {
                    if (entry.Key is not null &&
                        string.Equals(entry.Key.ToString(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = entry.Value;
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }

        private static string? ReadStringValue(object? value) => value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text.Trim(),
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()?.Trim(),
            _ => null
        };

        private static bool? ReadBooleanValue(object? value) => value switch
        {
            bool boolean => boolean,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.False } => false,
            string text when bool.TryParse(text, out var boolean) => boolean,
            _ => null
        };

        private static double? ReadDoubleValue(object? value) => value switch
        {
            double number when double.IsFinite(number) => number,
            float number when double.IsFinite(number) => number,
            decimal number => (double)number,
            int number => number,
            long number => number,
            JsonElement { ValueKind: JsonValueKind.Number } element
                when element.TryGetDouble(out var number) && double.IsFinite(number) => number,
            string text when double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number) && double.IsFinite(number) => number,
            _ => null
        };

        private static bool TryParseColor(string text, out GraphThreeDColor color)
        {
            var hex = text.Trim().TrimStart('#');
            if (hex.Length != 6 && hex.Length != 8)
            {
                color = default;
                return false;
            }

            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                color = default;
                return false;
            }

            color = hex.Length == 8
                ? new GraphThreeDColor(
                    (byte)((value >> 16) & 0xFF),
                    (byte)((value >> 8) & 0xFF),
                    (byte)(value & 0xFF),
                    (byte)((value >> 24) & 0xFF))
                : new GraphThreeDColor(
                    (byte)((value >> 16) & 0xFF),
                    (byte)((value >> 8) & 0xFF),
                    (byte)(value & 0xFF));
            return true;
        }
    }

    private sealed class PortReferenceComparer : IEqualityComparer<PortReference>
    {
        public static PortReferenceComparer Instance { get; } = new();

        public bool Equals(PortReference? x, PortReference? y) =>
            x is not null &&
            y is not null &&
            string.Equals(x.NodeId, y.NodeId, StringComparison.Ordinal) &&
            string.Equals(x.PortId, y.PortId, StringComparison.Ordinal);

        public int GetHashCode(PortReference obj) =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.NodeId),
                StringComparer.Ordinal.GetHashCode(obj.PortId));
    }
}
