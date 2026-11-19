using System.Text.Json;
using System.Text.Json.Serialization;
using Kudo;

namespace Kudo.Core.Serialization;

public sealed class GraphClipboardPayload
{
    public const string CurrentFormat = "Kudo.Elements/1";

    public string Format { get; init; } = CurrentFormat;

    public List<NodeModel> Nodes { get; init; } = [];

    public List<EdgeModel> Edges { get; init; } = [];

    public List<GroupModel> Groups { get; set; } = [];

    public List<AnnotationModel> Annotations { get; set; } = [];
}

public sealed class GraphClipboardJsonSerializer
{
    public string Serialize(GraphClipboardPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Nodes is null ||
            payload.Edges is null ||
            payload.Groups is null ||
            payload.Annotations is null ||
            (payload.Nodes.Count == 0 &&
             payload.Edges.Count == 0 &&
             payload.Annotations.Count == 0))
        {
            throw new ArgumentException("The graph clipboard payload is empty.", nameof(payload));
        }

        return JsonSerializer.Serialize(
            payload,
            GraphClipboardJsonContext.Default.GraphClipboardPayload);
    }

    public bool TryDeserialize(string? json, out GraphClipboardPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var candidate = JsonSerializer.Deserialize(
                json,
                GraphClipboardJsonContext.Default.GraphClipboardPayload);
            if (candidate is not null)
            {
                candidate.Groups ??= [];
                candidate.Annotations ??= [];
                foreach (var node in candidate.Nodes ?? [])
                {
                    node.ExtensionState = node.ExtensionState?.DeepClone();
                    node.Ports ??= [];
                    foreach (var port in node.Ports)
                    {
                        port.Capacity = (port.Capacity ?? new PortCapacity()).Normalize();
                        port.IncomingDirection = PortGeometry.NormalizeDirection(port.IncomingDirection);
                        port.OutgoingDirection = PortGeometry.NormalizeDirection(port.OutgoingDirection);
                        port.Properties ??= [];
                    }
                }

                foreach (var edge in candidate.Edges ?? [])
                {
                    edge.Waypoints ??= [];
                    edge.Properties ??= [];
                }
            }

            if (candidate is null ||
                candidate.Format != GraphClipboardPayload.CurrentFormat ||
                candidate.Nodes is null ||
                candidate.Edges is null ||
                candidate.Nodes.Count > 1000 ||
                candidate.Edges.Count > 2000 ||
                candidate.Groups.Count > 500 ||
                candidate.Annotations.Count > 1000 ||
                (candidate.Nodes.Count == 0 &&
                 candidate.Edges.Count == 0 &&
                 candidate.Annotations.Count == 0))
            {
                return false;
            }

            payload = candidate;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(GraphClipboardPayload))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(List<object?>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
internal partial class GraphClipboardJsonContext : JsonSerializerContext;
