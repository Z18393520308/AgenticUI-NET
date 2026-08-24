# AgenticUI.NET 架构

## 设计目标

AgenticUI.NET 不把 AI 绑定到具体控件框架。WPF、WinForms 以及未来 Web 端都映射到同一组
稳定概念：

```text
控件身份 → 描述与状态 → 可执行语义动作 → 可观察语义事件
```

控件的 UI 框架只负责将 `click`、`setText`、`selectItem` 等动作转换成正确的 UI
线程操作，并将用户交互转换为统一事件。

## 分层

### AgenticUI.Core

- `AgenticControlRegistry`：弱引用控件注册表，防止注册表阻止窗口释放。
- `AgenticEventBus`：进程内广播，每个事件分配单调递增顺序号。
- `AgenticCommandDispatcher`：查找控件、验证动作、执行授权并分发命令。
- `AgenticLogRecorder`：JSONL 审计日志和敏感字段脱敏。
- `AgenticInteractionRecorder`：把用户语义事件转换成可回放命令。
- `AgenticReplay`：按顺序执行录制命令。

### UI 适配层

WPF 使用 `AdornerLayer` 绘制独立高亮，不改变按钮的按下状态。WinForms 使用添加到顶层
窗体的前景覆盖层，并通过窗口样式和命中测试实现点击穿透。两端均提供：

- 替换式 `AgenticButton`、`AgenticTextBox`、`AgenticCheckBox`、
  `AgenticRadioButton`、`AgenticComboBox`
- 原生控件接入方式
- 原生外观及可选现代主题

### 本机远程层

`AgenticUI.Remote` 使用 Named Pipe 提供本机 JSONL 协议。服务启动时默认生成 256 位
随机令牌，客户端必须先认证。每个请求有独立请求 ID，客户端后台接收循环可以同时处理
并发响应与广播事件。服务位于被控应用进程内，因此所有语义动作最终由对应 UI
Dispatcher/消息线程执行。

### 可选网络网关

`AgenticUI.Gateway` 是独立 `.NET 8` 进程，不进入 WPF/WinForms 应用进程。它只在 TLS 上接受
WebSocket 请求，使用独立的 Gateway 令牌认证远程客户端，经连接数、速率和动作白名单检查后，
再用另一把本机令牌连接 `AgenticUI.Remote` Named Pipe。桌面应用继续只暴露本机管道。

可选 UDP 服务是单向广播器，仅公布 WSS 地址和非敏感服务元数据。控制面始终走 TCP/TLS
体系，UDP 不参与认证、命令和事件传输。

## 身份

业务代码应显式提供长期稳定 ID，例如 `settings.notifications.email`。如果没有提供，
注册表生成 `temporary.N`，仅适合调试和一次性会话。临时 ID 不保证跨运行稳定。

同一进程内不允许两个活动控件使用同一个稳定 ID。

## 事件来源

每个事件带有来源：

- `User`：用户直接操作。
- `Remote`：本机远程命令导致。
- `Programmatic`：宿主程序主动操作。
- `Replay`：录制回放导致。

首版 UI 适配器能够可靠区分用户和远程来源。宿主程序若需要标记
`Programmatic`，后续将提供显式操作作用域 API。

## 安全边界

- WPF/WinForms 控件库和 `AgenticUI.Remote` 不监听 TCP，不默认暴露局域网或互联网端口。
- 独立 Gateway 默认不随桌面应用启动，配置不完整时拒绝启动，只接受 WSS/TLS。
- Gateway 的公网令牌与本机 Pipe 令牌必须不同；动作还受白名单和宿主授权器双重约束。
- UDP 发现默认关闭且只发送公开元数据，不接收控制命令。
- 未认证连接不能枚举控件、订阅事件或执行命令。
- 认证令牌使用密码学安全随机数生成，并以固定时间方式比较。
- `.NET 8` 服务使用 `CurrentUserOnly` 管道选项，把连接限制在当前操作系统用户。
- `IAgenticCommandAuthorizer` 可针对控件、动作和参数拒绝命令。
- 密码控件和标记为敏感的文本控件默认脱敏。
- 敏感文本默认不进入可回放录制文件。
- 高风险业务动作仍应由应用自身进行权限检查和二次确认。

## 后续演进

1. Gateway 的可轮换短期令牌、mTLS/OIDC 与设备身份。
2. 控件状态主动推送和远程命令取消。
3. WPF/WinForms 视觉自动化测试及无障碍树映射。
4. Web Components/React 适配层。
5. 企业集中审计、策略、设备管理和签名录制文件。
