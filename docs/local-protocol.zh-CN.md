# AgenticUI.NET 本机协议

传输使用本机 Named Pipe，默认管道名为 `AgenticUI.NET`。每条消息是一行 UTF-8 JSON。

## 认证

服务端默认生成 256 位随机令牌。连接建立后的第一个请求应完成认证：

```json
{
  "requestId":"auth-1",
  "type":"authenticate",
  "authenticationToken":"由宿主应用安全提供的令牌",
  "clientName":"My AI Agent"
}
```

成功响应：

```json
{"requestId":"auth-1","type":"authenticated"}
```

认证前不能枚举控件、执行命令或接收事件。令牌只应通过受信任的本机渠道传递，不应写入
源码、普通日志或提交到版本控制。

## 枚举控件

请求：

```json
{"requestId":"list-1","type":"listControls"}
```

响应：

```json
{
  "type":"controls",
  "requestId":"list-1",
  "controls":[{
    "id":"login.submit",
    "name":"登录",
    "kind":"button",
    "isTemporaryId":false,
    "isSensitive":false,
    "isEnabled":true,
    "actions":["focus","highlight","clearHighlight","click"],
    "state":{"visible":true,"focused":false}
  }]
}
```

## 执行动作

```json
{
  "type":"execute",
  "requestId":"execute-1",
  "command":{
    "requestId":"req-42",
    "controlId":"login.submit",
    "action":"highlight",
    "arguments":{}
  }
}
```

文本框、数值控件和下拉列表的参数示例：

```json
{"controlId":"login.username","action":"setText","arguments":{"text":"alice"}}
{"controlId":"login.username","action":"getText","arguments":{}}
{"controlId":"login.remember","action":"getChecked","arguments":{}}
{"controlId":"login.role","action":"selectItem","arguments":{"index":1}}
{"controlId":"demo.volume","action":"setValue","arguments":{"value":40}}
{"controlId":"demo.date","action":"setValue","arguments":{"value":"2026-08-01"}}
{"controlId":"demo.grid","action":"selectRow","arguments":{"row":0}}
{"controlId":"demo.grid","action":"getCell","arguments":{"row":0,"column":"Name"}}
{"controlId":"demo.grid","action":"setCell","arguments":{"row":0,"column":0,"text":"Alice"}}
{"controlId":"demo.tree","action":"expand","arguments":{"path":"公司/研发"}}
{"controlId":"demo.menu","action":"click","arguments":{"path":"文件/打开"}}
```

`getText` 成功后，响应里的 `result.control.state.text` 即为当前文本（敏感字段为 `null`）。  
`getChecked` 成功后，响应里的 `result.control.state.checked` 为是否选中。
`getValue` 成功后，响应里的 `result.control.state.value` 为当前值；日期接受 ISO-8601 或
`yyyy-MM-dd`，数值控件接受 JSON 数字。
## 事件广播

连接的客户端会收到事件消息：

```json
{
  "type":"event",
  "event":{
    "sequence":18,
    "controlId":"login.submit",
    "name":"clicked",
    "source":"User",
    "timestamp":"2026-07-31T02:30:00Z",
    "data":{}
  }
}
```

客户端必须允许事件消息与普通响应交错出现。普通响应通过顶层 `requestId` 与请求关联；
事件没有请求 ID。`AgenticNamedPipeClient` 使用后台接收循环，通过 `EventReceived`
暴露事件，并支持多个并发请求。

## 支持的动作

- `focus`
- `highlight`
- `clearHighlight`
- `click`
- `setText`
- `getText`
- `setChecked`
- `getChecked`
- `setValue`
- `getValue`
- `selectItem`
- `selectRow`
- `getCell`
- `setCell`
- `expand`
- `collapse`
- `openDropDown`
- `closeDropDown`

`click` 按控件类型解释：按钮触发点击；文本框聚焦；单选框选中；复选框切换；
下拉列表打开选项。显式设置状态仍应优先使用 `setText` / `setChecked` / `setValue`。
`selectItem` 接受从零开始的 `index`，或者与项目显示文本匹配的 `value`；树节点还支持
`path`（用 `/` 分隔，如 `公司/研发`）。
`selectRow` / `getCell` / `setCell` 用于表格：`row` 为行号，`column` 为列号或列名，
`setCell` 另需 `text`（或 `value`）。
`expand` / `collapse` 用于树，参数同树的 `selectItem`。
菜单与工具栏的 `click` 使用 `path`（如 `文件/打开`）或 `value`（项文本，忽略 `&`）。
`CheckedListBox` 的 `setChecked` 需要同时提供 `index` 和 `checked`。
WinForms 另有 `NumericUpDown`、`CheckedListBox`、`StatusStrip`；WPF 另有 `ToggleButton`。
表格、树、ListView、菜单/工具栏、进度条、标签两端均支持。

控件描述中的 `actions` 是该控件实际允许的动作集合，客户端不应假设每种控件都支持
所有动作。
