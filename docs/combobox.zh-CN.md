# 下拉列表与选项使用指南

适用：当前 **Unreleased 源码**，稳定 NuGet 0.6.1 尚不包含本页新增能力。
覆盖 WPF / WinForms 的 ComboBox 和普通 ListBox，以及 WinForms CheckedListBox 的单项选择。
WPF ListView、TabControl、树节点和 DataGrid 的动作维持原语义，不声明 `items.v1`。
人和远程端仍操作同一个原生控件；控件不包含模型、搜索决策或业务数据加载逻辑。

## 1. 为什么看到“三体”却选择失败

旧版 `selectItem(value)` 使用选项对象的 `ToString()`，对象绑定时与界面显示文字可能不同。
新版将候选读取、文字匹配、选择结果和事件统一：

- WinForms 默认使用 `GetItemText`，复用 DisplayMember、Format 等原生格式化规则。
- WPF 默认使用 DisplayMemberPath（支持绑定路径），没有时尝试 TextSearch.TextPath、
  原生 ComboBoxItem/ListBoxItem 的普通内容，应用 ItemStringFormat；没有这些信息才退回 ToString。
- 任意复杂 ItemTemplate、转换器拼接或自绘内容不保证自动解析。只在这种情况下配置本页的
  可选解析器；不扫描或公开整个 DTO，也不强制修改业务对象的 ToString。

`value: "三体"` 不会自动选择显示为 `BK-001 三体` 的选项。应读取真实文字或业务键后选择，
不能通过自动“包含匹配”冒险选错物料、图书版本或工艺参数。

## 2. 被控软件的普通数据绑定

WPF 示例（`Books` 的每项含 `Id`、`Name` 属性）：

```xml
<aui:AgenticComboBox AgenticId="inbound.line.material"
                     ItemsSource="{Binding Books}"
                     DisplayMemberPath="Name"
                     SelectedValuePath="Id"
                     SelectedValue="{Binding SelectedBookId, Mode=TwoWay}" />
```

`aui` 使用现有 WPF 命名空间 `clr-namespace:AgenticUI.Wpf;assembly=AgenticUI.Wpf`。
原生 ComboBox 也可以通过现有 AgenticProperties.Enabled / Id 附加属性接入。

WinForms 示例：

```csharp
var booksCombo = new AgenticComboBox
{
    AgenticId = "inbound.line.material",
    DropDownStyle = ComboBoxStyle.DropDownList,
    DisplayMember = "Name",
    ValueMember = "Id",
    DataSource = books
};
```

通常不需要新增 AI 专用绑定。`itemKey` 默认来自 SelectedValuePath / ValueMember，只输出标量的
字符串形式。没有值绑定或值是复杂对象时返回 null；不会把位置假装成稳定业务键。
业务键应唯一，重复键会导致选择歧义错误。不要把密码、连接串或密钥作为业务键。

## 3. 先读取，再选择

先通过 `listControls` 查看控件 `actions` 包含 `getItems`，且 `capabilities` 包含 `items.v1`。
旧目标没有此能力时不能假设升级已生效。以下 JSON 是放入 execute 请求的 command 对象。

```json
{
  "controlId": "inbound.line.material",
  "action": "getItems",
  "arguments": { "start": 0, "count": 50 }
}
```

响应的 `result.control.state` 示例（编号仅为示例，不代表实际软件的候选数据）：

```json
{
  "selectedIndex": -1,
  "selection": null,
  "itemKey": null,
  "itemCount": 3,
  "start": 0,
  "count": 3,
  "total": 3,
  "itemsVersion": "opaque-version-from-target",
  "items": [
    { "index": 0, "text": "流浪地球", "itemKey": "BK-002", "isSelected": false, "isEnabled": true },
    { "index": 1, "text": "三体", "itemKey": "BK-001", "isSelected": false, "isEnabled": true },
    { "index": 2, "text": "球状闪电", "itemKey": "BK-003", "isSelected": false, "isEnabled": true }
  ]
}
```

优先原样使用读取到的唯一业务键：

```json
{
  "controlId": "inbound.line.material",
  "action": "selectItem",
  "arguments": { "itemKey": "BK-001" }
}
```

没有业务键时，使用确认过的索引并携带**实际响应**的版本：

```json
{
  "controlId": "inbound.line.material",
  "action": "selectItem",
  "arguments": { "index": 1, "itemsVersion": "opaque-version-from-target" }
}
```

仍兼容 `arguments: { "value": "三体" }`。规则是：忽略大小写的完整显示文字匹配 →
去掉首尾空白的完整匹配 → 旧 ToString 完整匹配。任何一级有多个匹配就报歧义，不继续猜测。
`itemKey` 严格区分大小写，只接受字符串，不能与 index/value 混用。

