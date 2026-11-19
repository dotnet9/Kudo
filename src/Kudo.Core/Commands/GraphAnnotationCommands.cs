// 标注命令：负责标注增删、位置、尺寸、正文和扩展属性变更。
namespace Kudo;

public sealed class AddAnnotationCommand(
    AnnotationModel annotation,
    string name = "Add annotation") : GraphCommand(name)
{
    public AnnotationModel Annotation { get; } =
        annotation ?? throw new ArgumentNullException(nameof(annotation));

    public override void Execute(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FindAnnotation(Annotation.Id) is not null)
        {
            throw new InvalidOperationException(
                $"An annotation with ID '{Annotation.Id}' already exists.");
        }

        document.AddAnnotation(Annotation);
    }

    public override void Undo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.RemoveAnnotation(Annotation.Id))
        {
            throw new InvalidOperationException(
                $"The annotation with ID '{Annotation.Id}' no longer exists.");
        }
    }
}
public sealed class RemoveAnnotationCommand(
    string annotationId,
    string name = "Delete annotation") : GraphCommand(name)
{
    private AnnotationModel? _annotation;
    private int _index;

    public string AnnotationId { get; } = string.IsNullOrWhiteSpace(annotationId)
        ? throw new ArgumentException("An annotation ID is required.", nameof(annotationId))
        : annotationId;

    public AnnotationModel? RemovedAnnotation => _annotation;

    public override void Execute(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var index = document.Annotations.FindIndex(annotation => annotation.Id == AnnotationId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"The annotation with ID '{AnnotationId}' does not exist.");
        }

        _index = index;
        _annotation = document.Annotations[index];
        document.Annotations.RemoveAt(index);
    }

    public override void Undo(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_annotation is null)
        {
            throw new InvalidOperationException("The command has not been executed.");
        }

        if (document.FindAnnotation(AnnotationId) is not null)
        {
            throw new InvalidOperationException(
                $"An annotation with ID '{AnnotationId}' already exists.");
        }

        document.Annotations.Insert(Math.Min(_index, document.Annotations.Count), _annotation);
    }
}

public sealed class MoveAnnotationCommand(
    string annotationId,
    GraphPoint oldPosition,
    GraphPoint newPosition,
    string name = "Move annotation") : GraphCommand(name)
{
    public string AnnotationId { get; } = string.IsNullOrWhiteSpace(annotationId)
        ? throw new ArgumentException("An annotation ID is required.", nameof(annotationId))
        : annotationId;

    public GraphPoint OldPosition { get; } = oldPosition;

    public GraphPoint NewPosition { get; } = newPosition;

    public override void Execute(GraphDocument document) => SetPosition(document, NewPosition);

    public override void Undo(GraphDocument document) => SetPosition(document, OldPosition);

    private void SetPosition(GraphDocument document, GraphPoint position)
    {
        ArgumentNullException.ThrowIfNull(document);
        var annotation = document.FindAnnotation(AnnotationId) ??
            throw new InvalidOperationException(
                $"The annotation with ID '{AnnotationId}' does not exist.");
        annotation.Position = position;
    }
}

public sealed class ResizeAnnotationCommand(
    string annotationId,
    GraphPoint oldPosition,
    GraphSize oldSize,
    GraphPoint newPosition,
    GraphSize newSize,
    string name = "Resize annotation") : GraphCommand(name)
{
    public string AnnotationId { get; } = string.IsNullOrWhiteSpace(annotationId)
        ? throw new ArgumentException("An annotation ID is required.", nameof(annotationId))
        : annotationId;

    public GraphPoint OldPosition { get; } = oldPosition;

    public GraphSize OldSize { get; } = oldSize;

    public GraphPoint NewPosition { get; } = newPosition;

    public GraphSize NewSize { get; } = newSize;

    public override void Execute(GraphDocument document) => SetBounds(document, NewPosition, NewSize);

    public override void Undo(GraphDocument document) => SetBounds(document, OldPosition, OldSize);

    private void SetBounds(GraphDocument document, GraphPoint position, GraphSize size)
    {
        ArgumentNullException.ThrowIfNull(document);
        var annotation = document.FindAnnotation(AnnotationId) ??
            throw new InvalidOperationException(
                $"The annotation with ID '{AnnotationId}' does not exist.");
        annotation.Position = position;
        annotation.Size = size;
    }
}

