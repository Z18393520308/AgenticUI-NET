# Changelog

## 0.2.0

- 首个稳定版，包含 WPF、WinForms、核心语义协议与本机安全网关四个 NuGet 包。
- 提供稳定/临时控件 ID、语义命令、事件广播、审计日志、操作录制与回放。
- 覆盖按钮、文本、选择、日期、数值、列表、表格、树、标签页和滑块等常用控件。
- 修复 WinForms 列表初始化在不同编译器下的重载歧义。
- 完善发布文档、社区规范、安全策略、授权说明和社区版/企业服务边界。

## 0.2.0-preview.1

### 示例与工程结构

- 主题下拉改为 Agentic 控件（`ui.theme`），支持远程打开/选择下一项。
- Workbench 演示真正的模态确认弹窗：远程 click `dialog.open` 打开后，再刷新即可看到并可 click `dialog.ok` / `dialog.cancel`（系统 MessageBox 内按钮无法接入）。
- 修复确认弹窗控件无法被远程枚举：改为主界面常驻确认区，`dialog.open` / `dialog.ok` / `dialog.cancel` 启动即可出现在列表中（不再依赖独立 ShowDialog 窗口）。
- Workbench 新增可远程点击的确认弹窗演示：`dialog.open` 打开弹窗，`dialog.ok` / `dialog.cancel` 可远程点击关闭。
- WPF Workbench / Remote Console 同步主题切换、明文令牌、枚举数量提示与默认管道名 `AgenticUI.NET.Wpf`。
- 修复 WinForms `Describe` 在远程枚举时跨线程访问控件导致的异常。
- WinForms Workbench 增加「原生外观 / 现代主题」切换演示；`AgenticModernTheme.Apply` 支持按主题应用或还原。
- 示例按 `samples/WinForms` 与 `samples/Wpf` 分目录存放，解决方案同步分组。
- 新增 WPF Workbench（`AgenticUI.Workbench.Wpf`），默认管道名 `AgenticUI.NET.Wpf`。
- 新增 WPF Remote Console（`AgenticUI.RemoteConsole.Wpf`）。
- WinForms Workbench 改为 Designer 布局，可在 VS 设计器中拖放 Agentic 控件。
- Workbench / Remote Console 支持文本框远程输入（`setText`）。

### 控件与协议修复

- 新增数据表格、树、列表视图、菜单/工具栏、进度条、标签和状态栏的中优先级远程控制能力；表格支持行选择与单元格读写，树支持按路径选择、展开和折叠。
- 新增高优先级日期、数值、列表、多选列表、标签页和滑块控件的 `setValue` / `getValue`、选择及状态支持，WinForms/WPF 保持等价能力。
- 新增语义动作 `getChecked`：读取复选框/单选框选中状态（`control.state.checked`），并支持读取其文案（`getText`）。
- 新增语义动作 `getText`：读取文本框内容或下拉选中项，结果位于命令返回的 `control.state.text`。
- 文本框、单选框、复选框支持 `click`（聚焦 / 选中 / 切换）。
- 修复 WPF `Describe`/`ExecuteAsync` 跨线程访问依赖属性导致的异常。
- 补充 WinForms 单选/复选/文本框 `click` 与 `setText` 相关测试。

### 既有预览能力

- 新增面向后续 AI 开发代理的完整交接文档。
- 修复 Workbench 首次显示时左侧面板过窄导致控件和文字被截断。
- 修复 WinForms 高亮框左右边被原控件重绘遮盖。
- 下拉列表新增点击打开、显式打开/关闭和可视化选择下一项操作。
- 新增带随机令牌的 Named Pipe 认证握手。
- 新增顶层请求 ID、并发请求关联和可靠事件订阅。
- 新增独立 WinForms Remote Console。
- 新增 Windows GitHub Actions 构建、测试与打包工作流。
- 新增四个 NuGet 预览包的打包元数据与符号包。
- 协议、安全和网关自动化测试增至 9 项。

## 0.1.0

- 首个 AgenticUI.NET MVP。
- WPF 与 WinForms AI 控件、原生控件接入、高亮、日志、录制回放和本机远程网关。
