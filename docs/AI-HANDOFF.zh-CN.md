# AgenticUI.NET AI 开发交接文档

> 本文档的目标读者是接手本仓库继续开发的 AI 编程代理。开始工作前请完整阅读本文，
> 并先阅读最新的 `docs/PROJECT-MEMORY.zh-CN.md`，然后阅读 `README.md`、
> `docs/architecture.zh-CN.md`、
> `docs/local-protocol.zh-CN.md` 和 `SECURITY.md`。

## 1. 项目身份

- 项目名称：`AgenticUI.NET`
- GitHub：<https://github.com/Z18393520308/AgenticUI-NET>
- 当前稳定版：`0.2.1`
- 当前默认分支：`main`
- 技术栈：C#、WPF、Windows Forms、Named Pipe、JSONL
- 目标框架：`.NET 8`、`.NET Framework 4.8`
- 开源许可证：`AGPL-3.0-only`，另提供商业许可路径
- 发布状态：四个 `0.2.1` NuGet 包、GitHub Release 和官网均已发布

项目不是普通的主题或皮肤控件库。它的核心目标是让 AI Agent 能够：

1. 通过稳定 ID 发现和定位桌面控件。
2. 读取控件类型、状态和支持的语义动作。
3. 高亮控件并向用户显示步骤编号和提示。
4. 记录用户的语义操作顺序和事件日志。
5. 通过本机安全通道远程触发控件动作。
6. 将来让 WPF、WinForms 和 Web 使用同一套语义协议。

核心抽象如下：

```text
稳定控件身份
    ↓
控件描述与状态
    ↓
语义动作（click、setText、selectItem……）
    ↓
语义事件（clicked、textChanged、selectionChanged……）
    ↓
日志、录制回放、AI Agent、本机远程控制台
```

## 2. 已确认的产品决策

以下决定来自项目发起人，不要擅自改变：

- 首版同时支持 `.NET 8` 和 `.NET Framework 4.8`。
- 同时提供替换式 AgenticUI 控件和原生控件附加接入。
- 远程控制首版只做本机，不开放 TCP、局域网或互联网。
- 控制端可能是 AI Agent、调试控制台、企业服务端或另一客户端。
- 默认记录语义事件；详细模式才记录按下、松开、焦点等底层事件。
- 控件 ID 使用“手动稳定 ID + 自动临时 ID”。
- 日志只保存在本地。
- 敏感文本默认脱敏，但允许应用显式关闭脱敏。
- 高亮能力全部抽象，首版实现描边、编号和提示气泡。
- 需要操作录制和回放。
- 提供原生外观和可选现代主题。
- 项目名称固定为 `AgenticUI.NET`。
- 需要可视化控制台。

## 3. 当前完成情况

### 3.1 已完成

- WPF 和 WinForms 替换式控件：
  - `AgenticButton`
  - `AgenticTextBox`
  - `AgenticCheckBox`
  - `AgenticRadioButton`
  - `AgenticComboBox`
- 原生控件接入：
  - WPF：`AgenticProperties.Enabled="True"`
  - WinForms：`AgenticControlBinder.Attach(...)`
- 稳定 ID、临时 ID、控件注册表和状态快照。
- 语义动作和语义事件。
- 本地事件广播和单调递增事件序号。
- JSONL 审计日志。
- 敏感字段默认脱敏。
- 用户语义操作录制和命令回放。
- WPF `AdornerLayer` 高亮。
- WinForms 窗体前景、点击穿透高亮覆盖层。
- 本机 Named Pipe 服务端和客户端。
- 256 位随机令牌认证。
- `.NET 8` 下的 `CurrentUserOnly` 管道限制。
- 请求 ID、并发请求匹配、独立事件接收循环。
- WinForms / WPF Workbench 示例。
- WinForms / WPF Remote Console。
- GitHub Actions Windows 构建、测试和打包。
- 四个 NuGet 预览包和符号包的打包配置。

### 3.2 已修复的重要问题

不要重新引入这些问题：

1. **Workbench 左侧控件被截断**
   - 原因：`SplitContainer.SplitterDistance` 在窗口完成布局前设置，被默认尺寸夹窄。
   - 现状：窗口 `Shown` 后再设置分隔宽度。

2. **WinForms 高亮只有上下边**
   - 原因：高亮画在父容器底层，子控件重绘覆盖左右边。
   - 现状：使用添加到顶层窗体的点击穿透覆盖层。

