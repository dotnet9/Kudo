// 命令基础设施：定义命令契约实现、组合执行和失败回滚语义。
namespace Kudo;

public abstract class GraphCommand(string name) : IGraphCommand
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("A command name is required.", nameof(name))
        : name;

    public abstract void Execute(GraphDocument document);

    public abstract void Undo(GraphDocument document);
}

public sealed class CompositeGraphCommand : GraphCommand
{
    public CompositeGraphCommand(string name, IEnumerable<IGraphCommand> commands) : base(name)
    {
        ArgumentNullException.ThrowIfNull(commands);
        Commands = commands.ToArray();
        if (Commands.Count == 0)
        {
            throw new ArgumentException("At least one child command is required.", nameof(commands));
        }
    }

    public IReadOnlyList<IGraphCommand> Commands { get; }

    public override void Execute(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var executed = new List<IGraphCommand>(Commands.Count);
        try
        {
            foreach (var command in Commands)
            {
                command.Execute(document);
                executed.Add(command);
            }
        }
        catch
        {
            for (var index = executed.Count - 1; index >= 0; index--)
            {
                executed[index].Undo(document);
            }

            throw;
        }
    }

    public override void Undo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var undone = new List<IGraphCommand>(Commands.Count);
        try
        {
            for (var index = Commands.Count - 1; index >= 0; index--)
            {
                Commands[index].Undo(document);
                undone.Add(Commands[index]);
            }
        }
        catch
        {
            for (var index = undone.Count - 1; index >= 0; index--)
            {
                undone[index].Execute(document);
            }

            throw;
        }
    }
}

public sealed class SetDocumentTitleCommand(
    string documentId,
    string? oldTitle,
    string? newTitle,
    string name = "Edit canvas properties") : GraphCommand(name)
{
    public string DocumentId { get; } = string.IsNullOrWhiteSpace(documentId)
        ? throw new ArgumentException("A document ID is required.", nameof(documentId))
        : documentId;

    public string? OldTitle { get; } = oldTitle;

    public string? NewTitle { get; } = newTitle;

    public override void Execute(GraphDocument document) => SetTitle(document, NewTitle);

    public override void Undo(GraphDocument document) => SetTitle(document, OldTitle);

    private void SetTitle(GraphDocument document, string? title)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Id != DocumentId)
        {
            throw new InvalidOperationException($"The document with ID '{DocumentId}' is not active.");
        }

        document.Title = title ?? string.Empty;
    }
}

public sealed class SetDocumentPropertiesCommand(
    string documentId,
    string oldTitle,
    string newTitle,
    string oldCreatedBy,
    string newCreatedBy,
    DateTime oldCreatedAt,
    DateTime newCreatedAt,
    string oldDescription,
    string newDescription,
    string name = "Edit canvas properties") : GraphCommand(name)
{
    public string DocumentId { get; } = string.IsNullOrWhiteSpace(documentId)
        ? throw new ArgumentException("A document ID is required.", nameof(documentId))
        : documentId;

    public string OldTitle { get; } = oldTitle ?? throw new ArgumentNullException(nameof(oldTitle));

    public string NewTitle { get; } = newTitle ?? throw new ArgumentNullException(nameof(newTitle));

    public string OldCreatedBy { get; } = oldCreatedBy ?? throw new ArgumentNullException(nameof(oldCreatedBy));

    public string NewCreatedBy { get; } = newCreatedBy ?? throw new ArgumentNullException(nameof(newCreatedBy));

    public DateTime OldCreatedAt { get; } = oldCreatedAt;

    public DateTime NewCreatedAt { get; } = newCreatedAt;

    public string OldDescription { get; } = oldDescription ?? throw new ArgumentNullException(nameof(oldDescription));

    public string NewDescription { get; } = newDescription ?? throw new ArgumentNullException(nameof(newDescription));

    public override void Execute(GraphDocument document) =>
        Apply(document, NewTitle, NewCreatedBy, NewCreatedAt, NewDescription);

    public override void Undo(GraphDocument document) =>
        Apply(document, OldTitle, OldCreatedBy, OldCreatedAt, OldDescription);

    private void Apply(
        GraphDocument document,
        string title,
        string createdBy,
        DateTime createdAt,
        string description)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Id != DocumentId)
        {
            throw new InvalidOperationException($"The document with ID '{DocumentId}' is not active.");
        }

        document.Title = title ?? string.Empty;
        document.CreatedBy = createdBy;
        document.CreatedAt = createdAt;
        document.Description = description;
    }
}

public sealed class SetCanvasSettingsCommand(
    string documentId,
    GraphCanvasSettings oldSettings,
    GraphCanvasSettings newSettings,
    string name = "Edit canvas settings") : GraphCommand(name)
{
    public string DocumentId { get; } = string.IsNullOrWhiteSpace(documentId)
        ? throw new ArgumentException("A document ID is required.", nameof(documentId))
        : documentId;

    public GraphCanvasSettings OldSettings { get; } =
        (oldSettings ?? throw new ArgumentNullException(nameof(oldSettings))).Normalize();

    public GraphCanvasSettings NewSettings { get; } =
        (newSettings ?? throw new ArgumentNullException(nameof(newSettings))).Normalize();

    public override void Execute(GraphDocument document) => Apply(document, NewSettings);

    public override void Undo(GraphDocument document) => Apply(document, OldSettings);

    private void Apply(GraphDocument document, GraphCanvasSettings settings)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Id != DocumentId)
        {
            throw new InvalidOperationException($"The document with ID '{DocumentId}' is not active.");
        }

        document.CanvasSettings = settings;
    }
}
