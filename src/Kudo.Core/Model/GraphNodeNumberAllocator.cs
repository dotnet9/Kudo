using System.Globalization;

namespace Kudo;

public static class GraphNodeNumberAllocator
{
    public static string Next(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var next = document.Nodes
            .Select(node => ParsePositive(node.Number))
            .Where(number => number.HasValue)
            .Select(number => number!.Value)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return next.ToString(next <= 999 ? "D3" : "D", CultureInfo.InvariantCulture);
    }

    private static int? ParsePositive(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) &&
        number > 0
            ? number
            : null;
}
