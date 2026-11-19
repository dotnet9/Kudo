using System.ComponentModel;
using System.Globalization;

namespace Kudo.Demo;

public enum DemoGraphTextKey
{
    [Description("数据采集阶段")]
    StageDataAcquisition,
    [Description("协议解析阶段")]
    StageProtocolParsing,
    [Description("数据清洗阶段")]
    StageDataCleaning,
    [Description("特征计算阶段")]
    StageFeatureComputation,
    [Description("规则评估阶段")]
    StageRuleEvaluation,
    [Description("事件路由阶段")]
    StageEventRouting,
    [Description("指标聚合阶段")]
    StageMetricAggregation,
    [Description("存储输出阶段")]
    StageStorageOutput,
    [Description("输入节点")]
    InputNode,
    [Description("转换节点")]
    TransformNode,
    [Description("输出节点")]
    OutputNode,
    [Description("基础文档")]
    BasicDocument,
    [Description("原始数据")]
    RawData,
    [Description("处理结果")]
    ProcessedResult,
    [Description("实时订单")]
    RealtimeOrders,
    [Description("客户画像")]
    CustomerProfile,
    [Description("数据合并")]
    DataMerge,
    [Description("规则校验")]
    RuleValidation,
    [Description("审批决策")]
    ApprovalDecision,
    [Description("订单风控")]
    OrderRiskControl,
    [Description("业务输入")]
    BusinessInputs,
    [Description("订单数据")]
    OrderData,
    [Description("画像特征")]
    ProfileFeatures,
    [Description("风险特征")]
    RiskFeatures,
    [Description("风险结论")]
    RiskConclusion,
    [Description("订单上下文")]
    OrderContext,
    [Description("订单风险说明")]
    OrderRiskNote,
    [Description("工业相机")]
    IndustrialCamera,
    [Description("图像去噪")]
    ImageDenoising,
    [Description("缺陷检测")]
    DefectInspection,
    [Description("结果归档")]
    ResultArchive,
    [Description("告警联动")]
    AlarmIntegration,
    [Description("Renderer 示例文档")]
    RendererDocument,
    [Description("连线样式示例文档")]
    EdgeStylesDocument,
    [Description("三维示例文档")]
    ThreeDDocument,
    [Description("预处理与推理")]
    PreprocessingInference,
    [Description("原始帧")]
    RawFrame,
    [Description("增强图像")]
    EnhancedImage,
    [Description("检测结果")]
    InspectionResult,
    [Description("缺陷事件")]
    DefectEvent,
    [Description("大图工作流最小节点数")]
    LargeWorkflowMinimumTwo,
    [Description("大图工作流标题")]
    LargeWorkflowTitle,
    [Description("业务场景")]
    BusinessScenario,
    [Description("未连接端口")]
    DisconnectedPort,
    [Description("刷新消息")]
    RefreshMessage,
    [Description("节点刷新")]
    NodeRefresh,
    [Description("根画布")]
    RootCanvas,
    [Description("节点数量变量")]
    VariableNodeCount,
    [Description("连线数量变量")]
    VariableEdgeCount,
    [Description("当前节点变量")]
    VariableCurrentNode,
    [Description("当前帧变量")]
    VariableCurrentFrame
}

public interface IDemoGraphTextProvider
{
    string Get(DemoGraphTextKey key);
}

public static class DemoGraphText
{
    public static IDemoGraphTextProvider English { get; } = new EnglishDemoGraphTextProvider();

    public static string Get(IDemoGraphTextProvider? provider, DemoGraphTextKey key) =>
        (provider ?? English).Get(key);

    public static string Format(
        IDemoGraphTextProvider? provider,
        DemoGraphTextKey key,
        params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(provider, key), arguments);

