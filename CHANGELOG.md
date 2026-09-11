# Changelog

## Unreleased

- 修复 WPF / WinForms 对象绑定下拉框按显示文字选择失败，以及状态和选择日志误返回对象 ToString 的问题。
- 下拉框与普通 ListBox 新增 items.v1：getItems 分页候选读取、itemKey 业务键选择、itemsVersion
  列表变化校验；复用既有显示/值绑定，复杂模板或自绘可选配置文字/键解析器。
- 兼容旧 index/value；完整文字优先，再做唯一的首尾空白归一匹配，最后兼容旧 ToString；
  重名、无效索引、禁用/未知可用状态和过期版本均拒绝，不做模糊自动选择。
- 选项状态/事件携带 itemKey，录制优先保存业务键；候选返回不序列化业务对象，延续敏感状态脱敏。
- 默认网络只读动作白名单加入 getItems，selectItem 仍需显式授权；增加共享协议、Windows UI、
  Named Pipe 回归测试和下拉列表接入文档，未改控制端、版本号或发布状态。

- 内嵌网关移除 HTTP.sys，改用用户态 TCP/SslStream 与自动持久化证书，不需要 netsh 或系统证书安装。
- 新增首次指纹核验、一次性配对码、DPAPI 保护的本地凭据、重连与撤销配对；UDP 公告不作为身份信任来源。
- 两个 Workbench/RemoteConsole 新增原生配对界面，WSS 不再自动跳过证书验证；配置预设不同端口。
- 增加真实 TLS 配对回归和 Windows net48 专项测试；更新安全边界和首次配对文档。

- 移除无人使用的独立 Gateway 程序及其启动配置，网络服务统一由应用内宿主管理；迁移网络测试和部署文档。

- 新增应用级 AgenticApplicationHost：通过 agenticui.json 控制本机管道、内嵌 WSS 和 UDP 发现，
  网络默认关闭，兼容 net8/net48；控件不包含 AI。
- 两个 Workbench 接入应用生命周期与配置文件，本机默认随机令牌；新增配置、管道和 WSS 转发测试。
- 增加内嵌网关部署说明，明确首次配对、动作白名单及重启生效边界。

## 0.6.1

- 修复 WPF 高亮覆盖层反复触发布局、导致界面卡顿或后续远程命令等待的问题；
  增加重入保护，目标位置、缩放和引导配置不变时不再重复定位与重绘。
- 增加有/无气泡、窗口与目标尺寸变化、动态文案更新后的 UI 空闲回归测试，
  以及真实 Named Pipe 高亮后连续 `setText` / `getText` 和清理测试。
- 完善 Windows 联合验收说明，明确核对实际应用 DLL、关闭旧进程后重启，避免旧包缓存混用。
- 保持现有动态引导协议、人工操作、权限校验和 .NET 8 / .NET Framework 4.8 支持不变。

## 0.6.0

- 新增 `dynamicGuidance.v1`：远程独立控制描边、步骤编号、气泡显隐/文本、位置和时长；
  同一引导可动态更新，显式隐藏不会回退静态 Hint。
- WPF/WinForms 引导按连接隔离，支持断线、卸载、超时清理，使用穿透、非激活的拥有者窗口；
  增加中文换行、屏幕工作区约束和单元格目标失效处理。
- 强化只读、禁用和可展示状态检查；WPF 点击复用原控件 OnClick/ICommand，文本编辑保留绑定。
- DataGrid 新增/删除尊重用户权限开关；setCell 改走原生文本单元格编辑/提交校验流程，
  非文本自定义编辑列暂不支持，不能退回反射修改数据对象绕过校验。
- 修复密码掩码控件状态泄露、敏感事件广播与本地日志脱敏不一致、事件订阅异常影响命令结果、
  拒绝请求遗漏审计及值变化录制缺失；Pipe 事件使用有界发送队列，慢连接断开。
- Gateway 请求去重改为最近 2048 个 ID 的窗口，超过该数量后连接可继续使用；不承诺跨连接
  或窗口外业务幂等。认证握手限时 10 秒，UDP 发现尊重显式关闭，WSS Dispose 不等待无限关闭握手。
