# AgenticUI.NET 快速开始

本指南以 `0.2.0` 为例。WPF 和 WinForms 应用均可使用 .NET 8；组件库同时兼容
.NET Framework 4.8。

## 1. 安装包

WPF：

```powershell
dotnet add package AgenticUI.Wpf --version 0.2.0
dotnet add package AgenticUI.Remote --version 0.2.0
```

WinForms：

```powershell
dotnet add package AgenticUI.WinForms --version 0.2.0
dotnet add package AgenticUI.Remote --version 0.2.0
```

只使用协议、注册表、日志和命令分发时安装：

```powershell
dotnet add package AgenticUI.Core --version 0.2.0
```

## 2. 为控件添加语义身份

### WPF

```xml
<Window
    xmlns:agentic="clr-namespace:AgenticUI.Wpf;assembly=AgenticUI.Wpf">
    <Button
        agentic:AgenticProperties.Enabled="True"
        agentic:AgenticProperties.Id="login.submit"
        agentic:AgenticProperties.DisplayName="登录"
        Content="登录" />
</Window>
```

也可以直接使用 `AgenticButton`、`AgenticTextBox`、`AgenticComboBox` 等替换式控件。

### WinForms

```csharp
AgenticControlBinder.Attach(loginButton, new AgenticControlOptions
{
    Id = "login.submit",
    DisplayName = "登录",
    Hint = "请点击登录"
});
```

未指定 ID 时会生成临时 ID。需要跨版本稳定定位的业务控件应始终配置手动 ID。

## 3. 启动本机语义网关

```csharp
using AgenticUI.Remote;

using var server = new AgenticNamedPipeServer("AgenticUI.NET");
server.Start();

// 仅通过受信任的本机渠道把令牌交给控制端。
var token = server.AuthenticationToken;
```

当前网关只使用本机 Named Pipe，不监听 TCP。连接令牌拥有本次会话的操作权限，不要写入
源码、日志或版本控制。

## 4. 发送语义命令

```csharp
using AgenticUI;
using AgenticUI.Remote;

using var client = await AgenticNamedPipeClient.ConnectAsync(
    authenticationToken: token,
    pipeName: "AgenticUI.NET",
    clientName: "My AI Agent");

var result = await client.ExecuteAsync(new AgenticCommand
{
    ControlId = "login.submit",
    Action = AgenticActions.Click
});
```

下拉列表可以使用 `OpenDropDown`、`CloseDropDown` 和 `SelectItem`。文本框可以使用
`SetText`；各类值控件可以使用 `SetValue` 和 `GetValue`。

## 5. 开启本地审计

```csharp
using var audit = new AgenticLogRecorder(
    "events.jsonl",
    options: new AgenticLogOptions
    {
        Level = AgenticLogLevel.Semantic,
        RedactSensitiveValues = true
    });
```

默认记录语义事件并脱敏敏感文本。只有在明确评估数据风险后，才应关闭脱敏或开启详细底层事件。

## 6. 下一步

- [架构说明](architecture.zh-CN.md)
- [本机协议](local-protocol.zh-CN.md)
- [安全策略](../SECURITY.md)
- [授权说明](../LICENSING.md)
