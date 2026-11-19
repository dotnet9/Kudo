# Kudo

Kudo 是基于 Avalonia 的开源模块化图形建模与节点编辑 SDK，内置可运行的桌面 Demo，覆盖节点属性编辑、画布设计和运行调试（Runtime 单步、断点、Watch）等场景。

> 文档约定：说明性正文使用简体中文；SDK、API、Minimap、Demo、Core、Designer、Runtime、Dock、AOT 等行业通用术语，以及包名、类型名和命令保持原样，不做生硬翻译。

当前版本为 `0.0.0.1`。SDK 源码工程统一支持 `net10.0` 和 `net11.0`（随已安装的 .NET SDK 自动选择）；Demo、测试和 Benchmark 会根据当前 SDK 自动选择可用的验证框架。所有第三方依赖均来自 nuget.org。

## 源码依赖

本仓库不使用 Git 子模块，也没有私有 NuGet 源依赖：SDK 全部源码位于 `src/`（含控件层 `Kudo.Controls`、统一主题层 `Kudo.Themes` 与停靠主题 `Kudo.Dock.Themes`），第三方库（Avalonia、Semi、Ursa、CodeWF Toolkit、SkiaSharp 等）统一在 `Directory.Packages.props` 中从 nuget.org 固定版本，克隆后直接 `dotnet restore` 即可还原。

需要产出 NuGet 包时运行 `pack.bat`（含构建、测试与格式校验的完整流程）或 `pack_quick.bat`（仅打包），产物输出到 `artifacts/packages`。

## 5 分钟上手

以 NuGet 包方式接入 Kudo：

```xml
<PackageReference Include="Kudo.Core" />
<PackageReference Include="Kudo.Avalonia" />
<PackageReference Include="Kudo.Themes" />
```

只处理模型和 JSON 时引用 `Kudo.Core`；需要桌面设计器时增加 `Kudo.Avalonia` 和 `Kudo.Themes`；需要 3D 预览时再增加 `Kudo.Avalonia.3D`。版本统一在 `Directory.Packages.props` 中维护。

```csharp
using Kudo;
using Kudo.Avalonia;

var source = new NodeModel
{
    Id = "source",
    Title = "Source",
    Position = new GraphPoint(60, 100)
}.AddPort(new PortModel
{
    Id = "out",
    Kind = PortKind.Output,
    Capacity = new PortCapacity { MaxOutgoing = null }
});
var target = new NodeModel
{
    Id = "target",
    Title = "Target",
    Position = new GraphPoint(360, 100)
}.AddPort(new PortModel { Id = "in", Kind = PortKind.Input });
var document = new GraphDocument { Nodes = [source, target] };
document.AddEdge(new EdgeModel
{
    Source = new PortReference("source", "out"),
    Target = new PortReference("target", "in"),
    TargetMarker = EdgeMarker.Arrow,
    StylePreset = GraphEdgeStylePreset.ActiveFlow
});

var canvas = new GraphCanvas { Document = document };
```

推荐通过统一的 AXAML XML 命名空间声明控件，调用方无需记忆各 UI 程序集的 CLR 命名空间：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:k="https://codewf.com">
    <k:GraphDesignerShell Document="{Binding Document}"
                          Catalog="{Binding NodeCatalog}"
                          NodeRequestedCommand="{Binding AddNodeCommand}"
                          IsReadOnly="{Binding IsReadOnly}"
                          Viewport="{Binding Viewport}"
                          ViewportOptions="{Binding ViewportOptions}"
                          GridSettings="{Binding GridSettings}"
                          RuntimeSnapshot="{Binding RuntimeSnapshot}"
                          BreakpointNodeIds="{Binding BreakpointNodeIds}"
                          SelectedNodeId="{Binding SelectedNodeId}"
                          SelectedEdgeId="{Binding SelectedEdgeId}"
                          SelectedAnnotationId="{Binding SelectedAnnotationId}" />
