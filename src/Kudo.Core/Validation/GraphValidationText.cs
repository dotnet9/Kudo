using System.ComponentModel;
using System.Globalization;

namespace Kudo;

public enum GraphValidationTextKey
{
    [Description("文档 ID 为空")]
    EmptyDocumentId,
    [Description("节点 ID 为空")]
    EmptyNodeId,
    [Description("节点 ID 重复")]
    DuplicateNodeId,
    [Description("端口 ID 为空")]
    EmptyPortId,
    [Description("端口 ID 重复")]
    DuplicatePortId,
    [Description("连线 ID 为空")]
    EmptyEdgeId,
    [Description("连线 ID 重复")]
    DuplicateEdgeId,
    [Description("分组 ID 为空")]
    EmptyGroupId,
    [Description("分组 ID 重复")]
    DuplicateGroupId,
    [Description("标注 ID 为空")]
    EmptyAnnotationId,
    [Description("标注 ID 重复")]
    DuplicateAnnotationId,
    [Description("分组没有节点")]
    EmptyGroup,
    [Description("分组边距无效")]
    InvalidGroupPadding,
    [Description("分组节点重复")]
    DuplicateGroupNode,
    [Description("分组节点不存在")]
    MissingGroupNode,
    [Description("节点属于多个分组")]
    MultipleGroupMembership,
    [Description("标注边界无效")]
    InvalidAnnotationBounds,
    [Description("连线端点缺失")]
    MissingEdgeEndpoint,
    [Description("节点自连接")]
    SelfConnection,
    [Description("端口类型不兼容")]
    IncompatiblePortKinds,
    [Description("关系类型无效")]
    InvalidRelationKind,
    [Description("数据类型不兼容")]
    IncompatibleDataType,
    [Description("连接重复")]
    DuplicateConnection,
    [Description("超出连接数量限制")]
    ConnectionLimitExceeded,
    [Description("交互连接端点缺失")]
    MissingConnectionEndpoint,
    [Description("交互连接节点自连接")]
    ConnectionSelfConnection,
    [Description("交互连接关系类型无效")]
    InvalidConnectionRelationKind,
    [Description("交互连接端口类型不兼容")]
    ConnectionPortKindsIncompatible,
    [Description("交互连接端口已禁用")]
    ConnectionPortDisabled,
    [Description("交互连接数据类型不兼容")]
    ConnectionDataTypesIncompatible,
    [Description("交互连接重复")]
    ConnectionDuplicate,
    [Description("输出端口超出连接数量限制")]
    SourceConnectionLimitExceeded,
    [Description("输入端口超出连接数量限制")]
    TargetConnectionLimitExceeded,
    [Description("连线未命名")]
    UntitledEdge,
    [Description("分组未命名")]
    UntitledGroup,
    [Description("标注未填写正文")]
    UntitledAnnotation
}

public interface IGraphValidationTextProvider
{
    string Get(GraphValidationTextKey key);
}

public static class GraphValidationText
{
    public static IGraphValidationTextProvider English { get; } = new EnglishGraphValidationTextProvider();

    public static string Get(IGraphValidationTextProvider? provider, GraphValidationTextKey key) =>
        (provider ?? English).Get(key);

    public static string Format(
        IGraphValidationTextProvider? provider,
        GraphValidationTextKey key,
        params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(provider, key), arguments);

    private sealed class EnglishGraphValidationTextProvider : IGraphValidationTextProvider
    {
        public string Get(GraphValidationTextKey key) => key switch
        {
            GraphValidationTextKey.EmptyDocumentId => "Document ID cannot be empty.",
            GraphValidationTextKey.EmptyNodeId => "Node ID cannot be empty.",
            GraphValidationTextKey.DuplicateNodeId => "Node ID “{0}” is duplicated.",
            GraphValidationTextKey.EmptyPortId => "Node “{0}” contains an empty port ID.",
            GraphValidationTextKey.DuplicatePortId => "Port ID “{1}” is duplicated on node “{0}”.",
            GraphValidationTextKey.EmptyEdgeId => "Edge ID cannot be empty.",
            GraphValidationTextKey.DuplicateEdgeId => "Edge ID “{0}” is duplicated.",
            GraphValidationTextKey.EmptyGroupId => "Group ID cannot be empty.",
            GraphValidationTextKey.DuplicateGroupId => "Group ID “{0}” is duplicated.",
            GraphValidationTextKey.EmptyAnnotationId => "Annotation ID cannot be empty.",
            GraphValidationTextKey.DuplicateAnnotationId => "Annotation ID “{0}” is duplicated.",
            GraphValidationTextKey.EmptyGroup => "Group “{0}” contains no nodes.",
            GraphValidationTextKey.InvalidGroupPadding => "Group “{0}” has invalid padding.",
            GraphValidationTextKey.DuplicateGroupNode => "Group “{0}” references node “{1}” more than once.",
            GraphValidationTextKey.MissingGroupNode => "Group “{0}” references missing node “{1}”.",
            GraphValidationTextKey.MultipleGroupMembership => "Node “{0}” belongs to multiple groups.",
            GraphValidationTextKey.InvalidAnnotationBounds => "Annotation “{0}” has invalid bounds.",
            GraphValidationTextKey.MissingEdgeEndpoint => "Edge “{0}” has a missing source or target.",
            GraphValidationTextKey.SelfConnection => "Edge “{0}” cannot connect a node to itself.",
            GraphValidationTextKey.IncompatiblePortKinds => "Edge “{0}” uses port kinds that are incompatible with its relation kind.",
            GraphValidationTextKey.InvalidRelationKind => "Edge “{0}” has an invalid relation kind.",
            GraphValidationTextKey.IncompatibleDataType => "Edge “{0}” connects incompatible port data types.",
            GraphValidationTextKey.DuplicateConnection => "Edge “{0}” duplicates an existing connection.",
            GraphValidationTextKey.ConnectionLimitExceeded => "Edge “{0}” exceeds a port connection capacity.",
            GraphValidationTextKey.MissingConnectionEndpoint => "Both source and target ports must exist.",
            GraphValidationTextKey.ConnectionSelfConnection => "A node cannot connect to itself.",
            GraphValidationTextKey.InvalidConnectionRelationKind => "The connection relation kind is invalid.",
            GraphValidationTextKey.ConnectionPortKindsIncompatible => "The source and target port kinds are not compatible with the relation kind.",
            GraphValidationTextKey.ConnectionPortDisabled => "One of the ports is disabled.",
            GraphValidationTextKey.ConnectionDataTypesIncompatible => "Port data types are not compatible.",
            GraphValidationTextKey.ConnectionDuplicate => "The same connection already exists.",
            GraphValidationTextKey.SourceConnectionLimitExceeded => "The source port has reached its connection capacity.",
            GraphValidationTextKey.TargetConnectionLimitExceeded => "The target port has reached its connection capacity.",
            GraphValidationTextKey.UntitledEdge => "Untitled edge",
            GraphValidationTextKey.UntitledGroup => "Untitled group",
            GraphValidationTextKey.UntitledAnnotation => "Untitled annotation",
            _ => key.ToString()
        };
    }
}