3. **WinForms 高亮时报“控件不支持透明的背景色”**
   - 原因：先设置 `Color.Transparent`，后启用
     `ControlStyles.SupportsTransparentBackColor`。
   - 现状：先 `SetStyle(...)`，再设置透明背景。

4. **下拉列表不能远程点击或选择**
   - 现状：支持 `click`、`openDropDown`、`closeDropDown`、`selectItem`。
   - Workbench 和 Remote Console 都有“选择下一项”操作。

5. **Workbench「点击」对文本框/单选/复选报不支持**
   - 原因：这些控件原先只暴露 `setText` / `setChecked`，未声明 `click`。
   - 现状：`click` 对文本框聚焦、单选框选中、复选框切换。

6. **WPF 命令后日志记录抛跨线程异常**
   - 原因：事件总线/`Describe` 可能在非 UI 线程读取 DependencyProperty。
   - 现状：`WpfControlAdapter.Describe`/`ExecuteAsync` 统一通过 Dispatcher 访问。

7. **WinForms 远程枚举控件抛跨线程异常**
   - 原因：Named Pipe 后台线程调用 `Describe()` 访问控件属性。
   - 现状：`WinFormsControlAdapter.Describe` 在 `InvokeRequired` 时切回 UI 线程。

8. **只有 .NET 9 SDK 的机器无法打开解决方案**
   - 原因：曾使用 `global.json` 固定 `8.0.100 + latestPatch`。
   - 现状：已经删除 `global.json`。
   - 不要重新加入限制具体 SDK 补丁版本的 `global.json`。

## 4. 仓库结构

```text
AgenticUI.NET.sln
Directory.Build.props
Directory.Build.targets
README.md
SECURITY.md
CHANGELOG.md
.github/workflows/ci.yml

src/
├── AgenticUI.Core/
├── AgenticUI.Remote/
├── AgenticUI.Wpf/
└── AgenticUI.WinForms/

samples/
├── WinForms/
│   ├── AgenticUI.Workbench.WinForms/
│   └── AgenticUI.RemoteConsole.WinForms/
└── Wpf/
    ├── AgenticUI.Workbench.Wpf/
    └── AgenticUI.RemoteConsole.Wpf/

tests/
├── AgenticUI.Core.Tests/
└── AgenticUI.WinForms.Tests/

docs/
├── architecture.zh-CN.md
├── local-protocol.zh-CN.md
└── AI-HANDOFF.zh-CN.md
```

## 5. 各项目职责和关键入口

### 5.1 `AgenticUI.Core`

框架无关，不允许引用 WPF 或 WinForms。

关键文件：

- `Protocol.cs`
  - `AgenticActions`
  - `AgenticEvents`
  - `AgenticControlDescriptor`
  - `AgenticEvent`
  - `AgenticCommand`
  - `AgenticCommandResult`
  - `AgenticJson.Options`
- `Abstractions.cs`
  - `IAgenticControl`
  - `IAgenticEventSink`
  - `IAgenticCommandAuthorizer`
- `AgenticControlRegistry.cs`
  - 使用弱引用保存控件。
  - 同一进程不允许两个活动控件使用同一个稳定 ID。
- `AgenticEventBus.cs`
  - 进程内广播。
  - 为事件分配单调递增序号。
- `AgenticCommandDispatcher.cs`
  - 查找控件。
  - 验证动作是否在 `descriptor.Actions` 中。
  - 调用授权器。
  - 执行控件命令。
- `AgenticLogRecorder.cs`
  - JSONL 审计日志。
  - 默认过滤详细事件。
  - 对敏感控件的文本和值脱敏。
- `AgenticInteractionRecorder.cs`
  - 将用户语义事件转换成可回放命令。
  - 默认不录制敏感文本。
- `AgenticReplay.cs`
  - 加载 JSONL 命令并顺序回放。

### 5.2 `AgenticUI.Remote`

关键文件：

- `RemoteProtocol.cs`
  - Named Pipe 请求/响应消息模型。
  - 顶层 `requestId` 用于响应关联。
- `AgenticNamedPipeServerOptions.cs`
  - 是否需要认证。
  - 自定义令牌。
  - 最大消息长度。
- `AgenticNamedPipeServer.cs`
  - 接受多个本机连接。
  - 首条消息认证。
  - 枚举控件和分发命令。
  - 向已认证客户端广播事件。
