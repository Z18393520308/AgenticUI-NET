# 应用内网关与首次配对（开发分支，尚未发布）

应用引用 AgenticUI.Remote，在入口初始化一次。无需独立 Gateway，无需 HTTP.sys、
netsh、管理员监听授权或向 Windows 证书库安装证书。控件仍不包含 AI。

## 一、接入与配置

WinForms 保留原有 UI 初始化，然后绑定应用生命周期：

```csharp
using (var host = AgenticApplicationHost.StartFromConfiguration())
{
    Application.Run(new MainForm());
}
```

WPF 在 App.OnStartup 调用同一初始化方法，在 App.OnExit 调用 host.Dispose()。
不要在控件构造函数、每个窗口或后台循环中反复启动服务。
业务中原有手动创建 AgenticNamedPipeServer 的代码应替换掉，避免重复管道。

将 agenticui.json 放在 EXE 同目录，项目设置复制：

```xml
<None Update="agenticui.json" CopyToOutputDirectory="PreserveNewest"
      CopyToPublishDirectory="PreserveNewest" />
```

本机调试配置示例（WPF 将管道名改为 AgenticUI.NET.Wpf，端口改为 7444）：

```json
{
  "AgenticUI": {
    "AutoStart": true,
    "Local": { "Enabled": true, "PipeName": "AgenticUI.NET" },
    "Network": {
      "Enabled": true,
      "BindAddress": "127.0.0.1",
      "ListenUrl": "https://localhost:7443",
      "AllowedActions": ["highlight", "clearHighlight", "getText", "getRows", "getColumns"]
    },
    "Discovery": {
      "Enabled": true,
      "ServiceName": "WinForms Demo"
    }
  }
}
```

仓库两个 Demo 仍默认关闭 Network 和 Discovery；本机调试只需把两个 Enabled 改为 true。
两者端口分别预配 7443 / 7444，不再争用端口。Discovery.PublicWebSocketUrl 省略时，
从 ListenUrl 和 WebSocketPath（默认 /agenticui）生成公告地址。

- AutoStart=false：关闭全部通信；Network.Enabled=false：仅本机管道。
- Network=true 要求 Local=true；Discovery 不会隐式开启网络。
- 修改配置后重启应用生效，不做文件热重载。
- 不同产品/多实例必须使用不同管道名、网络端口及身份目录。
- 缺失文件/非法配置不启动服务，LastError 报告错误，业务 UI 可继续使用。
- 网络启动失败保留已经启动的本机管道；不会广播失败的服务。
- 配置沿用 https:// 作为监听描述，控制端使用 wss://；不支持明文 ws://。

跨电脑时另设置：
- BindAddress 为本机局域网 IP，或显式设置 0.0.0.0 监听所有 IPv4 网卡。
- ListenUrl 为 https://目标电脑可访问的主机名或IP:7443。
- 如公告地址与监听描述不同，显式填 Discovery.PublicWebSocketUrl。
- 不要向其他电脑公告 localhost。端口仍可能被 Windows 防火墙、网络隔离策略阻挡，
  本库不自动修改防火墙；按需要放行可信局域网的 TCP 控制端口与扫描端 UDP 47731。

## 二、Demo 的实际操作顺序

1. 改配置、重启目标 Workbench，确认网络已启动。
2. 在目标端点击“网络身份 / 开启首次配对”，显示完整 SHA-256 证书指纹和一次性码。
3. 控制端选择 WSS，点击“扫描 Gateway”，选中软件后点击连接。
4. 对照目标软件本地显示的完整指纹（或通过可信渠道核验），确认一致后勾选确认、
   输入一次性码。不要仅根据 UDP 名称或公告指纹认定可信。
5. 配对成功后自动连接，凭据在控制端本地保存。普通重启不必再次配对。

一次性码有效 3 分钟、最多尝试 5 次，只能成功使用一次；关闭目标端配对窗口即取消。
只能由本机 UI/API 开启配对，网络请求无法开启。该按钮不注册为 AI 控件，不能远程触发。
目标端点击“撤销所有已配对控制端”会撤销配对凭据并断开当前网络会话。
控制端“忘记当前配对”只删本地记录，不等于撤销目标端授权。

## 三、业务软件如何提供配对界面

目标端可以复用 samples/Shared 的 WinForms/WPF 对话框，也可自己实现原生界面：

```csharp
var pairing = AgenticApplicationHost.Current?.Pairing;
if (pairing != null)
{
    var fingerprint = pairing.CertificateFingerprint; // 本地显示，供独立核验
    var code = pairing.BeginPairing();                  // 本地显示，切勿广播/写日志
    // 用户关闭窗口时：pairing.CancelPairing();
    // 用户撤销授权时：pairing.RevokeAllClients();
}
```

不应将这几个管理方法暴露为远程可调用的控件动作。

