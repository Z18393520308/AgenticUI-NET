# 随应用启动的内嵌网关（开发分支，尚未发布）

应用引用 AgenticUI.Remote，在入口初始化一次，即可通过配置选择本机管道、
进程内 WSS 和 UDP 发现。不需要单独运行 Gateway，也不应在每个控件构造函数里启动服务。
控件仍不包含模型或 Agent，人和控制端操作同一套真实界面。

## 1. 接入入口

WinForms（net8 与 net48 均可使用宿主 API；保留各自原有的 UI 初始化代码）：

```csharp
using AgenticUI.Remote;

[STAThread]
static void Main()
{
    // 保留项目原有的 ApplicationConfiguration.Initialize() 或 EnableVisualStyles()。
    using (var host = AgenticApplicationHost.StartFromConfiguration())
    {
        // 将 host.LastError 显示在本地诊断页，不要输出认证令牌。
        Application.Run(new MainForm());
    }
}
```

WPF 在 App 中绑定生命周期（不要让某个业务窗口关闭时停止整个应用的服务）：

```csharp
private AgenticApplicationHost _host;
protected override void OnStartup(StartupEventArgs e)
{
    _host = AgenticApplicationHost.StartFromConfiguration();
    base.OnStartup(e);
}
protected override void OnExit(ExitEventArgs e)
{
    _host?.Dispose();
    base.OnExit(e);
}
```

将 agenticui.json 放在 EXE 同目录，项目中设置复制：

```xml
<None Update="agenticui.json" CopyToOutputDirectory="PreserveNewest"
      CopyToPublishDirectory="PreserveNewest" />
```

也可传入配置文件绝对路径。此文件是独立配置，不是旧 Gateway 的 appsettings.json。
每个应用域只创建一个宿主；重复初始化返回同一实例。Dispose 后可显式重新初始化。
不同软件请使用不同 PipeName，多实例软件也需要各自独立的管道名及网络端口。

## 2. 安全默认配置

```json
{
  "AgenticUI": {
    "AutoStart": true,
    "Local": {
      "Enabled": true,
      "PipeName": "MyProduct.AgenticUI",
      "TokenEnvironmentVariable": "AGENTICUI_PIPE_TOKEN"
    },
    "Network": {
      "Enabled": false,
      "ListenUrl": "https://localhost:7443",
      "WebSocketPath": "/agenticui",
      "TokenEnvironmentVariable": "AGENTICUI_GATEWAY_TOKEN",
      "AllowedActions": ["highlight", "clearHighlight", "getText", "getRows", "getColumns"],
      "AllowedOrigins": [],
      "MaxConnections": 32,
      "RequestsPerMinute": 120,
      "ConnectionAttemptsPerMinute": 30,
      "MaximumMessageLength": 65536,
      "CommandTimeoutSeconds": 30
    },
    "Discovery": {
      "Enabled": false,
      "Port": 47731,
      "IntervalSeconds": 5,
      "ServiceName": "MyProduct",
      "PublicWebSocketUrl": "wss://localhost:7443/agenticui"
    }
  }
}
```

- 完全关闭通信：AutoStart=false。
- 只允许本机：Local.Enabled=true，Network.Enabled=false。
- 允许网络：保持 Local.Enabled=true，并设置 Network.Enabled=true。
- 允许局域网发现：再设置 Discovery.Enabled=true；网络关闭时不会广播。
- 配置修改后重启应用生效；不支持文件热重载。
- 缺失文件、拼错字段或非法配置不会开启服务，通过 LastError 报告，不中断业务 UI。
- 网络令牌缺失、HTTP.sys 启动失败时，已经启动的本机管道仍保留；配置校验失败则整体不启动。

没有配置本机令牌环境变量时，会生成本次运行的随机令牌。开发 Demo 可点击本地状态栏
复制令牌给控制端；不再默认使用固定开发令牌。生产软件应自行保护本地令牌交付界面。
网络令牌必须通过环境变量注入，至少 32 个字符，且不同于本机令牌。
建议使用 AgenticRemoteSecurity.CreateToken() 生成，不要将令牌提交到仓库或放进广播。

