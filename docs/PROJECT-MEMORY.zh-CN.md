# AgenticUI.NET 项目长期记忆

> 本文档是项目的持久上下文，供维护者和后续 AI 代理使用。
> 如果对话记录与仓库状态不一致，以当前代码、`CHANGELOG.md`
> 和发布页为准。本文档不保存密钥、令牌、手机号、内部绝对路径等敏感信息。

## 1. 项目定位

- 项目名称固定为 `AgenticUI.NET`。
- 它不是普通的主题或皮肤库，而是一套同时服务人类用户和 AI 的语义化 UI 控件基础设施。
- 核心目标是让现有专业软件获得可发现、可引导、可操作、可记录、可审计的 AI 交互能力。
- 重点场景包括工业上位机、仪器仪表软件、设计/CAD 类软件、工程与科研工具、企业桌面客户端，以及其他学习成本高的专业软件。
- 产品长期方向是建立跨 WPF、WinForms 和 Web 的统一语义协议，而不是将 AI 绑定到屏幕坐标、视觉识别或某一种 Agent 产品。

## 2. 不可随意改变的产品决策

- 同时支持 `.NET 8` 和 `.NET Framework 4.8`。
- 同时提供替换式 AgenticUI 控件和原生控件附加接入方式。
- 控件 ID 采用“手动稳定 ID + 自动临时 ID”；业务自动化应显式设置稳定 ID。
- 默认只记录语义事件；按下、松开、焦点等底层事件只在详细模式记录。
- 操作日志默认仅保存在本地。
- 密码和敏感文本默认脱敏，宿主应用可显式关闭脱敏，但必须了解风险。
- 高亮能力保持抽象；当前实现描边、步骤编号和提示气泡。高亮不等于按下控件。
- 需要语义操作录制与回放。
- 提供原生外观和可选现代主题，不强制宿主更换视觉风格。
- 首版远程能力仅限本机，使用 Named Pipe；未经新的安全设计不得直接开放 TCP、局域网或互联网接口。
- 控制端可以是 AI Agent、调试控制台、企业服务或另一个客户端；核心协议不依赖特定大模型或 Agent。

## 3. 核心模型

```text
稳定控件身份
    ↓
控件描述、状态和支持的动作
    ↓
语义命令（click、setText、selectItem、setChecked……）
    ↓
语义事件（clicked、textChanged、selectionChanged……）
    ↓
事件广播、本地日志、录制回放、AI 自动化和审计
```

这套模型的重要价值是：AI 可直接操作语义控件，而人类仍使用同一套界面。
这为“人与 AI 共用界面”、操作可追溯、减少视觉试错和降低专业软件学习成本提供基础。

## 4. 已实现能力

- 四个分层包：`AgenticUI.Core`、`AgenticUI.Remote`、`AgenticUI.Wpf`、`AgenticUI.WinForms`。
- WPF 与 WinForms 替换式控件，以及对现有原生控件的附加/绑定接入。
- 基础控件包括按钮、文本框、单选框、复选框和下拉列表；后续已扩展日期、数值、列表、标签页、滑块、表格、树等常用控件的语义能力。
- 控件注册表、状态快照、语义命令分发、事件广播和单调递增事件序号。
- JSONL 审计日志、默认脱敏、用户语义操作录制和命令回放。
- WPF `AdornerLayer` 高亮和 WinForms 顶层点击穿透覆盖层。
- 本机 Named Pipe JSONL 协议，默认随机生成 256 位令牌。
- `.NET 8` 使用 `CurrentUserOnly`，并支持请求 ID、并发响应匹配和独立事件接收循环。
- WinForms/WPF Workbench 与 Remote Console 演示。
- Windows GitHub Actions 构建、测试、打包、NuGet 可信发布和 GitHub Pages 官网部署。

## 5. 安全与隐私红线

- 永远不把 NuGet 密钥、GitHub 令牌、Named Pipe 认证令牌或其他凭据写入仓库、日志、Issue 或聊天。
- NuGet 发布已使用 GitHub OIDC Trusted Publishing，不应退回长期 `NUGET_API_KEY`。
- 代码、符号包、文档和截图中不得泄露手机号、密钥、本机用户名或内部绝对路径。
- 发布前应检查 `.nupkg` 与 `.snupkg` 的元数据和字符串。
- Git 提交和标签优先使用项目通用的 noreply 身份，避免意外公开个人邮箱。
- 高风险业务动作必须继续由宿主应用做权限校验和必要的二次确认，不能只依赖控件库认证。

## 6. 开源与商业边界

