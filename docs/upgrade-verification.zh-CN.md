# 动态引导升级：实现与验收记录

日期：2026-09-10。下文保留本地开发阶段的验收记录。
维护者反馈本次高亮修复测试无问题，授权将其作为正式版 `0.6.1` 发布。
这是维护者的验收反馈，不代表本地 macOS 已运行 Windows 测试，也不扩展为全部 DPI、
net48 宿主和真实模型场景均已验收；本次 Windows 自动测试由 CI 和发布工作流执行。
此前维护者要求提交、推送并发布 0.6.0，自动测试结果以该版本 Windows CI 为准；
人工视觉验收和跨仓库真实模型联调仍未完成。

## 高亮布局修复包联调（2026-09-10）

控件源码提交 `934641f` 已修复 WPF 高亮反复触发布局的问题，但配套 Agent 原先使用的
`0.6.0-dev.guidance.2` 包不包含它。本轮基于该生产源码生成新的 `.3` 联调包，
整组更新 Core、Remote、Wpf、WinForms；旧包保持不变，没有改动正式版本号或发布标签。

- WPF 新增 3 个回归用例：有/无气泡时高亮、窗口移动/尺寸变化、目标尺寸和文案更新后
  可到达 ApplicationIdle；真实 Named Pipe 高亮后连续三轮 setText/getText 并清理。
  命令与空闲等待均有超时，避免普通优先级命令返回掩盖持续布局；Windows 运行仍待完成。
- Agent 用独立缓存还原四库中的实际依赖（Core/Remote/Wpf），没有使用源码引用。
  确认包版本、依赖版本、还原来源，应用输出三个 DLL 与对应 nupkg 内文件 SHA-256 完全一致。
- Wpf 包的 net48 与 net8.0-windows7.0 二进制均检查到 `_updating`、`_lastTarget`、
  `_lastOptions`、`UpdateOverlayCore` 防护标识；元数据检查不替代 Windows 运行测试。
- 新增 Agent 的 verify-guidance-binaries.ps1，可独立检查实际部署目录，并接入原 verify.ps1。
  控件 Windows 联合入口继续调用后者；磁盘 DLL 校验通过后仍须关闭旧进程并重新启动。

本轮 macOS 实际验证：

| 项目 | 结果 |
| --- | --- |
| 控件完整方案 Release，含 net48 和新增 WPF 测试 | 构建通过，0 警告、0 错误 |
| 控件 Core / Gateway | 39/39、11/11 通过 |
| Agent 完整方案（本地 .3 包，独立缓存） | 构建通过，0 警告、0 错误 |
| Agent 核心 / 可移植编排 / 离线评测 | 173/173、29/29、5/5 通过 |
| 三个应用 DLL 与包内二进制比对 | SHA-256 全部一致 |
| WPF / WinForms / Controller Windows 测试 | 仅编译，未运行 |
| PowerShell 验证入口和人工拖动、输入、DPI 验收 | 当前机器无 PowerShell 和 Windows Desktop Runtime，未执行 |

两仓库 TRX 位于各自的 `artifacts/guidance-layout-fix-tests/`。
本轮只生成本地联调产物、测试及文档，没有提交、推送、创建标签或发布公共 NuGet。
下一步按 [Windows 联合验收入口](windows-acceptance.zh-CN.md) 执行，不将本地编译通过
表述为 Windows 卡死已实机消除。下方第三阶段和第一轮记录为历史基线。

## 第三阶段补充（2026-09-10）

下方原记录保留第一轮基线；第三阶段最新结果如下：

- 文本框与 DataGrid 只读状态上报、敏感状态严格布尔 readOnly 保留、控制端映射与验证已补齐。
- WPF / WinForms 树节点完整路径、选中/展开状态分离、展开/折叠事件、录制路径契约已补齐。
- 控制端按 treeStateVersion=1 开放人工展开/折叠，并核验事件路径与当前真实状态；旧目标仍拒绝。
- 新增三级树与普通/敏感只读 Demo；[状态契约](control-state-contract.zh-CN.md)与
  [Windows 联合验收入口](windows-acceptance.zh-CN.md)已准备。
- 两方案 Release 构建均 0 警告/错误；核心 39/39、Gateway 11/11、Agent 核心 173/173、
  控制端 net8.0 编排 29/29、离线评测 5/5。Windows 测试目标仍仅编译。
- 当前联调包为 `0.6.0-dev.guidance.2`，旧 .1 包保留。二进制清单位于 Agent packages，
  Windows 脚本检查版本/SHA-256，使用新隔离缓存并输出 TRX / Markdown 报告。
