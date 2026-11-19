using Kudo.Core.Documents;
using Kudo.Avalonia;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

public sealed record DemoSampleTaskDefinition(
    string Id,
    string Title,
    string TaskDirectory,
    int Order);

public sealed class DemoSampleTaskLibrary
{
    public const string DefaultStartupTaskId = "order-risk-control";

    public const string SampleTaskIdProperty = "KudoDemoSampleTaskId";

    public const string SampleOrderProperty = "KudoDemoSampleOrder";

    private static readonly StringComparer TaskIdComparer = StringComparer.Ordinal;

    private readonly IReadOnlyDictionary<string, DemoSampleTaskDefinition> _tasksById;

    public DemoSampleTaskLibrary(string? rootDirectory = null)
    {
        RootDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "Samples")
                : rootDirectory);
        Tasks = DiscoverTasks();
        _tasksById = Tasks.ToDictionary(task => task.Id, TaskIdComparer);
    }

    public string RootDirectory { get; }

    public IReadOnlyList<DemoSampleTaskDefinition> Tasks { get; }

    public string GetTaskDirectory(string taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        if (_tasksById.TryGetValue(taskId, out var task))
        {
            return task.TaskDirectory;
        }

        throw new ArgumentOutOfRangeException(
            nameof(taskId),
            taskId,
            GraphLocalization.Get(MainWindowLanguage.UnknownSampleTask));
    }

    private IReadOnlyList<DemoSampleTaskDefinition> DiscoverTasks()
    {
        if (!Directory.Exists(RootDirectory))
        {
            return [];
        }

        var tasks = Directory
            .EnumerateDirectories(RootDirectory)
            .Select(TryCreateDefinition)
            .OfType<DemoSampleTaskDefinition>()
            .OrderBy(task => task.Order)
            .ThenBy(task => task.Title, StringComparer.CurrentCulture)
            .ToArray();
        var duplicateId = tasks
            .GroupBy(task => task.Id, TaskIdComparer)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicateId is not null)
        {
            throw new InvalidOperationException(
                GraphLocalization.Format(MainWindowLanguage.DuplicateSampleTaskId, duplicateId));
        }

        return tasks;
    }

    private DemoSampleTaskDefinition? TryCreateDefinition(string directory)
    {
        var taskPath = Path.Combine(directory, GraphDocumentManager.TaskFileName);
        if (!File.Exists(taskPath))
        {
            return null;
        }

        var metadata = DemoSampleTaskMetadataReader.Read(taskPath);
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var id = metadata.SampleTaskId;
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Path.GetFileName(normalizedDirectory);
        }

        var title = string.IsNullOrWhiteSpace(metadata.Title)
            ? Path.GetFileName(normalizedDirectory)
            : metadata.Title;
        return new DemoSampleTaskDefinition(
            id,
            title,
            normalizedDirectory,
            metadata.SampleOrder);
    }

}
