using System.Text.Json;

namespace Kudo;

/// <summary>
/// 保存由宿主扩展拥有的版本化节点状态。Kudo 只负责持久化，不解释 Payload。
/// </summary>
public sealed record GraphExtensionState
{
    public required string SchemaId { get; init; }

    public required int SchemaVersion { get; init; }

    public required JsonElement Payload { get; init; }

    public GraphExtensionState DeepClone() => this with { Payload = Payload.Clone() };
}
