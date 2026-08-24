# AgenticUI.NET 本机协议

传输使用本机 Named Pipe，默认管道名为 `AgenticUI.NET`。每条消息是一行 UTF-8 JSON。
该端点不应跨机器暴露。跨机器访问由独立 `AgenticUI.Gateway` 通过 WSS/TLS 转发，见
[Gateway 安全部署指南](gateway.zh-CN.md)。

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

默认只返回**当前对用户可见、且未被完全遮挡**的控件（在活动标签页内、滚动可视区内、
宿主窗口未最小化）。需要全量（含隐藏）时设置：

```json
{"requestId":"list-1","type":"listControls","includeHidden":true}
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
    "state":{"visible":true,"displayable":true,"focused":false}
  }]
}
```

`state.visible` 为框架级可见性；`state.displayable` 表示是否处于当前可展示区域、
中心点未被其它控件完全遮挡，且（若存在模态顶级弹窗）属于该弹窗。
远程默认枚举按 `displayable` 过滤。存在模态弹窗时，远程 `execute` 也只能操作该弹窗内控件，
以遵守软件原有的模态操作规则。

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
{"controlId":"demo.grid","action":"getRow","arguments":{"row":0}}
{"controlId":"demo.grid","action":"getRows","arguments":{"start":0,"count":50}}
{"controlId":"demo.grid","action":"getColumns","arguments":{}}
{"controlId":"demo.grid","action":"getCell","arguments":{"row":0,"column":"Name"}}
{"controlId":"demo.grid","action":"setCell","arguments":{"row":0,"column":0,"text":"Alice"}}
{"controlId":"demo.grid","action":"scrollToRow","arguments":{"row":20}}
{"controlId":"demo.grid","action":"addRow","arguments":{"values":{"Name":"Alice","Score":95}}}
{"controlId":"demo.grid","action":"deleteRow","arguments":{"row":2}}
{"controlId":"demo.grid","action":"sortByColumn","arguments":{"column":"Score","direction":"descending"}}
{"controlId":"demo.grid","action":"filterByColumn","arguments":{"column":"Name","value":"Alice","mode":"contains"}}
{"controlId":"demo.grid","action":"highlightCell","arguments":{"row":0,"column":"Name"}}
{"controlId":"demo.grid","action":"selectCell","arguments":{"row":0,"column":"Name"}}
{"controlId":"demo.tree","action":"expand","arguments":{"path":"公司/研发"}}
{"controlId":"demo.menu","action":"click","arguments":{"path":"文件/打开"}}
```

`getText` 成功后，响应里的 `result.control.state.text` 即为当前文本（敏感字段为 `null`）。  
`getChecked` 成功后，响应里的 `result.control.state.checked` 为是否选中。
`getValue` 成功后，响应里的 `result.control.state.value` 为当前值；日期接受 ISO-8601 或
`yyyy-MM-dd`，数值控件接受 JSON 数字。

## 应用内鼠标动作

鼠标动作只作用于指定的、已注册的 AgenticUI 控件，不使用系统级鼠标注入。坐标均为目标
控件内部 `0～1` 的相对值，因此不会因 DPI 或窗口尺寸变化而直接失效：

```json
{"controlId":"editor.canvas","action":"mouseMove","arguments":{"xRatio":0.5,"yRatio":0.5}}
{"controlId":"editor.canvas","action":"mouseClick","arguments":{"xRatio":0.5,"yRatio":0.5,"button":"left"}}
{"controlId":"editor.canvas","action":"mouseDoubleClick","arguments":{"xRatio":0.5,"yRatio":0.5}}
{"controlId":"editor.canvas","action":"mouseWheel","arguments":{"xRatio":0.5,"yRatio":0.5,"delta":-120}}
{"controlId":"editor.canvas","action":"mouseDrag","arguments":{"startXRatio":0.2,"startYRatio":0.3,"endXRatio":0.8,"endYRatio":0.7,"button":"left","steps":12}}
```

- `button` 可选 `left`、`right`、`middle`，默认 `left`。
- `mouseWheel.delta` 范围为 `-1200～1200` 且不能为 `0`，默认 `120`。
- `mouseDrag.steps` 范围为 `1～100`，默认 `10`；四个起止坐标必须提供。
- 单击等普通业务操作应优先使用 `click` 等语义动作，鼠标动作主要用于画布和自定义绘图区域。
- 控件、坐标点或拖拽路径被遮挡、离开活动模态窗口、隐藏或禁用时，动作会被拒绝。

WSS Gateway 默认动作白名单不包含这些低层动作。需要跨机器使用时应逐项授权，并继续通过
宿主 `IAgenticCommandAuthorizer` 检查。

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
- `getRow`
- `getRows`
- `getColumns`
- `getCell`
- `setCell`
- `scrollToRow`
- `addRow`
- `deleteRow`
- `sortByColumn`
- `filterByColumn`
- `highlightCell`
- `selectCell`
- `expand`
- `collapse`
- `openDropDown`
- `closeDropDown`

`click` 按控件类型解释：按钮触发点击；文本框聚焦；单选框选中；复选框切换；
下拉列表打开选项。显式设置状态仍应优先使用 `setText` / `setChecked` / `setValue`。
`selectItem` 接受从零开始的 `index`，或者与项目显示文本匹配的 `value`；树节点还支持
`path`（用 `/` 分隔，如 `公司/研发`）。

### 表格动作

表格中的 `row` / `start` / `column` 数字索引都从 0 开始；`column` 也可使用列名、
列标题或绑定属性名。`row` 始终指当前排序、过滤后视图中的行号。

- `getRow` 返回 `state.row`；`getRows` 返回 `state.rows`，支持 `start` / `count` 分页。
  `count` 默认 50，最大 500，响应同时包含 `start` / `count` / `total`。
- `getColumns` 返回 `state.columns`，包含索引、名称、标题、绑定属性、
  只读/可见状态和排序方向。
- `selectRow` / `scrollToRow` 需要 `row`；`selectCell` / `highlightCell` 需要 `row` 和
  `column`。`clearHighlight` 会同时清除整个控件和单元格高亮。
- `getCell` / `setCell` 需要 `row` 和 `column`；`setCell` 另需 `text` 或 `value`。
- `addRow` 的 `values` 是以列名为键的 JSON 对象；`deleteRow` 需要 `row`。
  WinForms 支持未绑定表格或 `BindingSource`；WPF 需要可写、可推断行类型的
  `IList` 数据源。
- `sortByColumn` 需要 `column`，`direction` 可为 `ascending` / `descending`（也接受
  `asc` / `desc`）。
- `filterByColumn` 需要 `column` 和 `value`，`mode` 可为 `contains`、`equals` 或
  `startsWith`；`value` 为空字符串或 `null` 时清除过滤。WinForms 绑定表格要求
  `BindingSource` 的底层数据源支持过滤。

WPF 的 `setCell` / `sortByColumn` / `filterByColumn` 要求目标列为可识别绑定路径的
`DataGridBoundColumn`。不支持的数据源或只读属性会返回明确的失败原因。
`expand` / `collapse` 用于树，参数同树的 `selectItem`。
菜单与工具栏的 `click` 使用 `path`（如 `文件/打开`）或 `value`（项文本，忽略 `&`）。
`CheckedListBox` 的 `setChecked` 需要同时提供 `index` 和 `checked`。
WinForms 另有 `NumericUpDown`、`CheckedListBox`、`StatusStrip`；WPF 另有 `ToggleButton`。
表格、树、ListView、菜单/工具栏、进度条、标签两端均支持。

控件描述中的 `actions` 是该控件实际允许的动作集合，客户端不应假设每种控件都支持
所有动作。