- `AgenticNamedPipeClient.cs`
  - 连接后完成认证。
  - 后台接收循环。
  - 使用并发字典匹配响应。
  - 通过 `EventReceived` 暴露广播事件。

默认管道名为 `AgenticUI.NET`。不要在没有新安全设计的情况下加入 TCP 服务。

### 5.3 `AgenticUI.Wpf`

关键文件：

- `AgenticProperties.cs`
  - 原生 WPF 控件的附加属性接入。
- `Controls.cs`
  - 五种替换式控件。
- `WpfControlAdapter.cs`
  - 注册控件、监听事件、生成描述。
  - 在 Dispatcher 上执行远程动作。
  - 下拉列表使用 `IsDropDownOpen`。
- `HighlightAdorner.cs`
  - 绘制完整边框、编号和提示气泡。
- `Themes/ModernTheme.xaml`
  - 可选现代主题，不应强制合并进 `App.xaml`；由 Workbench 在运行时按需加载/移除。

演示项目：`samples/Wpf/AgenticUI.Workbench.Wpf`
- 默认管道名：`AgenticUI.NET.Wpf`（避免与 WinForms Workbench 冲突）。
- 根布局使用 `AdornerDecorator`，否则高亮无法附着。
- 同时演示替换式控件与原生控件附加属性接入。
- 演示区有「原生外观 / 现代主题」下拉；现代主题时合并 `ModernTheme.xaml`。

演示项目：`samples/Wpf/AgenticUI.RemoteConsole.Wpf`
- 默认管道名预填 `AgenticUI.NET.Wpf`；令牌为明文输入；枚举成功后显示控件数量。

### 5.4 `AgenticUI.WinForms`

关键文件：

- `AgenticControlBinder.cs`
  - 给现有 WinForms 控件添加 AgenticUI 能力。
- `AgenticControlOptions.cs`
  - ID、显示名称、敏感标记、步骤编号和提示。
- `Controls.cs`
  - 五种替换式控件。
- `WinFormsControlAdapter.cs`
  - 在 WinForms UI 线程执行动作。
  - 监听点击、文本、选择、下拉打开/关闭等事件。
  - 远程 JSON 参数可能是 `JsonElement`，转换代码必须兼容字符串表示。
- `WinFormsHighlight.cs`
  - 将覆盖层添加到顶层窗体并 `BringToFront()`。
  - 覆盖层使用 `Region` 只保留边框、编号和提示区域。
  - 使用 `WS_EX_TRANSPARENT` 和 `HTTRANSPARENT` 实现点击穿透。
- `AgenticModernTheme.cs`
  - `AgenticUiTheme.Native` / `Modern`；`Apply(root, theme)` 可切换。
  - 现代主题会重写按钮/输入框样式；原生主题恢复系统视觉样式。
  - Workbench 演示区有主题下拉框；切换后若有大标题，宿主需自行保留字号。
  - 不要递归覆盖开发者显式设置的标题字体（由宿主在切换后恢复）。

## 6. 当前语义协议

### 6.1 通用动作

| 动作 | 说明 |
|---|---|
| `focus` | 聚焦控件 |
| `highlight` | 显示 AI 指引 |
| `clearHighlight` | 取消指引 |
| `click` | 按钮：触发；文本框：聚焦；单选：选中；复选：切换；下拉：打开 |
| `setText` | 设置文本框内容 |
| `getText` | 读取文本框内容、下拉选中项或勾选控件文案（结果在 `control.state.text`；敏感字段为 `null`） |
| `setChecked` | 设置单选框或复选框 |
| `getChecked` | 读取单选框/复选框是否选中（结果在 `control.state.checked`） |
| `selectItem` | 通过 `index` 或 `value` 选择项目 |
| `openDropDown` | 打开下拉列表 |
| `closeDropDown` | 关闭下拉列表 |

### 6.2 当前事件

- `clicked`
- `pressed`
- `released`
- `textChanged`
- `checkedChanged`
- `selectionChanged`
- `dropDownOpened`
- `dropDownClosed`
- `focusChanged`
- `remoteActionCompleted`
- `remoteActionRejected`

### 6.3 事件来源

- `User`
- `Remote`
- `Programmatic`
- `Replay`

当前 UI 适配器主要可靠区分 `User` 和 `Remote`。显式
`Programmatic` 操作作用域仍未实现。

## 7. 不可破坏的工程约束