- 当前环境未找到 PowerShell，也无法运行 Windows Desktop Runtime；新 Windows 脚本尚未在 Windows 执行，
  人工视觉验收与真实模型联调仍全部待执行。本地自动测试不能证明像素位置或焦点行为正确。

## 第一轮记录（保留历史）

## 本轮实现

1. **统一展示协议**：`dynamicGuidance.v1` 能力声明；远程设置描边、步骤编号、气泡文案、
   显隐、位置、超时和 guidanceId；旧静态调用兼容。详见[动态引导文档](guidance.zh-CN.md)。
2. **桌面渲染与生命周期**：WPF / WinForms 原控件外层独立展示；同会话同 ID 原位更新，
   多连接隔离、断线清理、超时释放；单元格跟踪目标身份，排序、删行、隐藏列等触发更新或关闭。
3. **配套 Agent 联动**：结构化 guidance 经计划、技能、历史转换保留；演示、智能、人工引导
   各有执行语义；气泡不变成业务动作。模型与编排继续留在控制端，控件没有模型依赖。
4. **原生操作保护**：禁用、只读与可显示性检查；文本绑定保留；WPF 按钮走原生 OnClick；
   DataGrid 文本编辑走原生编辑/提交并检查结果。自定义编辑器暂不做猜测性的直接写属性。
5. **事件与隐私**：失败命令也审计，订阅者异常隔离，远程状态和事件统一敏感处理；远程事件
   使用有界队列，慢连接超限断开。不是防篡改审计系统，也不保证业务或设备动作已完成。
6. **传输联动**：Gateway 请求 ID 改为最近 2048 个窗口，修复长连接超过 2048 请求即失效；
   显式关闭发现保持关闭；控制端支持官方 WSS 客户端，业务命令不做自动重试。
7. **示例与协议预留**：两个 Remote Console 增加动态引导演示；JSON Schema 不依赖 UI 框架。
   Web 组件本身尚未实现。

## 已执行验证

环境：macOS / .NET SDK 8.0.126。构建时启用 Windows targeting，只证明 Windows 代码可编译。

| 验证项目 | 结果 |
| --- | --- |
| 控件完整方案 Release，含 net48、net8.0-windows 与示例 | 0 警告、0 错误 |
| AgenticUI.Core.Tests | 35 / 35 通过 |
| AgenticUI.Gateway.Tests | 11 / 11 通过 |
| 真实回环 WSS/TLS → Named Pipe 集成 | 连续 2050 次发现后动态气泡命令成功，断开后清理会话；包含在 Gateway 测试内 |
| 工控机 Agent 完整方案，使用本地包、全新隔离还原缓存 | 0 警告、0 错误，无需控件源码目录 |
| AgenticRemote.Core.Tests | 145 / 145 通过 |
| 工控机 Agent 离线评测 | 5 / 5 通过；未调用真实大模型 |
| JSON Schema 语法、两仓库 git diff --check | 通过 |
| WPF / WinForms / Controller Windows 测试项目 | 仅编译；未执行运行时测试 |

联调包 `0.6.0-dev.guidance.1` 是从本轮未提交源码生成的本地产物，不是正式发布，也不是
可由现有 Git 标签重建的稳定包。Core、Remote、Wpf、WinForms 四个包应整组使用。
打包方案时 Gateway 报“不可打包”提示是其 `IsPackable=false` 的预期行为；四个库已生成。

## Windows 发布前必须补齐

- [ ] Windows 执行完整方案测试，包括新增 WPF、WinForms 与 Controller 测试。
- [ ] .NET 8 与 .NET Framework 4.8 宿主分别运行；不接 AI 也正常使用。
- [ ] 动态改文案、三个开关独立组合、默认 Hint 不残留、计时器更新、断线/取消清理。
- [ ] 高 DPI、多屏、屏幕边缘、窗口最小化/切换、滚动、表格排序与容器复用。
- [ ] 确认引导不抢焦点、不挡点击，不绕过模态弹窗、禁用状态和只读校验。
- [ ] 工控机 Agent 演示、智能、引导、技能保存/回放端到端验收，使用隔离业务数据库。
- [ ] 验证宿主自定义绑定、编辑器、行增删生命周期；控件动作成功不作为业务成功凭据。

当前人工引导只核验已支持的语义动作。双击、鼠标轨迹、表格行/单元格等尚无精确人工
完成判据的动作会明确拒绝，不用一次点击或选择事件冒充完成；演示/智能模式的远程动作
能力不因此删除。真实大模型联调、Windows 视觉验收、Web 适配器均未在本轮完成。
