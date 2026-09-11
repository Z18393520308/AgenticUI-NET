# Windows 联合验收入口

维护者已反馈本次高亮修复测试无问题，并授权发布 `0.6.1`。
本文件保留联调包验收流程；自动测试以 Windows CI 为准，未逐项报告的人工场景仍需按清单验证。

## 1. 准备两份最新源码

把最新控件源码同步到 Windows，包含新增测试与脚本；配套 Agent 仓库的本地修改需单独同步。
控件库与工控机 Agent 保持两个独立仓库；不需要固定放置位置，不合并 .git。
请同步 Agent 的 packages、Directory.Build.props 和 scripts/verify.ps1。
当前高亮修复联调包为 `0.6.0-dev.guidance.3`，同时同步新增
scripts/verify-guidance-binaries.ps1 和机器可读包清单；旧 .2 不含布局循环修复。
正式使用请升级四个依赖中的所需包至 `0.6.1`。上述 `.3` 校验清单仅适用于对应联调包，
不能用来校验正式包或手改版本字段后继续使用；两种版本的 DLL 哈希不相同。

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
Agent 的第 7 步还会核对业务应用输出中的 Core/Remote/Wpf DLL 与包内二进制是否一致。
该检查针对磁盘文件，不检查正在运行的进程；关闭旧进程并从已验证目录重启是必须步骤。

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
- 高亮显示期间窗口可以拖动，人工点击与输入正常；连续 setText/getText 均返回，取消后无残留。
- 两个连接分别提示，断开或取消其中一个，只清理其所属提示。
- 表格滚动/排序、多屏、100%/150%/200% DPI、窗口移动与模态弹窗。

## 4. 再接配套 Agent

在 Agent 仓库启动 WarehouseManager.App 和 AgenticRemote.Controller，先用本机 Named Pipe。
使用测试数据库、测试账号；配置真实模型需要自行授权，密钥只在本机保存。
逐一验证演示、智能、人工引导、技能保存/回放、手动预览与任务引导并存、取消与断线。
只有本机链路通过后，再验证应用内 WSS Gateway 的可信证书、认证和连接隔离。

针对高亮布局循环的自动回归可单独运行（控件库根目录）：

```powershell
dotnet test tests/AgenticUI.Wpf.Tests/AgenticUI.Wpf.Tests.csproj -c Release --filter "FullyQualifiedName~HighlightReturnsToIdleAfterResizeAndHintUpdate|FullyQualifiedName~NamedPipeHighlightThenTextCommandsCompleteAndReturnToIdle"
```

共 3 个用例：有/无气泡的布局稳定检查、真实管道上的高亮后连续读写检查。
测试等待低优先级 ApplicationIdle 并设置超时，避免只验证高亮命令已返回却漏掉持续重排。
当前 macOS 仅完成这些测试的编译；必须在 Windows 上运行，不将其标为已通过。

若有问题，提供复现步骤、目标 controlId、action、已脱敏日志、TRX 和截图；不要提供密钥或
真实业务数据。将问题按控件端/控制端分别修复，然后重复对应测试。

2026-09-10：维护者已要求发布 0.6.0；自动测试由 Windows CI 校验。
人工视觉验收仍待完成，不宣称已通过；Web 组件开发另起后续阶段。
