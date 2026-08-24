# AgenticUI.NET 快速开始

本指南以 `0.5.0` 为例。WPF 和 WinForms 应用均可使用 .NET 8；组件库同时兼容
.NET Framework 4.8。

## 1. 安装包

WPF：

```powershell
dotnet add package AgenticUI.Wpf --version 0.5.0
dotnet add package AgenticUI.Remote --version 0.5.0
```

WinForms：

```powershell
dotnet add package AgenticUI.WinForms --version 0.5.0
dotnet add package AgenticUI.Remote --version 0.5.0
```

只使用协议、注册表、日志和命令分发时安装：

```powershell
dotnet add package AgenticUI.Core --version 0.5.0
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
需要承载自定义绘图、CAD 或组态画面时，可以使用新增的 `AgenticCanvas`：

```xml
<agentic:AgenticCanvas
    AgenticId="editor.canvas"
    AgenticDisplayName="编辑画布"
    Hint="可在当前画布内执行应用内鼠标动作" />
```

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
自定义绘图区域可以直接继承或实例化 `AgenticPanel`：

```csharp
var surface = new AgenticPanel
{
    AgenticId = "editor.canvas",
    AgenticDisplayName = "编辑画布",
    Hint = "可在当前画布内执行应用内鼠标动作"
};
```

## 3. 启动本机语义网关

```csharp
using AgenticUI.Remote;

using var server = new AgenticNamedPipeServer("AgenticUI.NET");
server.Start();

// 仅通过受信任的本机渠道把令牌交给控制端。
var token = server.AuthenticationToken;
```

稳定版 `0.5.0` 默认只使用本机 Named Pipe，不监听 TCP。可选的独立
`AgenticUI.Gateway` 使用 WSS/TLS 转发到本机管道，桌面应用本身仍不监听网络端口。连接令牌
拥有本次会话的操作权限，不要写入源码、日志或版本控制。跨机器部署见
[Gateway 安全部署指南](gateway.zh-CN.md)。

本地调试可使用固定开发令牌；Remote Console 与 Workbench 已预填。通过 Gateway 连接示例：

```csharp
using AgenticUI;
using AgenticUI.Remote;

using var client = await AgenticWebSocketClient.ConnectAsync(
    new Uri(AgenticRemoteSecurity.DevelopmentGatewayWebSocketUrl),
    AgenticRemoteSecurity.DevelopmentGatewayToken,
    clientName: "My AI Agent",
    skipTlsValidationForDevelopment: true);

await client.ExecuteAsync(new AgenticCommand
{
    ControlId = "login.submit",
    Action = AgenticActions.Highlight
});
```

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

DataGrid 分页读取和单元格高亮：

```csharp
var rows = await client.ExecuteAsync(new AgenticCommand
{
    ControlId = "orders.grid",
    Action = AgenticActions.GetRows,
    Arguments = { ["start"] = 0, ["count"] = 50 }
});

await client.ExecuteAsync(new AgenticCommand
{
    ControlId = "orders.grid",
    Action = AgenticActions.HighlightCell,
    Arguments = { ["row"] = 0, ["column"] = "OrderNumber" }
});
```

`row` 使用当前排序、过滤后的视图行号。完整表格动作和参数见
[DataGrid 使用指南](datagrid.zh-CN.md)。

画布、自定义绘图和 CAD 视图还可以使用仅限当前应用界面的鼠标动作：

```csharp
await client.ExecuteAsync(new AgenticCommand
{
    ControlId = "editor.canvas",
    Action = AgenticActions.MouseDrag,
    Arguments =
    {
        ["startXRatio"] = 0.2,
        ["startYRatio"] = 0.3,
        ["endXRatio"] = 0.8,
        ["endYRatio"] = 0.7
    }
});
```

鼠标坐标是控件内 `0～1` 的相对值，不移动系统真实指针，也不能操作其他软件。普通按钮等标准
控件仍应优先使用 `Click` 等语义动作。完整参数和安全限制见
[本机协议的应用内鼠标动作](local-protocol.zh-CN.md#应用内鼠标动作)。

仓库中的 WPF 与 WinForms Workbench 均提供 `demo.mouseSurface` 交互画布，并在对应的
Remote Console 中提供移动、单击、双击、滚轮、拖拽以及 DataGrid 读取、高亮、新增和排序
按钮，可直接连接后体验完整命令链路。

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
- [Gateway 安全部署指南](gateway.zh-CN.md)
- [DataGrid 使用指南](datagrid.zh-CN.md)
- [安全策略](../SECURITY.md)
- [授权说明](../LICENSING.md)
