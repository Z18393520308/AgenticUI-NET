# Windows 联合验收入口

第三阶段已准备源码、联调包和脚本；**当前尚未在 Windows 执行此脚本或人工视觉验收**。

## 1. 准备两份最新源码

先把本次修改同步到 Windows，包含新增文件。仅拉取 GitHub 不能获得尚未提交的本地修改。
控件库与工控机 Agent 保持两个独立仓库；不需要固定放置位置，不合并 .git。
请同步 Agent 的 packages、Directory.Build.props 和 scripts/verify.ps1。

需要 .NET 8 SDK、.NET 8 Windows Desktop Runtime。net48 实机验收还需要 .NET Framework 4.8；
现有两套 Workbench 为 net8.0-windows，构建 net48 库不等于运行过 net48 宿主。
PowerShell 5.1 或 7 均可按脚本语法执行；脚本已使用 UTF-8 BOM 兼容 5.1 的中文源文件。

## 2. 一条命令执行自动检查

在控件库仓库根目录运行（示例 Agent 路径请替换为实际目录）：

```powershell
.\scripts\verify-windows.ps1 -AgentRepositoryPath "D:\projects\工控机Agent"
```

只检查控件库则省略 AgentRepositoryPath。脚本不会修改系统执行策略；如被组织策略阻止，
请遵循管理员的脚本签名/执行流程。

脚本依次执行：控件方案还原、Release 构建（含 net48 库）、整个控件方案测试；检查 Agent
当前包版本及 SHA-256，然后复用 Agent 的 7 步 verify.ps1，使用全新独立 NuGet 缓存，
避免同版本缓存混用。任何命令非零退出都会失败，不会继续宣称全部通过。

结果位于控件仓库忽略目录 `artifacts/windows-acceptance/<本次编号>/`：

- report.md：自动检查结果，以及尚未勾选的人工验收。
- controls / agent：测试生成的 TRX 结果。

脚本不调用模型、不运行正式数据库业务、不连接生产设备、不提交 Git，也不发布包。
Agent 测试使用临时 SQLite 和模拟目标；Windows UI 测试仍不等于人工视觉检查。

## 3. 先用控件 Demo 验收

在两个终端分别启动 WPF Workbench 和 Remote Console：

```powershell
dotnet run --project samples/Wpf/AgenticUI.Workbench.Wpf -c Release --no-build
dotnet run --project samples/Wpf/AgenticUI.RemoteConsole.Wpf -c Release --no-build
```

完成 WPF 后关闭示例，再分别测试 WinForms：

```powershell
dotnet run --project samples/WinForms/AgenticUI.Workbench.WinForms -c Release --no-build
dotnet run --project samples/WinForms/AgenticUI.RemoteConsole.WinForms -c Release --no-build
```

Workbench 保持展示目标所在标签页，必要时滚动到控件位置，再刷新发现列表。管道名与令牌
使用示例界面提供的本机信息，不粘贴到公开报告或聊天中。

重点目标：

- `demo.readOnly`：state.readOnly=true；远程 setText 拒绝，原文字保持不变。
- `demo.sensitiveReadOnly`：readOnly=true，但远程状态与默认日志没有具体文字。
- `demo.tree`：公司/研发/设计、公司/销售/订单。选择设计后展开销售，应得到不同的 path
  和 expansionPath；再人工折叠，检查 collapsed 的路径与 expanded=false。
- 动态引导：分别测试仅描边、仅编号、仅气泡，修改气泡后原位更新；显式隐藏不回退旧 Hint。
- 两个连接分别提示，断开或取消其中一个，只清理其所属提示。
- 表格滚动/排序、多屏、100%/150%/200% DPI、窗口移动与模态弹窗。

## 4. 再接配套 Agent

在 Agent 仓库启动 WarehouseManager.App 和 AgenticRemote.Controller，先用本机 Named Pipe。
使用测试数据库、测试账号；配置真实模型需要自行授权，密钥只在本机保存。
逐一验证演示、智能、人工引导、技能保存/回放、手动预览与任务引导并存、取消与断线。
只有本机链路通过后，再验证独立 WSS Gateway 的可信证书、认证和连接隔离。

若有问题，提供复现步骤、目标 controlId、action、已脱敏日志、TRX 和截图；不要提供密钥或
真实业务数据。将问题按控件端/控制端分别修复，然后重复对应测试。

2026-09-10：维护者已要求发布 0.6.0；自动测试由 Windows CI 校验。
人工视觉验收仍待完成，不宣称已通过；Web 组件开发另起后续阶段。
