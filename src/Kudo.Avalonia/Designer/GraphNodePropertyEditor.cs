// 节点属性编辑器：负责基础属性、背景外观和宿主图片选择请求。
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using static Kudo.Avalonia.Designer.GraphPropertyEditorControls;
using static Kudo.Avalonia.Designer.GraphPropertyValueConverter;
using PropertiesLanguage = global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView;

namespace Kudo.Avalonia.Designer;

internal static class GraphNodePropertyEditor
{
    public static void Show(
        StackPanel content,
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        bool showBackgroundStretchEditor,
        Action<string> notifyEditCommitted,
        Action<GraphNodeBackgroundImageRequest> requestBackgroundImage,
        Action<GraphPropertyEditorContext?> refresh)
    {
        var appearance = node.Appearance.Normalize();
        var hasPorts = node.Ports.Count > 0;
        var isTextNode = appearance.VisualMode == GraphNodeVisualMode.Text;
        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.BasicInformation)));
        var numberEditor = CreateTextEditor("NodeNumberEditor", L(PropertiesLanguage.Number), node.Number);
        var nameEditor = CreateTextEditor("NodeNameEditor", L(PropertiesLanguage.Name), node.Name);
        var descriptionEditor = CreateTextEditor(
            "NodeDescriptionEditor",
            L(PropertiesLanguage.Description),
            node.Description);
        var xEditor = CreateTextEditor(
            "NodeXEditor",
            L(PropertiesLanguage.XCoordinate),
            node.Position.X.ToString("0.##", CultureInfo.CurrentCulture));
        var yEditor = CreateTextEditor(
            "NodeYEditor",
            L(PropertiesLanguage.YCoordinate),
            node.Position.Y.ToString("0.##", CultureInfo.CurrentCulture));
        var rotationEditor = CreateNumericEditor(
            "NodeRotationEditor",
            (decimal)GraphNodeGeometry.NormalizeRotation(node.Rotation),
            -180,
            180,
            1,
            "0.##");
        var lockEditor = new CheckBox
        {
            Name = "NodeLockedEditor",
            IsChecked = node.IsLocked,
            IsEnabled = !editorContext.IsReadOnly
        };
        var widthEditor = CreateTextEditor("NodeWidthEditor", L(PropertiesLanguage.Width), node.Size.Width.ToString("0.##", CultureInfo.CurrentCulture));
        var heightEditor = CreateTextEditor("NodeHeightEditor", L(PropertiesLanguage.Height), node.Size.Height.ToString("0.##", CultureInfo.CurrentCulture));
        SetEditorReadOnly(numberEditor, editorContext.IsReadOnly);
        SetEditorReadOnly(nameEditor, editorContext.IsReadOnly);
        SetEditorReadOnly(descriptionEditor, editorContext.IsReadOnly);
        SetEditorReadOnly(xEditor, editorContext.IsReadOnly || node.IsLocked);
        SetEditorReadOnly(yEditor, editorContext.IsReadOnly || node.IsLocked);
        rotationEditor.IsEnabled = false;
        SetEditorReadOnly(widthEditor, editorContext.IsReadOnly || node.IsLocked);
        SetEditorReadOnly(heightEditor, editorContext.IsReadOnly || node.IsLocked);
        content.Children.Add(CreateReadOnlyField(L(PropertiesLanguage.DefinitionId), node.DefinitionId));
        content.Children.Add(CreateField(L(PropertiesLanguage.Number), numberEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.Name), nameEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.Description), descriptionEditor));
        var positionContent = new StackPanel { Spacing = 2 };
        positionContent.Children.Add(CreateField(L(PropertiesLanguage.XCoordinate), xEditor));
        positionContent.Children.Add(CreateField(L(PropertiesLanguage.YCoordinate), yEditor));
        content.Children.Add(new Expander
        {
            Header = $"[{node.Position.X:0.##},{node.Position.Y:0.##}]",
            Content = positionContent,
            IsExpanded = false
        });
        var sizeContent = new StackPanel { Spacing = 2 };
        sizeContent.Children.Add(CreateField(L(PropertiesLanguage.Width), widthEditor));
        sizeContent.Children.Add(CreateField(L(PropertiesLanguage.Height), heightEditor));
        content.Children.Add(new Expander
        {
            Header = $"[{node.Size.Width:0.##}x{node.Size.Height:0.##}]",
            Content = sizeContent,
            IsExpanded = false
        });
        content.Children.Add(CreateField(L(PropertiesLanguage.NodeLocked), lockEditor));
        AttachTextCommit(numberEditor, () => ApplyNode(
            editorContext,
            node.Id,
            numberEditor,
            nameEditor,
            descriptionEditor,
            xEditor,
            yEditor,
            rotationEditor,
            notifyEditCommitted));
        AttachTextCommit(nameEditor, () => ApplyNode(
            editorContext,
            node.Id,
            numberEditor,
            nameEditor,
            descriptionEditor,
            xEditor,
            yEditor,
            rotationEditor,
            notifyEditCommitted));
        AttachTextCommit(descriptionEditor, () => ApplyNode(
            editorContext,
            node.Id,
            numberEditor,
            nameEditor,
            descriptionEditor,
            xEditor,
            yEditor,
            rotationEditor,
            notifyEditCommitted));
        AttachTextCommit(xEditor, () => ApplyNode(
            editorContext,
            node.Id,
            numberEditor,
            nameEditor,
            descriptionEditor,
            xEditor,
            yEditor,
            rotationEditor,
            notifyEditCommitted));
        AttachTextCommit(yEditor, () => ApplyNode(
            editorContext,
            node.Id,
            numberEditor,
            nameEditor,
            descriptionEditor,
            xEditor,
            yEditor,
            rotationEditor,
            notifyEditCommitted));
        AttachNumericCommit(rotationEditor, () => ApplyNode(
            editorContext,
            node.Id,
            numberEditor,
            nameEditor,
            descriptionEditor,
            xEditor,
            yEditor,
            rotationEditor,
            notifyEditCommitted));
        AttachTextCommit(widthEditor, () => ApplyNodeSize(editorContext, node.Id, widthEditor, heightEditor, notifyEditCommitted));
        AttachTextCommit(heightEditor, () => ApplyNodeSize(editorContext, node.Id, widthEditor, heightEditor, notifyEditCommitted));
        lockEditor.PropertyChanged += (_, change) =>
        {
            if (change.Property == ToggleButton.IsCheckedProperty)
            {
                ApplyNodeLock(editorContext, node.Id, lockEditor, notifyEditCommitted);
            }
        };

        GraphNodeDefinitionPropertyEditor.Show(
            content,
            editorContext,
            node,
            notifyEditCommitted);

        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.Appearance)));
        var visualModeEditor = CreateEnumEditor(
            "NodeVisualModeEditor",
            appearance.VisualMode,
            FormatNodeVisualMode);
        var stretchEditor = CreateEnumEditor(
            "NodeBackgroundStretchEditor",
            appearance.Stretch,
            FormatImageStretch);
        var opacityEditor = CreatePercentageSliderEditor(
            "NodeBackgroundOpacityEditor",
            appearance.Opacity * 100,
            0,
            100);
        var nameVisibilityEditor = CreateOptionalLabelVisibilityEditor(
            "NodeNameVisibilityEditor",
            appearance.NameVisibility);
        var descriptionVisibilityEditor = CreateOptionalLabelVisibilityEditor(
            "NodeDescriptionVisibilityEditor",
            appearance.DescriptionVisibility);
        var portLabelVisibilityEditor = CreateOptionalLabelVisibilityEditor(
            "NodePortLabelVisibilityEditor",
            appearance.PortLabelVisibility);
        var textForegroundEditor = CreateColorEditor(
            "NodeTextForegroundEditor",
            appearance.TextAppearance.Foreground,
            L(PropertiesLanguage.ClearColor));
        var textFontSizeEditor = CreateNumericEditor(
            "NodeTextFontSizeEditor",
            (decimal)appearance.TextAppearance.FontSize,
            6,
            256,
            1,
            "0.##");
        var textFontWeightEditor = CreateEnumEditor(
            "NodeTextFontWeightEditor",
            appearance.TextAppearance.FontWeight,
            FormatFontWeight);
        visualModeEditor.IsEnabled = !editorContext.IsReadOnly;
        stretchEditor.IsEnabled = !editorContext.IsReadOnly;
        opacityEditor.Slider.IsEnabled = !editorContext.IsReadOnly;
        nameVisibilityEditor.IsEnabled = !editorContext.IsReadOnly;
        descriptionVisibilityEditor.IsEnabled = !editorContext.IsReadOnly;
        portLabelVisibilityEditor.IsEnabled = !editorContext.IsReadOnly;
        textForegroundEditor.IsEnabled = !editorContext.IsReadOnly;
        textFontSizeEditor.IsEnabled = !editorContext.IsReadOnly;
        textFontWeightEditor.IsEnabled = !editorContext.IsReadOnly;
        content.Children.Add(CreateField(L(PropertiesLanguage.NodeVisualMode), visualModeEditor));
        if (!isTextNode)
        {
            content.Children.Add(CreateField(
                L(PropertiesLanguage.BackgroundImage),
                CreateBackgroundImageEditor(
                    editorContext,
                    node,
                    requestBackgroundImage,
                    notifyEditCommitted,
                    refresh)));
            if (showBackgroundStretchEditor)
            {
                content.Children.Add(CreateField(L(PropertiesLanguage.BackgroundStretch), stretchEditor));
            }

            content.Children.Add(CreateField(L(PropertiesLanguage.BackgroundOpacity), opacityEditor));
        }

        content.Children.Add(CreateField(L(PropertiesLanguage.NodeNameVisibility), nameVisibilityEditor));
        content.Children.Add(CreateField(
            L(PropertiesLanguage.NodeDescriptionVisibility),
            descriptionVisibilityEditor));
        if (hasPorts)
        {
            content.Children.Add(CreateField(L(PropertiesLanguage.PortLabelVisibility), portLabelVisibilityEditor));
        }

        content.Children.Add(CreateField(L(PropertiesLanguage.TextForeground), textForegroundEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.TextFontSize), textFontSizeEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.TextFontWeight), textFontWeightEditor));
        AttachSelectionCommit(visualModeEditor, () =>
        {
            CommitAppearance();
            refresh(editorContext);
        });
        AttachSelectionCommit(stretchEditor, CommitAppearance);
        GraphNodeAppearance? opacityEditStart = null;
        opacityEditor.AttachEdit(
            () => opacityEditStart = editorContext.Session.Document
                .FindNode(node.Id)?
                .Appearance
                .Normalize(),
            () => PreviewAppearance(
                editorContext,
                node.Id,
                visualModeEditor,
                stretchEditor,
                opacityEditor,
                nameVisibilityEditor,
                descriptionVisibilityEditor,
                portLabelVisibilityEditor,
                textForegroundEditor,
                textFontSizeEditor,
                textFontWeightEditor),
            () =>
            {
                CommitAppearancePreview(
                    editorContext,
                    node.Id,
                    opacityEditStart,
                    notifyEditCommitted);
                opacityEditStart = null;
            });
        AttachSelectionCommit(nameVisibilityEditor, CommitAppearance);
        AttachSelectionCommit(descriptionVisibilityEditor, CommitAppearance);
        AttachSelectionCommit(portLabelVisibilityEditor, CommitAppearance);
        AttachColorCommit(textForegroundEditor, CommitAppearance);
        AttachNumericCommit(textFontSizeEditor, CommitAppearance);
        AttachSelectionCommit(textFontWeightEditor, CommitAppearance);

        void CommitAppearance() => ApplyAppearance(
            editorContext,
            node.Id,
            visualModeEditor,
            stretchEditor,
            opacityEditor,
            nameVisibilityEditor,
            descriptionVisibilityEditor,
            portLabelVisibilityEditor,
            textForegroundEditor,
            textFontSizeEditor,
            textFontWeightEditor,
            notifyEditCommitted);

    }

    private static Grid CreateBackgroundImageEditor(
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        Action<GraphNodeBackgroundImageRequest> requestBackgroundImage,
        Action<string> notifyEditCommitted,
        Action<GraphPropertyEditorContext?> refresh)
    {
        var imagePath = node.Appearance.BackgroundImage;
        var displayText = FormatBackgroundImageName(imagePath);
        var display = CreateTextEditor(
            "NodeBackgroundImageEditor",
            L(PropertiesLanguage.BackgroundImage),
            displayText);
        SetEditorReadOnly(display, true);
        GraphToolTipHelper.SetText(display, string.IsNullOrWhiteSpace(imagePath) ? displayText : imagePath);
        display.Classes.Add("graph-property-editor");

        var selectImage = CreateIconActionButton(
            "SelectNodeBackgroundImageButton",
            L(PropertiesLanguage.SelectBackgroundImage),
            "SemiIconImageStroked");
        selectImage.IsEnabled = !editorContext.IsReadOnly;
        selectImage.Click += (_, _) => requestBackgroundImage(
            new GraphNodeBackgroundImageRequest(editorContext, node.Id));

        var clearImage = CreateIconActionButton(
            "ClearNodeBackgroundImageButton",
            L(PropertiesLanguage.ClearBackgroundImage),
            "SemiIconClear");
        clearImage.IsEnabled = !editorContext.IsReadOnly && imagePath is not null;
        clearImage.Click += (_, _) => ClearBackgroundImage(
            editorContext,
            node.Id,
            notifyEditCommitted,
            refresh);

        var editor = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 2,
            Children = { display, selectImage, clearImage }
        };
        editor.Classes.Add("graph-background-image-editor");
        Grid.SetColumn(selectImage, 1);
        Grid.SetColumn(clearImage, 2);
        return editor;
    }

    private static void ApplyNode(
        GraphPropertyEditorContext editorContext,
        string nodeId,
        TextBox numberEditor,
        TextBox nameEditor,
        TextBox descriptionEditor,
        TextBox xEditor,
        TextBox yEditor,
        NumericUpDown rotationEditor,
        Action<string> notifyEditCommitted)
    {
        var node = editorContext.Session.Document.FindNode(nodeId);
        if (node is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.NodeMissing));
            return;
        }

        if (!TryParseCoordinate(xEditor.Text, out var x) ||
            !TryParseCoordinate(yEditor.Text, out var y))
        {
            editorContext.SetMessage(L(PropertiesLanguage.InvalidCoordinates));
            return;
        }

        var command = GraphPropertyCommandFactory.CreateNodePropertiesCommand(
            node,
            NormalizeOptionalText(numberEditor.Text) ?? string.Empty,
            NormalizeOptionalText(nameEditor.Text),
            NormalizeOptionalText(descriptionEditor.Text),
            new GraphPoint(x, y),
            rotationEditor.Value is { } rotation
                ? decimal.ToDouble(rotation)
                : node.Rotation);
        if (command is null || !editorContext.Execute(command))
        {
            return;
        }

        var message = F(PropertiesLanguage.NodeUpdated, node.Id);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static void ApplyNodeSize(
        GraphPropertyEditorContext editorContext,
        string nodeId,
        TextBox widthEditor,
        TextBox heightEditor,
        Action<string> notifyEditCommitted)
    {
        var node = editorContext.Session.Document.FindNode(nodeId);
        if (node is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.NodeMissing));
            return;
        }

        if (!double.TryParse(widthEditor.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var width) ||
            !double.TryParse(heightEditor.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var height) ||
            !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            editorContext.SetMessage(L(PropertiesLanguage.InvalidSize));
            return;
        }

        var command = new ResizeNodeCommand(
            node.Id,
            node.Position,
            node.Size,
            node.Position,
            new GraphSize(width, height),
            GraphLocalization.Get(PropertiesLanguage.Size));
        if (node.Size == command.NewSize || !editorContext.Execute(command))
            return;

        var message = F(PropertiesLanguage.NodeUpdated, node.Id);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static void ApplyNodeLock(
        GraphPropertyEditorContext editorContext,
        string nodeId,
        CheckBox lockEditor,
        Action<string> notifyEditCommitted)
    {
        var node = editorContext.Session.Document.FindNode(nodeId);
        if (node is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.NodeMissing));
            return;
        }

        var command = GraphPropertyCommandFactory.CreateNodeLockCommand(
            node,
            lockEditor.IsChecked == true);
        if (command is null || !editorContext.Execute(command))
        {
            return;
        }

        var message = F(PropertiesLanguage.NodeUpdated, node.Id);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static void ApplyAppearance(
        GraphPropertyEditorContext editorContext,
        string nodeId,
        ComboBox visualModeEditor,
        ComboBox stretchEditor,
        GraphPercentageEditor opacityEditor,
        ComboBox nameVisibilityEditor,
        ComboBox descriptionVisibilityEditor,
        ComboBox portLabelVisibilityEditor,
        GraphColorEditor textForegroundEditor,
        NumericUpDown textFontSizeEditor,
        ComboBox textFontWeightEditor,
        Action<string> notifyEditCommitted)
    {
        if (!TryCreateAppearance(
                editorContext,
                nodeId,
                visualModeEditor,
                stretchEditor,
                opacityEditor,
                nameVisibilityEditor,
                descriptionVisibilityEditor,
                portLabelVisibilityEditor,
                textForegroundEditor,
                textFontSizeEditor,
                textFontWeightEditor,
                out var node,
                out var appearance))
        {
            return;
        }

        ExecuteAppearanceCommand(
            editorContext,
            node,
            appearance,
            notifyEditCommitted);
    }

    private static void PreviewAppearance(
        GraphPropertyEditorContext editorContext,
        string nodeId,
        ComboBox visualModeEditor,
        ComboBox stretchEditor,
        GraphPercentageEditor opacityEditor,
        ComboBox nameVisibilityEditor,
        ComboBox descriptionVisibilityEditor,
        ComboBox portLabelVisibilityEditor,
        GraphColorEditor textForegroundEditor,
        NumericUpDown textFontSizeEditor,
        ComboBox textFontWeightEditor)
    {
        if (!TryCreateAppearance(
                editorContext,
                nodeId,
                visualModeEditor,
                stretchEditor,
                opacityEditor,
                nameVisibilityEditor,
                descriptionVisibilityEditor,
                portLabelVisibilityEditor,
                textForegroundEditor,
                textFontSizeEditor,
                textFontWeightEditor,
                out var node,
                out var appearance))
        {
            return;
        }

        var command = GraphPropertyCommandFactory.CreateNodeAppearanceCommand(node, appearance);
        if (command is not null)
        {
            editorContext.TryPreview(command);
        }
    }

    private static void CommitAppearancePreview(
        GraphPropertyEditorContext editorContext,
        string nodeId,
        GraphNodeAppearance? oldAppearance,
        Action<string> notifyEditCommitted)
    {
        var node = editorContext.Session.Document.FindNode(nodeId);
        if (node is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.NodeMissing));
            return;
        }

        if (oldAppearance is null)
        {
            return;
        }

        var command = GraphPropertyCommandFactory.CreateNodeAppearanceCommand(
            node.Id,
            oldAppearance,
            node.Appearance);
        if (command is null || !editorContext.TryCommitPreview(command))
        {
            return;
        }

        var message = F(PropertiesLanguage.NodeAppearanceUpdated, node.Id);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static bool TryCreateAppearance(
        GraphPropertyEditorContext editorContext,
        string nodeId,
        ComboBox visualModeEditor,
        ComboBox stretchEditor,
        GraphPercentageEditor opacityEditor,
        ComboBox nameVisibilityEditor,
        ComboBox descriptionVisibilityEditor,
        ComboBox portLabelVisibilityEditor,
        GraphColorEditor textForegroundEditor,
        NumericUpDown textFontSizeEditor,
        ComboBox textFontWeightEditor,
        out NodeModel node,
        out GraphNodeAppearance appearance)
    {
        node = editorContext.Session.Document.FindNode(nodeId)!;
        appearance = null!;
        if (node is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.NodeMissing));
            return false;
        }

        var opacityPercentage = opacityEditor.Value;
        if (!double.IsFinite(opacityPercentage) || opacityPercentage is < 0 or > 100)
        {
            editorContext.SetMessage(L(PropertiesLanguage.InvalidBackgroundOpacity));
            return false;
        }

        var current = node.Appearance.Normalize();
        appearance = current with
        {
            VisualMode = GetEnumValue(visualModeEditor, current.VisualMode),
            Stretch = GetEnumValue(stretchEditor, current.Stretch),
            Opacity = opacityPercentage / 100d,
            NameVisibility = GetOptionalLabelVisibility(nameVisibilityEditor),
            DescriptionVisibility = GetOptionalLabelVisibility(descriptionVisibilityEditor),
            PortLabelVisibility = GetOptionalLabelVisibility(portLabelVisibilityEditor),
            TextAppearance = current.TextAppearance.Normalize() with
            {
                Foreground = textForegroundEditor.Value,
                FontSize = textFontSizeEditor.Value is { } fontSize
                    ? decimal.ToDouble(fontSize)
                    : current.TextAppearance.FontSize,
                FontWeight = GetEnumValue(
                    textFontWeightEditor,
                    current.TextAppearance.FontWeight)
            }
        };
        return true;
    }

    private static void ClearBackgroundImage(
        GraphPropertyEditorContext editorContext,
        string nodeId,
        Action<string> notifyEditCommitted,
        Action<GraphPropertyEditorContext?> refresh)
    {
        var node = editorContext.Session.Document.FindNode(nodeId);
        if (node is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.NodeMissing));
            return;
        }

        ExecuteAppearanceCommand(
            editorContext,
            node,
            node.Appearance with { BackgroundImage = null },
            notifyEditCommitted);
        refresh(editorContext);
    }

    private static void ExecuteAppearanceCommand(
        GraphPropertyEditorContext editorContext,
        NodeModel node,
        GraphNodeAppearance appearance,
        Action<string> notifyEditCommitted)
    {
        var command = GraphPropertyCommandFactory.CreateNodeAppearanceCommand(node, appearance);
        if (command is null || !editorContext.Execute(command))
        {
            return;
        }

        var message = F(PropertiesLanguage.NodeAppearanceUpdated, node.Id);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static string FormatImageStretch(GraphImageStretch stretch) => stretch switch
    {
        GraphImageStretch.Fill => L(PropertiesLanguage.ImageStretchFill),
        GraphImageStretch.Uniform => L(PropertiesLanguage.ImageStretchUniform),
        GraphImageStretch.UniformToFill => L(PropertiesLanguage.ImageStretchUniformToFill),
        _ => L(PropertiesLanguage.ImageStretchNone)
    };

    private static string FormatNodeVisualMode(GraphNodeVisualMode visualMode) => visualMode switch
    {
        GraphNodeVisualMode.Card => L(PropertiesLanguage.NodeVisualModeCard),
        GraphNodeVisualMode.Symbol => L(PropertiesLanguage.NodeVisualModeSymbol),
        GraphNodeVisualMode.Text => L(PropertiesLanguage.NodeVisualModeText),
        _ => L(PropertiesLanguage.NodeVisualModeAuto)
    };

    private static ComboBox CreateOptionalLabelVisibilityEditor(
        string name,
        GraphLabelVisibility? value)
    {
        var items = new List<ComboBoxItem>
        {
            new()
            {
                Content = L(PropertiesLanguage.UseCanvasSetting),
                Tag = null
            }
        };
        items.AddRange(Enum.GetValues<GraphLabelVisibility>().Select(visibility => new ComboBoxItem
        {
            Content = FormatLabelVisibility(visibility),
            Tag = visibility
        }));
        return new ComboBox
        {
            Name = name,
            ItemsSource = items,
            SelectedItem = items.Single(item => Equals(item.Tag, value)),
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
        };
    }

    private static GraphLabelVisibility? GetOptionalLabelVisibility(ComboBox editor) =>
        editor.SelectedItem is ComboBoxItem { Tag: GraphLabelVisibility value } ? value : null;

    private static string FormatLabelVisibility(GraphLabelVisibility visibility) => visibility switch
    {
        GraphLabelVisibility.Always => L(PropertiesLanguage.LabelVisibilityAlways),
        GraphLabelVisibility.SelectedOrHovered => L(PropertiesLanguage.LabelVisibilitySelectedOrHovered),
        GraphLabelVisibility.Hovered => L(PropertiesLanguage.LabelVisibilityHovered),
        GraphLabelVisibility.Never => L(PropertiesLanguage.LabelVisibilityNever),
        _ => L(PropertiesLanguage.LabelVisibilityAuto)
    };

    private static string FormatFontWeight(GraphFontWeight weight) => weight switch
    {
        GraphFontWeight.Medium => L(PropertiesLanguage.FontWeightMedium),
        GraphFontWeight.SemiBold => L(PropertiesLanguage.FontWeightSemiBold),
        GraphFontWeight.Bold => L(PropertiesLanguage.FontWeightBold),
        _ => L(PropertiesLanguage.FontWeightNormal)
    };

    private static string FormatBackgroundImageName(string? imagePath) =>
        string.IsNullOrWhiteSpace(imagePath)
            ? L(PropertiesLanguage.NoBackgroundImage)
            : Path.GetFileName(imagePath.Replace('\\', Path.DirectorySeparatorChar));

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
