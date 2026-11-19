using System.ComponentModel;

namespace Kudo.Avalonia.Docking;

/// <summary>工具窗在停靠布局中的位置。</summary>
public enum GraphDockToolPlacement
{
    [Description("左侧")]
    Left,

    [Description("左上区域")]
    LeftTop,

    [Description("左下区域")]
    LeftBottom,

    [Description("右侧")]
    Right,

    [Description("底部")]
    Bottom
}

/// <summary>工具窗的角色（用于 Ribbon 命令归类）。</summary>
public enum GraphDockToolRole
{
    [Description("自定义工具")]
    Custom,

    [Description("模型工具箱")]
    Toolbox,

    [Description("文档列表")]
    Documents,

    [Description("属性面板")]
    Properties,

    [Description("诊断面板")]
    Diagnostics
}

public static class GraphDockIds
{
    private const string DocumentPrefix = "graph-document-";

    public const string Documents = "GraphDocuments";

    public const string Workspace = "GraphWorkspace";

    public const string WorkspaceRoot = "GraphWorkspaceRoot";

    public const string LeftWorkspace = "GraphLeftWorkspace";

    public static string CreateDocumentId(string documentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        return $"{DocumentPrefix}{documentId}";
    }
}

public sealed record GraphDockToolDefinition(
    string Id,
    string Title,
    object Content,
    GraphDockToolPlacement Placement,
    GraphDockToolRole Role = GraphDockToolRole.Custom);

public sealed class GraphDockToolVisibilityChangedEventArgs(string toolId, bool isVisible) : EventArgs
{
    public string ToolId { get; } = toolId;

    public bool IsVisible { get; } = isVisible;
}