- 社区版使用 `AGPL-3.0-only`。
- 个人和企业都可在遵守 AGPL 的前提下使用社区版；“企业用户”本身不等于必须付费。
- 需要集成到闭源产品且不希望履行 AGPL 义务的组织，可另行签署商业许可。
- 企业身份、集中审计、细粒度策略、跨机器网关、设备管理、LTS/SLA、行业适配和私有部署属于未来可选企业服务方向，不应被宣称为当前已交付功能。
- 详细条款以 `LICENSE`、`LICENSING.md` 和 `EDITIONS.md` 为准。

## 7. 当前发布基线

- 首个稳定版为 `v0.2.0`，当前稳定版为 `v0.2.1`。
- NuGet 已公开发布 `AgenticUI.Core` 、`AgenticUI.Remote`、`AgenticUI.Wpf` 和 `AgenticUI.WinForms` 的 `0.2.1` 版本及符号包。
- GitHub Release 包含四个主包和四个符号包。
- 发布工作流为 `.github/workflows/release.yml`；NuGet 可信发布策略必须与该文件名匹配。
- 发布作业使用 PowerShell 逐个枚举包文件，不要再将带引号的 `*.nupkg` 直接传给 `dotnet nuget push`。
- 官网由 GitHub Pages 部署，仓库首页、快速开始和演示图已完成。
- 仓库的准确 URL 以 `git remote get-url origin` 和 GitHub 项目设置为准，不在本记忆中重复保存可能包含个人标识的账号字符串。

## 8. 已解决且不得回归的问题

- Workbench 左侧控件被截断：窗口 `Shown` 后再设置分隔宽度。
- WinForms 高亮只有上下边：改用顶层点击穿透覆盖层。
- WinForms 高亮透明背景异常：先启用 `SupportsTransparentBackColor` 再设置透明背景。
- 下拉列表不能远程操作：已支持 `click`、`openDropDown`、`closeDropDown` 和 `selectItem`。
- 文本框/单选/复选不支持 `click`：已分别映射为聚焦、选中和切换。
- WPF 和 WinForms 远程枚举/命令的跨线程异常：已切回对应 UI 线程。
- 特定 SDK 补丁版本导致方案无法加载：已删除锁死 SDK 的 `global.json`，不要重新加回。
- NuGet 首次发布时 PowerShell 未展开带引号的通配符：已改为 `Get-ChildItem` 逐包发布。

## 9. 仓库和资料管理约定

- Git 只管理源码项目目录，GitHub 仓库位于“源码”分类下。
- 代码密切相关的 README、API/架构文档、示例、官网源码和社区文件属于仓库内容。
- 专利草稿、视频、PPT、字幕、宣传素材等非源码资料应在工作空间的其他分类中管理，不由源码仓库跟踪。
- 早期公开历史已清理；如果需要追溯旧历史，应使用工作空间外的私有 Git bundle 备份，不得将其重新推送到公开仓库。

## 10. 宣传表达原则

- 对外定位应突出：专业桌面软件的 AI 自动化升级、人机共用界面、语义操作、本地审计、默认脱敏和 WPF/WinForms 兼容。
- 可以说它能减少依赖屏幕坐标和视觉试错，并有望降低 token 消耗和软件学习成本；不应在没有测量数据时宣称确定的“指数级”改善。
- “终结通用 Agent”或“完全不再需要 Agent”应视为议题性标题，不是已被证明的技术结论。
- 展示功能时明确区分“已实现”、“正在开发”和“长期构想”。

## 11. 下一阶段建议

1. 为稳定版建立真实 WPF/WinForms 集成应用和视觉自动化测试。
2. 完善 API 稳定性策略、升级指南和版本兼容承诺。
3. 扩展控件状态主动推送、命令取消和更细的授权策略。
4. 在不破坏核心语义协议的前提下设计 Web Components/React 适配层。
5. 收集可复现的性能、token 消耗、可审计性和学习成本数据，为对外宣传提供证据。

## 12. 后续 AI 接手顺序

1. 先阅读本文档。
2. 再阅读 `README.md`、`docs/AI-HANDOFF.zh-CN.md`、
   `docs/architecture.zh-CN.md`、`docs/local-protocol.zh-CN.md` 和 `SECURITY.md`。
3. 执行 `git status -sb`，不覆盖用户未提交的修改。
4. 对照 `CHANGELOG.md` 和现有测试确认功能，不只依赖历史对话。
5. 修改后按风险运行构建、测试、打包和隐私扫描。
6. 发布新稳定版前，确认版本、Tag、NuGet 包、GitHub Release、官网和文档一致。
