# 只读与树节点状态契约（第三阶段）

状态：此契约从正式版 `0.6.0` 起提供，当前文档适用于 `0.6.1`。
控件只提供真实状态、原生动作和事件；模型判断及人工完成核验仍在控制端。

## 只读状态

WPF TextBox、WinForms TextBoxBase 和两套 DataGrid 的 `state.readOnly` 返回布尔值。
这是编辑限制，不等于禁用：只读文本仍可被发现、聚焦、显示引导；编辑命令仍由控件拒绝。
ComboBox 的“文本编辑器只读”不代表禁止选择，因此不能把它当作整个选择动作的 readOnly。

敏感控件的文字、路径、选项和其他状态仍脱敏，**只允许布尔类型的 readOnly 穿过脱敏边界**。
同名字符串（包括字符串 "true"）、对象和数组不会被保留。控制端计划验证和提示上下文也沿用
这个规则，不通过读取敏感内容来判断是否可操作。

```json
{"isSensitive":true,"state":{"text":null,"readOnly":true}}
```

## 树节点状态

两套 TreeView 使用相同字段：

| 字段 | 含义 |
| --- | --- |
| treeStateVersion | 数字 1，表示支持本文状态与事件契约 |
| path | 当前选中节点的完整标题路径；未选择或无法唯一定位时为 null |
| selection / text | 当前选择的显示标题，保留旧字段 |
| expansionPath | 最近发生展开/折叠的节点完整路径，不是当前选中节点 |
| expanded | expansionPath 对应节点当前真实 IsExpanded；节点被移除或路径失效时为 null |

例如先选择研发下的设计，再展开销售：

```json
{
  "treeStateVersion": 1,
  "path": "公司/研发/设计",
  "selection": "设计",
  "expansionPath": "公司/销售",
  "expanded": true
}
```

这不是整棵树的展开状态快照，也不保证任意旧节点仍存在。读取状态不会展开节点。
节点被移除、改名或变得歧义后，控制端应重新发现/规划，不能缓存标题路径作为稳定主键。

## 事件与命令

- `selectionChanged`：`data.selection` + `data.path`。
- `expanded` / `collapsed`：`data.path` + `data.expanded`。
- WPF 在树根监听节点冒泡事件，包含被原应用标为 Handled 的事件；不会修改 Handled 或
  替换原生处理。动态节点和已生成的数据绑定容器使用同一规则。
- `selectItem` / `expand` / `collapse` 继续接受原有 `path`、`value`、`index`，WPF 通过
  SetCurrentValue 保留节点绑定，节点自身禁用/隐藏时不执行动作。
- 人工引导的展开/折叠只接受完整 `path`。同时匹配人工事件中的路径和之后读取的
  expansionPath / expanded；仅发生一次事件不等于完成。
- 旧目标没有 treeStateVersion 时，控制端仍明确拒绝展开/折叠人工引导。普通远程动作保留。
- 录制器保存完整路径的选择/展开/折叠；缺少有效路径或敏感路径默认不录制。

## 路径范围与限制

沿用 `/` 分层的旧协议，不在本轮引入另一套节点 ID。标题必须非空、不能包含 `/` 或控制字符，
同一父节点下的标题不应重名（比较忽略大小写）。无法唯一定位时返回 null / 拒绝路径命令，
不猜测第一个同名节点。实际节点依然可以通过原软件由人使用。

WPF 优先使用原生 AutomationProperties.Name、文字 Header 或已显示模板的单一文本。
复杂多文本标题建议绑定原生 AutomationProperties.Name。数据绑定节点的容器未生成时，
先显式展开父节点，再读取/操作子节点；不会为“发现”偷偷展开整个业务树。虚拟化或复杂模板
无法可靠解析时不宣称支持唯一标题路径。这些场景仍需 Windows 实机验证。

## 联合验收

执行入口和检查步骤见 [Windows 联合验收](windows-acceptance.zh-CN.md)。
新字段均为 JSON 标量，可被未来 Web 适配器实现；本轮没有实现 Web 控件或引入模型 SDK。
