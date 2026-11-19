using System.ComponentModel;

namespace Kudo.Avalonia;

public enum GraphEditOperation
{
    [Description("选择节点")]
    SelectNodes,
    [Description("选择连线")]
    SelectEdges,
    [Description("选择分组")]
    SelectGroup,
    [Description("选择标注")]
    SelectAnnotation,
    [Description("添加节点")]
    AddNodes,
    [Description("添加标注")]
    AddAnnotation,
    [Description("移动节点")]
    MoveNodes,
    [Description("调整节点层级")]
    ReorderNodes,
    [Description("移动标注")]
    MoveAnnotation,
    [Description("调整节点尺寸")]
    ResizeNode,
    [Description("设置节点锁定状态")]
    SetNodeLocked,
    [Description("调整标注尺寸")]
    ResizeAnnotation,
    [Description("连接端口")]
    ConnectPorts,
    [Description("重连连线端点")]
    ReconnectEdge,
    [Description("删除节点")]
    DeleteNodes,
    [Description("删除连线")]
    DeleteEdges,
    [Description("删除标注")]
    DeleteAnnotation,
    [Description("创建分组")]
    CreateGroup,
    [Description("取消分组")]
    RemoveGroups,
    [Description("切换分组折叠状态")]
    SetGroupCollapsed,
    [Description("添加连线途经点")]
    AddEdgeWaypoint,
    [Description("移动连线途经点")]
    MoveEdgeWaypoint,
    [Description("删除连线途经点")]
    RemoveEdgeWaypoint,
    [Description("编辑节点属性")]
    EditNodeProperties,
    [Description("编辑连线属性")]
    EditEdgeProperties,
    [Description("编辑标注正文")]
    EditAnnotationText,
    [Description("编辑标注属性")]
    EditAnnotationProperties,
    [Description("编辑文档属性")]
    EditDocumentProperties,
    [Description("执行自定义命令")]
    ExecuteCommand,
    [Description("编辑分组属性")]
    EditGroupProperties
}

public sealed record GraphEditRequest(
    GraphEditOperation Operation,
    GraphDocument Document,
    IReadOnlyList<NodeModel>? Nodes = null,
    EdgeModel? Edge = null,
    GroupModel? Group = null,
    AnnotationModel? Annotation = null,
    PortReference? Source = null,
    PortReference? Target = null,
    IGraphCommand? Command = null,
    IReadOnlyList<EdgeModel>? Edges = null)
{
    public IReadOnlyList<NodeModel> TargetNodes { get; } = Nodes ?? Array.Empty<NodeModel>();

    public IReadOnlyList<EdgeModel> TargetEdges { get; } =
        Edges ?? (Edge is null ? Array.Empty<EdgeModel>() : [Edge]);
}
