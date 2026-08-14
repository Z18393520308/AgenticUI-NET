# AgenticUI.NET

[![CI](https://github.com/Z18393520308/AgenticUI-NET/actions/workflows/ci.yml/badge.svg)](https://github.com/Z18393520308/AgenticUI-NET/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AgenticUI.Core.svg)](https://www.nuget.org/packages/AgenticUI.Core)
[![License: AGPL-3.0](https://img.shields.io/badge/license-AGPL--3.0--only-blue.svg)](LICENSE)

AgenticUI.NET 是一套面向 AI Agent 的桌面 UI 协议、组件库和控件库。它让 WPF 与
Windows Forms 应用中的控件可以被稳定识别、观察、高亮、记录和通过本机语义命令触发。

> 当前稳定版为 `0.2.1`。网络远程控制默认不开放，只提供本机命名管道。

![AgenticUI.NET 语义控件与事件时间线演示](docs/images/agenticui-overview.png)

## 安装

WPF：

```powershell
dotnet add package AgenticUI.Wpf --version 0.2.1
dotnet add package AgenticUI.Remote --version 0.2.1
```

WinForms：

```powershell
dotnet add package AgenticUI.WinForms --version 0.2.1
dotnet add package AgenticUI.Remote --version 0.2.1
```

只使用协议、注册表、日志和命令分发时安装 `AgenticUI.Core`。完整步骤见
[快速开始](docs/quickstart.zh-CN.md)。

## 当前能力

- 同时支持 `.NET 8` 和 `.NET Framework 4.8`
- 稳定手动 ID，以及未指定 ID 时的临时自动编号
- WPF 与 WinForms 的替换式控件
- 无需替换原生控件的附加属性/Binder 接入
- 按钮、输入框、单选框、复选框和下拉列表
- 控件描边、步骤编号和提示气泡
- 语义事件广播；详细模式可包含按下、松开和焦点事件
- 本地 JSONL 审计日志，敏感文本默认脱敏且可配置
- 用户操作录制和语义命令回放
- 本机 Named Pipe 网关及可视化 WinForms / WPF Workbench 与 Remote Console
- 原生外观，以及可选现代主题

## 项目结构

```text
src/
├── AgenticUI.Core       # 协议、注册表、事件总线、日志、录制与回放
├── AgenticUI.Remote     # 本机命名管道服务端与客户端
├── AgenticUI.Wpf        # WPF 控件、附加属性和 Adorner 高亮层
└── AgenticUI.WinForms   # WinForms 控件、Binder 和高亮层
samples/
├── WinForms/
│   ├── AgenticUI.Workbench.WinForms    # 被控应用与内置检查器演示
│   └── AgenticUI.RemoteConsole.WinForms # 独立远程控制台
└── Wpf/
    ├── AgenticUI.Workbench.Wpf         # 被控应用与内置检查器演示
    └── AgenticUI.RemoteConsole.Wpf      # 独立远程控制台
tests/
├── AgenticUI.Core.Tests
└── AgenticUI.WinForms.Tests
```

更完整的设计见 [架构说明](docs/architecture.zh-CN.md) 和
[本机协议](docs/local-protocol.zh-CN.md)。由其他 AI 或开发者继续维护时，请先阅读
[AI 开发交接文档](docs/AI-HANDOFF.zh-CN.md)。

## WPF 快速接入

直接使用 AgenticUI 控件：

```xml
<agentic:AgenticButton
    AgenticId="login.submit"
    AgenticDisplayName="登录"
    InstructionNumber="1"
    Hint="请点击登录"
    Content="登录" />
```

保留已有原生控件：

```xml
<Button
    agentic:AgenticProperties.Enabled="True"
    agentic:AgenticProperties.Id="login.submit"
    agentic:AgenticProperties.DisplayName="登录"
    Content="登录" />
```

现代主题为可选资源，不会强制改变原生外观：

```xml
<ResourceDictionary Source="/AgenticUI.Wpf;component/Themes/ModernTheme.xaml" />
```

## WinForms 快速接入

直接使用 `AgenticButton`：

```csharp
var login = new AgenticButton
{
    AgenticId = "login.submit",
    AgenticDisplayName = "登录",
    InstructionNumber = 1,
    Hint = "请点击登录",
    Text = "登录"
};
```

保留已有原生控件：

```csharp
AgenticControlBinder.Attach(existingButton, new AgenticControlOptions
{
    Id = "login.submit",
    DisplayName = "登录"
});
```

## 日志、录制和回放

```csharp
using var audit = new AgenticLogRecorder(
    "events.jsonl",
    options: new AgenticLogOptions
    {
        Level = AgenticLogLevel.Semantic,
        RedactSensitiveValues = true
    });

using var recording = new AgenticInteractionRecorder("recording.jsonl");

var commands = await AgenticReplay.LoadCommandsAsync("recording.jsonl");
var replay = new AgenticReplay(new AgenticCommandDispatcher());
await replay.ReplayAsync(commands, TimeSpan.FromMilliseconds(250));
```

敏感控件的文本默认不会写入录制文件。只有显式设置
`AgenticRecordingOptions.RecordSensitiveText = true` 才会记录。

## 本机远程控制

在被控制的桌面应用中启动服务：

```csharp
using var server = new AgenticNamedPipeServer("AgenticUI.NET");
server.Start();

// 将令牌通过受信任的本机渠道提供给控制端。
var token = server.AuthenticationToken;
```

客户端发送语义命令：

```csharp
using var client = await AgenticNamedPipeClient.ConnectAsync(
    authenticationToken: token,
    pipeName: "AgenticUI.NET",
    clientName: "My AI Agent");
var response = await client.ExecuteAsync(new AgenticCommand
{
    ControlId = "login.submit",
    Action = AgenticActions.Highlight
});
```

服务端默认生成 256 位随机连接令牌，未认证客户端不能枚举控件、接收事件或执行命令。
可以通过 `AgenticNamedPipeServerOptions` 提供由宿主应用管理的令牌。客户端支持并发请求，
使用请求 ID 匹配响应，事件则由独立后台接收循环通过 `EventReceived` 推送。

下拉列表支持点击打开和直接选择项目：

```csharp
await client.ExecuteAsync(new AgenticCommand
{
    ControlId = "login.role",
    Action = AgenticActions.OpenDropDown
});

await client.ExecuteAsync(new AgenticCommand
{
    ControlId = "login.role",
    Action = AgenticActions.SelectItem,
    Arguments = { ["index"] = 1 }
});
```

也可以使用 `CloseDropDown` 关闭列表。对下拉列表执行 `Click` 的语义同样是打开列表。
Workbench 和独立 Remote Console 都提供“选择下一项”按钮。

## NuGet 包

`0.2.1` 提供四个包：

- [`AgenticUI.Core`](https://www.nuget.org/packages/AgenticUI.Core)
- [`AgenticUI.Remote`](https://www.nuget.org/packages/AgenticUI.Remote)
- [`AgenticUI.Wpf`](https://www.nuget.org/packages/AgenticUI.Wpf)
- [`AgenticUI.WinForms`](https://www.nuget.org/packages/AgenticUI.WinForms)

也可以在本地生成全部包：

```powershell
dotnet pack AgenticUI.NET.sln -c Release -o artifacts/packages
```

仓库中的 CI 会在 Windows 上构建、测试并把包作为工作流产物保存；版本标签触发的发布工作流
会在验证通过后推送到 nuget.org。

## 构建

需要安装 .NET 8 或更高版本 SDK。项目不锁定具体 SDK 补丁版本，因此只有 .NET 9/10
SDK 的开发机也可以构建面向 .NET 8 的目标。

```powershell
dotnet build AgenticUI.NET.sln -c Release
dotnet test tests/AgenticUI.Core.Tests/AgenticUI.Core.Tests.csproj -c Release
dotnet pack AgenticUI.NET.sln -c Release -o artifacts/packages
```

WPF/WinForms 运行及视觉测试需要 Windows。代码可以在装有 Windows Desktop
Targeting Pack 的其他系统上交叉编译。

## 授权

AgenticUI.NET 采用双许可证模式：

- **开源路径：** [AGPL-3.0-only](LICENSE)。个人或企业均可使用，但必须履行 AGPL 的全部义务。
- **商业路径：** 希望闭源集成、分发或部署且不履行 AGPL 开源义务时，需要另行签署商业许可证。

企业身份本身不会自动产生费用；是否需要商业许可证取决于使用者选择的授权路径和实际使用方式。
详细说明见 [LICENSING.md](LICENSING.md)。商业合同和贡献者协议目前提供
[商业许可模板](COMMERCIAL-LICENSE-TEMPLATE.md) 与 [CLA 模板](CLA-TEMPLATE.md)，正式使用前必须
填写许可方法定信息并经过法律审查。

社区版现有能力和规划中的企业服务边界见 [EDITIONS.md](EDITIONS.md)。远程身份认证、网络传输、
集中审计和企业策略属于后续商业能力方向，不代表当前版本已经提供或承诺发布日期。

## 参与项目

- [贡献指南](CONTRIBUTING.md)
- [社区行为准则](CODE_OF_CONDUCT.md)
- [安全策略](SECURITY.md)
- [更新记录](CHANGELOG.md)
