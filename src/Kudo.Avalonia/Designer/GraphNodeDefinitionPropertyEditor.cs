using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Kudo;
using Kudo.Avalonia;
using static Kudo.Avalonia.Designer.GraphPropertyEditorControls;
using PropertiesLanguage = global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView;

namespace Kudo.Avalonia.Designer;

internal static class GraphNodeDefinitionPropertyEditor
{
    private const decimal MinimumNumber = -1000000000m;
    private const decimal MaximumNumber = 1000000000m;

    public static void Show(
        StackPanel content,
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        Action<string> notifyEditCommitted)
    {
        var definition = editorContext.ResolveNodeDefinition(node);
        if (definition?.PropertyDefinitions.Count > 0 != true)
        {
            return;
        }

        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.SymbolProperties)));
        string? currentGroupName = null;
        foreach (var property in definition.PropertyDefinitions)
        {
            if (!string.IsNullOrWhiteSpace(property.GroupName) &&
                !string.Equals(currentGroupName, property.GroupName, StringComparison.Ordinal))
            {
                currentGroupName = property.GroupName;
                content.Children.Add(CreateSectionTitle(property.GroupName));
            }

            var editor = CreateEditor(
                editorContext,
                node,
                property,
                notifyEditCommitted);
            content.Children.Add(CreateField(FormatLabel(property), editor));
        }
    }

    private static Control CreateEditor(
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        GraphNodePropertyDefinition property,
        Action<string> notifyEditCommitted) => property.ValueKind switch
        {
            GraphNodePropertyValueKind.Boolean => CreateBooleanEditor(
                editorContext,
                node,
                property,
                notifyEditCommitted),
            GraphNodePropertyValueKind.Choice => CreateChoiceEditor(
                editorContext,
                node,
                property,
                notifyEditCommitted),
            GraphNodePropertyValueKind.Integer => CreateNumericPropertyEditor(
                editorContext,
                node,
                property,
                notifyEditCommitted,
                fallbackIncrement: 1,
                formatString: "0",
                roundValue: true),
            GraphNodePropertyValueKind.Number => CreateNumericPropertyEditor(
                editorContext,
                node,
                property,
                notifyEditCommitted,
                fallbackIncrement: 0.1m,
                formatString: "0.###",
                roundValue: false),
            GraphNodePropertyValueKind.Percent => CreatePercentEditor(
                editorContext,
                node,
                property,
                notifyEditCommitted),
            GraphNodePropertyValueKind.Color => CreateColorEditor(
                editorContext,
                node,
                property,
                notifyEditCommitted),
            _ => CreateStringEditor(
                editorContext,
                node,
                property,
                notifyEditCommitted)
        };

    private static TextBox CreateStringEditor(
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        GraphNodePropertyDefinition property,
        Action<string> notifyEditCommitted)
    {
        var editor = CreateTextEditor(
            CreateEditorName(property),
            property.DisplayName,
            FormatStringValue(GetPropertyValue(node, property)));
        SetEditorReadOnly(editor, editorContext.IsReadOnly || property.IsReadOnly);
        AttachTextCommit(editor, () => ApplyProperty(
            editorContext,
            node.Id,
            property,
            editor.Text ?? string.Empty,
            notifyEditCommitted));
        return editor;
    }

    private static GraphColorEditor CreateColorEditor(
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        GraphNodePropertyDefinition property,
        Action<string> notifyEditCommitted)
    {
        var editor = GraphPropertyEditorControls.CreateColorEditor(
            CreateEditorName(property),
            FormatStringValue(GetPropertyValue(node, property)),
            L(PropertiesLanguage.ClearColor));
        editor.IsEnabled = !editorContext.IsReadOnly && !property.IsReadOnly;
        AttachColorCommit(editor, () => ApplyProperty(
            editorContext,
            node.Id,
            property,
            editor.Value,
            notifyEditCommitted));
        return editor;
    }

    private static NumericUpDown CreateNumericPropertyEditor(
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        GraphNodePropertyDefinition property,
        Action<string> notifyEditCommitted,
        decimal fallbackIncrement,
        string formatString,
        bool roundValue)
    {
        var minimum = property.Minimum ?? MinimumNumber;
        var maximum = property.Maximum ?? MaximumNumber;
        var increment = property.Step is > 0 ? property.Step.Value : fallbackIncrement;
        var editor = CreateNumericEditor(
            CreateEditorName(property),
            Math.Clamp(ToDecimal(GetPropertyValue(node, property)), minimum, maximum),
            minimum,
            maximum,
            increment,
            formatString);
        editor.IsEnabled = !editorContext.IsReadOnly && !property.IsReadOnly;
        AttachNumericCommit(editor, () =>
        {
            var value = Math.Clamp(editor.Value ?? 0, minimum, maximum);
            ApplyProperty(
                editorContext,
                node.Id,
                property,
                roundValue ? decimal.ToInt32(Math.Round(value)) : decimal.ToDouble(value),
                notifyEditCommitted);
        });
        return editor;
    }

    private static GraphPercentageEditor CreatePercentEditor(
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        GraphNodePropertyDefinition property,
        Action<string> notifyEditCommitted)
    {
        var editor = CreatePercentageSliderEditor(
            CreateEditorName(property),
            decimal.ToDouble(ToDecimal(GetPropertyValue(node, property))),
            decimal.ToDouble(property.Minimum ?? 0),
            decimal.ToDouble(property.Maximum ?? 100),
            decimal.ToDouble(property.Step is > 0 ? property.Step.Value : 1));
        editor.Slider.IsEnabled = !editorContext.IsReadOnly && !property.IsReadOnly;
        Dictionary<string, object?>? editStartProperties = null;
        editor.AttachEdit(
            () =>
            {
                var current = editorContext.Session.Document.FindNode(node.Id);
                editStartProperties = current is null
                    ? null
                    : GraphNodeDefinitionValue.CloneDictionary(current.Properties);
            },
            () => PreviewProperty(
                editorContext,
                node.Id,
                property,
                Math.Round(editor.Value, 2)),
            () =>
            {
                CommitPropertyPreview(
                    editorContext,
                    node.Id,
                    editStartProperties,
                    notifyEditCommitted);
                editStartProperties = null;
            });
        return editor;
    }

    private static CheckBox CreateBooleanEditor(
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        GraphNodePropertyDefinition property,
        Action<string> notifyEditCommitted)
    {
        var editor = new CheckBox
        {
            Name = CreateEditorName(property),
            IsChecked = ToBoolean(GetPropertyValue(node, property))
        };
        editor.IsEnabled = !editorContext.IsReadOnly && !property.IsReadOnly;
        editor.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(CheckBox.IsChecked))
            {
                return;
            }

            ApplyProperty(
                editorContext,
                node.Id,
                property,
                editor.IsChecked == true,
                notifyEditCommitted);
        };
        return editor;
    }

    private static Control CreateChoiceEditor(
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        GraphNodePropertyDefinition property,
        Action<string> notifyEditCommitted)
    {
        var value = FormatStringValue(GetPropertyValue(node, property));
        var options = CreateChoiceOptions(property);
        if (!string.IsNullOrWhiteSpace(value) &&
            !options.Any(option => string.Equals(option.Value, value, StringComparison.Ordinal)))
        {
            options.Insert(0, new GraphNodePropertyOption(value, value));
        }

        if (options.Count == 0)
        {
            return CreateStringEditor(
                editorContext,
                node,
                property,
                notifyEditCommitted);
        }

        return property.Editor == GraphNodePropertyEditorKind.RadioButton
            ? CreateRadioButtonEditor(
                editorContext,
                node,
                property,
                value,
                options,
                notifyEditCommitted)
            : CreateComboBoxEditor(
                editorContext,
                node,
                property,
                value,
                options,
                notifyEditCommitted);
    }

    private static ComboBox CreateComboBoxEditor(
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        GraphNodePropertyDefinition property,
        string value,
        IReadOnlyList<GraphNodePropertyOption> options,
        Action<string> notifyEditCommitted)
    {

        var items = options
            .Select(option => new ComboBoxItem
            {
                Content = option.DisplayName,
                Tag = option.Value
            })
            .ToArray();
        var editor = new ComboBox
        {
            Name = CreateEditorName(property),
            ItemsSource = items,
            SelectedItem = items.FirstOrDefault(item => string.Equals(
                item.Tag as string,
                value,
                StringComparison.Ordinal)) ?? items[0]
        };
        editor.IsEnabled = !editorContext.IsReadOnly && !property.IsReadOnly;
        AttachSelectionCommit(editor, () =>
        {
            if (editor.SelectedItem is not ComboBoxItem { Tag: string selected })
            {
                return;
            }

            ApplyProperty(
                editorContext,
                node.Id,
                property,
                selected,
                notifyEditCommitted);
        });
        return editor;
    }

    private static StackPanel CreateRadioButtonEditor(
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        GraphNodePropertyDefinition property,
        string value,
        IReadOnlyList<GraphNodePropertyOption> options,
        Action<string> notifyEditCommitted)
    {
        var editor = new StackPanel
        {
            Name = CreateEditorName(property),
            Spacing = 4
        };
        foreach (var option in options)
        {
            var radio = new RadioButton
            {
                Content = option.DisplayName,
                GroupName = $"{node.Id}-{property.Name}",
                IsChecked = string.Equals(option.Value, value, StringComparison.Ordinal),
                IsEnabled = !editorContext.IsReadOnly && !property.IsReadOnly,
                Tag = option.Value
            };
            radio.PropertyChanged += (_, change) =>
            {
                if (change.Property == RadioButton.IsCheckedProperty &&
                    radio.IsChecked == true &&
                    radio.Tag is string selected)
                {
                    ApplyProperty(
                        editorContext,
                        node.Id,
                        property,
                        selected,
                        notifyEditCommitted);
                }
            };
            editor.Children.Add(radio);
        }

        return editor;
    }

    private static List<GraphNodePropertyOption> CreateChoiceOptions(
        GraphNodePropertyDefinition property)
    {
        var configured = property.ChoiceOptions.Count > 0
            ? property.ChoiceOptions
            : property.Options
                .Select(option => new GraphNodePropertyOption(option, option))
                .ToArray();
        return configured
            .Where(option => !string.IsNullOrWhiteSpace(option.Value))
            .GroupBy(option => option.Value, StringComparer.Ordinal)
            .Select(group => group.First() with
            {
                DisplayName = string.IsNullOrWhiteSpace(group.First().DisplayName)
                    ? group.Key
                    : group.First().DisplayName
            })
            .ToList();
    }

    private static void ApplyProperty(
        GraphPropertyEditorContext editorContext,
        string nodeId,
        GraphNodePropertyDefinition property,
        object? value,
        Action<string> notifyEditCommitted)
    {
        var node = editorContext.Session.Document.FindNode(nodeId);
        if (node is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.NodeMissing));
            return;
        }

        var properties = GraphNodeDefinitionValue.CloneDictionary(node.Properties);
        properties[property.Name] = NormalizeValue(property, value);
        var command = GraphPropertyCommandFactory.CreateNodeExtensionPropertiesCommand(
            node,
            properties);
        if (command is null || !editorContext.Execute(command))
        {
            return;
        }

        var message = F(PropertiesLanguage.NodeSymbolPropertiesUpdated, node.Id);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static void PreviewProperty(
        GraphPropertyEditorContext editorContext,
        string nodeId,
        GraphNodePropertyDefinition property,
        object? value)
    {
        var node = editorContext.Session.Document.FindNode(nodeId);
        if (node is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.NodeMissing));
            return;
        }

        var properties = GraphNodeDefinitionValue.CloneDictionary(node.Properties);
        properties[property.Name] = NormalizeValue(property, value);
        var command = GraphPropertyCommandFactory.CreateNodeExtensionPropertiesCommand(
            node,
            properties);
        if (command is not null)
        {
            editorContext.TryPreview(command);
        }
    }

    private static void CommitPropertyPreview(
        GraphPropertyEditorContext editorContext,
        string nodeId,
        IReadOnlyDictionary<string, object?>? oldProperties,
        Action<string> notifyEditCommitted)
    {
        var node = editorContext.Session.Document.FindNode(nodeId);
        if (node is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.NodeMissing));
            return;
        }

        if (oldProperties is null)
        {
            return;
        }

        var command = GraphPropertyCommandFactory.CreateNodeExtensionPropertiesCommand(
            node.Id,
            oldProperties,
            node.Properties);
        if (command is null || !editorContext.TryCommitPreview(command))
        {
            return;
        }

        var message = F(PropertiesLanguage.NodeSymbolPropertiesUpdated, node.Id);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static object? NormalizeValue(GraphNodePropertyDefinition property, object? value)
    {
        object? normalized = property.ValueKind switch
        {
            GraphNodePropertyValueKind.Integer => value is IConvertible convertible
                ? Convert.ToInt32(convertible, CultureInfo.InvariantCulture)
                : 0,
            GraphNodePropertyValueKind.Number or GraphNodePropertyValueKind.Percent =>
                value is IConvertible convertible
                    ? Convert.ToDouble(convertible, CultureInfo.InvariantCulture)
                    : 0d,
            GraphNodePropertyValueKind.Boolean => value is bool boolean && boolean,
            GraphNodePropertyValueKind.Color => string.IsNullOrWhiteSpace(value?.ToString())
                ? null
                : value.ToString(),
            _ => value?.ToString() ?? string.Empty
        };
        return ClampValue(property, normalized);
    }

    private static object? ClampValue(GraphNodePropertyDefinition property, object? value)
    {
        if (value is null || property.ValueKind is not (
                GraphNodePropertyValueKind.Integer or
                GraphNodePropertyValueKind.Number or
                GraphNodePropertyValueKind.Percent))
        {
            return value;
        }

        var minimum = property.ValueKind == GraphNodePropertyValueKind.Percent
            ? property.Minimum ?? 0
            : property.Minimum;
        var maximum = property.ValueKind == GraphNodePropertyValueKind.Percent
            ? property.Maximum ?? 100
            : property.Maximum;
        var decimalValue = ToDecimal(value);
        if (minimum is { } min)
        {
            decimalValue = Math.Max(decimalValue, min);
        }

        if (maximum is { } max)
        {
            decimalValue = Math.Min(decimalValue, max);
        }

        return property.ValueKind == GraphNodePropertyValueKind.Integer
            ? decimal.ToInt32(Math.Round(decimalValue))
            : decimal.ToDouble(decimalValue);
    }

    private static object? GetPropertyValue(
        NodeModel node,
        GraphNodePropertyDefinition property) =>
        node.Properties.TryGetValue(property.Name, out var value)
            ? value
            : property.DefaultValue;

    private static string FormatLabel(GraphNodePropertyDefinition property) =>
        string.IsNullOrWhiteSpace(property.Unit)
            ? property.DisplayName
            : string.Create(
                CultureInfo.CurrentCulture,
                $"{property.DisplayName} ({property.Unit})");

    private static string FormatStringValue(object? value) => value switch
    {
        null => string.Empty,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
        JsonElement { ValueKind: JsonValueKind.Number } element => element.GetRawText(),
        JsonElement { ValueKind: JsonValueKind.True } => bool.TrueString,
        JsonElement { ValueKind: JsonValueKind.False } => bool.FalseString,
        JsonElement element => element.GetRawText(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static decimal ToDecimal(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Number &&
                element.TryGetDecimal(out var decimalValue)
                ? decimalValue
                : 0;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return Convert.ToDecimal(convertible, CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                return 0;
            }
        }

        return decimal.TryParse(
            value?.ToString(),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;
    }

    private static bool ToBoolean(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(element.GetString(), out var parsed) && parsed,
                _ => false
            };
        }

        return value switch
        {
            bool boolean => boolean,
            string text => bool.TryParse(text, out var parsed) && parsed,
            _ => false
        };
    }

    private static string CreateEditorName(GraphNodePropertyDefinition property)
    {
        var name = new string(property.Name
            .Where(char.IsLetterOrDigit)
            .ToArray());
        return string.IsNullOrWhiteSpace(name)
            ? "NodeDynamicPropertyEditor"
            : $"NodeDynamicProperty{name}Editor";
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