接手 AI 修改代码时必须遵守：

1. 每次新增动作，都必须同时完成：
   - `AgenticActions` 常量。
   - 对应控件的 `descriptor.Actions`。
   - WPF 或 WinForms 执行实现。
   - 参数验证。
   - 事件或结果反馈。
   - 测试和协议文档。
2. 所有 WPF/WinForms 控件操作必须回到对应 UI 线程。
3. 不使用远程鼠标注入代替语义动作。
4. 密码和敏感文本默认不写入日志或录制。
5. 不把认证令牌写入源码、普通日志或版本控制。
6. 保持 `.NET Framework 4.8` 兼容：
   - 注意旧 API 不可用。
   - 必要时使用条件编译。
   - 继续保留 `Microsoft.NETFramework.ReferenceAssemblies`。
7. JSON 协议统一使用 `AgenticJson.Options`：
   - camelCase。
   - 枚举字符串化。
8. 项目启用了 `TreatWarningsAsErrors`，不能用忽略警告代替修复。
9. 不重新加入限制 SDK 补丁版本的 `global.json`。
10. 不在未决定许可证前发布到 nuget.org。

## 8. 构建、测试和运行

### 8.1 环境

- Windows 10/11。
- Visual Studio 2022 或更高版本。
- .NET 8 或更高版本 SDK。
- 需要 Windows Desktop 开发组件。

只有 .NET 9/10 SDK 也可以构建面向 .NET 8 的项目。

### 8.2 常用命令

```powershell
dotnet restore AgenticUI.NET.sln
dotnet build AgenticUI.NET.sln -c Release
dotnet test AgenticUI.NET.sln -c Release --no-build
dotnet pack AgenticUI.NET.sln -c Release --no-build -o artifacts/packages
```

### 8.3 示例运行

1. 启动 `AgenticUI.Workbench.WinForms` 或 `AgenticUI.Workbench.Wpf`。
2. 确认左侧演示控件完整显示。
3. 在右侧选择 `login.role`：
   - 点击“高亮”，应出现完整四边框。
   - 点击“点击”，下拉列表应打开。
   - 点击“选择下一项”，选中项应变化。
4. 选择 `login.username`，在“输入文本”框中输入内容并点“设置文本”（或按 Enter），左侧账号框应更新。
5. 复制 Workbench 显示的连接令牌（WPF 版还需使用管道名 `AgenticUI.NET.Wpf`）。
6. 启动 `AgenticUI.RemoteConsole.WinForms` 或 `AgenticUI.RemoteConsole.Wpf`。
7. 输入对应管道名和令牌。
8. 连接后远程高亮、点击、选择下拉项，以及对文本框执行“设置文本”。
9. 远程 click `dialog.open` 打开真正的模态确认弹窗；弹窗出现后在远程端刷新列表，即可 click `dialog.ok` / `dialog.cancel` 关闭（不要用系统 MessageBox，其内部按钮无法接入）。

### 8.4 自动测试

- `AgenticUI.Core.Tests`
  - 注册表。
  - 事件序号。
  - 命令分发。
  - 日志脱敏。
  - 录制命令。
  - Named Pipe 认证、并发请求和事件广播。
- `AgenticUI.WinForms.Tests`
  - 必须在 Windows 上运行。
  - 使用 STA 线程和真实 WinForms 控件。
  - 验证下拉列表动作、高亮和取消高亮。

CI 文件：`.github/workflows/ci.yml`。

## 9. 已知限制和风险

### P0：发布前必须处理

1. **商业授权主体和合同仍需完善**
   - 开源路径已经确定为 `AGPL-3.0-only`，根目录包含正式许可证全文。
   - 商业许可证和 CLA 当前只有草案模板；必须填写许可方法定名称、联系方式、适用法律和价格。
   - 外部贡献在正式 CLA 流程建立前可以审查，但不应合并需要商业再许可的版权内容。

2. **Windows 视觉验收不完整**
   - 需要覆盖 100%、125%、150%、200% DPI。
   - 需要覆盖多显示器、窗口缩放、滚动容器、TabPage 和嵌套 UserControl。
   - WinForms 覆盖层尤其需要验证屏幕坐标转换和裁剪。

3. **NuGet 尚未正式发布**
   - 当前只在 CI 产生预览包。
   - 发布前检查包 ID 是否被占用、版本策略、许可证元数据和签名。

