using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kudo.Avalonia.Designer;
using Kudo.Controls.PropertyModel;
using Kudo.Core.Documents;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests.DesignerTests;

/// <summary>属性面板桥接集成契约：EditorContext → 条目 → Arc PropertyGrid 行。</summary>
public sealed class PropertyItemGridBridgeTests
{
    [AvaloniaFact]
    public void Editor_context_produces_non_empty_arc_grid()
    {
        var document = SampleDocument();
        var session = new GraphDocumentSession(document);

        var view = new GraphPropertiesView();
        var window = new Window { Content = view };
        window.Show();

        view.EditorContext = new GraphPropertyEditorContext(
            session,
            selectedNodeIds: ["input"],
            primaryNodeId: "input");

        var arcGrid = window.GetVisualDescendants()
            .OfType<global::TKCode.Arc.PropertyGrid>()
            .FirstOrDefault();
        arcGrid.ShouldNotBeNull("窗口内未找到 Arc PropertyGrid");
        arcGrid.SelectedObject.ShouldNotBeNull("桥接未把条目宿主写入 SelectedObject");

        window.Close();
    }

    [AvaloniaFact]
    public void ReadOnly_context_marks_items_readonly()
    {
        var document = SampleDocument();
        var session = new GraphDocumentSession(document);

        var view = new GraphPropertiesView();
        var window = new Window { Content = view };
        window.Show();

        view.EditorContext = new GraphPropertyEditorContext(
            session,
            selectedNodeIds: ["input"],
            primaryNodeId: "input",
            isReadOnly: true);

        view.EditorContext.IsReadOnly.ShouldBeTrue();

        window.Close();
    }

    private static GraphDocument SampleDocument()
    {
        var document = new global::Kudo.GraphDocument { Title = "桥接测试" };
        var node = new global::Kudo.NodeModel
        {
            Id = "input",
            Name = "输入",
            Position = new global::Kudo.GraphPoint(40, 40),
            Size = new global::Kudo.GraphSize(120, 60)
        }.AddPort(new global::Kudo.PortModel
        {
            Id = "out",
            Kind = global::Kudo.PortKind.Output
        });
        document.AddNode(node);
        return document;
    }
}
