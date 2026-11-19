using System.ComponentModel;

namespace Kudo.Demo.Desktop.Services;

public enum MainWindowSelectionAction
{
    [Description("全选")]
    SelectAll,
    [Description("清除选择")]
    Clear,
    [Description("反选")]
    Invert,
    [Description("选择同类型节点")]
    SameType,
    [Description("选择相邻节点")]
    Adjacent,
    [Description("选择上游节点")]
    Upstream,
    [Description("选择下游节点")]
    Downstream,
    [Description("选择全部相连节点")]
    Connected,
    [Description("选择最短路径")]
    ShortestPath,
    [Description("选择第一个环")]
    FirstCycle,
    [Description("选择源节点")]
    Sources,
    [Description("选择汇节点")]
    Sinks,
    [Description("选择孤立节点")]
    Isolated,
    [Description("选择关键路径")]
    CriticalPath,
    [Description("选择视口内节点")]
    Viewport
}

public enum MainWindowCanvasViewAction
{
    [Description("应用分层布局")]
    ApplyLayeredLayout,
    [Description("适应完整文档")]
    FitDocument,
    [Description("适应当前选择")]
    FitSelection,
    [Description("实际大小")]
    ActualSize,
    [Description("重置视图")]
    Reset
}

public enum MainWindowArrangementAction
{
    [Description("左对齐")]
    AlignLeft,
    [Description("水平居中对齐")]
    AlignHorizontalCenter,
    [Description("右对齐")]
    AlignRight,
    [Description("顶部对齐")]
    AlignTop,
    [Description("垂直居中对齐")]
    AlignVerticalCenter,
    [Description("底部对齐")]
    AlignBottom,
    [Description("水平均匀分布")]
    DistributeHorizontally,
    [Description("垂直均匀分布")]
    DistributeVertically
}

public enum MainWindowDocumentEditAction
{
    [Description("撤销")]
    Undo,
    [Description("重做")]
    Redo,
    [Description("复制所选元素")]
    DuplicateSelection,
    [Description("删除所选元素")]
    DeleteSelection,
    [Description("锁定所选节点")]
    LockSelection,
    [Description("解锁所选节点")]
    UnlockSelection,
    [Description("组合所选节点")]
    GroupSelection,
    [Description("取消所选分组")]
    UngroupSelection,
    [Description("切换所选分组折叠状态")]
    ToggleSelectedGroup
}

public enum MainWindowModelingAction
{
    [Description("添加阀门")]
    AddValveNode,
    [Description("添加泵")]
    AddPumpNode,
    [Description("添加仪表")]
    AddInstrumentNode,
    [Description("添加标注")]
    AddAnnotation
}

public enum MainWindowClipboardAction
{
    [Description("复制")]
    Copy,
    [Description("剪切")]
    Cut,
    [Description("粘贴")]
    Paste
}