选择后检查 `succeeded` 和返回的 `state.selectedIndex / selection / itemKey`；不能仅凭下拉框
展开或 itemCount 判断成功。状态表示控件选择结果，不代表入库、提交等业务事务已成功。

## 4. 分页、版本与加载边界

- start 从 0 开始，默认 0；count 默认 50，范围 1～500。响应 count 是实际条数。
- total 是当前控件中**已经加载、排序和过滤后的视图数量**，不代表数据库总量。
- getItems 本身不打开下拉框、不加载业务数据、不更改选择；普通已加载绑定无需先展开。
  若应用本来就在 DropDownOpened 中加载数据，控制端仍应先 openDropDown 并等待应用加载完成。
- 后续分页可以携带第一次的 itemsVersion。选项的顺序、对象实例、文字、键、可用状态变化时，
  下一次捕获会更换版本；过期请求拒绝执行。单纯选中另一项不改变版本。
- 版本是当前控件适配器实例内的快照标记，不跨重启保存，也不保证发现两次观察之间先改变又
  恢复的所有历史。为兼容旧客户端，版本校验是可选的；不传版本的 index 没有防过期保证。
- 实现会读取当前已加载集合的元数据以校验快照，返回载荷按页限量。超大/虚拟数据源应在业务
  层分页，不要在显示字段或自定义解析器里进行网络、数据库调用等阻塞操作。
- WPF 如果通过未生成容器的样式控制 IsEnabled，可能返回 `isEnabled: null`。这不是可用；
  先展开/让容器可见并重新读取，库不会直接设置索引来绕过未知或禁用状态。
- 对多选 ListBox，当前协议仍是单项选择和 SelectedIndex 语义，isSelected 表示该主选中项，
  不表示所有多选项目。CheckedListBox 的复选状态仍走原 getChecked/setChecked。

## 5. 复杂模板或自绘的可选配置

仅在普通显示绑定不足时配置，不改变模型、界面或原来的人工交互：

```csharp
// WPF：booksCombo 可以是原生或 AgenticComboBox。
AgenticProperties.SetItemTextProvider(booksCombo,
    item => $"{((Book)item!).Id} / {((Book)item).Name}");
AgenticProperties.SetItemKeyProvider(booksCombo, item => ((Book)item!).Id);
```

```csharp
// WinForms：原生控件可先 Attach；Agentic 控件构造时已经附加。
var options = AgenticControlBinder.GetOptions(booksCombo);
options.ItemTextProvider = item => $"{((Book)item!).Id} / {((Book)item).Name}";
options.ItemKeyProvider = item => ((Book)item!).Id;
```

解析器只在本机 UI 线程调用，应纯读取、快速且无副作用；返回的文字应准确描述人看到的选项。
业务键只作为选择定位，不赋予额外授权。

## 6. 权限、隐私与日志

- getItems 属于只读观察动作；selectItem 仍遵守控件禁用、可展示和模态窗口约束。
- 网络默认只读白名单新增 getItems；selectItem 仍需显式加入 Network.AllowedActions，配对不
  替代动作授权。显式配置过白名单的旧应用不会自动新增 getItems，需要自行按需添加。
- 本轮不改控制端。控制端需要随后按能力协商增加“读候选 → 选业务键 → 验证结果”的流程。
- 候选响应只包含必要字段，不序列化源对象或 ToString 兼容文本。敏感控件的远程状态沿用
  AgenticPrivacy 策略，items、itemKey 等内容为 null；异常信息也不会回显敏感候选值。
- selectionChanged 的 selection 使用统一文字，新增 itemKey；本地交互录制优先保存 itemKey，
  无键时保持旧的 index 方式。敏感事件与录制延续原有默认脱敏/跳过规则。

## 7. Windows 验收

```powershell
dotnet build AgenticUI.NET.sln -c Release
dotnet test tests/AgenticUI.Core.Tests/AgenticUI.Core.Tests.csproj -c Release
dotnet test tests/AgenticUI.WinForms.Tests/AgenticUI.WinForms.Tests.csproj -c Release
dotnet test tests/AgenticUI.Wpf.Tests/AgenticUI.Wpf.Tests.csproj -c Release
```

重点验收：对象绑定显示“三体”但未覆盖 ToString；getItems → itemKey 选择；重名失败；
插入/排序后旧版本失败；禁用选项拒绝；原生事件和绑定仍正常；Named Pipe 敏感候选不泄露。
.NET Framework 4.8 同步编译，但这些 UI 回归工程当前以 .NET 8 Windows 运行；使用 net48
的实际业务应用仍需 Windows 手工验证。Mac 上构建成功不代表 Windows UI 测试已执行。
