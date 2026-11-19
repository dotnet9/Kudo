// 便签属性编辑器：负责文本、位置、尺寸和扩展属性提交。
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Kudo.Avalonia;
using static Kudo.Avalonia.Designer.GraphPropertyEditorControls;
using static Kudo.Avalonia.Designer.GraphPropertyValueConverter;
using PropertiesLanguage = global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView;

namespace Kudo.Avalonia.Designer;

internal static class GraphAnnotationPropertyEditor
{
    public static void Show(
        StackPanel content,
        GraphPropertyEditorContext editorContext,
        AnnotationModel annotation,
        Action<string> notifyEditCommitted)
    {
        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.BasicInformation)));
        content.Children.Add(CreateReadOnlyField("ID", annotation.Id));
        content.Children.Add(CreateReadOnlyField(
            L(PropertiesLanguage.Size),
            $"{annotation.Size.Width:0.##} × {annotation.Size.Height:0.##}"));
        content.Children.Add(CreateSectionTitle(L(PropertiesLanguage.EditableProperties)));
        var textEditor = CreateTextEditor(
            "AnnotationTextEditor",
            L(PropertiesLanguage.Text),
            annotation.Text);
        textEditor.AcceptsReturn = true;
        textEditor.TextWrapping = TextWrapping.Wrap;
        textEditor.MinHeight = 72;
        var xEditor = CreateTextEditor(
            "AnnotationXEditor",
            L(PropertiesLanguage.XCoordinate),
            annotation.Position.X.ToString("0.##", CultureInfo.CurrentCulture));
        var yEditor = CreateTextEditor(
            "AnnotationYEditor",
            L(PropertiesLanguage.YCoordinate),
            annotation.Position.Y.ToString("0.##", CultureInfo.CurrentCulture));
        var widthEditor = CreateTextEditor(
            "AnnotationWidthEditor",
            L(PropertiesLanguage.Width),
            annotation.Size.Width.ToString("0.##", CultureInfo.CurrentCulture));
        var heightEditor = CreateTextEditor(
            "AnnotationHeightEditor",
            L(PropertiesLanguage.Height),
            annotation.Size.Height.ToString("0.##", CultureInfo.CurrentCulture));
        var propertiesEditor = CreateTextEditor(
            "AnnotationPropertiesEditor",
            L(PropertiesLanguage.ExtensionProperties),
            FormatExtensionProperties(annotation.Properties));
        propertiesEditor.AcceptsReturn = true;
        propertiesEditor.TextWrapping = TextWrapping.NoWrap;
        propertiesEditor.MinHeight = 96;
        foreach (var editor in new[]
                 {
                     textEditor,
                     xEditor,
                     yEditor,
                     widthEditor,
                     heightEditor,
                     propertiesEditor
                 })
        {
            SetEditorReadOnly(editor, editorContext.IsReadOnly);
        }

        content.Children.Add(CreateField(L(PropertiesLanguage.Text), textEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.XCoordinate), xEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.YCoordinate), yEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.Width), widthEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.Height), heightEditor));
        content.Children.Add(CreateField(L(PropertiesLanguage.ExtensionProperties), propertiesEditor));
        content.Children.Add(CreateInfo(L(PropertiesLanguage.ExtensionPropertiesHint)));

        void Commit() => Apply(
            editorContext,
            annotation.Id,
            textEditor,
            xEditor,
            yEditor,
            widthEditor,
            heightEditor,
            propertiesEditor,
            notifyEditCommitted);
        AttachTextCommit(textEditor, Commit, commitOnEnter: false);
        AttachTextCommit(xEditor, Commit);
        AttachTextCommit(yEditor, Commit);
        AttachTextCommit(widthEditor, Commit);
        AttachTextCommit(heightEditor, Commit);
        AttachTextCommit(propertiesEditor, Commit, commitOnEnter: false);
    }

    private static void Apply(
        GraphPropertyEditorContext editorContext,
        string annotationId,
        TextBox textEditor,
        TextBox xEditor,
        TextBox yEditor,
        TextBox widthEditor,
        TextBox heightEditor,
        TextBox propertiesEditor,
        Action<string> notifyEditCommitted)
    {
        var annotation = editorContext.Session.Document.FindAnnotation(annotationId);
        if (annotation is null)
        {
            editorContext.SetMessage(L(PropertiesLanguage.AnnotationMissing));
            return;
        }

        if (!TryParseCoordinate(xEditor.Text, out var x) ||
            !TryParseCoordinate(yEditor.Text, out var y) ||
            !TryParsePositiveSize(widthEditor.Text, out var width) ||
            !TryParsePositiveSize(heightEditor.Text, out var height))
        {
            editorContext.SetMessage(L(PropertiesLanguage.InvalidAnnotationBounds));
            return;
        }

        if (!TryParseExtensionProperties(propertiesEditor.Text, out var properties))
        {
            editorContext.SetMessage(L(PropertiesLanguage.InvalidAnnotationProperties));
            return;
        }

        var command = GraphPropertyCommandFactory.CreateAnnotationPropertiesCommand(
            annotation,
            NormalizeOptionalText(textEditor.Text),
            new GraphPoint(x, y),
            new GraphSize(width, height),
            properties);
        if (command is null || !editorContext.Execute(command))
        {
            return;
        }

        var message = F(PropertiesLanguage.AnnotationUpdated, annotation.Id);
        editorContext.SetMessage(message);
        notifyEditCommitted(message);
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