控制端新增 API：

```csharp
var store = new AgenticPairingStore();
using var client = await AgenticWebSocketClient.ConnectPairedAsync(
    new Uri("wss://目标电脑:7443/agenticui"),
    async prompt =>
    {
        // 在 UI 线程弹窗，展示 prompt.Endpoint 与 CertificateFingerprint。
        // 用户独立核验后返回目标端一次性码；取消则返回 null。
        return await ShowPairingDialogOnUiThreadAsync(prompt);
    },
    store);
```

上面的 ShowPairingDialogOnUiThreadAsync 是业务 UI 的示意函数；完整可编译示例在
samples/Shared 和两个 RemoteConsole。回调可能来自后台线程，必须自行切回 UI 线程。
后续连接严格按完整 WSS 地址与证书指纹验证，成功认证后才保存首次信任记录。
证书不同、过期或身份文件损坏都不会自动重新信任；先核实原因，再由用户显式解除旧配对。
地址变化需重新确认，不根据 UDP 的 InstanceId 自动迁移信任。

## 四、密钥、证书及权限

- 目标端首次启用网络时自动生成 RSA 2048 / SHA-256 自签名证书，有效 2 年。
  证书是应用身份，不依赖系统证书信任链；客户端通过独立核验后的完整指纹校验身份。
- 不修改 X509Store、不调用 netsh。TLS 使用平台 SslStream，最低/当前协议为 TLS 1.2。
- 目标身份默认在当前用户 LocalApplicationData/AgenticUI.NET/identities 下按管道名隔离；
  可用 Network.StateDirectory 指定本应用专用目录。不要放进源码、共享目录或安装目录。
- Windows 身份私钥和客户端配对凭据使用当前用户 DPAPI 保护。不同 Windows 账户不能
  直接复制这些文件使用。非 Windows 的开发测试使用权限受限的本地文件，不宣称等同 DPAPI。
- 目标端最多保存 64 个客户端凭据哈希，不保存客户端明文令牌；一次性码只在内存存在。
- 证书正常重启不更换。过期/损坏会拒绝启动，不静默生成新证书；当前还没有自动续期
  或身份重置 UI，需管理员备份并显式更换本应用身份后，让各控制端重新核验配对。
- 对身份目录实行独占锁，防止两个进程同时修改。更新软件不能清空该目录。
- 配对不绕过 AllowedActions 或业务授权器。默认只允许读取/引导等动作；写操作按需逐项开放。
- UDP 只发送公开发现信息，不承载认证、配对码、令牌或控制命令。

为了兼容既有集成，ConnectAsync 的显式令牌入口保留；可配置
Network.TokenEnvironmentVariable（默认 AGENTICUI_GATEWAY_TOKEN）提供至少 32 位的网络令牌，
且不能与 Pipe 令牌相同。默认无需设置此变量，直接走首次配对。
设置了该变量就额外保留一条共享令牌认证入口；撤销配对不会使该环境变量令牌失效。
生产不需要该入口时请勿设置它。两个 Demo 的 WSS 模式已改为配对，不再自动跳过证书验证。

现有浏览器不能直接使用 .NET 客户端的指纹配对接口。未来 Web 控制端仍需受浏览器信任的
HTTPS 证书/可信代理或另行设计的本地连接桥接；不能承诺自签名证书在浏览器无提示可用。

## 五、实现与验收

net8 的 WebSocket 帧使用平台实现；net48 的服务端采用 Ninja.WebSockets 1.1.8 兼容实现，
TLS 使用 .NET，不自行实现加密算法。HTTP Upgrade 入口只接受指定路径，8 KiB 头部上限、
10 秒握手期限，连接数量在 TLS 握手前限制；消息和发送队列均有界。
保留动作白名单、限流、Pipe 会话隔离、断线引导清理、命令超时不自动重试。

自动化测试覆盖真实 TLS 配对/重连、错误码、取消、证书变化拒绝、撤销，以及旧转发回归。
另外提供 AgenticUI.Remote.Net48.Tests，在 Windows 验证 Schannel、DPAPI 及 net48 双端路径。
macOS 上编译 net48 不代表执行过 Windows 测试。

Windows 手动验收：
- [ ] 普通用户、无 HTTP.sys 绑定、未安装证书的干净机器上启动。
- [ ] 两个 Demo 独立端口启动，扫描、核验、首次配对并读取控件。
- [ ] 普通重启保留配对；错误码、取消、证书更换必须拒绝。
- [ ] 撤销授权后已有连接断开，旧凭据不能重新连接。
- [ ] 关闭 Network/Discovery 并重启后不再监听/广播；人工操作不受影响。
- [ ] 跨电脑验证防火墙、网卡绑定和真实公告地址。

本功能尚未发布，0.6.1 NuGet 不包含以上 API。