</UserControl>
```

`GraphDesignerShell` 已将文档、工具箱、画布、Minimap、运行状态和选择状态封装为可绑定属性；`Viewport`（Offset + Zoom）与 `ViewportOptions`（默认 10%–500%）统一双向绑定，缩放、视口、网格和选择状态不再分散维护。需要 Session、Ruler、节点工具条和 Undo/Redo 历史时，使用可绑定 `Session` 的 `GraphDocumentHost`；右侧属性面板可通过 `GraphPropertiesView.EditorContext` 绑定并随任务、节点、分组、连线或便签选择切换。统一前缀还可使用 `GraphThreeDView`、`GraphDockContentTemplate` 和 `KudoSemiTheme`。

完整安装、窗口接入、保存、Demo 操作、导出打印和领域扩展见[使用与开发手册](docs/使用与开发手册.md)；不知道该引用哪个包时先看[包选择与依赖评估](docs/包选择与依赖评估.md)。

## 项目结构

- `Kudo.Core`：不依赖界面的图模型、算法、序列化、文档会话、运行时、校验和命令契约。
- `Kudo.Avalonia`：Avalonia 画布、Designer、Dock 工作区、属性编辑器及 PNG/SVG/PDF 导出打印。
- `Kudo.Controls`：按 Arc 范式沉淀的自研控件（图标按钮等），样式随控件内聚。
- `Kudo.Dock.Themes`：Dock 停靠组件的 Semi 风格主题。
- `Kudo.Themes`：Kudo 控件、画布设计器与桌面控件的统一主题资源（Arc/Semi 映射层）。
- `Kudo.Avalonia.3D`：可选 OpenGL 3D 预览控件和软件渲染 fallback，代码命名空间为 `Kudo.Avalonia.ThreeD`。

模型编辑建议通过 `GraphDocumentEditor` 执行命令并消费 `GraphChangeSet`；直接修改公开模型后，调用 `Synchronize` 显式通知缓存和会话。开发期仍允许调整这些 API，当前不承诺兼容层。

## 构建与测试

```bash
dotnet restore Kudo.slnx
dotnet build Kudo.slnx --no-restore
dotnet test Kudo.slnx --no-build
```

Demo 发布脚本：

- `publish_win-x64.bat`：Windows x64 Native AOT 发布。
- `publish_linux-x64.bat`：Linux x64 普通自包含单文件发布。
- `publish_linux-arm64.bat`：Linux arm64 普通自包含单文件发布。
- `publish_all.bat`：依次发布以上三个平台。

## 桌面 Demo

运行可编辑的桌面 Demo：

```bash
dotnet run --project samples/Kudo.Demo.Desktop/Kudo.Demo.Desktop.csproj
```

### 模型库与任务目录

Desktop Demo 的模型库由配置文件和资源目录组成：

```text
Models/
├─ Model.xml
├─ Symbols/
│  ├─ BasicSymbols.Valve.xml
│  └─ BasicSymbols.Pump.xml
└─ Assets/
   ├─ BasicSymbols.Valve.svg
   └─ BasicSymbols.Pump.svg
