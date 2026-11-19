# Kudo Avalonia 公共主题

这个项目维护所有业务系统共享的主题约定：字体、尺寸、配色、通用控件适配和公共交互样式。产品品牌资源放在业务工程的 `Themes`；只服务一个视图的样式和资源放在该视图的 `Styles` / `Resources` 属性中。

## 资源归属与入口

| 位置 | 职责 |
| --- | --- |
| `Themes/Shared/Typography.axaml` | 公共字体与字号。 |
| `Themes/Shared/Metrics.axaml` | 公共尺寸、间距、圆角。 |
| `Themes/Palettes/*.axaml` | Default、Light、Dark 及 Semi 四种配色。 |
| `Themes/Shared/Default.axaml` | 保留的资源聚合入口，维持已有资源键和主题变体。 |
| `Themes/Controls` | 基于 CodeWF / Semi / Ursa 控件主题的公共适配。 |
| `Styles/Shell/*.axaml` | 跨业务复用的窗口、表单、对话框、加载层、登录视觉、工具栏、标题栏和状态栏样式。 |
| `Styles/Shell.axaml` | 保留的样式聚合入口；子文件按原有顺序加载。 |

新增独立样式文件应按可复用的用途命名，不按单个业务 View 命名。控件模板由控件库维护，品牌差异优先通过资源覆盖或 `ControlTheme.BasedOn` 表达。`VComboBox` 继承 CodeWF 的 `CodeWFVComboBoxBaseTheme`，只覆盖下拉图标尺寸；应用须先加载 `ACSemiTheme`，再加载 `CommonTheme`。

公共字体入口为 `KudoThemeFonts.DefaultFontFamily`。Windows 7 使用系统自带的 `Microsoft YaHei`，其他平台继续使用包内的 Noto Sans SC；应用设置 Avalonia 默认字体时可使用 `KudoThemeFonts.PlatformDefaultFamilyName`，避免在 Windows 7 启动期间加载内嵌字体。

已发布的资源键和入口路径需要保持兼容。`Border.BuildModelDialogRoot` 仅为旧调用方保留，新视图使用 `BusinessDialogRoot` 并在本地设置 `Padding`。移动资源前应检查 AXAML 和 C# 调用；弹出层样式需考虑独立视觉树；用于局部 `StaticResource` 的资源应先于该视图的样式声明。

## 颜色资源

通用背景、文字、边框、状态色优先直接使用 Semi token，避免再创建一层 `App*Brush` 别名。确实需要产品级个性化时，优先在主题入口覆写 Semi token；只有无法表达具体业务语义时，才新增项目自己的资源 Key。

| 场景 | 推荐 Key |
| --- | --- |
| 普通背景 | `SemiColorBackground0` |
| 次级背景、分区背景 | `SemiColorBackground1` |
| 普通边框 | `SemiColorBorder` |
| 主文字 | `SemiColorText0` |
| 次文字 | `SemiColorText2` |
| 主色、主色浅背景 | `SemiColorPrimary`、`SemiColorPrimaryLight` |
| 成功状态 | `SemiColorSuccess`、`SemiColorSuccessLight` |
| 危险状态 | `SemiColorDanger` |
| 登录窗口边框 | `LoginWindowBorderBrush`，该资源放在 `Themes/Shared/Shell.axaml`，因为它属于壳窗口样式。 |

粗细资源放在 `Themes/Shared/Shell.axaml`。

| 资源 Key | 用途 |
| --- | --- |
| `ShellBorderThickness` | 主壳窗口、登录窗口外框粗细。 |
| `BusinessDialogTitleBarBorderThickness` | 业务对话框标题栏分隔线粗细。当前值为 `0`，表示标题栏不额外画边框，只由窗体外框统一画一圈。 |
| `QuickToolBarBorderThickness` | 快捷工具栏分隔线粗细。 |
| `TaskBarBorderThickness` | 底部任务栏分隔线粗细。 |
| `RibbonFlyoutBorderThickness` | Ribbon 弹出层边框粗细，定义在 `Themes/Shared/Ribbon.axaml`。 |

字号资源放在 `Themes/Shared/Shell.axaml`。调用方需要局部调整公共日志控件字号时，优先在 App、Window 或局部资源中覆写对应 Key，不要修改 `Kudo.AvaloniaCommon` 的用户控件源码。

| 资源 Key | 用途 |
| --- | --- |
| `CustomLogViewLogFontSize` | `CustomLogView` 正常日志区域字号，默认跟随公共 Typography Token。 |
| `CustomLogViewSmallLogFontSize` | `CustomLogViewSmall` 小日志区域字号，默认跟随公共 Typography Token。 |

## CodeWFWindow 与 Semi

`S-ComponentC/App.axaml` 中应通过下面的主题入口加载 CodeWF 样式：

```xml
<codewf:ACSemiTheme />
```

`CodeWFWindow` 的默认框架边框来自 CodeWF/Semi 资源。设备客户端常用窗口类在 `Styles/Shell.axaml` 中统一覆盖：

| 选择器 | 边框行为 |
| --- | --- |
| `codewf\|CodeWFWindow.ShellWindow` | 主壳窗口，使用 `SemiColorBorder`。 |
| `codewf\|CodeWFWindow.AppDialogWindow` | 普通应用对话框，使用 `SemiColorBorder`。 |
| `codewf\|CodeWFWindow.BusinessDialogWindow` | 业务对话框，使用 `SemiColorBorder`。 |
| `Border.AppDialogRoot` | 客户区根容器不画边框，只负责背景。 |
| `Border.BusinessDialogRoot` | 客户区根容器不画边框，只负责背景。 |

需要调整通用颜色时，优先覆写 Semi token 或公共样式，不要在单个视图里直接写颜色值。
