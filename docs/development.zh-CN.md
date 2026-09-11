# AgenticUI.NET 开发文档

本文件是唯一的架构、协议、维护和开发交接参考。应用首次接入请先读[使用指南](quickstart.zh-CN.md)。
这里描述当前源码，不把源码实现等同于已发布包；正式版/源码能力分界统一维护在
[版本范围](quickstart.zh-CN.md#start)。核对日期：2026-09-11。

## 目录

- [1. 不可改变的产品边界](#boundaries)
- [2. 工程与运行链路](#architecture)
- [3. 通信协议与结果语义](#protocol)
- [4. 动作参考](#actions)
- [5. 选项协议 items.v1](#items-contract)
- [6. DataGrid 协议](#grid-contract)
- [7. 动态引导协议](#guidance-contract)
- [8. 状态、事件和隐私](#state-events)
- [9. 应用宿主、WSS 与配对](#hosting)
- [10. 扩展控件和安全授权](#extension)
- [11. 构建、本地包和验证](#build)
- [12. Windows 验收](#verification)
- [13. 发布与文档维护](#maintenance)

<a id="boundaries"></a>
## 1. 不可改变的产品边界

1. 控件不含 AI。模型、提示词、任务规划、演示节奏、人工完成判定和技能学习属于外部控制端。
2. 人和 AI 操作同一真实控件，复用原生事件、绑定、编辑和业务校验，不另造 AI 专用影子界面。
3. 同时支持 .NET 8 / .NET Framework 4.8，提供替换控件和原生附加接入；默认原生外观。
4. 稳定手动 ID + 临时自动 ID。引导编号是展示步骤，不是业务身份。
5. 描边、编号、气泡独立可控。高亮不代表按下、授权、确认或业务成功。
6. 默认只开本机 Named Pipe；可选应用内 WSS/TLS 显式开启。不得退回明文控制或跳过证书核验。
7. UDP 只做公开发现公告，绝不传认证、命令、事件或秘密。
8. 日志本地保存、默认语义级别、敏感数据默认脱敏；不得把记录器当防篡改审计或业务事务系统。
9. 鼠标只作用于已注册的本进程界面，不升级成系统 SendInput、桌面或其他软件控制。
10. Web 是未来真实组件适配层，不是把模型塞进控件，也不是给机器人再做一套页面。

宣传可以说减少视觉试错、提供可追踪交互基础；没有测量不能承诺“指数级节省 token”、
“终结 Agent”或“绝对安全”。外部 Agent 仓库不属于本仓库，不把其模式、包清单或测试结果
写成控件库已经实现的能力。

<a id="architecture"></a>
## 2. 工程与运行链路

| 项目 | 目标框架 | 职责与关键入口 |
| --- | --- | --- |
| AgenticUI.Core | net8.0 / netstandard2.0 | Protocol、Abstractions、Registry、Dispatcher、EventBus、Privacy、Guidance、Items、日志/录制/回放 |
| AgenticUI.Remote | net8.0 / net48 | NamedPipeServer/Client、RemoteProtocol、ApplicationHost、TLS/WSS、PairingStore/Service、Discovery |
| AgenticUI.Wpf | net8.0-windows / net48 | Controls、AgenticProperties、WpfControlAdapter、WpfItemAccess/Realization、WpfHighlight、WpfMouseInput、WpfTreeState |
| AgenticUI.WinForms | net8.0-windows / net48 | Controls、AgenticControlBinder/Options、WinFormsControlAdapter、ItemAccess、Highlight、MouseInput |

源代码位于 [src](../src)。Core 不引用桌面框架或模型 SDK，UI 库依赖 Core，Remote 不负责绘制。
跨机链路：

```text
外部控制端
  ├─ 同机：Named Pipe + 令牌 ────────────────────┐
  └─ 跨机：WSS/TLS + 配对凭据                    │
               ↓                               │
       应用内 Gateway → 独立本机 Pipe 连接 ─────┘
                                               ↓
                          Registry → Dispatcher → 授权 → UI 线程 → 原控件
                                               ↓
                                 原生事件 / 结果 → 事件总线 / 本地日志 / 响应
```

Registry 弱引用保存控件，按不区分大小写的 ID 查找；稳定 ID Trim 后注册，重复活动 ID 拒绝。
WPF 在 Loaded/Unloaded 管理注册，WinForms 根据控件生命周期管理。控件对象和事件订阅必须
正确释放，不能以弱引用为由忽略清理。

WPF 描述/执行切 Dispatcher，WinForms 切控件 UI 线程。`Describe()` 也不能跨线程读界面。
覆盖层是归属业务窗口的独立、非激活、点击穿透窗口；不是旧 Adorner 高亮，也不是改控件边框。
WPF 保留布局重入保护及“位置/选项不变不重复更新”，禁止在 LayoutUpdated 中无条件重绘。

<a id="protocol"></a>
## 3. 通信协议与结果语义

以 [Protocol.cs](../src/AgenticUI.Core/Protocol.cs)、[RemoteProtocol.cs](../src/AgenticUI.Remote/RemoteProtocol.cs)
和对应版本测试为实现依据。JSON 使用 AgenticJson.Options：camelCase、枚举字符串。
动作名称发送本文规范大小写，不依赖部分入口的不区分大小写比较。

### 3.1 帧、认证与枚举

Named Pipe 每行一条 UTF-8 JSON。WSS 每条文本消息一份请求 JSON，不是 JSONL 字节流。
Pipe 客户端固定连接本机。认证前不得执行、发现或接收事件：

```json
{"requestId":"auth-1","type":"authenticate","authenticationToken":"LOCAL_TOKEN_PLACEHOLDER","clientName":"MyClient"}
```

成功响应（示例省略 null 字段）：

```json
{"requestId":"auth-1","type":"authenticated"}
```

```json
{"requestId":"list-1","type":"listControls","includeHidden":false}
```

```json
{
  "requestId":"list-1", "type":"controls",
  "controls":[{
    "id":"login.username", "name":"账号", "kind":"textBox",
    "isTemporaryId":false, "isSensitive":false, "isEnabled":true,
    "actions":["focus","highlight","clearHighlight","setText","getText"],
    "capabilities":["dynamicGuidance.v1"],
    "state":{"visible":true,"displayable":true,"focused":false,"readOnly":false,"text":""}
  }]
}
```

示例 actions 是节选。默认发现仅返回当前 displayable 的控件；includeHidden=true 返回
注册表中包含不可展示的项，不授予操作隐藏/遮挡/模态外控件的权限。当前检查是窗口内
可展示范围及命中测试，不声称能证明它未被任何外部进程窗口覆盖。

### 3.2 命令与响应

```json
{
  "requestId":"transport-1", "type":"execute",
  "command":{
    "requestId":"command-1", "controlId":"login.username",
    "action":"setText", "arguments":{"text":"alice"}
  }
}
```

```json
{
  "requestId":"transport-1", "type":"result",
  "result":{"requestId":"command-1","succeeded":true,
    "control":{"id":"login.username","state":{"text":"alice","readOnly":false}}}
}
```

控件描述为节选。失败可能是顶层 type=error/error，也可能是 result.succeeded=false/error；
客户端必须处理两种。顶层 requestId 关联通信响应，command.requestId 关联命令与审计，
不是业务幂等键。WSS 最近 2048 个请求 ID 去重也不提供跨重连或业务 exactly-once 保证。

AgenticCommand.SessionId 标记 JsonIgnore，由服务端分配；不接受远程自报的会话身份。
每个 WSS 会话对应独立 Pipe 连接，断线清理该会话引导。进程内调用默认 local 作用域。
客户端事件与响应可交错，必须有独立接收循环，不能“发一次就把下一行当结果”。

`IAgenticRemoteClient` 提供 ListControlsAsync、ExecuteAsync、EventReceived、ConnectionFaulted、
IsConnected 和 Dispose。官方 Pipe/WSS 客户端处理响应关联。CancellationToken 能取消本地等待，
不表示已经执行的 UI/设备动作被撤销；协议尚无通用远程 cancel 请求。超时结果未知，不自动重试。

### 3.3 成功与安全检查

Dispatcher：查控件 → 检查声明动作 → 调用 IAgenticCommandAuthorizer → 执行 → 发布完成/拒绝事件。
适配器再次检查 enabled/displayable、模态、只读、目标项和编辑限制。
`remoteActionCompleted` 的 outcomeScope=control，不是业务提交证明；气泡或事件也不能替代后置状态。
失败不保证完全无副作用，特别是同步业务处理已执行或数据模型不支持取消编辑时。

<a id="actions"></a>
## 4. 动作参考

以目标返回的 actions 为准，参数全部在 arguments。声明表示适配器有实现，不保证当前数据源
支持该次操作。通用 focus/highlight/clearHighlight 和五种鼠标动作向已注册 UI 控件提供。

| 动作 | 目标/参数 | 结果或限制 |
| --- | --- | --- |
| focus | 无参数 | 尝试原生聚焦；不可聚焦控件不能仅凭成功响应推断已获焦点 |
| click | 按钮/输入/勾选/下拉；菜单另有 path/value | 按钮原生点击，文本聚焦，单选选中，复选切换，下拉打开 |
| setText / getText | 文本框；设置 text | getText 结果 state.text；只读拒绝修改，设置不隐含失焦/提交 |
| setChecked / getChecked | 单选/复选/ToggleButton；checked 布尔 | state.checked；CheckedListBox 设置额外要求 index |
| setValue / getValue | 日期/数值/滑块；value | 日期 ISO-8601 或 yyyy-MM-dd；数值用 JSON 数字；受范围限制 |
| getValue | ProgressBar | 只读 value/minimum/maximum，无通用 setValue |
| selectItem | ComboBox/ListBox/ListView/TabControl/树 | 列表 index/value；items.v1 另支持 itemKey；树另支持 path |
| getItems | ComboBox、普通 ListBox、WinForms CheckedListBox | 0.7.0 起分页候选；不适用于 WPF ListView/TabControl/树/Grid |
| openDropDown / closeDropDown | ComboBox | 打开/关闭；click 也可打开 |
| expand / collapse | TreeView；path/value/index | 完整 path 最可靠，路径限制见树状态 |
| highlight / clearHighlight | 通用 | 见引导协议 |

标签、文本展示、列表及部分导航控件也提供 getText；不是任意对象的序列化接口。
CheckedListBox 的 getChecked 返回当前选中项的 checked，全部勾选索引在 checkedIndices。
菜单/工具栏 click 用 path（如文件/打开）或 value（项文字，WinForms 忽略快捷键标记 &）。
不承诺按文本解析任意复杂自定义模板。

### 应用内鼠标

| 动作 | 参数 |
| --- | --- |
| mouseMove / mouseClick / mouseDoubleClick | xRatio、yRatio；点击可带 button |
| mouseWheel | xRatio、yRatio、delta（默认 120，-1200～1200，非零） |
| mouseDrag | startXRatio、startYRatio、endXRatio、endYRatio；button、steps |

比例在 0～1，具体命中点仍须有效；非拖拽的 xRatio/yRatio 默认各 0.5。
button=left/right/middle，默认 left；steps=1～100，默认 10。
拖拽四个坐标必填。动作只向本进程目标窗口投递鼠标消息，不能做系统鼠标注入。
不得越过隐藏、禁用、遮挡或模态边界。WSS 默认不允许这些底层动作。

<a id="items-contract"></a>
## 5. 选项协议 items.v1（0.7.0 起）

实现入口：[AgenticItems.cs](../src/AgenticUI.Core/AgenticItems.cs)、
[WpfItemAccess.cs](../src/AgenticUI.Wpf/WpfItemAccess.cs)、
[WinFormsItemAccess.cs](../src/AgenticUI.WinForms/WinFormsItemAccess.cs)。

### 5.1 读取与匹配

getItems：start 默认 0 且非负；count 默认 50，范围 1～500。响应 state：

```json
{
  "start":0,"count":1,"total":1,"itemsVersion":"OPAQUE_VERSION_PLACEHOLDER",
  "items":[{"index":0,"text":"三体","itemKey":"BK-001","isSelected":false,"isEnabled":null}]
}
```

total 是当前已加载、排序/过滤后的视图数量，不是数据库总量。越过末尾返回空页。
页面仅在本次响应出现，不缓存进日常枚举；分页仍需读取集合元数据以验证快照，不是数据库分页。
getItems 不展开、不预加载业务数据。超大数据源由业务层分页，解析器必须快速、无副作用。

selectItem 三种定位：

- index：从 0 开始，越界/格式错误不回退；兼容旧请求同时传 value 时 index 优先。
- itemKey：严格区分大小写的唯一字符串，不与 index/value 混用；无绑定不生成假稳定键。
- value：忽略大小写的完整显示文本 → 去首尾空白后的完整文本 → 旧 ToString 完整文本。
  任一级多个匹配立即歧义失败，不猜第一个、不做包含匹配。

itemsVersion 可选，可用于读取和选择；必须原样回传。身份、顺序、文字、键或兼容文字改变
会更新版本；单纯选中状态和可用状态改变不更新。版本不跨进程/适配器重建持久化，不能发现
两次采样之间先改后恢复的一切历史，也不授予选择权限。没有版本的旧 index 无防过期保证。
选择后返回 selectedIndex/selection/itemKey；isSelected 仅代表主选中索引，不是所有多选项。

### 5.2 文字、键及容器

WPF：DisplayMemberPath → TextSearch.TextPath → 原生项普通内容 → ToString，应用 ItemStringFormat。
键来自 SelectedValuePath，仅返回标量字符串。WinForms 文本复用 GetItemText（DisplayMember/Format），
键来自 ValueMember。不公开 DTO、隐私属性或兼容匹配用的 LegacyText。
复杂模板/自绘不能自动解析时，业务可配置以下可选委托：

```csharp
// WPF；booksCombo 是现有控件，Book 是应用自己的业务类型。
AgenticProperties.SetItemTextProvider(booksCombo, item => ((Book)item!).Name);
AgenticProperties.SetItemKeyProvider(booksCombo, item => ((Book)item!).Id);
```

```csharp
// WinForms；原生控件先 Attach，替换式控件已自动附加。
var options = AgenticControlBinder.GetOptions(booksCombo);
options.ItemTextProvider = item => ((Book)item!).Name;
options.ItemKeyProvider = item => ((Book)item!).Id;
```

均为 Func<object?, string?>，必须返回与人看到的内容一致的语义，不在解析器中访问网络/数据库。
itemKey 不授予业务权限，不能包含秘密。

[WpfItemRealization.cs](../src/AgenticUI.Wpf/WpfItemRealization.cs) 将“定位”和“授权选择”分离：

1. 先 LocateIndex，记录数据版本和控件所属窗口/注册身份。
2. 按需展开 ComboBox、定位虚拟化容器或滚动 ListBox；等待异步让出 UI 线程。
3. 等待预算 2 秒；监听集合、人工交互/改选、关闭和卸载；同控件拒绝并发修改，允许查询。
4. 提交前重捕获快照，ResolveIndex 再核验真实 IsEnabled；Unknown 不当作 true。
5. 用原生 SelectedIndex/绑定提交；结束清理订阅，关闭本次临时展开，保留原本展开状态。

不为生成容器临时改选、不用假容器、不在轮询中反复 UpdateLayout。版本不再因未知状态变为
已知而误失效，但真实禁用仍拒绝。展开触发业务加载导致列表变化时，要求控制端重读，不自动重试。
等待期间不持续标记所有事件为 Remote，以免把人工操作伪装成远程操作。

<a id="grid-contract"></a>
## 6. DataGrid 协议

WPF kind=dataGrid，WinForms kind=dataGridView。row/start 从 0 开始，按当前视图而非数据主键；
新增占位行不用于普通读取。WinForms 视图排除隐藏行。WPF 的日常 rowCount 可能包含新增占位项，
读取分页以 getRows.total 为准。操作会受实时数据源能力和权限影响。

| 动作 | arguments | state 结果/行为 |
| --- | --- | --- |
| getColumns | 无 | columns |
| getRow | row | rowIndex、row |
| getRows | start=0、count=50（0～500；0 返回空页） | rows、start、实际 count、total |
| getCell | row、column | cell、text、rowIndex、columnIndex |
| setCell | row、column、text 或 value | 原生文本编辑/提交，检查校验结果 |
| selectRow / scrollToRow | row | 选择/滚动，不代表业务提交 |
| selectCell | row、column | 定位真实单元格 |
| highlightCell | row、column，加引导参数 | 展示，不修改数据 |
| addRow | values 对象 | rowIndex、row；WinForms 另有 filteredOut |
| deleteRow | row | 删除目标，之后重读 |
| sortByColumn | column、direction | ascending/descending 或 asc/desc，默认 ascending |
| filterByColumn | column、value、mode | contains/equals/startsWith，默认 contains；空/null 清除 |
| clearHighlight | 可选 guidanceId | 清理本会话的表格及单元格引导 |

column 可为列集合索引，不是显示排序位置。WinForms 可匹配 Name/HeaderText/DataPropertyName；
WPF 可匹配 Header/绑定路径。重名列标题不适合作为稳定定位依据，先 getColumns，用明确名称/索引。
WPF columns 含 index/name/header/bindingPath/readOnly/visible/sortDirection；WinForms 使用
dataProperty/valueType 等字段。行对象含 `_index`；WinForms 另有 `_sourceIndex`、`_visible`。
行字典键取列绑定路径/名称等，冲突可能退回索引字符串，不猜字段名。

getCell 当前虽然不改数据，但 WPF 会设置 SelectedItem，WinForms 会设置 CurrentCell，
可能改变选择或触发相应事件；需要保持选择的观察场景优先 getRow/getRows。它不是纯粹无 UI 副作用读取。
读取的是表格视图状态。部分表格结果在适配器中保留为最后一次操作快照，不把后来枚举的旧
row/cell 字段当成实时业务事实；需要时重新执行读取命令。不会自动跟数据库锁或事务联动。

### 数据源与编辑限制

- 两端 setCell 尊重表格/列/单元格只读并走原生文本编辑器；模板、ComboBox、CheckBox 等
  非文本编辑列不做直接业务属性赋值旁路。修改失败后重新读取，不承诺模型回滚。
- 两端增删要求表格非只读且允许人工增删。WPF 增删需可写、可推断行类型的 IList，新增类型
  需公开无参构造函数；values 按公开可写属性转换。只读集合和不可写属性明确失败。
- WinForms 增删支持未绑定表格或 BindingSource，绑定新增需要 AddNew 能创建可写 CLR 行对象。
  绑定排序/过滤必须由数据源提供；SupportsFiltering=false 时不能通过设置字符串强行过滤。
- WPF 排序/过滤需 DataGridBoundColumn 的可识别属性绑定，尊重可排序设置；现有 Filter 会与
  当前 AgenticUI 条件组合。每次只有一个 AgenticUI 列过滤，不是任意查询表达式。
- WinForms 列不能为 NotSortable。新增行若被过滤掉，rowIndex=-1、filteredOut=true。
- 操作顺序：读列 → 读视图 → 核对业务关键字段 → 操作 → 重读。没有行版本/行键协议，不缓存旧行号。

<a id="guidance-contract"></a>
## 7. 动态引导协议

能力名 dynamicGuidance.v1；规范参数另有机器可读 [guidance-v1.schema.json](protocol/guidance-v1.schema.json)。
由控制端决定意图，目标只做纯文本渲染。下面字段适用于 highlight 和 highlightCell：

| 参数 | 默认/范围 |
| --- | --- |
| guidanceId | 默认 default；1～128 字符，不允许控制字符 |
| showOutline | true |
| instructionNumber | 0～99999；兼容别名 number，二者同时存在时前者优先 |
| showNumber | 省略由编号是否大于 0 决定；显式 true 需正编号 |
| hint | 最多 1000 字符纯文本，支持中文换行，不执行标记/脚本 |
| showBubble | 省略由有效提示是否非空决定；显式 true 需非空，false 永不回退旧提示 |
| placement | auto/top/bottom；屏幕边界会调整，位置只是偏好 |
| durationMs | 0～3600000；0 保持到清理/卸载/断线，更新重置计时 |

完整快照更新，不把上一次远程参数做局部合并；未传编号/提示允许兼容本地默认属性，不修改
控件预设 Hint/InstructionNumber。三个显隐都 false 清除对应项。每控件每类引导集合最多 16 项。
同会话/同控件/同 guidanceId 原位更新；clearHighlight 未传 ID 清理此会话在控件上的全部引导。
清理即使被模态阻挡也允许。UI 退出/卸载/断线/超时释放覆盖窗口和计时器，不抢焦点、不挡点击。

单元格高亮追踪仍有效的目标或清理；排序/删行/过滤/容器回收后重新读取，不承诺旧 row 永远
指向同一记录。业务确认和人工完成核验留在控制端/宿主，不能用气泡代替授权或签字。

<a id="state-events"></a>
## 8. 状态、事件和隐私

### 8.1 状态约定

- visible：框架可见性；displayable：处于当前可展示/模态范围并通过窗口内命中测试。
- focused：真实焦点；isEnabled：控件启用状态，不等于业务授权。
- readOnly：TextBox 和 DataGrid 的布尔编辑约束，仍可观察/聚焦/引导。ComboBox 的文本编辑器
  只读不等于禁止选项选择，不滥用此字段封禁整个下拉框。
- 选择状态通常含 selectedIndex/selection/text/itemCount；0.7.0 起普通选项另有 itemKey。
- 日期/数值类含 value；范围类含 minimum/maximum。缺失字段不是 false，也不是成功。

TreeView 两端 treeStateVersion=1：

| 字段 | 语义 |
| --- | --- |
| path | 当前选中节点的唯一完整标题路径；没有可靠路径时 null |
| selection/text | 当前选中显示标题，兼容字段 |
| expansionPath | 最近展开/折叠的节点路径，不是选中节点 |
| expanded | 上述节点实时展开状态；已失效则 null |

这是最近操作状态，不是完整树快照。路径 `/` 分层，标题非空，不含 `/`/控制字符，同父标题
不应忽略大小写重名；歧义拒绝，不猜。WPF 优先 AutomationProperties.Name、文字 Header、
已显示模板的单一文本；复杂模板建议绑定原生 AutomationProperties.Name。未生成子节点先显式
展开父节点，不为发现偷偷展开整棵树。读取不引发业务展开。

### 8.2 事件

```json
{"type":"event","event":{"sequence":18,"controlId":"login.submit","name":"clicked","source":"User","timestamp":"2026-09-11T00:00:00Z","isSensitive":false,"data":{}}}
```

sequence 为事件总线分配的递增序号，不是跨重启永久序列或消息可靠交付凭据。
事件包括 clicked、pressed、released、textChanged、valueChanged、checkedChanged、selectionChanged、
expanded、collapsed、dropDownOpened、dropDownClosed、focusChanged、remoteActionCompleted、remoteActionRejected。
树选择带 selection/path，展开/折叠带 path/expanded；0.7.0 起选项选择带 selection/itemKey。

Source 枚举有 User/Remote/Programmatic/Replay，但现有适配器主要按远程调用作用域区分 Remote
与默认 User；普通宿主代码改值也可能落入 User，未提供通用 Programmatic/Replay 来源作用域。
内置回放经 Dispatcher 走 Remote，不保证事件显示 Replay。不能仅凭枚举值声称精确识别人类行为。

事件总线隔离订阅异常；网络事件队列有界，慢连接超限断开，不是永久可靠消息队列。
没有通用 stateChanged 全量状态推送协议。控制端重连后需要重新发现、读取和判断。

### 8.3 隐私边界

AgenticPrivacy 负责远程描述/事件净化；敏感描述仅保留严格布尔 readOnly，其余 state=null。
事件保留 action/requestId 等必要关联数据，其他敏感载荷替换；失败错误不回显秘密候选值。
本地原始事件订阅和直接 Describe/Dispatcher 调用不是网络净化边界，自定义传输必须调用净化器。
ID/name/actions/capabilities 是元数据，仍需开发者保证不放入秘密；引导文本也不得含秘密。

AgenticLogOptions 控制本地记录：Semantic 默认，Detailed 加 pressed/released/focusChanged；
RedactSensitiveValues 默认 true。不是所有普通文本默认屏蔽，宿主应正确标记 Sensitive。
AgenticInteractionRecorder 默认不录敏感文本/值，跳过 Remote/Replay，录制文件会覆盖同名文件；
日志追加写入。回放不遇错自动停止，不提供事务回滚、自动重试、签名、轮转或保留周期管理。

<a id="hosting"></a>
## 9. 应用宿主、WSS 与配对（0.7.0 起）

入口 [AgenticApplicationHost](../src/AgenticUI.Remote/Hosting/AgenticApplicationHost.cs)，
配置定义 [AgenticHostOptions](../src/AgenticUI.Remote/Hosting/AgenticHostOptions.cs)。
StartFromConfiguration 默认读取 EXE 同目录 agenticui.json；Start(options) 使用快照。
每应用域 Current 单例，第二次 Start 返回既有宿主，不热重载；Dispose 后允许重新初始化。
控件自身不重复启动端口，不能同时手动创建相同 NamedPipeServer。

### 9.1 全部配置字段

所有字段在 AgenticUI 对象内；字段名不区分大小写，未知字段拒绝，允许 JSON 注释。

| 字段 | 默认 | 约束/用途 |
| --- | --- | --- |
| AutoStart | true | false 停止全部通信 |
| Local.Enabled | true | 网络开启时必须 true |
| Local.PipeName | AgenticUI.NET | ≤100 字符；字母/数字/点/横线/下划线 |
| Local.TokenEnvironmentVariable | AGENTICUI_PIPE_TOKEN | 未设置值时每进程随机令牌；已设置需至少 32 字符 |
| Network.Enabled | false | 显式开启网络 |
| Network.BindAddress | 127.0.0.1 | IP 地址；0.0.0.0 绑定全部 IPv4，不用于公告地址 |
| Network.ListenUrl | https://localhost:7443 | HTTPS 根地址，无凭据/查询/片段/通配主机 |
| Network.WebSocketPath | /agenticui | 非根路径，无查询/空白 |
| Network.StateDirectory | 空 | 默认当前用户 LocalApplicationData/AgenticUI.NET/identities，按 PipeName 哈希隔离 |
| Network.TokenEnvironmentVariable | AGENTICUI_GATEWAY_TOKEN | 可选兼容共享令牌入口，至少 32 字符，必须与 Pipe 令牌不同 |
| Network.AllowedActions | 见下文 | 明确逐项，不允许 *；设置数组是替换，不是追加默认值 |
| Network.AllowedOrigins | 空数组 | 带 Origin 请求必须匹配配置的 HTTPS 源；不代替认证 |
| Network.MaxConnections | 32 | 1～256 |
| Network.RequestsPerMinute | 120 | 1～10000 |
| Network.ConnectionAttemptsPerMinute | 30 | 1～1000 |
| Network.MaximumMessageLength | 1048576 | 1024～1048576 字节 |
| Network.CommandTimeoutSeconds | 30 | 1～300；结果未知，不自动重试 |
| Discovery.Enabled | false | 只有网络成功才广播 |
| Discovery.Port | 47731 | 1～65535 |
| Discovery.IntervalSeconds | 5 | 2～3600 |
| Discovery.ServiceName | AgenticUI application | 非空，≤100 字符 |
| Discovery.PublicWebSocketUrl | 空 | 由 ListenUrl/path 生成；显式值必须 WSS、同路径、无凭据/查询/片段 |

默认 AllowedActions：highlight、clearHighlight、focus、getText、getValue、getChecked、getItems、
getRow、getRows、getColumns、getCell、scrollToRow、highlightCell、selectCell。
这是读取/引导/定位集合，**不全是纯只读**，focus/selectCell/scrollToRow 会改变 UI 状态。
click/setText/selectItem/增删改/鼠标等需显式添加。配对不扩大白名单。

配置缺失/非法时 LastError 给出净化诊断，不抛入业务 UI；不要仅看 AutoStart 判断服务成功。
若网络初始化阶段失败，已启动的 Pipe 可以保留；无效配置可能在启动任何服务前就被拒绝。
必须看 LocalRunning/NetworkRunning/DiscoveryRunning。不会广播未成功启动的网络服务。

### 9.2 TLS、身份与配对

网络使用用户态 TcpListener/SslStream，不依赖 HTTP.sys/系统证书安装。当前 TLS 1.2，自动
RSA 2048/SHA-256 自签名证书有效 2 年。信任来自独立核验后的完整指纹，不把 UDP 当信任源。
身份目录独占，升级不要清空；证书过期/损坏拒绝启动，不静默生成新身份。当前无自动续期 UI。
Windows 私钥/控制端凭据用当前用户 DPAPI；非 Windows 测试文件权限保护不等于 DPAPI。

目标本地 API：Pairing.CertificateFingerprint、BeginPairing()、CancelPairing()、RevokeAllClients()。
码只在内存，有效 3 分钟，最多尝试 5 次，只能成功一次；目标最多存 64 个客户端凭据哈希。
配对管理不得远程注册。控制端接口：

```csharp
// 此处为接入片段；ShowPairingDialogOnUiThreadAsync 由应用实现，完整 UI 见 samples/Shared。
using var client = await AgenticWebSocketClient.ConnectPairedAsync(
    new Uri("wss://localhost:7443/agenticui"),
    prompt => ShowPairingDialogOnUiThreadAsync(prompt),
    new AgenticPairingStore());
```

回调 Func<AgenticPairingPrompt,Task<string?>> 接收 Endpoint/CertificateFingerprint，独立核验后
返回一次性码，取消返回 null；可能从后台调用，必须自行切 UI。认证成功才保存首次记录，
重连按完整端点和指纹匹配，地址变化不凭 UDP InstanceId 自动迁移。Store.Forget(uri) 仅删本地
记录，目标 RevokeAllClients 才撤销对端权限并断开连接。

原始 WSS 配对首请求 type=pair，authenticationToken 携带一次性码；成功 type=paired 返回
pairingToken，随后重新建立受信任连接以此凭据 authenticate。配对成功不是已登录业务会话，
优先用官方客户端而非自行跳过步骤。此类响应不能记录令牌。

兼容 ConnectAsync 显式令牌入口仍存在。设置 AGENTICUI_GATEWAY_TOKEN 会额外开放共享令牌认证，
撤销配对不使环境变量令牌失效，不需要时不要设置。浏览器不能直接复用 .NET 自签名指纹
客户端；未来浏览器直连需要受浏览器信任的 HTTPS 或经设计的本地桥接，当前没有 Web 控件。

### 9.3 发现、限额与宿主授权限制

AgenticGatewayDiscovery.ScanAsync 默认监听 6 秒；ListenContinuousAsync 可持续接收。
公告协议 AgenticUI.Discovery.v1，仅含服务名、实例、WSS 地址、版本、时间、能力和提示性指纹。
它是广播发现，不验证地址可达、来源真实性或身份；不含 Pipe 名/令牌/控件数据。跨电脑须
正确网卡/地址、防火墙与广播网络，不能只修改 Enabled。

HTTP Upgrade 只接受指定路径，头部上限 8 KiB、握手期限 10 秒；TLS 握手前限制连接数量。
net8 WebSocket 用平台实现，net48 服务端用 Ninja.WebSockets 1.1.8；不自行实现加密算法。
消息、队列、速率有界，慢客户端断开；这不是互联网边界防火墙或已完成第三方安全审计的保证。

重要：当前 ApplicationHost 内部创建默认 Dispatcher，**未提供注入自定义 Authorizer 的公开入口**。
AllowedActions 是动作级白名单，不是用户/控件/参数级权限。业务权限始终保留在应用内；
需要自定义本机命令授权可用下节的手动 Pipe 构造。不要编造 Host.Authorizer 配置项，也不要
把手动 Pipe 与 ApplicationHost 重复启动来“补授权”。

<a id="extension"></a>
## 10. 扩展控件和安全授权

自定义 IAgenticControl 实现 Describe、ExecuteAsync、IsRemotelyDiscoverable，注册到同一
Registry，卸载时 Unregister，保留强引用以供真实 UI 使用。ExecuteAsync 必须校验参数并切
目标 UI 线程，复用原控件事件/绑定而非直接改数据库。源 DTO 不可默认全量序列化。

新增动作的完成清单：

- 在 AgenticActions 声明，并仅向真正支持的控件 actions 发布；新状态契约提供能力协商。
- 两端实现或明确平台差异，不把常量存在当作实现完成。
- 校验参数类型/范围、只读/禁用/显示/模态、数据源约束；不执行任意反射方法或脚本。
- 反馈明确结果和必要语义事件，兼容 JsonElement 参数与旧客户端，不泄露敏感错误文本。
- UI 操作不跨线程，不持有 UI 锁等待远程/订阅回调；异步取消不伪装成业务回滚。
- Core 单测、真实 UI 测试、Pipe/WSS 集成和本文件对应契约同步更新。
- net48 编译及 Windows 运行覆盖；中文注释说明关键安全和生命周期原因。

自定义本机授权（API 片段，policy 为应用实现的 IAgenticCommandAuthorizer）：

```csharp
var dispatcher = new AgenticCommandDispatcher(authorizer: policy);
using var server = new AgenticNamedPipeServer("MyProduct", dispatcher: dispatcher);
server.Start();
// 在宿主生命周期内保持 server 存活，退出时释放。
```

接口 AuthorizeAsync(control, command, cancellationToken) 返回 ValueTask<bool>。
默认 AllowLocalCommandsAuthorizer 允许已声明动作，并非完整权限系统。接口没有内置认证用户
对象；业务需自己的可信上下文，不能信任调用参数自报角色。许可证和业务权限也不由配对码代替。

### Web 演进约束

未来适配器仍基于真实 DOM/组件：ID/描述/状态/actions/capabilities、纯文本引导、原生输入事件、
敏感净化及会话清理应保持同义。Core 协议不引入 HWND/DependencyProperty/模型供应商类型。
浏览器信任、跨源、取消、组件生命周期和事件来源要另行设计并测试；不能宣称目前已经有
React/Vue 包、MCP/CLI 服务或浏览器即插即用控制端。

<a id="build"></a>
## 11. 构建、本地包和验证

先在实际 Git 根目录执行 git status，保留用户未提交改动。源码、示例、文档、官网属于本仓库；
工作空间中的视频、PPT、专利草稿与外部 Agent 不合并进来，不公开私有历史备份或内部路径。
仓库没有 global.json 补丁锁；若出现 SDK 解析错误还需检查父目录。net48 使用引用程序集包，
Windows targeting 可在 macOS 编译，但不能运行 WindowsDesktop UI 测试。

```powershell
dotnet restore AgenticUI.NET.sln
dotnet build AgenticUI.NET.sln -c Release --no-restore
dotnet test AgenticUI.NET.sln -c Release --no-build
dotnet pack AgenticUI.NET.sln -c Release --no-build -o artifacts/packages
```

完整 test 命令仅在 Windows 执行。跨平台可运行 Core.Tests、Gateway.Tests 的 net8 测试：

```powershell
dotnet test tests/AgenticUI.Core.Tests/AgenticUI.Core.Tests.csproj -c Release
dotnet test tests/AgenticUI.Gateway.Tests/AgenticUI.Gateway.Tests.csproj -c Release
```

### 在另一台电脑测试未发布源码

方式一：应用用 ProjectReference 指向所需库项目，移除相同包的 PackageReference，不同时引用
旧包和新源码。完整仓库源码一起同步，不能只复制旧版本号的单个 DLL 后假定全部升级。

方式二：生成独立预发布版本的本地包，每次修改使用新的唯一版本，避免覆盖正式 0.7.0 或
同版本 NuGet 缓存。以下仅本地打包，不更改仓库发布版本，也不上传：

```powershell
$localVersion = "0.7.1-local." + (Get-Date -Format "yyyyMMddHHmmss")
dotnet pack AgenticUI.NET.sln -c Release -p:Version=$localVersion -o artifacts/local-packages
```

把生成的四个 nupkg 放到另一电脑专用本地 NuGet 源，在应用目录执行（替换目录/实际版本）：

```powershell
dotnet nuget add source "D:\local-packages\AgenticUI" --name AgenticUILocal
dotnet add package AgenticUI.Wpf --version <实际本地版本>
dotnet add package AgenticUI.Remote --version <实际本地版本>
```

WinForms 替换为 AgenticUI.WinForms。保留 nuget.org 用于外部依赖，不把整份包缓存复制到源码。
核对 project.assets.json、输出 DLL 及必要时 SHA-256；关闭旧进程，从新输出目录启动。
本地包、测试结果保存在忽略的 artifacts，不提交二进制、令牌、配对文件或真实日志。

<a id="verification"></a>
## 12. Windows 验收

执行 [scripts/verify-windows.ps1](../scripts/verify-windows.ps1)，不擅自更改系统执行策略：

```powershell
.\scripts\verify-windows.ps1
```

脚本还原、构建、运行测试，生成 artifacts/windows-acceptance 下的报告/TRX。可选
AgentRepositoryPath 只适用于具备对应 verify.ps1 和联调包清单的外部 Agent 仓库，不是通用
第三方应用验证参数；两个仓库的源码、包版本和结果分别管理。不调用真实模型或生产设备。

| 测试工程 | 主要覆盖 | 运行位置 |
| --- | --- | --- |
| Core.Tests | 协议、注册、授权、隐私、录制、Pipe、Host、选项版本 | 跨平台 net8 |
| Gateway.Tests | 真实回环 TLS、转发、配对/重连/撤销、限额 | 跨平台 net8；Windows 仍需验证 |
| WinForms.Tests | 真实控件、表格、鼠标、引导、选择与只读 | Windows |
| Wpf.Tests | Dispatcher、绑定、引导空闲/布局、选项容器、Pipe | Windows |
| Remote.Net48.Tests | net48 TLS/配对/DPAPI | Windows net48 |

不要把历史测试数量写成永久成功标记；看本次目标 commit 的结果。新增 WPF 选项容器修复
在 macOS 只能构建，合并/交付前需 Windows 回归。UI 测试也不等于全部人工视觉验收。

- [ ] WPF/WinForms 人工流程不依赖 AI 或网络，.NET 8 与实际 net48 宿主分别运行。
- [ ] 控件可发现范围、标签/滚动、只读、禁用、敏感、模态、自定义确认按钮正确。
- [ ] 动态文本更新、独立显隐、到期/断线/卸载清理，不抢焦点、不挡点击。
- [ ] 高亮后窗口可拖动、连续 setText/getText 返回，Dispatcher 可到低优先级空闲。
- [ ] 100%/150%/200% DPI、多屏、屏幕边缘、缩放/最小化、滚动和数据容器复用。
- [ ] 两连接引导隔离，一方断开只清自己的；慢订阅不阻塞另一端或 UI。
- [ ] items：显示绑定、重名、键、旧版本、普通/隐式样式关闭状态选择、虚拟化远端项、
  Setter/DataTrigger 禁用、准备中人工改选/变更/取消、异常模板超时且 UI 仍响应。
- [ ] Grid：原生编辑校验、增删权限、排序/过滤后重读、失败后的实际数据核对。
- [ ] 干净普通用户机器无需 netsh/安装系统证书即可启用网络；证书/指纹/码核验、重连/撤销正确。
- [ ] 实际局域网 IP/防火墙/广播验证；配置关闭后不监听/公告，身份文件升级不丢失。
- [ ] 日志/截图/包/PDB 无秘密和内部路径，第三方自定义编辑器单独验收。

<a id="maintenance"></a>
## 13. 发布与文档维护

工作流事实以 [.github/workflows](../.github/workflows) 为准：

- ci.yml：main 推送、PR、手动运行，Windows 还原/构建/测试/打包，产物不等于 NuGet 发布。
- release.yml：v* 标签校验 VersionPrefix，Windows 验证后 NuGet OIDC 登录、发布包和 GitHub Release。
  手动 dispatch 不满足标签条件时只验证/产出包，不会走发布步骤。
- deploy-website.yml：官网相关 main 改动或手动运行，Node 22/npm ci/build，GitHub Pages 部署。
  官网源码在 website；它是宣传网站，不是已实现的 AgenticUI Web 控件库。

未授权不提交/推送/建标签/发包。通常功能分支开发、中文提交说明行为和验证，Windows CI
通过再按仓库保护规则合并。发布前核对版本、CHANGELOG、四包依赖、许可元数据、隐私扫描，
发布后分别核对 GitHub Release、NuGet 四包和官网。不能只凭上传成功宣称索引和人工验收完成。
发行版本一旦公开不覆盖；本地联调使用唯一后缀。不要退回提交长期 NuGet 密钥的方式。

### 两份文档的维护规则

- 使用指南负责安装、接入、示例、配置操作和常见问题；本文负责完整参数、边界、架构和验证。
- README/NUGET-README 只是入口摘要，不再复制长教程或维护另一份参数表。
- 新能力在使用指南标明正式版/源码版；发布后更新范围，不用旧开发包版本教新用户安装。
- 新参数同一次改动更新实现、测试、本文；新教程提供可编译片段并验证链接，不单独堆“补充说明”。
- 历史版本差异留 CHANGELOG，临时验收结果留 CI/忽略的 artifacts；不把历史未完成清单当当前事实。
- 技术事实冲突时核对对应版本源码/测试并修正文档，法律条款以 LICENSE 和授权文件为准。

文档检查使用 Node.js 内置模块，无需安装额外 npm 依赖：

```powershell
node scripts/verify-docs.mjs
node scripts/verify-docs.mjs --build-examples
# 发布前先 pack，再使用本次产物验证，避免尚未发布的版本只能去 nuget.org 查找。
dotnet pack AgenticUI.NET.sln -c Release -o artifacts/packages
node scripts/verify-docs.mjs --build-examples --package-source artifacts/packages
```

第二条从使用指南提取完整 WPF/WinForms/控制端代码，生成到忽略的 artifacts，并使用指南中
指定的正式 NuGet 版本编译，防止只在新源码上编译掩盖旧包 API 不兼容。发布前 CI 使用
--package-source 指定本次打包目录，仅 AgenticUI.* 从该目录解析，其余依赖来自 nuget.org；
本地与公开源使用不同的隔离缓存。发布后再执行不带该参数的命令核验正式源。它不运行 Windows UI。

本次文档收敛记录：原 architecture、local-protocol、control-state-contract 合并到本文；
原 datagrid、combobox、guidance、embedded-host、gateway 的接入说明进使用指南，参数进本文；
原 AI-HANDOFF、PROJECT-MEMORY 的有效工程决策进本文，过期实现/发布结论不延续；
原 windows-acceptance、upgrade-verification 的可复用流程进验收章节，历史流水不复制。
旧 Markdown 从当前树移除，已提交历史可从 Git 查回；保留协议 Schema、演示图和社区治理文件。

保留的独立文件：[LICENSE](../LICENSE)、[授权说明](../LICENSING.md)、[版本/服务边界](../EDITIONS.md)、
[安全策略](../SECURITY.md)、[贡献指南](../CONTRIBUTING.md)、[行为准则](../CODE_OF_CONDUCT.md)、
[修改记录](../CHANGELOG.md)、商业许可和 CLA 模板。模板并非已生效合同；不在文档整理时改授权条款。