```

`Model.xml` 保存模型分组并引用 `Symbols/*.xml` 图元配置，分组和图元的用户可见元数据使用 `*Key` 国际化属性。`Symbols`、`Assets` 直接平铺，文件名采用 `<领域名>.<图元名>`，资源变体采用 `<领域名>.<图元名>.<变体名>`。图元配置包含名称、节点类型、图标、默认尺寸、端口、属性和默认节点背景图等元数据，不兼容 JSON 模型库格式。带 `Resource` 的模型默认按 `Symbol` 图元模式加载：PNG 使用像素尺寸、SVG 使用 `viewBox` 计算宽高比，在 `DefaultSize` 内等比创建且初始宽高均不超过 200；该上限不参与节点创建后的手工 Resize。模型库加载时会校验所有已声明资源的路径和文件完整性；模型被拖入画布时，仅将该节点实际引用的图片复制到当前任务资源目录。

任务以目录为最小保存和迁移单元：

```text
大一统模型/
├─ Manifest.xml
├─ Task.xml
└─ Assets/
   ├─ a.png
   └─ b.png
```

`Manifest.xml` 描述包格式和入口，`Task.xml` 使用 `Kudo.Task/1` 格式，保存画布设置、节点布局、连接关系和任务相对资源路径；`Assets/` 保存任务实际使用的图片。目录工程和 `.mdl` ZIP 工程使用完全相同的内部结构。保存时校验引用文件，另存为、任务复制和草稿只打包当前 Document 引用的资源；保存、打开和草稿恢复都会拒绝缺失引用文件的残缺任务。当前 SDK 不读取 JSON 任务格式或单文件 `.graph.json`。

Windows 下运行 `publish_all.bat`，可将 `Kudo.Demo.Desktop` 发布为
`win-x64`、`linux-x64`、`linux-arm64` 应用。
`win-x64` 使用 Native AOT、VC-LTL 和 `Roots.xml` 免裁配置，Linux 使用
self-contained single-file。产物位于 `artifacts/publish/{RID}/{TFM}/{项目名}`。

性能基准使用固定 Seed 生成约 2 倍 Edge 的 100、500、2000 Node 场景，除模型、缓存和空间索引外，还覆盖固定视口下的首次 Render、连续平移和连续 Zoom。Render workload 同时包含纯即时绘制，以及约 1/3 input 类型节点嵌入轻量 Avalonia Control 两种模式，且不引入 BenchmarkDotNet：

```bash
dotnet run -c Release --project benchmarks/Kudo.Benchmarks -- --sizes 100,500,2000 --warmup 1 --iterations 3 --frames 60 --output artifacts/benchmarks/performance-report.md
```

Render workload 通过 Avalonia Headless + Skia 渲染完整 Visual Tree，仅用于同一机器、同一配置下的 CPU / 离屏回归；结果不等同于桌面窗口合成、VSync、GPU 或 OpenGL 性能。

发布前手工执行 `--smoke` 验证基准入口可运行，不使用易受机器负载影响的绝对耗时作为门禁。

## 本地打包验收

Windows 下运行 `pack.bat net11.0` 或 `pack.bat net10.0`。脚本会依次执行对应目标框架的 Release Restore、Build、全部测试、格式与 Analyzer 校验、Benchmark Smoke 和 6 个 SDK 工程打包。产物位于 `artifacts/packages`。

1. `src` 下 6 个 SDK 工程均可生成包含所选目标框架资产的本地包产物。
2. 每个包包含本 README、MIT License 元数据和正确的 GitHub 仓库地址。
3. Desktop Demo 兼作功能验收入口，Ribbon 可按需加载 Renderer、Edge Styles、3D 和大图性能示例。

1.0 功能范围：

- Desktop Demo 使用 CodeWFWindow 作为成熟窗体外壳，保留自定义 TitleBar 内容和系统窗口操作。
- 顶部 Ribbon 风格分类命令区使用 Ursa `IconButton` 和 Semi Icon，包含文件、建模、布局、运行和视图操作，业务按钮统一绑定 ViewModel Command，并支持双击页签收起。
- Demo 默认使用 Semi 浅色主题；`KudoSemiTheme` 统一 Canvas、Minimap 和 Designer 面板的背景、文字、边框及语义色。
- Kudo 的控件样式保留在 SDK 主题包中，`Tokens` 定义可覆盖资源，`Styles` 定义复用控件外观。属性面板使用 `KudoPropertiesFontSize`，默认 `12`；业务主题可在加载 `KudoSemiTheme` 后覆盖该键，主题包不依赖业务系统提供 `Fonts.Size.Content`。
- SDK 和 Desktop Demo 使用 JSON 语言资源提供 `en-US`、`zh-CN`；SDK 默认及缺失 Key 回退为英文，Demo 首次启动跟随操作系统，用户选择后保存语言偏好并在下次启动恢复。
- 工作区采用左、中、右三栏：左侧上下显示任务管理器和分组模型库，中间上下显示 Document 画布和日志 / 调试 Tool，右侧显示上下文属性。
- Demo 中一个任务对应一个 `GraphDocumentSession`；选择任务显示任务属性，选择画布组件或 Edge 后切换对应属性。
- `GraphDocumentDraftStore` 按 Session 原子保存 `Task.xml`、草稿元数据和任务资源，支持恢复原任务目录并跳过损坏草稿。
- `GraphDocumentDraftAutosaveService` 防抖保存连续编辑，正式保存后清理草稿，并支持批量恢复与显式放弃。
- `GraphDocument.AddNodes`、`AddEdges` 和 `AddGroups` 支持先校验、后一次性写入的批量装载；Desktop Demo 可从 Ribbon 切换生成 100 / 500 / 1000 / 5000 节点的“生产数据监控”性能示例。
- `GraphCanvas` 默认启用低缩放 LOD：依次简化节点和连线、隐藏标签与端口，并对默认 Renderer 的总览内容使用受尺寸约束的位图缓存；平移和缩放时仅重绘动态选择、断点与 Runtime 状态。自定义 Renderer、内嵌 Avalonia Control 和 OpenGL Control 不会被静默替换。
- `GraphDocumentList` 统一展示活动任务、Dirty、Runtime、组件、连线和分组数量，并通过右键菜单发出重命名、复制和关闭请求。
- `GraphValidator` 聚合结构与业务校验规则；“校验结果”Tool 提供问题数量 Badge 和严重级别过滤，可逐条定位，也可批量选择当前筛选结果涉及的节点、连线端点和分组成员。
- `GraphNodePalette` 基于 `IGraphNodeCatalog` 提供可复用的分组、搜索和响应式模型宫格；Desktop Demo 从 `Models/Model.xml` 加载模型及图标，模型卡片根据可用宽度自动换列，拖入 Canvas 时显示节点尺寸预览、Grid Snap 与智能参考线，释放后在预览位置创建并导入所需资源。
- Docking 的 `GraphWorkspaceSettingsStore` 持久化 Ribbon 展开状态、Dock Tool 可见性以及 Grid、Snap to Grid、参考线和常用间距；使用原子替换保存，首次启动不主动生成配置文件。
- Desktop Demo 的 Ribbon 使用靠左 `TabStrip` 分组；切换页签会更新命令区并自动展开，双击页签可折叠或展开，不再占用独立收起按钮。
- 使用 DocumentDock 同时打开和切换多个画板，每个画板保持独立的 Viewport、Dirty 和 Runtime 状态。
- Designer 的 `GraphDocumentView` 组合 Canvas、水平/垂直坐标标尺、Minimap、Viewport、Undo/Redo、Runtime 同步和模型拖放，可直接作为 Dock Document 内容；标尺随统一视口变更和自适应网格密度同步刷新。
- 每个 Document 使用独立 `GraphRuntimeSession` 执行 Start/Pause/Step/Stop/Reset 和周期 Snapshot 刷新；切换或关闭画板不会改变其他 Document 的运行状态。
- `GraphRuntimeCommandAvailability` 统一计算命令可用性；`GraphRuntimeCommandExecutor` 返回完成、断点、Fault 或异常结果。
- Runtime 调试支持 F5 运行或暂停、F6 单步、F9 切换所选节点断点、Shift+F5 停止；调用栈、变量 / Watch 和执行记录均按 Document 独立维护。
- “断点”Tool 支持逐条定位、移除、全部清除和批量选择所有断点节点。
- Designer 的 `GraphBreakpointsView` 通过宿主回调定位节点、移除或清空断点，Tool 内容区不重复显示标题。
- Designer 的 `GraphVariablesView` 管理 Watch、展示 Snapshot 变量，并通过 `VariableRequested` 请求宿主定位关联节点。
- 日志 Tool 使用 `CodeWF.Log.Avalonia` 的 `LogView`，统一显示编辑与 Runtime 结构化事件；每个事件由 LineTemplate 显式换行，Runtime 日志包含 Document 标题、Tick 和节点 ID。
- 每个 Document 维护独立的 Undo/Redo 历史和保存点；Ribbon 按钮、Ctrl+Z、Ctrl+Y 与 Ctrl+Shift+Z 始终作用于活动画板。
- 按住空格后使用左键拖动，或直接使用鼠标中键、右键拖动平移画布；普通左键拖动空白区域仍用于框选，滚轮用于缩放。
- `GraphCanvas.IsReadOnly` 可将画板切换为查看模式：平移、缩放、选择、Runtime 状态和属性查看继续可用，节点拖放、移动、尺寸调整、连线、Waypoint、删除、Clipboard 写入、属性写入及 Undo/Redo 会被统一禁止；Desktop Demo 在“视图”Ribbon 提供切换入口。
- `GraphCanvas.EditPolicy` 接受宿主实现的 `IGraphEditPolicy`，可按 `GraphEditOperation` 和当前 Node、Edge、Group、Port 上下文拒绝选择、移动、Resize、连接、重连、删除、分组、折叠或 Waypoint 编辑；拒绝原因通过 `LastEditRejectionReason` 和 `GraphEditRejected` 暴露给宿主。只读模式仍允许策略认可的选择操作。
- `GraphDocumentView.CanExecuteCommand`、`TryAuthorizeCommand` 和 `TryExecuteCommand` 会通过 `GraphEditRequestFactory` 将 Add、Paste、布局、路由和属性等 `IGraphCommand` 分类为相同的策略操作；Undo 按逆向修改重新分类，Redo 按正向修改再次校验。Demo 会在导入模型资源或复制跨任务资源前预授权，任务重命名也经过相同门禁；策略拒绝时不会污染任务、改写剪贴板或误报成功。
- `GraphCanvas.BackgroundRenderer` 接受宿主实现的 `IGraphBackgroundRenderer`，通过只读 `GraphBackgroundRenderContext` 自定义背景和网格绘制；默认实现保持当前自适应主次网格行为，Renderer 不获得 Canvas 或 Document 的修改入口。
- `GraphCanvas.NodeRenderer` 接受宿主实现的 `IGraphNodeRenderer`，可按节点模型、选择、Runtime 和断点状态绘制节点外观；端口及 `NodeControls` 内容保持独立，节点内嵌任意 Avalonia Control 的能力不受影响。
- `GraphCanvas.PortRenderer` 接受宿主实现的 `IGraphPortRenderer`，可按所属节点、端口方向、连线起点和候选状态自定义端口与标签外观；端口命中和连接校验仍由 Canvas 保持一致。
- `GraphCanvas.EdgeRenderer` 接受宿主实现的 `IGraphEdgeRenderer`，按 Geometry、RuntimeOverlay、Label 三层替换连线路径、Marker、流动信号和标签；只读 Context 提供 `GraphEdgeRoute`，其公开 `Draw` 方法可复用 SDK 的路由与 Marker 绘制。
- `GraphCanvas.GroupRenderer` 接受宿主实现的 `IGraphGroupRenderer`，可替换分组背景、标题、折叠状态和跨组连线代理端口；代理端口只读描述包含关联 Edge、端点类型和屏幕位置。
- `GraphCanvas.InputBehaviors` 支持按优先级注册 `IGraphInputBehavior`，统一观察 Pointer、Wheel 和 Keyboard 输入，返回 `PreventDefault` 可阻止 Canvas 默认交互；释放注册句柄即可恢复默认行为，阻止活动指针序列时会回滚交互并释放 Pointer Capture。
- `GraphCanvas.SelectedNodeIds` 与 `SelectedGroupIds` 是只读可观察的 Document 有序快照，可通过 `Mode=OneWayToSource` 将完整节点和分组选择直接同步给 ViewModel；`GraphDesignerShell` 与 `GraphDocumentHost` 同样暴露这些属性。`SelectionChangedCommand`、`GraphEditCommittedCommand`、`GraphEditRejectedCommand` 和 `ConnectionValidationChangedCommand` 也可直接在 AXAML 绑定，分别提供完整选择快照、已提交命令、权限拒绝和连接校验结果，无需在代码隐藏中订阅事件。
- Desktop Demo 的“文件 → 扩展示例”会创建一条完整的工业视觉质检流程，并同时应用自定义 Background、Node、Port、Edge、Group Renderer，便于直接验证扩展契约、Runtime 状态和现有编辑能力能否共存。
- Desktop Demo 的“文件 → 连线样式”连续展示 12 条状态与动画预设；`GraphEdgeStylePreset` 可通过 MVVM 在运行时切换颜色、Dash、Glow 和动画，`GraphCanvas` 自动刷新且无需手工调用 `RefreshGraph()`。`GraphEdgeAppearance` 继续支持链式自定义，并随 Document JSON 持久化。
- 通过交互式 Minimap 浏览文档并查看当前可见区域。
- `GraphCanvas.FitToSelection` 可将当前所选节点、完整分组边界或单独所选 Edge 自动居中并缩放到可见区域；Desktop Demo 在“布局”Ribbon 提供“适应选择”，没有选择时自动禁用。
- `GraphCanvas`、`GraphDocumentView` 与 `GraphDesignerShell` 统一实现 `IGraphViewportHost`：通过 `Viewport`、`ViewportOptions`、`ViewportChanged` 观察完整状态，并使用 `SetZoom`、`ZoomAt`、`ZoomBy`、`PanBy`、`FitToDocument`、`FitToSelection` 等语义动作导航。默认范围为 10%–500%，缩放后锚点对应的图坐标保持不变；滚轮和 Desktop Demo 使用同一实现。
- 多选 Node 会显示共同外框和旋转手柄，可围绕选区中心整体旋转；预览期间实时更新，Esc 取消，完成后作为单条 Undo/Redo 记录。
- `GraphCanvas.SelectNodesInViewport` 复用 Node 空间索引选择当前视口内相交或完全包含的节点；Desktop Demo 提供“选择视口”，用于大图局部编辑。
- `GraphCanvas.InvertNodeSelection` 按 Document 顺序选择当前未选中的节点，并继续遵守统一选择策略；Desktop Demo 提供“反选”入口。
- `GraphCanvas.SelectNodesOfSameType` 以主选节点的 `Type` 批量选择同类节点，适用于同类组件配置和检查；Desktop Demo 提供“选择同类”入口。
- `GraphTraversal.GetAdjacentNodeIds` 与 `GraphCanvas.SelectAdjacentNodes` 支持按上游、下游或无向关系选择一跳邻居；Desktop Demo 提供“选择相邻”入口。
- `GraphTraversal.GetReachableNodeIds` 与 `GraphCanvas.SelectReachableNodes` 支持从当前节点按 Upstream、Downstream 或 Connected 遍历并选择图分支；Desktop Demo 在“建模 → 选择”提供“选择上游 / 下游 / 分支”。
- `GraphTraversal.GetShortestPathNodeIds` 使用 BFS 查找有向或无向最短节点路径；`GraphCanvas.SelectShortestPathBetweenSelectedNodes` 以主选节点为目标选择两个节点之间的最短路径，Demo 仅在恰好选择两个节点时启用“选择路径”。
- `CycleDetector.FindCycleNodeIds` 使用非递归 DFS 返回首个有向环路且不混入下游节点；`GraphCanvas.SelectFirstCycle` 与 Demo“定位环路”可直接选择环路节点，无环时保持现有选择。
- `GraphStructureQueries` 按有效 Edge 入度 / 出度区分 Source、Sink 和 Isolated 节点；`GraphCanvas.SelectNodesByRole` 与 Demo“源节点 / 汇节点 / 孤立节点”可快速定位流程入口、出口和未接线模型。
- `GraphTraversal.GetLongestDirectedPathNodeIds` 对 DAG 使用拓扑序和动态规划计算最长有向节点链；`GraphCanvas.SelectCriticalPath` 与 Demo“关键路径”用于定位最长处理链，存在环路时明确返回不可用。
- `GraphCanvas.NodeControls` 可按 `NodeModel.Type` 注册 Control Factory，在节点内容区嵌入任意 Avalonia `Control`；`GraphNodeControlContext` 同步选择、断点和 Runtime 节点状态，嵌入控件可保留自身输入交互。
- `GraphCanvas` 默认按 Viewport 加 160 屏幕像素 Buffer 裁剪 Node、Group、Edge 和 Control Host；可通过 `ViewportCullingMargin` 调整 Buffer，或通过 `IsViewportCullingEnabled` 关闭。离开视口的嵌入 Control 只暂停布局和显示，不会被移除或重建，因此内部状态与 OpenGL Control 实例会保留。
- `Kudo.Avalonia.3D` 提供真实 OpenGL Renderer，并在初始化失败时按配置切换软件 fallback；主 Demo 的“文件 → 3D 示例”同屏展示 2D Document、独立 3D 预览和 `transform` 节点内嵌的 `GraphThreeDView`。启动参数 `-type software` 会固定使用软件 Renderer。
- 渲染节点标题和端口标签；双击默认节点标题可使用轻量 `TextBox` 原位编辑，Enter 或失焦提交，Esc 取消，并接入统一编辑策略及 Undo/Redo。
- 单击选择节点，按住 Shift 单击切换多选；在空白区域按住左键拖动进行框选，Shift + 框选扩展当前选择。
- 支持 Ribbon 或 Ctrl+A 执行 Select All；按 Esc 可取消正在进行的节点拖动、尺寸调整、连线、端点重连、框选或画布平移，无活动交互时清除当前选择。节点内嵌 Control 的 Esc 输入由控件自身处理。
- 选中节点后可用方向键按 1 个图坐标单位微移，按住 Shift 时以 10 个单位加速；一次按键形成一条可撤销、可重做的历史记录。
- 单选节点后显示八方向尺寸手柄；拖动手柄实时调整位置和尺寸，Shift 保持宽高比，Alt 从中心对称缩放，两者可组合使用；最小尺寸可通过 `GraphCanvas.MinimumNodeSize` 配置，并支持 Undo/Redo。
- 可将两个以上节点创建为非嵌套分组；分组边界随成员位置自动计算，点击分组背景可选择并整体拖动成员，双击或通过右键菜单折叠/展开，Ctrl+G 创建分组，Ctrl+Shift+G 取消分组。
- 折叠分组会隐藏成员节点及其嵌入 Control，组内连线不再绘制，跨组连线通过分组边缘的输入/输出代理端点继续显示；状态可持久化并支持 Undo/Redo。
- 节点和画布提供右键快捷菜单；短按右键打开常用选择、Clipboard、删除、分组、连线路由、对齐和分布命令，拖动右键仍用于平移。
- 批量拖动或删除所选节点，并将一次操作作为一条命令写入当前 Document 的 Undo/Redo 历史。
- GraphCanvas 支持随 Zoom 自适应密度的主/次 Grid，可切换显示并配置 Grid 间距。
- 节点和多节点拖动支持 Snap to Grid；拖动时可按主节点计算水平、垂直智能参考线，参考线吸附优先于 Grid Snap。
- GraphCanvas 根据 Runtime Snapshot 显示节点运行、暂停和故障状态，高亮活动 Edge，并沿 Direct、Bezier、Orthogonal、Manual、SmartOrthogonal 实际路径显示流动信号。
- 从一个端口拖动到另一个端口，经过连接校验后创建连线；靠近兼容端口时按固定屏幕距离自动吸附并显示候选高亮，距离可通过 `GraphCanvas.ConnectionSnapDistance` 配置。
- 选中已有连线后显示起点和终点手柄；拖动任一手柄到兼容端口可重连，保留原连线 ID、Label、路由和属性，并支持 Undo/Redo。
- 渲染 Direct、Bezier、Orthogonal、Manual 和 SmartOrthogonal 连线路由；SmartOrthogonal 使用可配置障碍 Padding、转弯代价和搜索上限绕开普通节点及折叠 Group，搜索超限时保留路由类型并退回稳定正交折线。节点几何变化时根据旧、新障碍范围增量失效端点或路径受影响的 SmartOrthogonal Edge，远处缓存路径保持不变；无法证明局部范围的自定义障碍 Provider 和结构变化自动回退全量智能路由失效。Manual Edge 的有序 Waypoint 使用图坐标持久化；所有路由统一参与绘制、命中、Label、Runtime 动画、Minimap 和文档可见范围计算；连线起点和终点可分别使用 None、Arrow、Circle 或 Diamond 标记。
- 选中 Manual Edge 后显示固定屏幕尺寸的 Waypoint 手柄；双击线段添加、拖动手柄移动、双击手柄删除，编辑支持 Undo/Redo，拖动中按 Esc 可恢复原位置。
- 沿 Direct、Bezier、Orthogonal、Manual 和 SmartOrthogonal 实际路径居中渲染 Edge Label。
- 选择连线并按删除键或退格键删除。
- 从 Demo 命令区添加输入、转换和输出节点。
- 节点新增、删除、拖动和尺寸调整，连线创建、端点重连、删除和路由切换，以及分层布局均可撤销和重做。
- Designer 的 `GraphPropertiesView` 通过 `GraphPropertyEditorContext` 接入宿主 Session、选择、断点和命令回调，统一编辑任务、组件、分组、Edge 和便签属性；单分组可即时修改标题、内边距、折叠状态和扩展属性，多分组只显示摘要。
- 节点可配置 `Auto`、`Card` 和 `Symbol` 外观模式，以及任务相对背景图、填充方式和透明度；`Symbol` 常态只绘制等比素材，不绘制卡片填充、常态描边或内嵌标题，选中和运行态仍保留临时轮廓。属性面板支持切换模式、选择、替换和清除图片，所有外观修改均进入当前任务的 Undo/Redo 历史。
- 支持节点、连线或所选子图的 Copy、Cut、Paste 和 Duplicate；子图会自动包含所选节点之间的 Edge 和完整包含的节点分组，Ctrl+C、Ctrl+X、Ctrl+V、Ctrl+D 可跨 Document 使用，粘贴时自动重建 Node、Port、Edge 和 Group ID，并只同步所选节点实际引用的任务资源。
- 应用基础分层布局、使图适应可见区域以及重置视图。
- 对所选节点执行左侧、水平居中、右侧、顶部、垂直居中和底部对齐，或按节点边界进行水平、垂直等距分布。
- 在状态栏查看节点数、连线数、分组数、当前选择和连接校验结果。
- 图结构校验覆盖重复 ID、缺失端点、端口方向、数据类型、连接数量限制和分组成员关系；业务规则继续通过 `IGraphValidationRule` 扩展。
- 将任务保存为包含 `Manifest.xml`、`Task.xml` 与 `Assets/` 的目录，或保存为同结构的 `.mdl` ZIP 文件；Save As、任务复制与跨任务粘贴只同步实际引用资源并校验缺失文件。
- 使用 Ursa OverlayMessageBox 确认放弃未保存画板，防止关闭画板或退出 Demo 时静默丢失修改。

Demo 默认打开“基础流程”和“订单风控”两个画板；“订单风控”由实时订单、客户画像、数据汇聚、规则校验和审批决策组成完整业务链，SmartOrthogonal Edge 会绕过同属风控分支的规则校验节点，可直接验证智能避障及不同连线路由。

节点分组暂不支持嵌套。展开尺寸不写入 JSON，而是由成员节点位置、尺寸、标题区和 `Padding` 动态计算；折叠尺寸由 Canvas 使用固定紧凑边界展示。

用户调整工作区后，Demo 将配置保存到平台本地应用数据目录下的 `Kudo.Avalonia/Demo/workspace.json`。未保存任务防抖写入同目录的 `workspace.drafts`，正式保存或确认放弃后自动清理，重启 Demo 时异步恢复。

## Runtime 单步运行

`IGraphRuntimeStateProvider.GetSnapshotAsync()` 使用 `ValueTask` 和 `CancellationToken`，可直接等待 I/O，不需要 `Task.Run`。Scheduler 会跳过尚未完成的重叠刷新，避免慢 Provider 造成 Tick 堆积。

Runtime 生命周期命令按 Session 串行执行。Stop、Pause 或 `DisposeAsync()` 会取消正在等待 Provider 的命令；异步释放会等待 Provider 退出，避免关闭任务后残留刷新回调。

1. `GraphRuntimeCommandAvailability.FromStatus()` 用于同步 Ribbon、Toolbar 和快捷键状态。
2. `GraphRuntimeCommandExecutor.ExecuteAsync()` 统一执行五种生命周期命令。
3. 执行结果区分 Completed、BreakpointHit、Faulted 和 Failed。
4. 调用方主动取消时继续抛出 `OperationCanceledException`。

`StepAsync()` 只请求一次 Snapshot，不启动周期 Scheduler，完成后会话保持 `Paused`。`TickCount` 由会话统一维护；`StopAsync()` 和 `ResetAsync()` 会将 Tick 清零。StateProvider 如需异步清理自身状态，可实现 `IGraphRuntimeResettableStateProvider.ResetAsync()`。

节点断点由 `GraphRuntimeSession` 按 Document 独立维护，不写入 `GraphDocument`。StateProvider 可通过 `GraphRuntimeSnapshot.ActiveNodeId` 报告当前执行节点；该节点命中 `BreakpointNodeIds` 时，会话会停止 Scheduler、切换为 `Paused` 并触发 `BreakpointHit`。GraphCanvas 使用红色圆点显示断点，并用绿色边框标记当前活动节点。

`BreakpointsChanged` 在断点集合实际变化后触发。Desktop Demo 的断点 Tool 可定位节点、移除单个断点或清空当前 Document 的全部断点。

`DiagnosticEmitted` 会发布结构化 `GraphRuntimeDiagnostic`，包含稳定 Code、级别、Runtime 状态、Tick、节点、时间、消息和可选异常。当前覆盖 Snapshot 刷新、暂停、单步、停止、重置、断点命中和故障；调用方可按 Document Session 订阅和展示，不必解析 Snapshot 文本。

1. `RecentDiagnostics` 保存当前 Session 最近的有界执行记录，默认最多 200 条。
2. `DiagnosticsChanged` 同步新增和清空操作，切换 Document 时互不影响。
3. Demo 的“执行记录”Tool 按时间倒序展示，支持级别过滤和数量 Badge，可逐条定位，也可批量选择当前筛选结果中带节点 ID 的记录。

StateProvider 可通过 `GraphRuntimeSnapshot.CallStack` 返回 `GraphRuntimeStackFrame`。每个 Frame 包含节点 ID、显示名和可选 Detail；调用栈只属于 Runtime Snapshot，不写入图文档。Designer 的 `GraphCallStackView` 通过 `FrameRequested` 请求宿主定位节点。

StateProvider 可通过 `GraphRuntimeSnapshot.Variables` 返回 `GraphRuntimeVariable`。Pause、Breakpoint 和 Fault 保留变量；Stop 和 Reset 清空变量。

1. `WatchExpressions` 属于 Runtime Session，切换 Document 时互不影响。
2. `WatchResults` 属于 Snapshot，Stop 和 Reset 清空结果但保留表达式。
3. Provider 可实现 `IGraphRuntimeWatchEvaluator` 求值；未实现时按变量名匹配。
4. `GraphVariablesView` 支持添加、删除 Watch，展示值或单项错误，并允许宿主定位变量关联节点。

```csharp
await runtimeSession.StepAsync();
Console.WriteLine(runtimeSession.TickCount);

await runtimeSession.ResetAsync();

runtimeSession.AddBreakpoint("transform-1");
runtimeSession.BreakpointHit += (_, e) => Console.WriteLine(e.NodeId);
runtimeSession.DiagnosticEmitted += (_, e) =>
    Console.WriteLine($"{e.Diagnostic.Code}: {e.Diagnostic.Message}");
```

## 设计文档

- [使用与开发手册](docs/使用与开发手册.md)
- [包选择与依赖评估](docs/包选择与依赖评估.md)
- [架构与设计](docs/架构与设计.md)
- [性能优化](docs/性能优化.md)