    private sealed class EnglishDemoGraphTextProvider : IDemoGraphTextProvider
    {
        public string Get(DemoGraphTextKey key) => key switch
        {
            DemoGraphTextKey.StageDataAcquisition => "Data acquisition",
            DemoGraphTextKey.StageProtocolParsing => "Protocol parsing",
            DemoGraphTextKey.StageDataCleaning => "Data cleaning",
            DemoGraphTextKey.StageFeatureComputation => "Feature computation",
            DemoGraphTextKey.StageRuleEvaluation => "Rule evaluation",
            DemoGraphTextKey.StageEventRouting => "Event routing",
            DemoGraphTextKey.StageMetricAggregation => "Metric aggregation",
            DemoGraphTextKey.StageStorageOutput => "Storage output",
            DemoGraphTextKey.InputNode => "Input",
            DemoGraphTextKey.TransformNode => "Transform",
            DemoGraphTextKey.OutputNode => "Output",
            DemoGraphTextKey.BasicDocument => "Basic workflow",
            DemoGraphTextKey.RawData => "Raw data",
            DemoGraphTextKey.ProcessedResult => "Processed result",
            DemoGraphTextKey.RealtimeOrders => "Real-time orders",
            DemoGraphTextKey.CustomerProfile => "Customer profile",
            DemoGraphTextKey.DataMerge => "Data merge",
            DemoGraphTextKey.RuleValidation => "Rule validation",
            DemoGraphTextKey.ApprovalDecision => "Approval decision",
            DemoGraphTextKey.OrderRiskControl => "Order risk control",
            DemoGraphTextKey.BusinessInputs => "Business inputs",
            DemoGraphTextKey.OrderData => "Order data",
            DemoGraphTextKey.ProfileFeatures => "Profile features",
            DemoGraphTextKey.RiskFeatures => "Risk features",
            DemoGraphTextKey.RiskConclusion => "Risk conclusion",
            DemoGraphTextKey.OrderContext => "Order context",
            DemoGraphTextKey.OrderRiskNote => "Review note: high-risk orders must pass rule validation before approval.",
            DemoGraphTextKey.IndustrialCamera => "Industrial camera",
            DemoGraphTextKey.ImageDenoising => "Image denoising",
            DemoGraphTextKey.DefectInspection => "Defect inspection",
            DemoGraphTextKey.ResultArchive => "Result archive",
            DemoGraphTextKey.AlarmIntegration => "Alarm integration",
            DemoGraphTextKey.RendererDocument => "Visual inspection · Renderer extension sample",
            DemoGraphTextKey.EdgeStylesDocument => "Edge styles and animation gallery",
            DemoGraphTextKey.ThreeDDocument => "2D and 3D linked preview",
            DemoGraphTextKey.PreprocessingInference => "Preprocessing and inference",
            DemoGraphTextKey.RawFrame => "Raw frame",
            DemoGraphTextKey.EnhancedImage => "Enhanced image",
            DemoGraphTextKey.InspectionResult => "Inspection result",
            DemoGraphTextKey.DefectEvent => "Defect event",
            DemoGraphTextKey.LargeWorkflowMinimumTwo => "A large workflow requires at least two nodes.",
            DemoGraphTextKey.LargeWorkflowTitle => "Production data monitoring · {0} nodes",
            DemoGraphTextKey.BusinessScenario => "Industrial production data acquisition, governance, evaluation, and output",
            DemoGraphTextKey.DisconnectedPort => "Component “{0}” port {1} is not connected.",
            DemoGraphTextKey.RefreshMessage => "State refresh #{0}",
            DemoGraphTextKey.NodeRefresh => "Refresh #{0}",
            DemoGraphTextKey.RootCanvas => "Demo root canvas",
            DemoGraphTextKey.VariableNodeCount => "Node count",
            DemoGraphTextKey.VariableEdgeCount => "Edge count",
            DemoGraphTextKey.VariableCurrentNode => "Current node",
            DemoGraphTextKey.VariableCurrentFrame => "Current frame",
            _ => key.ToString()
        };
    }
}
