# AgenticUI.Gateway 安全部署指南

`AgenticUI.Gateway` 是独立于桌面应用运行的 `.NET 8` 进程。它只接受 WSS（WebSocket over
TLS）连接，并把通过认证和动作策略检查的请求转发到本机 `AgenticUI.Remote` Named Pipe。
WPF/WinForms 应用不需要监听任何网络端口。

```text
远程 AI 客户端
    │ WSS/TLS + Gateway 令牌
    ▼
AgenticUI.Gateway
    │ 本机 Named Pipe + 独立的 Pipe 令牌
    ▼
WPF / WinForms 应用
```

UDP 发现默认关闭；启用后 Gateway 只向局域网广播服务名称和 WSS 地址，不接收 UDP
控制命令，也不会广播认证令牌、Named Pipe 名称或控件数据。

## 1. 先启动桌面应用的 Named Pipe

生产环境应显式配置本机 Pipe 令牌，并让桌面应用和 Gateway 通过操作系统密钥存储或受保护
环境变量分别读取同一个值：

```csharp
var pipeToken = Environment.GetEnvironmentVariable("AGENTICUI_PIPE_TOKEN")
    ?? throw new InvalidOperationException("Missing local pipe token.");

using var server = new AgenticNamedPipeServer(
    pipeName: "AgenticUI.NET",
    options: new AgenticNamedPipeServerOptions
    {
        AuthenticationToken = pipeToken,
        RequireAuthentication = true
    });
server.Start();
```

不要把令牌写进 `appsettings.json`、源码、日志或 Git。Gateway 的公网令牌必须与 Pipe 令牌
不同，建议分别生成至少 32 个随机字节。

## 2. 配置 TLS 和 Gateway

Kestrel 默认监听 `https://0.0.0.0:7443`。本地开发可使用受信任的开发证书；正式部署应使用
由客户端信任、名称与服务器域名匹配的证书。下面只展示环境变量名，不应把真实值提交到仓库：

```powershell
$env:AgenticUI__AuthenticationToken = "<独立的 Gateway 随机令牌>"
$env:AgenticUI__LocalAuthenticationToken = "<桌面应用使用的 Pipe 令牌>"
$env:Kestrel__Certificates__Default__Path = "C:\secure\agenticui-gateway.pfx"
$env:Kestrel__Certificates__Default__Password = "<证书密码>"

dotnet run --project src/AgenticUI.Gateway/AgenticUI.Gateway.csproj -c Release
```

正式部署还应限制防火墙入站来源、使用专用低权限账户运行 Gateway、保护证书私钥，并在反向
代理或负载均衡器终止 TLS 时正确配置可信代理。不要为了方便同时开放 `ws://` 明文端口。

## 3. WSS 协议

连接地址默认为 `wss://服务器:7443/agenticui`。每个 WebSocket 文本消息包含一个完整 JSON
对象；与 Named Pipe 的字段模型相同，但不使用 JSONL 换行分帧。

连接后的第一条消息必须认证：

```json
{
  "requestId": "auth-1",
  "type": "authenticate",
  "authenticationToken": "由安全配置提供的 Gateway 令牌",
  "clientName": "Production AI Agent"
}
```

认证成功后可枚举控件：

```json
{"requestId":"list-1","type":"listControls","includeHidden":false}
```

以及发送语义动作：

```json
{
  "requestId":"execute-1",
  "type":"execute",
  "command":{
    "requestId":"command-1",
    "controlId":"orders.grid",
    "action":"highlightCell",
    "arguments":{"row":0,"column":"OrderNumber"}
  }
}
```

Gateway 会继续转发桌面应用产生的 `event` 消息。客户端必须使用唯一、非空且不超过 128
字符的 `requestId`。

## 4. 默认安全策略

默认配置允许枚举以及读取、聚焦和高亮类动作，不允许 `click`、`setText`、`addRow`、
`deleteRow` 等会改变业务状态的动作。需要某项写操作时，在 `AgenticUI:AllowedActions` 中
逐项加入；`"*"` 可放行所有动作，但不建议用于生产环境。

`mouseMove`、`mouseClick`、`mouseDoubleClick`、`mouseWheel` 和 `mouseDrag` 同样默认不放行。
它们虽然只能作用于应用内已注册控件，但属于低层输入能力，生产环境应按具体画布控件和业务
场景在 Gateway 白名单与宿主 `IAgenticCommandAuthorizer` 中同时授权。

Gateway 还默认限制：

- 最大 32 个并发 WebSocket 连接；
- 每来源 IP 每分钟最多 30 次 HTTP/WSS 连接尝试；
- 每连接每分钟 120 个请求；
- 单消息最大 1 MiB；
- 可选浏览器 `Origin` 白名单；
- 结构化审计只记录来源地址、客户端名、请求 ID、控件 ID、动作与结果，不记录参数和令牌。

这些限制不替代宿主应用的 `IAgenticCommandAuthorizer`。涉及付款、删除、设备启停、工艺参数
修改等高风险操作时，桌面应用仍应做业务授权和二次确认。

## 5. 可选 UDP 局域网发现

发现服务默认关闭。确认局域网允许广播后再设置：

```json
{
  "AgenticUI": {
    "Discovery": {
      "Enabled": true,
      "Port": 47731,
      "IntervalSeconds": 5,
      "ServiceName": "Factory A AgenticUI Gateway",
      "PublicWebSocketUrl": "wss://gateway.factory.example:7443/agenticui"
    }
  }
}
```

Gateway 只发送 `AgenticUI.Discovery.v1` 广播。可用同一程序的监听模式检查广播：

```powershell
dotnet run --project src/AgenticUI.Gateway/AgenticUI.Gateway.csproj -- --discover
```

监听器只显示发现到的服务，不发送认证或控制数据。跨网段发现应使用管理员明确配置的 DNS、
服务注册表或反向代理地址，不应扩大 UDP 广播范围。

## 6. 配置项

| 配置 | 默认值 | 说明 |
| --- | --- | --- |
| `PipeName` | `AgenticUI.NET` | 本机 Named Pipe 名称 |
| `AuthenticationToken` | 空（启动失败） | WSS 客户端令牌，至少 32 字符 |
| `LocalAuthenticationToken` | 空（启动失败） | Pipe 令牌，至少 32 字符且不得与公网令牌相同 |
| `WebSocketPath` | `/agenticui` | WSS 路径 |
| `AllowedActions` | 读取/引导动作 | Gateway 动作白名单 |
| `AllowedOrigins` | 空 | 浏览器 Origin 白名单；非浏览器客户端通常不发送 Origin |
| `MaxConnections` | `32` | 最大并发连接数 |
| `ConnectionAttemptsPerMinute` | `30` | 每来源 IP 每分钟 HTTP/WSS 连接尝试数 |
| `RequestsPerMinute` | `120` | 单连接每分钟请求数 |
| `Discovery.Enabled` | `false` | 是否发送 UDP 发现广播 |

环境变量使用双下划线表示层级，例如
`AgenticUI__Discovery__Enabled=true`。健康检查地址为 HTTPS `/healthz`，只返回运行状态、命令
传输类型和发现服务是否启用，不返回密钥或控件信息。
