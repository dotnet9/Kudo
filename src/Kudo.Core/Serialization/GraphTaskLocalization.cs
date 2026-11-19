using System.Globalization;

namespace Kudo.Core.Serialization;

/// <summary>
/// Provides host-registerable messages for task persistence without coupling Kudo.Core to a UI framework.
/// </summary>
public static class GraphTaskLocalization
{
    public const string InvalidXml = nameof(InvalidXml);
    public const string EmptyPayload = nameof(EmptyPayload);
    public const string UnsupportedFormat = nameof(UnsupportedFormat);
    public const string UnsupportedSchemaVersion = nameof(UnsupportedSchemaVersion);
    public const string ExtensionSchemaIdRequired = nameof(ExtensionSchemaIdRequired);
    public const string ExtensionSchemaVersionRequired = nameof(ExtensionSchemaVersionRequired);
    public const string ExtensionPayloadRequired = nameof(ExtensionPayloadRequired);
    public const string InvalidNodeAssetPath = nameof(InvalidNodeAssetPath);
    public const string PropertyNameRequired = nameof(PropertyNameRequired);
    public const string InvalidPropertyValue = nameof(InvalidPropertyValue);
    public const string UnsupportedPropertyType = nameof(UnsupportedPropertyType);
    public const string TargetTaskDirectoryRequired = nameof(TargetTaskDirectoryRequired);
    public const string SessionNotManaged = nameof(SessionNotManaged);
    public const string TaskDirectoryNotFound = nameof(TaskDirectoryNotFound);
    public const string TaskFileNotFound = nameof(TaskFileNotFound);
    public const string TaskManifestNotFound = nameof(TaskManifestNotFound);
    public const string ImageAssetNotFound = nameof(ImageAssetNotFound);
    public const string ImageFormatNotSupported = nameof(ImageFormatNotSupported);
    public const string AssetPathEscapesDirectory = nameof(AssetPathEscapesDirectory);
    public const string ReferencedAssetNotFound = nameof(ReferencedAssetNotFound);
    public const string InvalidAssetPath = nameof(InvalidAssetPath);
    public const string DraftIncomplete = nameof(DraftIncomplete);
    public const string DraftLoadFailed = nameof(DraftLoadFailed);

    private static Func<string, string>? _resolver;

    public static void Register(Func<string, string> resolver) =>
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    public static string Get(string key) => _resolver?.Invoke(key) ?? GetFallback(key);

    public static string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    private static string GetFallback(string key) => key switch
    {
        InvalidXml => "The task payload is not valid XML.",
        EmptyPayload => "The task payload is empty.",
        UnsupportedFormat => "Unsupported task format '{0}'.",
        UnsupportedSchemaVersion => "Unsupported schema version '{0}'.",
        ExtensionSchemaIdRequired => "Node '{0}' extension state requires a schema ID.",
        ExtensionSchemaVersionRequired => "Node '{0}' extension state requires a positive schema version.",
        ExtensionPayloadRequired => "Node '{0}' extension state requires a payload.",
        InvalidNodeAssetPath => "Node '{0}' uses invalid task asset path '{1}'.",
        PropertyNameRequired => "A task property is missing its name.",
        InvalidPropertyValue => "Task value '{0}' is invalid for type '{1}'.",
        UnsupportedPropertyType => "Task property type '{0}' is not supported.",
        TargetTaskDirectoryRequired => "A target task directory is required.",
        SessionNotManaged => "The session is not managed by this document manager.",
        TaskDirectoryNotFound => "Task directory '{0}' does not exist.",
        TaskFileNotFound => "The task directory does not contain Task.xml.",
        TaskManifestNotFound => "The task directory does not contain Manifest.xml.",
        ImageAssetNotFound => "The image asset does not exist.",
        ImageFormatNotSupported => "Image format '{0}' is not supported.",
        AssetPathEscapesDirectory => "An asset path escapes the task assets directory.",
        ReferencedAssetNotFound => "The referenced task asset '{0}' does not exist.",
        InvalidAssetPath => "Invalid task asset path '{0}'.",
        DraftIncomplete => "Draft '{0}' is empty or incomplete.",
        DraftLoadFailed => "Draft '{0}' could not be loaded: {1}",
        _ => key
    };
}
