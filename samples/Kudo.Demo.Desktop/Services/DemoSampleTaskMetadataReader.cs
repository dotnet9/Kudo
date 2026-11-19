using Kudo.Core.Serialization;

namespace Kudo.Demo.Desktop.Services;

internal sealed record DemoSampleTaskMetadata(
    string? Title,
    string? SampleTaskId,
    int SampleOrder);

internal static class DemoSampleTaskMetadataReader
{
    public static DemoSampleTaskMetadata Read(string taskPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskPath);
        var document = new GraphTaskXmlSerializer().Deserialize(File.ReadAllText(taskPath));
        return new DemoSampleTaskMetadata(
            document.Title,
            ReadString(document.Properties, DemoSampleTaskLibrary.SampleTaskIdProperty),
            ReadInt32(document.Properties, DemoSampleTaskLibrary.SampleOrderProperty, int.MaxValue));
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, object?> properties,
        string key) =>
        properties.TryGetValue(key, out var value) ? value as string : null;

    private static int ReadInt32(
        IReadOnlyDictionary<string, object?> properties,
        string key,
        int defaultValue)
    {
        if (!properties.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return value switch
        {
            int integer => integer,
            long integer when integer is >= int.MinValue and <= int.MaxValue => (int)integer,
            double number when number is >= int.MinValue and <= int.MaxValue => (int)number,
            decimal number when number is >= int.MinValue and <= int.MaxValue => (int)number,
            string text when int.TryParse(text, out var integer) => integer,
            _ => defaultValue
        };
    }
}
