namespace Kudo;

/// <summary>
/// Formats the stable, user-assigned identity of a node for history and other user-facing
/// descriptions. The format deliberately contains no localized text.
/// </summary>
public static class GraphHistoryIdentity
{
    public static string FormatNode(NodeModel? node, string fallback = "")
    {
        if (node is null)
        {
            return fallback;
        }

        var parts = new[]
        {
            node.Number,
            node.Name,
            node.DefinitionId
        };
        var identity = string.Join(
            "-",
            parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
        return string.IsNullOrWhiteSpace(identity)
            ? fallback
            : identity;
    }
}