public sealed class SetAnnotationTextCommand(
    string annotationId,
    string? oldText,
    string? newText,
    string name = "Edit annotation text") : GraphCommand(name)
{
    public string AnnotationId { get; } = string.IsNullOrWhiteSpace(annotationId)
        ? throw new ArgumentException("An annotation ID is required.", nameof(annotationId))
        : annotationId;

    public string? OldText { get; } = oldText;

    public string? NewText { get; } = newText;

    public override void Execute(GraphDocument document) => SetText(document, NewText);

    public override void Undo(GraphDocument document) => SetText(document, OldText);

    private void SetText(GraphDocument document, string? text)
    {
        ArgumentNullException.ThrowIfNull(document);
        var annotation = document.FindAnnotation(AnnotationId) ??
            throw new InvalidOperationException(
                $"The annotation with ID '{AnnotationId}' does not exist.");
        annotation.Text = text;
    }
}

public sealed class SetAnnotationPropertiesCommand : GraphCommand
{
    private readonly Dictionary<string, object?> _oldProperties;
    private readonly Dictionary<string, object?> _newProperties;

    public SetAnnotationPropertiesCommand(
        string annotationId,
        string? oldText,
        string? newText,
        GraphPoint oldPosition,
        GraphPoint newPosition,
        GraphSize oldSize,
        GraphSize newSize,
        IReadOnlyDictionary<string, object?> oldProperties,
        IReadOnlyDictionary<string, object?> newProperties,
        string name = "Edit annotation properties")
        : base(name)
    {
        AnnotationId = string.IsNullOrWhiteSpace(annotationId)
            ? throw new ArgumentException("An annotation ID is required.", nameof(annotationId))
            : annotationId;
        OldText = oldText;
        NewText = newText;
        OldPosition = oldPosition;
        NewPosition = newPosition;
        OldSize = oldSize;
        NewSize = newSize;
        _oldProperties = CopyProperties(oldProperties, nameof(oldProperties));
        _newProperties = CopyProperties(newProperties, nameof(newProperties));
    }

    public string AnnotationId { get; }

    public string? OldText { get; }

    public string? NewText { get; }

    public GraphPoint OldPosition { get; }

    public GraphPoint NewPosition { get; }

    public GraphSize OldSize { get; }

    public GraphSize NewSize { get; }

    public IReadOnlyDictionary<string, object?> OldProperties => _oldProperties;

    public IReadOnlyDictionary<string, object?> NewProperties => _newProperties;

    public override void Execute(GraphDocument document) =>
        Apply(document, NewText, NewPosition, NewSize, _newProperties);

    public override void Undo(GraphDocument document) =>
        Apply(document, OldText, OldPosition, OldSize, _oldProperties);

    private void Apply(
        GraphDocument document,
        string? text,
        GraphPoint position,
        GraphSize size,
        IReadOnlyDictionary<string, object?> properties)
    {
        ArgumentNullException.ThrowIfNull(document);
        var annotation = document.FindAnnotation(AnnotationId) ??
            throw new InvalidOperationException(
                $"The annotation with ID '{AnnotationId}' does not exist.");
        annotation.Text = text;
        annotation.Position = position;
        annotation.Size = size;
        annotation.Properties = CopyProperties(properties, nameof(properties));
    }

    private static Dictionary<string, object?> CopyProperties(
        IReadOnlyDictionary<string, object?> properties,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(properties, parameterName);
        return new Dictionary<string, object?>(properties, StringComparer.Ordinal);
    }
}
