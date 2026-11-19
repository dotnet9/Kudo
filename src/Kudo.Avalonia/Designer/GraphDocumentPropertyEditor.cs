// 文档属性编辑器：负责文档摘要、运行状态和标题提交。
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Kudo.Avalonia;
using Kudo.Core.Runtime;
using static Kudo.Avalonia.Designer.GraphPropertyEditorControls;
using static Kudo.Avalonia.Designer.GraphPropertyValueConverter;
using Ursa.Controls;
using AvaloniaNumericUpDown = Avalonia.Controls.NumericUpDown;
using PropertiesLanguage = global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView;

namespace Kudo.Avalonia.Designer;

internal static class GraphDocumentPropertyEditor
{
    public static void Show(
        StackPanel content,
        GraphPropertyEditorContext editorContext,
        Action<string> notifyEditCommitted)
    {
        var session = editorContext.Session;
        var document = session.Document;
        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.TaskInformation)));
        content.Children.Add(CreateReadOnlyField("ID", document.Id));
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.Nodes),
            document.Nodes.Count.ToString(CultureInfo.CurrentCulture)));
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.Edges),
            document.Edges.Count.ToString(CultureInfo.CurrentCulture)));
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.Annotations),
            document.Annotations.Count.ToString(CultureInfo.CurrentCulture)));
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.Breakpoint),
            editorContext.BreakpointNodeIds.Count.ToString(CultureInfo.CurrentCulture)));
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.RuntimeStatus),
            FormatRuntimeStatus(session.RuntimeStatus)));
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.SaveStatus),
            session.IsDirty ? L(PropertiesLanguage.Unsaved) : L(PropertiesLanguage.Saved)));
        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.EditableProperties)));
        var titleEditor = CreateTextEditor(
            "DocumentTitleEditor",
            L(PropertiesLanguage.TaskTitle),
            document.Title);
        var createdByEditor = CreateTextEditor(
            "DocumentCreatedByEditor",
            L(PropertiesLanguage.CreatedBy),
            document.CreatedBy);
        var createdAtEditor = new DateTimePicker
        {
            Name = "DocumentCreatedAtEditor",
            SelectedDate = document.CreatedAt,
            DisplayFormat = "yyyy/MM/dd HH:mm",
            IsReadOnly = editorContext.IsReadOnly,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
        };
        var descriptionEditor = CreateTextEditor(
            "DocumentDescriptionEditor",
            L(PropertiesLanguage.Description),
            document.Description);
        SetEditorReadOnly(titleEditor, editorContext.IsReadOnly);
        SetEditorReadOnly(createdByEditor, editorContext.IsReadOnly);
        SetEditorReadOnly(descriptionEditor, editorContext.IsReadOnly);
        content.Children.Add(CreateField(L(PropertiesLanguage.Title), titleEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.CreatedBy), createdByEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.CreatedAt), createdAtEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.Description), descriptionEditor));
        AttachTextCommit(titleEditor, () => Apply(
            editorContext,
            titleEditor,
            createdByEditor,
            createdAtEditor,
            descriptionEditor,
            notifyEditCommitted));
        AttachTextCommit(createdByEditor, () => Apply(
            editorContext,
            titleEditor,
            createdByEditor,
            createdAtEditor,
            descriptionEditor,
            notifyEditCommitted));
        AttachTextCommit(descriptionEditor, () => Apply(
            editorContext,
            titleEditor,
            createdByEditor,
            createdAtEditor,
            descriptionEditor,
            notifyEditCommitted));
        createdAtEditor.PropertyChanged += (_, change) =>
        {
            if (change.Property == DateTimePicker.SelectedDateProperty)
            {
                Apply(
                    editorContext,
                    titleEditor,
                    createdByEditor,
                    createdAtEditor,
                    descriptionEditor,
                    notifyEditCommitted);
            }
        };

        var settings = editorContext.CanvasSettings.Normalize();
        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.CanvasDisplay)));
        var gridVisibleEditor = CreateBooleanEditor(
            "CanvasGridVisibleEditor",
            settings.Grid.IsVisible);
        var gridSnapEditor = CreateBooleanEditor(
            "CanvasGridSnapEditor",
            settings.Grid.IsSnapEnabled);
        var guidesEditor = CreateBooleanEditor(
            "CanvasAlignmentGuidesEditor",
            settings.Grid.IsGuidesEnabled);
        var gridSpacingEditor = CreateNumericEditor(
            "CanvasGridSpacingEditor",
            (decimal)settings.Grid.Spacing,
            1,
            1000,
            1,
            "0.##");
        var minimapEditor = new CheckBox
        {
            Name = "CanvasMinimapVisibleEditor",
            IsChecked = settings.IsMinimapVisible
        };
        var lodEditor = new CheckBox
        {
            Name = "CanvasLevelOfDetailEnabledEditor",
            IsChecked = settings.IsLevelOfDetailEnabled
        };
        var showPortsEditor = new CheckBox
        {
            Name = "CanvasShowPortsEditor",
            IsChecked = settings.ShowPorts
        };
        var simplifiedThresholdEditor = CreateThresholdEditor(
            "CanvasSimplifiedRenderThresholdEditor",
            settings.LodSimplifiedZoomThreshold);
        var labelThresholdEditor = CreateThresholdEditor(
            "CanvasLabelVisibilityThresholdEditor",
            settings.LodLabelsZoomThreshold);
        var portThresholdEditor = CreateThresholdEditor(
            "CanvasPortVisibilityThresholdEditor",
            settings.LodPortsZoomThreshold);
        var portLabelVisibilityEditor = CreateEnumEditor(
            "CanvasPortLabelVisibilityEditor",
            settings.PortLabelVisibility,
            FormatLabelVisibility);
        var symbolNameVisibilityEditor = CreateEnumEditor(
            "CanvasSymbolNameVisibilityEditor",
            settings.SymbolNameVisibility,
            FormatLabelVisibility);
        var symbolDescriptionVisibilityEditor = CreateEnumEditor(
            "CanvasSymbolDescriptionVisibilityEditor",
            settings.SymbolDescriptionVisibility,
            FormatLabelVisibility);
        var canEditDisplay = !editorContext.IsReadOnly;
        gridVisibleEditor.IsEnabled = canEditDisplay;
        gridSnapEditor.IsEnabled = canEditDisplay;
        guidesEditor.IsEnabled = canEditDisplay;
        gridSpacingEditor.IsEnabled = canEditDisplay;
        minimapEditor.IsEnabled = canEditDisplay;
        lodEditor.IsEnabled = canEditDisplay;
        showPortsEditor.IsEnabled = canEditDisplay;
        simplifiedThresholdEditor.IsEnabled = canEditDisplay;
        labelThresholdEditor.IsEnabled = canEditDisplay;
        portThresholdEditor.IsEnabled = canEditDisplay;
        portLabelVisibilityEditor.IsEnabled = canEditDisplay;
        symbolNameVisibilityEditor.IsEnabled = canEditDisplay;
        symbolDescriptionVisibilityEditor.IsEnabled = canEditDisplay;
        content.Children.Add(CreateField(L(PropertiesLanguage.GridVisible), gridVisibleEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.GridSnapEnabled), gridSnapEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.AlignmentGuidesEnabled), guidesEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.GridSpacing), gridSpacingEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.MinimapVisible), minimapEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.LevelOfDetailEnabled), lodEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.ShowPorts), showPortsEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.SimplifiedRenderThreshold), simplifiedThresholdEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.LabelVisibilityThreshold), labelThresholdEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.PortVisibilityThreshold), portThresholdEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.PortLabelVisibility), portLabelVisibilityEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.SymbolNameVisibility), symbolNameVisibilityEditor));
        content.Children.Add(CreateField(
            L(PropertiesLanguage.SymbolDescriptionVisibility),
            symbolDescriptionVisibilityEditor));

        void CommitCanvasSettings() => ApplyCanvasSettings(
            editorContext,
            gridVisibleEditor,
            gridSnapEditor,
            guidesEditor,
            gridSpacingEditor,
            minimapEditor,
            lodEditor,
            showPortsEditor,
            simplifiedThresholdEditor,
            labelThresholdEditor,
            portThresholdEditor,
            portLabelVisibilityEditor,
            symbolNameVisibilityEditor,
            symbolDescriptionVisibilityEditor,
            notifyEditCommitted);

        foreach (var editor in new[]
                 {
                     gridVisibleEditor,
                     gridSnapEditor,
                     guidesEditor,
                     minimapEditor,
                     lodEditor,
                     showPortsEditor
                 })
        {
            editor.PropertyChanged += (_, change) =>
            {
                if (change.Property == ToggleButton.IsCheckedProperty)
                {
                    CommitCanvasSettings();
                }
            };
        }

        AttachNumericCommit(gridSpacingEditor, CommitCanvasSettings);
        AttachNumericCommit(simplifiedThresholdEditor, CommitCanvasSettings);
        AttachNumericCommit(labelThresholdEditor, CommitCanvasSettings);
        AttachNumericCommit(portThresholdEditor, CommitCanvasSettings);
        AttachSelectionCommit(portLabelVisibilityEditor, CommitCanvasSettings);
        AttachSelectionCommit(symbolNameVisibilityEditor, CommitCanvasSettings);
        AttachSelectionCommit(symbolDescriptionVisibilityEditor, CommitCanvasSettings);
    }

    private static void Apply(
        GraphPropertyEditorContext editorContext,
        TextBox titleEditor,
        TextBox createdByEditor,
        DateTimePicker createdAtEditor,
        TextBox descriptionEditor,
        Action<string> notifyEditCommitted)
    {
        var document = editorContext.Session.Document;
        if (createdAtEditor.SelectedDate is not { } createdAt)
        {
            editorContext.SetMessage(L(PropertiesLanguage.InvalidCreatedAt));
            return;
        }

        var command = GraphPropertyCommandFactory.CreateDocumentPropertiesCommand(
            document,
            NormalizeOptionalText(titleEditor.Text) ?? string.Empty,
            NormalizeOptionalText(createdByEditor.Text) ?? string.Empty,
            createdAt,
            NormalizeOptionalText(descriptionEditor.Text) ?? string.Empty);
        if (command is null || !editorContext.Execute(command))
        {
            return;
        }

        var message = L(PropertiesLanguage.TaskTitleUpdated);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static AvaloniaNumericUpDown CreateThresholdEditor(
        string name,
        double threshold) => CreateNumericEditor(
            name,
            ToPercent(threshold),
            10,
            300,
            5,
            "0");

    private static void ApplyCanvasSettings(
        GraphPropertyEditorContext editorContext,
        CheckBox gridVisibleEditor,
        CheckBox gridSnapEditor,
        CheckBox guidesEditor,
        AvaloniaNumericUpDown gridSpacingEditor,
        CheckBox minimapEditor,
        CheckBox lodEditor,
        CheckBox showPortsEditor,
        AvaloniaNumericUpDown simplifiedThresholdEditor,
        AvaloniaNumericUpDown labelThresholdEditor,
        AvaloniaNumericUpDown portThresholdEditor,
        ComboBox portLabelVisibilityEditor,
        ComboBox symbolNameVisibilityEditor,
        ComboBox symbolDescriptionVisibilityEditor,
        Action<string> notifyEditCommitted)
    {
        if (!TryReadThreshold(simplifiedThresholdEditor, out var simplifiedThreshold) ||
            !TryReadThreshold(labelThresholdEditor, out var labelThreshold) ||
            !TryReadThreshold(portThresholdEditor, out var portThreshold))
        {
            editorContext.SetMessage(L(PropertiesLanguage.InvalidCanvasDisplaySettings));
            return;
        }

        if (gridSpacingEditor.Value is not { } gridSpacing || gridSpacing is < 1 or > 1000)
        {
            editorContext.SetMessage(L(PropertiesLanguage.InvalidCanvasDisplaySettings));
            return;
        }

        var document = editorContext.Session.Document;
        var settings = document.CanvasSettings.Normalize();
        var updated = settings with
        {
            Grid = settings.Grid with
            {
                IsVisible = gridVisibleEditor.IsChecked == true,
                IsSnapEnabled = gridSnapEditor.IsChecked == true,
                IsGuidesEnabled = guidesEditor.IsChecked == true,
                Spacing = decimal.ToDouble(gridSpacing)
            },
            IsMinimapVisible = minimapEditor.IsChecked == true,
            IsLevelOfDetailEnabled = lodEditor.IsChecked == true,
            ShowPorts = showPortsEditor.IsChecked == true,
            LodPortsZoomThreshold = portThreshold,
            LodLabelsZoomThreshold = labelThreshold,
            LodSimplifiedZoomThreshold = simplifiedThreshold,
            PortLabelVisibility = GetEnumValue(
                portLabelVisibilityEditor,
                settings.PortLabelVisibility),
            SymbolNameVisibility = GetEnumValue(
                symbolNameVisibilityEditor,
                settings.SymbolNameVisibility),
            SymbolDescriptionVisibility = GetEnumValue(
                symbolDescriptionVisibilityEditor,
                settings.SymbolDescriptionVisibility)
        };
        var command = GraphPropertyCommandFactory.CreateCanvasSettingsCommand(document, updated);
        if (command is null || !editorContext.Execute(command))
        {
            return;
        }

        var message = L(PropertiesLanguage.CanvasDisplayUpdated);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static bool TryReadThreshold(AvaloniaNumericUpDown editor, out double threshold)
    {
        if (editor.Value is { } value &&
            value >= 10 &&
            value <= 300)
        {
            threshold = decimal.ToDouble(value) / 100d;
            return true;
        }

        threshold = 0;
        return false;
    }

    private static decimal ToPercent(double threshold) =>
        double.IsFinite(threshold)
            ? decimal.Round((decimal)(threshold * 100d), 0)
            : 100;

    private static CheckBox CreateBooleanEditor(string name, bool value) => new()
    {
        Name = name,
        IsChecked = value
    };

    private static string FormatLabelVisibility(GraphLabelVisibility visibility) => visibility switch
    {
        GraphLabelVisibility.Always => L(PropertiesLanguage.LabelVisibilityAlways),
        GraphLabelVisibility.SelectedOrHovered => L(PropertiesLanguage.LabelVisibilitySelectedOrHovered),
        GraphLabelVisibility.Hovered => L(PropertiesLanguage.LabelVisibilityHovered),
        GraphLabelVisibility.Never => L(PropertiesLanguage.LabelVisibilityNever),
        _ => L(PropertiesLanguage.LabelVisibilityAuto)
    };

    private static string FormatRuntimeStatus(RuntimeStatus status) => status switch
    {
        RuntimeStatus.Starting => L(PropertiesLanguage.RuntimeStarting),
        RuntimeStatus.Running => L(PropertiesLanguage.RuntimeRunning),
        RuntimeStatus.Paused => L(PropertiesLanguage.RuntimePaused),
        RuntimeStatus.Stopping => L(PropertiesLanguage.RuntimeStopping),
        RuntimeStatus.Faulted => L(PropertiesLanguage.RuntimeFaulted),
        _ => L(PropertiesLanguage.RuntimeStopped)
    };

    private static string L(string key) => GraphLocalization.Get(key);
}