- 更新两个 Remote Console 动态引导演示；新增 WPF 界面测试、动态协议回归和真实 WSS/TLS 集成测试。
- 第三阶段：文本框/DataGrid 返回只读状态；敏感状态只保留严格布尔类型的 readOnly。
- WPF / WinForms 树节点统一 path、treeStateVersion、expansionPath 和 expanded；补齐选择与展开/折叠语义事件及路径录制。
- WPF 路径支持原生节点和已生成的绑定容器；歧义路径不猜测，节点属性修改保留绑定。
- 新增 Windows 联合验收入口、只读与三级树 Demo，以及跨项目状态匹配回归。

## 0.5.0

- `AgenticUI.Remote` 新增统一的 `IAgenticRemoteClient` 抽象和 `AgenticWebSocketClient`，
  AI 控制端可用同一套接口连接本机 Named Pipe 或远程 WSS Gateway。
- 新增 `AgenticGatewayDiscovery`，可按需监听并校验 Gateway 的 UDP 局域网发现广播；发现过程
  支持超时、去重和取消，UDP 仍不承载令牌、认证或控制命令。
- WPF 与 WinForms Remote Console 新增 Pipe / Gateway 传输选择、WSS 地址连接和 UDP 自动发现，
  可直接联调跨机器安全访问链路。
- Workbench、Gateway 和 Remote Console 增加一致的本地 Development 配置与启动说明；生产环境
  仍必须使用随机独立令牌、可信 TLS 证书和显式动作白名单。
- 补充 WebSocket 客户端、Gateway 发现、开发环境配置和安全校验文档及回归测试。

## 0.4.0

- 新增独立 `.NET 8` `AgenticUI.Gateway`，通过 WSS/TLS 将远程语义请求安全转发到本机
  Named Pipe；WPF/WinForms 宿主仍不监听网络端口。
- Gateway 使用相互独立的公网与本机令牌，并提供最大连接数、单连接速率、消息大小、
  Origin 和动作白名单限制以及不含参数/令牌的结构化审计日志。
- 新增默认关闭的 UDP 局域网发现广播，只发布公开 WSS 服务元数据，不接收认证和控制命令。
- 新增 Gateway 配置校验、安全策略和发现报文回归测试及部署文档。
- WPF/WinForms 新增仅限当前应用界面的 `mouseMove`、`mouseClick`、`mouseDoubleClick`、
  `mouseWheel` 和 `mouseDrag`；使用控件内相对坐标，不移动系统真实指针，并拒绝越界、遮挡、
  禁用或非活动模态窗口中的输入。
- 新增 WPF `AgenticCanvas` 与 WinForms `AgenticPanel` 智能画布控件；两套 Workbench 增加
  可视化鼠标画布，Remote Console 增加鼠标动作和 DataGrid 扩展动作的一键演示。

## 0.3.0

- WPF `DataGrid` 与 WinForms `DataGridView` 新增行/列分页读取、滚动定位、增删行、
  按列排序/过滤、单元格选择与单元格高亮等远程动作。
- 新增 `getRow` / `getRows` / `getColumns`，其中 `getRows` 默认返回 50 行、
  最多返回 500 行，便于 AI 分页读取大型表格并控制 Token 消耗。
- 表格行号统一按当前排序、过滤后的可见视图计算；列可使用索引、
  列名、列标题或绑定属性定位。
- 新增 WinForms DataGrid 扩展动作测试，协议文档同步补充全部参数和返回状态。

## 0.2.1

- 远程 `listControls` 默认只返回当前可展示控件（活动页、滚动可视区、未被完全遮挡）；可用 `includeHidden:true` 枚举全量。
- 存在模态顶级弹窗时，远程仅枚举/操作弹窗内智能控件，主窗控件不可见也不可执行，以遵守原软件模态规则。
- 控件状态新增 `displayable`；本地 Workbench 仍可全量 Snapshot 便于调试。
- 简化 WPF 与 WinForms Workbench 示例，突出远程可发现性与可展示控件规则。
- 修复 WinForms Workbench 列表初始化在不同编译器下的 `AddRange` 重载歧义。

## 0.2.0

- 首个稳定版，包含 WPF、WinForms、核心语义协议与本机安全网关四个 NuGet 包。
- 提供稳定/临时控件 ID、语义命令、事件广播、审计日志、操作录制与回放。
- 覆盖按钮、文本、选择、日期、数值、列表、表格、树、标签页和滑块等常用控件。
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
