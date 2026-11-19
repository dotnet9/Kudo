using System.Collections;
using System.Globalization;
using System.Text.Json;
using Kudo;
using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using CodeWF.Toolkit.Lang;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class DemoGraphHistoryDescriptionProvider : IGraphHistoryDescriptionProvider
{
    public GraphHistoryDescription? Describe(GraphHistoryDescriptionContext context)
    {
        return context.Command switch
        {
            SetNodeExtensionPropertiesCommand command => DescribeProperties(context, command),
            ReconfigureNodeCommand command => DescribeExtensionState(context, command),
            _ => null
        };
    }

    private static GraphHistoryDescription DescribeProperties(
        GraphHistoryDescriptionContext context,
        SetNodeExtensionPropertiesCommand command)
    {
        var node = context.Document.FindNode(command.NodeId);
        var changes = DescribeDictionary(
            command.OldProperties,
            command.NewProperties,
            "node.property.");
        return new(
            GraphHistoryOperationKeys.EditNode,
            CreateSubject(node, command.NodeId),
            changes,
            suppressLog: changes.Count == 0);
    }

    private static GraphHistoryDescription DescribeExtensionState(
        GraphHistoryDescriptionContext context,
        ReconfigureNodeCommand command)
    {
        var node = context.Document.FindNode(command.NodeId);
        var oldState = command.OldState;
        var newState = command.NewState;
        var changes = DescribeJson(
            oldState?.Payload,
            newState.Payload,
            "extension.");
        if (!string.Equals(oldState?.SchemaId, newState.SchemaId, StringComparison.Ordinal))
        {
            changes.Insert(
                0,
                new GraphHistoryPropertyChange(
                    "extension.schema",
                    oldState?.SchemaId,
                    newState.SchemaId));
        }

        return new(
            GraphHistoryOperationKeys.EditNode,
            CreateSubject(node, command.NodeId),
            changes,
            suppressLog: changes.Count == 0);
    }

    private static List<GraphHistoryPropertyChange> DescribeDictionary(
        IReadOnlyDictionary<string, object?> oldValues,
        IReadOnlyDictionary<string, object?> newValues,
        string prefix)
    {
        var changes = new List<GraphHistoryPropertyChange>();
        foreach (var key in oldValues.Keys.Concat(newValues.Keys).Distinct(StringComparer.Ordinal))
        {
            oldValues.TryGetValue(key, out var oldValue);
            newValues.TryGetValue(key, out var newValue);
            if (ValuesEqual(oldValue, newValue))
            {
                continue;
            }

            changes.Add(new GraphHistoryPropertyChange(prefix + key, oldValue, newValue));
        }

        return changes;
    }

    private static List<GraphHistoryPropertyChange> DescribeJson(
        JsonElement? oldValue,
        JsonElement newValue,
        string prefix)
    {
        var changes = new List<GraphHistoryPropertyChange>();
        DescribeJsonValue(
            oldValue,
            newValue,
            prefix.TrimEnd('.'),
            changes);
        return changes;
    }

    private static void DescribeJsonValue(
        JsonElement? oldValue,
        JsonElement? newValue,
        string path,
        ICollection<GraphHistoryPropertyChange> changes)
    {
        if (oldValue is { } oldElement && newValue is { } newElement &&
            oldElement.ValueKind == JsonValueKind.Object &&
            newElement.ValueKind == JsonValueKind.Object)
        {
            var names = oldElement.EnumerateObject().Select(property => property.Name)
                .Concat(newElement.EnumerateObject().Select(property => property.Name))
                .Distinct(StringComparer.Ordinal);
            foreach (var name in names)
            {
                var oldProperty = oldElement.TryGetProperty(name, out var oldChild)
                    ? oldChild
                    : (JsonElement?)null;
                var newProperty = newElement.TryGetProperty(name, out var newChild)
                    ? newChild
                    : (JsonElement?)null;
                DescribeJsonValue(oldProperty, newProperty, path + "." + name, changes);
            }

            return;
        }

        if (oldValue is { } oldArray && newValue is { } newArray &&
            oldArray.ValueKind == JsonValueKind.Array &&
            newArray.ValueKind == JsonValueKind.Array)
        {
            var oldItems = oldArray.EnumerateArray().ToArray();
            var newItems = newArray.EnumerateArray().ToArray();
            for (var index = 0; index < Math.Max(oldItems.Length, newItems.Length); index++)
            {
                var oldChild = index < oldItems.Length ? oldItems[index] : (JsonElement?)null;
                var newChild = index < newItems.Length ? newItems[index] : (JsonElement?)null;
                DescribeJsonValue(oldChild, newChild, $"{path}.{index}", changes);
            }

            return;
        }

        if (JsonValuesEqual(oldValue, newValue))
        {
            return;
        }

        changes.Add(new GraphHistoryPropertyChange(path, oldValue, newValue));
    }

    private static GraphHistorySubject CreateSubject(NodeModel? node, string nodeId) =>
        node is null
            ? new("node", nodeId)
            : new("node", node.Id, node.Type, GraphHistoryIdentity.FormatNode(node));

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left is JsonElement leftJson && right is JsonElement rightJson)
        {
            return JsonValuesEqual(leftJson, rightJson);
        }

        return Equals(left, right);
    }

    private static bool JsonValuesEqual(JsonElement? left, JsonElement? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Value.GetRawText() == right.Value.GetRawText();
    }
}

