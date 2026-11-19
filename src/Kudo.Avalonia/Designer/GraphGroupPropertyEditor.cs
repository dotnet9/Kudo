// 分组属性编辑器：负责分组字段构建、校验和可撤销命令提交。
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Kudo.Avalonia;
using static Kudo.Avalonia.Designer.GraphPropertyEditorControls;
using static Kudo.Avalonia.Designer.GraphPropertyValueConverter;
using PropertiesLanguage = global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView;

namespace Kudo.Avalonia.Designer;

internal static class GraphGroupPropertyEditor
{
    private enum GroupDisplayState
    {
        [Description("展开分组")]
        Expanded,
        [Description("折叠分组")]
        Collapsed
    }

    public static void Show(
        StackPanel content,
        GraphPropertyEditorContext editorContext,
        GroupModel group,
        Action<string> notifyEditCommitted)
    {
        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.BasicInformation)));
        content.Children.Add(CreateReadOnlyField("ID", group.Id));
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.Nodes),
            group.NodeIds.Count.ToString(CultureInfo.CurrentCulture)));
        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.EditableProperties)));
        var titleEditor = CreateTextEditor("GroupTitleEditor", L(PropertiesLanguage.Title), group.Title);
        var paddingEditor = CreateTextEditor(
            "GroupPaddingEditor",
            L(PropertiesLanguage.Padding),
            group.Padding.ToString("0.##", CultureInfo.CurrentCulture));
        var stateEditor = CreateEnumEditor(
            "GroupStateEditor",
            group.IsCollapsed ? GroupDisplayState.Collapsed : GroupDisplayState.Expanded,
            FormatGroupDisplayState);
        var propertiesEditor = CreateTextEditor(
            "GroupPropertiesEditor",
            L(PropertiesLanguage.ExtensionProperties),
            FormatExtensionProperties(group.Properties));
        propertiesEditor.AcceptsReturn = true;
        propertiesEditor.TextWrapping = TextWrapping.NoWrap;
        propertiesEditor.MinHeight = 96;
        SetEditorReadOnly(titleEditor, editorContext.IsReadOnly);
        SetEditorReadOnly(paddingEditor, editorContext.IsReadOnly);
        SetEditorReadOnly(propertiesEditor, editorContext.IsReadOnly);
        stateEditor.IsEnabled = !editorContext.IsReadOnly;
        content.Children.Add(CreateField(L(PropertiesLanguage.Title), titleEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.Padding), paddingEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.State), stateEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.ExtensionProperties), propertiesEditor));
        content.Children.Add(CreateInfo(L(PropertiesLanguage.ExtensionPropertiesHint)));

        void Commit() => Apply(
            editorContext,
            group.Id,
            titleEditor,
            paddingEditor,
            stateEditor,
            propertiesEditor,
            notifyEditCommitted);
        AttachTextCommit(titleEditor, Commit);
        AttachTextCommit(paddingEditor, Commit);
        AttachSelectionCommit(stateEditor, Commit);
        AttachTextCommit(propertiesEditor, Commit, commitOnEnter: false);
    }

    private static void Apply(
        GraphPropertyEditorContext editorContext,
        string groupId,
        TextBox titleEditor,
        TextBox paddingEditor,
        global::TKCode.Arc.Combobox stateEditor,
        TextBox propertiesEditor,
        Action<string> notifyEditCommitted)
    {
        var group = editorContext.Session.Document.FindGroup(groupId);
        if (group is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.GroupMissing));
            return;
        }

        if (!TryParseNonNegativeNumber(paddingEditor.Text, out var padding))
        {
            editorContext.SetMessage(L(PropertiesLanguage.InvalidGroupPadding));
            return;
        }

        if (!TryParseExtensionProperties(propertiesEditor.Text, out var properties))
        {
            editorContext.SetMessage(L(PropertiesLanguage.InvalidExtensionProperties));
            return;
        }

        var state = GetEnumValue(
            stateEditor,
            group.IsCollapsed ? GroupDisplayState.Collapsed : GroupDisplayState.Expanded);
        var command = GraphPropertyCommandFactory.CreateGroupPropertiesCommand(
            group,
            NormalizeOptionalText(titleEditor.Text),
            padding,
            state == GroupDisplayState.Collapsed,
            properties);
        if (command is null || !editorContext.Execute(command))
        {
            return;
        }

        var message = F(PropertiesLanguage.GroupUpdated, group.Id);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static string FormatGroupDisplayState(GroupDisplayState state) => state switch
    {
        GroupDisplayState.Collapsed => L(PropertiesLanguage.Collapsed),
        _ => L(PropertiesLanguage.Expanded)
    };

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
