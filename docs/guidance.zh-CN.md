# 远程动态高亮、编号与气泡

本文能力从正式版 `0.6.0` 起提供；旧版 0.5.0 不支持这些新增参数。

## 产品边界

AgenticUI 只提供可发现、可操作、可观察、可引导的控件。控件不包含大模型、Agent 规划或
自然语言执行器。人和外部控制端访问同一个实际控件，业务逻辑留在原应用内。

配套工控机 Agent 决定是否显示气泡、显示什么；控件只把经过协议验证的纯文本显示在
实际目标旁边。气泡内容不执行，不自动点击，不授予权限，也不是业务完成证明。

## 先检查能力

字段约束见 [JSON Schema](protocol/guidance-v1.schema.json)，已执行结果与待验收项见
[升级验证记录](upgrade-verification.zh-CN.md)。
第三阶段的只读与树节点字段见[状态契约](control-state-contract.zh-CN.md)，实机入口见
[Windows 联合验收](windows-acceptance.zh-CN.md)。

`listControls` 返回的每个可引导控件新增：

```json
{"capabilities":["dynamicGuidance.v1"]}
```

旧目标不包含该能力时，不要发送新增参数后假设它已生效。控制端应明确提示升级，或由
用户选择旧版静态引导。新增描述字段对旧客户端是可忽略的扩展。

## 显示与更新

以下是 `execute` 请求中的 `command`；通过 Named Pipe、WSS 或本机 dispatcher 调用时
使用相同字段：

```json
{
  "controlId": "settings.temperature",
  "action": "highlight",
  "arguments": {
    "guidanceId": "task-123-step-2",
    "showOutline": true,
    "showNumber": true,
    "instructionNumber": 2,
    "showBubble": true,
    "hint": "请填写目标温度，填写后检查单位是否为 ℃。",
    "placement": "auto",
    "durationMs": 0
  }
}
```

再次向同一控件、同一连接、同一 `guidanceId` 发送 `highlight`，更新现有展示，不触发
业务动作，也不修改控件预设的 `Hint` / `InstructionNumber`。

- `controlId` 是稳定控件身份；`instructionNumber` 是当前任务的步骤号。
- `guidanceId` 可省略，默认 `default`；长度 1–128，不允许控制字符。
- `showOutline` 默认 true。
- `instructionNumber` 范围 0–99999；兼容旧控制端传入的 `number`，二者同时出现时前者优先。
- `showNumber` 省略时由编号是否大于 0 决定；显式 true 必须提供正编号。
- `hint` 为最多 1000 字符的纯文本，支持中文换行，不解释 HTML / Markdown / 脚本。
- `showBubble` 省略时由提示是否非空决定；显式 true 要求非空提示；显式 false 始终隐藏气泡。
- `placement` 为 `auto`、`top`、`bottom`。位置只是偏好，边缘处会调整并限制在目标屏幕工作区。
- `durationMs` 范围 0–3600000。0 表示保持到清除、连接断开或目标卸载；再次更新重置计时。

每次命令是完整展示快照，不是对上次远程状态的局部合并。建议新客户端始终显式传递三个
显隐开关和提示。省略编号/文本时，为兼容旧调用，允许回退控件的本地默认属性。
**显式 `showBubble:false` 时绝不会显示本地默认 Hint。**

仅气泡：`showOutline:false, showNumber:false, showBubble:true`。
仅描边：`showOutline:true, showNumber:false, showBubble:false`。
三个开关都为 false 时，清除对应 guidanceId 的展示。

## 清除与连接隔离

```json
{"controlId":"settings.temperature","action":"clearHighlight","arguments":{"guidanceId":"task-123-step-2"}}
```

省略 `guidanceId` 会清除该连接在此控件上的全部引导，不会清理其他连接的引导。会话身份由
Named Pipe 服务端赋予，不接受网络 JSON 提供的 `sessionId`。Gateway 每个 WSS 连接使用独立
Pipe 连接，所以隔离及断线清理也适用于 WSS。进程内直接调用默认使用 local 作用域。

清理属于展示资源释放，即使窗口已被模态对话框阻挡也允许清理。应用退出、控件卸载和
引导超时释放窗口及计时器。每个控件的每类引导集合最多 16 个并发展示。

## DataGrid 单元格

使用 `highlightCell`，在同样参数中增加 `row` 和 `column`。行号仍是当前视图行号，不能
视为主键。发生重排、过滤或容器复用时，引导会跟随仍有效的目标或关闭；控制端必须重新
读取视图再发下一条命令，不应缓存旧行号。

## Agent 侧约定

模型步骤使用 `guidance` 对象表达显示意图，其中 `text` 由控制端映射为协议的 `hint`。
不需要模型生成 `highlight` / `clearHighlight` 业务步骤。执行、等待人工、显示提示是不同
概念；高风险确认仍由宿主业务及控制端策略独立执行，气泡不能代替确认。

演示模式先展示再执行；智能模式也接受模型指定的引导；引导模式等待人工事件；示教只
记录人的业务操作。提示文字不应包含密码、密钥或来自本机凭据槽的真实内容。

配套 Agent 智能模式显示气泡时，默认预览 1600ms；若指定 durationMs，则预览时间取其与
5000ms 的较小值。人工引导尚不能核验的双击、轨迹和表格定位动作会明确拒绝，而不是
误报完成。任务清理使用有超时的尽力清理；连接关闭后另由服务端清理该会话展示。

## 可运行演示与验收

1. Windows 启动任一 Workbench，再启动对应 Remote Console，连接 Pipe 或 WSS Gateway。
2. 选择带有本地 Hint 的按钮，在“动态引导”中输入不同内容，点击“显示 / 更新引导”。
3. 修改文本再次更新；取消“气泡”但保留“描边”，确认旧 Hint 没有重新出现。
4. 分别验证仅气泡、仅编号、3 秒消失；移动、缩放窗口，滚动目标，切换标签页。
5. 同时连接两个 Console，分别显示引导；断开其中一个，只应移除该连接的引导。
6. 检查人工点击、焦点、禁用控件、只读文本、模态弹窗及 DataGrid 排序后定位。
7. 在 100%、150%、200% 缩放、多显示器以及各个屏幕边缘检查提示换行和点击穿透。

跨平台测试覆盖参数、布局、隔离、脱敏、Pipe 和真实 WSS/TLS 转发；WPF/WinForms 界面
测试需 Windows Desktop Runtime。macOS 编译成功不能代替 Windows 视觉验收。

## Web 扩展预留

协议仅包含 JSON 标量，不包含 HWND、DependencyProperty 或模型提供方类型；Core 的布局和
引导参数不依赖桌面框架。后续 Web 组件应沿用能力名、快照更新与清理语义，并在同一页面的
真实组件上渲染气泡、调用原事件处理；当前尚未实现 Web 组件。