internal sealed class GraphHistoryLogFormatter
{
    private readonly GraphHistoryDescriptionRegistry _descriptions;
    private readonly Func<IGraphNodeCatalog?> _catalogProvider;

    public GraphHistoryLogFormatter(
        GraphHistoryDescriptionRegistry descriptions,
        Func<IGraphNodeCatalog?> catalogProvider)
    {
        _descriptions = descriptions ?? throw new ArgumentNullException(nameof(descriptions));
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
    }

    public string? Format(
        GraphDocument document,
        GraphHistoryOperationChangedEventArgs operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Direction is not (GraphDocumentEditDirection.Execute or
            GraphDocumentEditDirection.Undo or
            GraphDocumentEditDirection.Redo) ||
            !operation.OperationContext.IsUserInitiated)
        {
            return null;
        }

        var context = new GraphHistoryDescriptionContext(
            document,
            operation.Command,
            operation.Direction,
            operation.Changes,
            I18nManager.Instance.Culture?.Name ?? CultureInfo.CurrentUICulture.Name,
            operation.OperationContext);
        if (!_descriptions.TryDescribe(context, out var description) ||
            description is null ||
            description.SuppressLog)
        {
            return null;
        }

        var subject = ResolveSubject(document, description.Subject);
        var operationText = TranslateOperation(
            description.OperationKey,
            subject,
            operation.Direction);
        var isExecute = operation.Direction == GraphDocumentEditDirection.Execute;
        if (description.Changes.Count == 0)
        {
            return isExecute
                ? operationText
                : FormatOperation(operation.Direction, operationText);
        }

        if (description.Changes.Count == 1)
        {
            var change = description.Changes[0];
            var fromValue = operation.Direction == GraphDocumentEditDirection.Undo
                ? change.NewValue
                : change.OldValue;
            var toValue = operation.Direction == GraphDocumentEditDirection.Undo
                ? change.OldValue
                : change.NewValue;
            return FormatProperty(
                operation.Direction,
                subject,
                ResolvePropertyLabel(description.Subject, change),
                FormatValue(fromValue, change.Unit),
                FormatValue(toValue, change.Unit));
        }