### P1：建议下一阶段完成

1. WPF STA 集成测试和高亮视觉测试。
2. WinForms 高亮覆盖层在滚动、MDI、无顶层 Form 场景的处理。
3. 显式 `Programmatic` / `Replay` 事件来源作用域。
4. 控件状态主动推送，而不是只在枚举或命令后返回。
5. 命令超时、取消和幂等键。
6. 令牌轮换、连接会话信息和认证失败速率限制。
7. `IAgenticCommandAuthorizer` 的可运行示例与企业策略示例。
8. 日志文件滚动、大小限制、保留周期和签名。
9. 增加 DatePicker、ListBox、Slider、TabControl、DataGrid 等控件。

### P2：长期方向

1. Web Components/React/Vue 适配层。
2. 无障碍树和 UI Automation 元数据映射。
3. AI 引导流程 DSL。
4. 可视化流程录制器和回放编辑器。
5. 企业集中审计、策略分发和设备管理。

## 10. 建议的下一轮任务

推荐先做“Windows 视觉与交互稳定性”里程碑，不要立即扩展大量新控件。

### 任务 A：建立 Windows 视觉测试基线

目标：

- 为 Workbench 自动截图。
- 在 100%、150%、200% DPI 下验证布局和高亮。
- 记录期望截图或像素差异阈值。

验收：

- 所有基础控件完整显示。
- 高亮四边、编号和提示不被裁剪。
- 高亮不阻断控件点击。

### 任务 B：完善 WPF 测试

目标：

- 新建 `AgenticUI.Wpf.Tests`。
- 在 STA Dispatcher 中创建真实窗口。
- 验证按钮点击、文本设置、勾选、下拉选择和 Adorner 高亮。

验收：

- `.NET 8` Windows CI 自动执行。
- 每个已声明动作至少有一个真实控件测试。

### 任务 C：状态订阅

目标：

- 新增明确的 `stateChanged` 消息。
- 只在状态真正变化时推送。
- 敏感状态不泄露值。

验收：

- 多客户端可以并发订阅。
- 慢客户端不会阻塞 UI 线程或其他客户端。
- 断线和取消逻辑有测试。

## 11. Git 和发布流程

- 不直接在 `main` 上开发较大功能。
- 分支命名：`agent/<简短描述>`。
- 本地构建和测试通过后推送。
- 默认创建草稿 PR。
- Windows CI 成功后再合并。
- 合并优先使用 squash。
- 不在用户未授权时发布 NuGet、创建 Release 或选择许可证。

最近已合并：

- PR #1：安全远程协议、Remote Console、打包、CI、布局、高亮和下拉修复。
- PR #2：移除 SDK 锁定并修复透明高亮初始化。

## 12. 给下一位 AI 的启动提示词

可以把下面文字连同仓库一起交给下一位 AI：

```text
你现在接手 AgenticUI.NET。先完整阅读：
1. docs/AI-HANDOFF.zh-CN.md
2. README.md
3. docs/architecture.zh-CN.md
4. docs/local-protocol.zh-CN.md
5. SECURITY.md

开始前运行 git status、git log -5、dotnet build AgenticUI.NET.sln -c Release。
不要重新加入 global.json SDK 补丁锁定，不要擅自选择许可证或发布 NuGet。
保持 .NET 8 和 .NET Framework 4.8 兼容，所有真实 UI 操作必须在 UI 线程执行。
任何新增语义动作都必须同步实现 descriptor、WPF/WinForms 执行、事件、测试和文档。

当前优先任务是 Windows 视觉与交互稳定性，其次是 WPF STA 集成测试和状态主动订阅。
先报告你准备处理的具体任务和验收标准，再开始修改。
```

## 13. 交接完成标准

下一位 AI 在开始编码前，至少应能回答：

- AgenticUI.NET 与普通控件库有什么区别？
- WPF 和 WinForms 如何接入原生控件？
- 稳定 ID 和临时 ID 的区别是什么？
- 远程连接如何认证？
- 为什么不能记录敏感文本？
- 为什么 WinForms 高亮必须在前景并点击穿透？
- 为什么不能重新加入当前形式的 `global.json`？
- 新增一个动作需要同步修改哪些位置？
- 哪些测试只能在 Windows CI 上执行？
- 当前许可证和 NuGet 发布为什么仍然阻塞？

如果这些问题不能回答，应继续阅读文档，不要直接修改架构。