## 3. Windows 首次部署：HTTPS 证书和监听权限

内嵌监听使用 Windows HTTP.sys，兼容 .NET 8 与 .NET Framework 4.8。
不需要 ASP.NET Core Hosting Bundle；但 HTTPS 证书绑定和 URL 授权必须由管理员/安装程序
预先设置。库不会自动提权、安装证书、修改防火墙或关闭 TLS 验证。
微软说明：[HttpListener 与 HTTPS 证书要求](https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener?view=netframework-4.8.1)。

示意命令（管理员执行前替换尖括号占位符；不要原样粘贴）：

```powershell
netsh http add urlacl url=https://<应用主机名>:7443/ user="<实际运行账户>"
netsh http add sslcert ipport=0.0.0.0:7443 certhash=<证书指纹> appid={<安装程序生成的GUID>} certstorename=MY
```

证书应在本地计算机个人证书库，含私钥，SAN 覆盖客户端使用的主机名；客户端必须信任签发链。
证书绑定中的 0.0.0.0 是 HTTP.sys 端口绑定参数，不是应用 ListenUrl 的允许值。
生产使用指定主机名，避免通配 URL；监听账户仅授予必要权限。
如存在其他 HTTPS 服务，先检查已有 URL/证书绑定，不要覆盖其他软件配置。
需要局域网访问时，由管理员按实际网络范围放行 TCP 7443；发现服务另涉及 UDP 47731。

把 ListenUrl 改为 https://实际主机名:7443，PublicWebSocketUrl 同步为
wss://实际主机名:7443/agenticui；localhost 仅供本机测试，不能用于其他机器发现。
NetworkRunning 表示监听已经启动，不代表证书链、DNS、防火墙均验收通过。

## 4. 控制端与安全边界

现有 AgenticWebSocketClient 继续使用相同认证/命令协议：

```csharp
using var client = await AgenticWebSocketClient.ConnectAsync(
    new Uri("wss://实际主机名:7443/agenticui"),
    Environment.GetEnvironmentVariable("AGENTICUI_GATEWAY_TOKEN"));
var controls = await client.ListControlsAsync();
```

AllowedActions 是明确的动作白名单，不支持 *。要允许修改，按业务需要加入 click、setText 等，
不要为了方便开放全部动作。网关仍通过真实 Pipe 会话执行，保留目标端授权、脱敏和引导清理。
默认不允许有 Origin 的浏览器连接；未来 Web 控制端需要逐项配置 HTTPS AllowedOrigins。
UDP 仅广播公开的服务名、WSS 地址与协议信息，不接收控制命令，也不广播认证令牌。
发现报文不是可信身份凭据，连接后仍须验证 TLS 和认证令牌。
网络请求有认证时限、连接数、每分钟请求数、消息大小限制；慢客户端断开。
命令超时不等于没有执行，不能自动重试写操作。

通过 Current.LocalRunning、NetworkRunning、DiscoveryRunning 检查状态；LastError 表示初始化失败，
运行中传输故障另通过 Trace 诊断。日志不写令牌和命令内容。

## 5. 验收与兼容

两个 Workbench 已包含默认关闭网络的 agenticui.json，并随应用启动/退出管理宿主。
依次验收：人工操作正常 → 本机连接 → WSS 认证/动作白名单 → UDP 发现 →
关闭开关并重启 → 退出后不再监听/广播。错误令牌、无效证书必须无法控制软件。
Windows HTTP.sys 实际证书绑定、权限以及 WPF/WinForms UI 仍需在 Windows 验证；
跨平台测试使用真实 WSS/TLS 验证内嵌转发会话，不代替 HTTP.sys 部署验收。

独立 AgenticUI.Gateway 程序已从源码移除，统一使用应用内宿主。
本功能尚未发布，现有 0.6.1 NuGet 不包含上述新 API。