        var details = string.Join(
            L(MainWindowLanguage.HistoryValueSeparator),
            description.Changes.Select(change =>
                F(
                    isExecute
                        ? MainWindowLanguage.HistoryExecuteValue
                        : operation.Direction == GraphDocumentEditDirection.Undo
                            ? MainWindowLanguage.HistoryUndoValue
                            : MainWindowLanguage.HistoryRedoValue,
                    ResolvePropertyLabel(description.Subject, change),
                    FormatValue(
                        operation.Direction == GraphDocumentEditDirection.Undo
                            ? change.NewValue
                            : change.OldValue,
                        change.Unit),
                    FormatValue(
                        operation.Direction == GraphDocumentEditDirection.Undo
                            ? change.OldValue
                            : change.NewValue,
                        change.Unit))));
        return F(
            isExecute
                ? MainWindowLanguage.HistoryExecuteChanges
                : operation.Direction == GraphDocumentEditDirection.Undo
                    ? MainWindowLanguage.HistoryUndoChanges
                    : MainWindowLanguage.HistoryRedoChanges,
            subject,
            details);
    }

    private string TranslateOperation(
        string operationKey,
        string subject,
        GraphDocumentEditDirection direction) =>
        operationKey switch
        {
            GraphHistoryOperationKeys.GraphEdit => F(MainWindowLanguage.HistoryGraphEdit, subject),
            GraphHistoryOperationKeys.BatchEdit => L(MainWindowLanguage.HistoryBatchEdit),
            GraphHistoryOperationKeys.AddNode => F(
                direction == GraphDocumentEditDirection.Execute
                    ? MainWindowLanguage.OperationAddNode
                    : MainWindowLanguage.HistoryAddNode,
                subject),
            GraphHistoryOperationKeys.DropNode => F(
                direction == GraphDocumentEditDirection.Execute
                    ? MainWindowLanguage.OperationDropNode
                    : MainWindowLanguage.HistoryAddNode,
                subject),
            GraphHistoryOperationKeys.Paste => L(MainWindowLanguage.OperationPaste),
            GraphHistoryOperationKeys.Duplicate => L(MainWindowLanguage.OperationDuplicate),
            GraphHistoryOperationKeys.DeleteNode => F(MainWindowLanguage.HistoryDeleteNode, subject),
            GraphHistoryOperationKeys.MoveNode => F(MainWindowLanguage.HistoryMoveNode, subject),
            GraphHistoryOperationKeys.ResizeNode => F(MainWindowLanguage.HistoryResizeNode, subject),
            GraphHistoryOperationKeys.LockNode => F(MainWindowLanguage.HistoryLockNode, subject),
            GraphHistoryOperationKeys.ArrangeNodes => L(MainWindowLanguage.HistoryArrangeNodes),
            GraphHistoryOperationKeys.EditNode => F(MainWindowLanguage.HistoryEditNode, subject),
            GraphHistoryOperationKeys.EditNodeParameters => F(MainWindowLanguage.HistoryEditNodeParameters, subject),
            GraphHistoryOperationKeys.EditNodeAppearance => F(MainWindowLanguage.HistoryEditNodeAppearance, subject),
            GraphHistoryOperationKeys.AddConnection => F(MainWindowLanguage.HistoryAddConnection, subject),
            GraphHistoryOperationKeys.DeleteConnection => F(MainWindowLanguage.HistoryDeleteConnection, subject),
            GraphHistoryOperationKeys.EditConnection => L(MainWindowLanguage.HistoryEditConnection),
            GraphHistoryOperationKeys.ReconnectConnection => F(MainWindowLanguage.HistoryReconnectConnection, subject),
            GraphHistoryOperationKeys.AddGroup => L(MainWindowLanguage.HistoryAddGroup),
            GraphHistoryOperationKeys.DeleteGroup => L(MainWindowLanguage.HistoryDeleteGroup),
            GraphHistoryOperationKeys.EditGroup => L(MainWindowLanguage.HistoryEditGroup),
            GraphHistoryOperationKeys.AddAnnotation => L(MainWindowLanguage.HistoryAddAnnotation),
            GraphHistoryOperationKeys.DeleteAnnotation => L(MainWindowLanguage.HistoryDeleteAnnotation),
            GraphHistoryOperationKeys.MoveAnnotation => L(MainWindowLanguage.HistoryMoveAnnotation),
            GraphHistoryOperationKeys.ResizeAnnotation => L(MainWindowLanguage.HistoryResizeAnnotation),
            GraphHistoryOperationKeys.EditAnnotation => L(MainWindowLanguage.HistoryEditAnnotation),
            GraphHistoryOperationKeys.EditDocument => L(MainWindowLanguage.HistoryEditDocument),
            GraphHistoryOperationKeys.EditCanvas => L(MainWindowLanguage.HistoryEditCanvas),
            _ => F(MainWindowLanguage.HistoryGraphEdit, subject)
        };

    private string ResolveSubject(GraphDocument document, GraphHistorySubject subject)
    {
        if (subject.Kind == "connection" && subject.Metadata is not null)
        {
            var source = ResolveEndpoint(document, subject.Metadata, "source");
            var target = ResolveEndpoint(document, subject.Metadata, "target");
            if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(target))
            {
                return F(MainWindowLanguage.HistoryConnectionRoute, source, target);
            }
        }

        if (!string.IsNullOrWhiteSpace(subject.DisplayName))
        {
            return subject.DisplayName;
        }

        return subject.Kind switch
        {
            "node" => L(MainWindowLanguage.HistoryNode),
            "edge" => L(MainWindowLanguage.HistoryConnection),
            "group" => L(MainWindowLanguage.HistoryGroup),
            "annotation" => L(MainWindowLanguage.HistoryAnnotation),
            "document" => L(MainWindowLanguage.HistoryDocument),
            "canvas" => L(MainWindowLanguage.HistoryCanvas),
            _ => L(MainWindowLanguage.HistoryGraphElement)
        };
    }

    private string ResolveEndpoint(
        GraphDocument document,
        IReadOnlyDictionary<string, string?> metadata,
        string prefix)
    {
        metadata.TryGetValue(prefix + "-node-id", out var nodeId);
        metadata.TryGetValue(prefix + "-port-id", out var portId);
        var node = string.IsNullOrWhiteSpace(nodeId) ? null : document.FindNode(nodeId);
        var port = node?.Ports.FirstOrDefault(candidate => candidate.Id == portId);
        var nodeName = GraphHistoryIdentity.FormatNode(
            node,
            node?.DefinitionId ?? node?.Type ?? nodeId ?? L(MainWindowLanguage.HistoryGraphElement));
        var definition = node is null
            ? null
            : _catalogProvider()?.FindById(node.DefinitionId) ??
              _catalogProvider()?.FindByNodeType(node.Type);
        var portName = definition?.DefaultPorts.FirstOrDefault(candidate => candidate.Id == portId)?.Name ??
                       port?.Name;
        if (string.IsNullOrWhiteSpace(portName))
        {
            portName = portId ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(portName)
            ? nodeName
            : F(MainWindowLanguage.HistoryConnectionEndpoint, nodeName, portName);
    }

    private string ResolvePropertyLabel(
        GraphHistorySubject subject,
        GraphHistoryPropertyChange change)
    {
        var key = change.Key;
        var builtInLabel = key switch
        {
            GraphHistoryPropertyKeys.NodeNumber => L(MainWindowLanguage.HistoryNodeNumber),
            GraphHistoryPropertyKeys.NodeName => L(MainWindowLanguage.HistoryNodeName),
            GraphHistoryPropertyKeys.NodeDescription => L(MainWindowLanguage.HistoryNodeDescription),
            GraphHistoryPropertyKeys.NodePosition => L(MainWindowLanguage.HistoryNodePosition),
            GraphHistoryPropertyKeys.NodeRotation => L(MainWindowLanguage.HistoryNodeRotation),
            GraphHistoryPropertyKeys.DocumentTitle => L(MainWindowLanguage.HistoryDocumentTitle),
            GraphHistoryPropertyKeys.DocumentCreatedBy => L(MainWindowLanguage.HistoryDocumentCreatedBy),
            GraphHistoryPropertyKeys.DocumentCreatedAt => L(MainWindowLanguage.HistoryDocumentCreatedAt),
            GraphHistoryPropertyKeys.DocumentDescription => L(MainWindowLanguage.HistoryDocumentDescription),
            _ => null
        };
        if (builtInLabel is not null)
        {
            return builtInLabel;
        }

        const string nodePropertyPrefix = "node.property.";
        if (key.StartsWith(nodePropertyPrefix, StringComparison.Ordinal))
        {
            var propertyName = key[nodePropertyPrefix.Length..];
            var definition = _catalogProvider()?.FindByNodeType(subject.Type ?? string.Empty);
            var property = definition?.PropertyDefinitions.FirstOrDefault(item =>
                string.Equals(item.Name, propertyName, StringComparison.Ordinal));
            if (property is not null)
            {
                return property.DisplayName;
            }

            return propertyName;
        }

        if (key.StartsWith("extension.", StringComparison.Ordinal))
        {
            return key["extension.".Length..];
        }

        return key;
    }

    private string FormatValue(object? value, string? unit)
    {
        var text = value switch
        {
            null => L(MainWindowLanguage.HistoryEmptyValue),
            JsonElement json => FormatJsonValue(json),
            DateTime dateTime => dateTime.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
        return string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(text)
            ? text
            : text + unit;
    }

    private string FormatJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => L(MainWindowLanguage.HistoryEmptyValue),
        JsonValueKind.String => value.GetString() ?? L(MainWindowLanguage.HistoryEmptyValue),
        JsonValueKind.True => L(MainWindowLanguage.HistoryBooleanTrue),
        JsonValueKind.False => L(MainWindowLanguage.HistoryBooleanFalse),
        _ => value.GetRawText()
    };

    private static string FormatOperation(GraphDocumentEditDirection direction, string operation) =>
        F(
            direction == GraphDocumentEditDirection.Undo
                ? MainWindowLanguage.HistoryUndoOperation
                : MainWindowLanguage.HistoryRedoOperation,
            operation);

    private static string FormatProperty(
        GraphDocumentEditDirection direction,
        string operation,
        string property,
        string oldValue,
        string newValue) =>
        F(
            direction == GraphDocumentEditDirection.Execute
                ? MainWindowLanguage.HistoryExecuteProperty
                : direction == GraphDocumentEditDirection.Undo
                    ? MainWindowLanguage.HistoryUndoProperty
                    : MainWindowLanguage.HistoryRedoProperty,
            operation,
            property,
            oldValue,
            newValue);

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
