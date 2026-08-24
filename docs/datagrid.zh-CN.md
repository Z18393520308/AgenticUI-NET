# AgenticUI.NET DataGrid 使用指南

本文适用于 AgenticUI.NET `0.3.0`，介绍 WPF `DataGrid` 和 WinForms
`DataGridView` 的 AI 语义操作能力。

## 1. 支持的动作

| 动作 | 用途 | 主要参数 |
| --- | --- | --- |
| `getColumns` | 读取列定义 | 无 |
| `getRow` | 读取一行 | `row` |
| `getRows` | 分页读取多行 | `start`、`count` |
| `getCell` | 读取单元格 | `row`、`column` |
| `setCell` | 修改单元格 | `row`、`column`、`value` 或 `text` |
| `selectRow` | 选中一行 | `row` |
| `selectCell` | 选中单元格 | `row`、`column` |
| `scrollToRow` | 滚动到指定行 | `row` |
| `highlightCell` | 给单元格显示 AI 指引框 | `row`、`column` |
| `addRow` | 新增一行 | `values` |
| `deleteRow` | 删除一行 | `row` |
| `sortByColumn` | 按列排序 | `column`、`direction` |
| `filterByColumn` | 按列过滤 | `column`、`value`、`mode` |
| `clearHighlight` | 清除表格或单元格高亮 | 无 |

索引统一从 `0` 开始。`row` 指当前排序、过滤后的可见视图行号，
不是数据库主键或原始数据源下标。

`column` 可以使用：

- 从 0 开始的列索引；
- WinForms 的 `Column.Name`、`HeaderText` 或 `DataPropertyName`；
- WPF 的列标题或 `DataGridBoundColumn` 绑定属性名。

建议 AI 先执行 `getColumns`，然后使用稳定的列名发送后续命令。

## 2. 安装

WPF：

```powershell
dotnet add package AgenticUI.Wpf --version 0.3.0
dotnet add package AgenticUI.Remote --version 0.3.0
```

WinForms：

```powershell
dotnet add package AgenticUI.WinForms --version 0.3.0
dotnet add package AgenticUI.Remote --version 0.3.0
```

## 3. WPF 接入

### 3.1 定义数据类型

`addRow` 需要能创建新对象，因此行类型应有公开无参构造函数，
要写入的字段应是公开可写属性。

```csharp
public sealed class OrderRow
{
    public string OrderNumber { get; set; } = "";
    public string Product { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
}

public ObservableCollection<OrderRow> Orders { get; } = new()
{
    new() { OrderNumber = "SO-001", Product = "伺服电机", Quantity = 2, Amount = 6800 },
    new() { OrderNumber = "SO-002", Product = "PLC", Quantity = 1, Amount = 3200 }
};
```

窗口初始化时设置数据上下文：

```csharp
public MainWindow()
{
    InitializeComponent();
    DataContext = this;
}
```

### 3.2 使用 AgenticDataGrid

```xml
<Window
    xmlns:agentic="clr-namespace:AgenticUI.Wpf;assembly=AgenticUI.Wpf">
    <agentic:AgenticDataGrid
        AgenticId="orders.grid"
        AgenticDisplayName="订单列表"
        InstructionNumber="2"
        Hint="请核对订单数据"
        ItemsSource="{Binding Orders}"
        AutoGenerateColumns="False"
        CanUserAddRows="False">
        <DataGrid.Columns>
            <DataGridTextColumn Header="订单号" Binding="{Binding OrderNumber}" />
            <DataGridTextColumn Header="产品" Binding="{Binding Product}" />
            <DataGridTextColumn Header="数量" Binding="{Binding Quantity}" />
            <DataGridTextColumn Header="金额" Binding="{Binding Amount}" />
        </DataGrid.Columns>
    </agentic:AgenticDataGrid>
</Window>
```

也可以保留原生 `DataGrid`：

```xml
<DataGrid
    agentic:AgenticProperties.Enabled="True"
    agentic:AgenticProperties.Id="orders.grid"
    agentic:AgenticProperties.DisplayName="订单列表"
    ItemsSource="{Binding Orders}" />
```

WPF 的 `setCell`、`sortByColumn` 和 `filterByColumn` 要求目标列是
`DataGridBoundColumn`，且绑定路径是可识别的公开属性。

## 4. WinForms 接入

### 4.1 未绑定表格（支持最完整）

```csharp
var grid = new AgenticDataGridView
{
    AgenticId = "orders.grid",
    AgenticDisplayName = "订单列表",
    InstructionNumber = 2,
    Hint = "请核对订单数据",
    AllowUserToAddRows = false,
    Dock = DockStyle.Fill
};

grid.Columns.Add(new DataGridViewTextBoxColumn
{
    Name = "OrderNumber",
    HeaderText = "订单号",
    SortMode = DataGridViewColumnSortMode.Automatic
});
grid.Columns.Add(new DataGridViewTextBoxColumn
{
    Name = "Product",
    HeaderText = "产品",
    SortMode = DataGridViewColumnSortMode.Automatic
});
grid.Columns.Add(new DataGridViewTextBoxColumn
{
    Name = "Quantity",
    HeaderText = "数量",
    ValueType = typeof(int),
    SortMode = DataGridViewColumnSortMode.Automatic
});

grid.Rows.Add("SO-001", "伺服电机", 2);
grid.Rows.Add("SO-002", "PLC", 1);
Controls.Add(grid);
```

必须设置可读的 `Name`；需要排序时，列的 `SortMode` 不能是
`NotSortable`。

### 4.2 保留原生 DataGridView

```csharp
AgenticControlBinder.Attach(existingGrid, new AgenticControlOptions
{
    Id = "orders.grid",
    DisplayName = "订单列表",
    InstructionNumber = 2,
    Hint = "请核对订单数据"
});
```

### 4.3 绑定数据源限制

- `addRow` / `deleteRow` 支持未绑定表格，或者 `DataSource` 为
  `BindingSource` 的表格。
- 绑定模式的 `addRow` 要求 `BindingSource.AddNew()` 能创建具有公开可写
  CLR 属性的行对象。
- `sortByColumn` 要求列可排序，且绑定数据源自身支持排序。
- `filterByColumn` 的绑定模式要求 `BindingSource.SupportsFiltering` 为 `true`。
- 普通 `BindingList<T>` 通常不支持内置过滤。如果需要所有动作都可用，
  建议先使用未绑定表格，或为业务数据源实现排序/过滤能力。

## 5. 启动本机网关

在被控应用中：

```csharp
using AgenticUI.Remote;

using var server = new AgenticNamedPipeServer("AgenticUI.NET");
server.Start();

// 只能通过受信任的本机渠道交给控制端。
var token = server.AuthenticationToken;
```

在 AI 控制端连接：

```csharp
using AgenticUI;
using AgenticUI.Remote;

using var client = await AgenticNamedPipeClient.ConnectAsync(
    authenticationToken: token,
    pipeName: "AgenticUI.NET",
    clientName: "My AI Agent");
```

服务仅使用本机 Named Pipe，不监听 TCP。不要把认证令牌写入源码、
日志、命令行历史或版本控制。

## 6. C# 命令辅助方法

```csharp
static async Task<AgenticControlDescriptor> ExecuteGridAsync(
    AgenticNamedPipeClient client,
    string action,
    Dictionary<string, object?>? arguments = null)
{
    var response = await client.ExecuteAsync(new AgenticCommand
    {
        ControlId = "orders.grid",
        Action = action,
        Arguments = arguments ?? new Dictionary<string, object?>()
    });

    if (response.Result?.Succeeded != true)
    {
        throw new InvalidOperationException(
            response.Result?.Error ?? response.Error ?? "DataGrid 命令执行失败。");
    }

    return response.Result.Control
        ?? throw new InvalidOperationException("命令未返回控件状态。");
}
```

以下代码示例都使用这个方法。返回数据位于
`AgenticControlDescriptor.State`。

如果命令发送者与被控界面在同一进程，也可以绕过 Named Pipe，
直接使用相同的语义命令：

```csharp
var dispatcher = new AgenticCommandDispatcher();
var result = await dispatcher.DispatchAsync(new AgenticCommand
{
    ControlId = "orders.grid",
    Action = AgenticActions.GetRows,
    Arguments = { ["start"] = 0, ["count"] = 50 }
});

if (!result.Succeeded)
{
    throw new InvalidOperationException(result.Error);
}
```

## 7. 读取表格

### 7.1 读取列

```csharp
var descriptor = await ExecuteGridAsync(client, AgenticActions.GetColumns);
var columns = descriptor.State["columns"];
```

WPF 列状态包含 `index`、`name`、`header`、`bindingPath`、`readOnly`、
`visible`、`sortDirection`。WinForms 使用 `dataProperty` 和 `valueType`等字段。

### 7.2 读取一行

```csharp
var descriptor = await ExecuteGridAsync(
    client,
    AgenticActions.GetRow,
    new() { ["row"] = 0 });

var row = descriptor.State["row"];
```

`state.rowIndex` 是视图行号，`state.row` 是以列名为键的对象。
行对象中 `_index` 为视图行号；WinForms 还返回 `_sourceIndex` 和
`_visible`。

### 7.3 分页读取

```csharp
var descriptor = await ExecuteGridAsync(
    client,
    AgenticActions.GetRows,
    new() { ["start"] = 0, ["count"] = 50 });

var rows = descriptor.State["rows"];
var total = descriptor.State["total"];
```

`start` 默认为 `0`，`count` 默认为 `50`、最大为 `500`。响应包含：

- `rows`：当前页数据；
- `start`：起始行号；
- `count`：本次实际返回行数；
- `total`：排序、过滤后的总行数。

大表格应通过分页读取，不要连续请求 500 行除非确有必要。

### 7.4 读取单元格

```csharp
var descriptor = await ExecuteGridAsync(
    client,
    AgenticActions.GetCell,
    new() { ["row"] = 0, ["column"] = "OrderNumber" });

var value = descriptor.State["cell"];
var text = descriptor.State["text"];
```

`cell` 保留尽可能原始的值类型，`text` 是字符串形式。

## 8. 修改数据

### 8.1 修改单元格

```csharp
await ExecuteGridAsync(
    client,
    AgenticActions.SetCell,
    new() { ["row"] = 0, ["column"] = "Quantity", ["value"] = 5 });
```

也可以使用 `text`。组件库会尝试转换到单元格或绑定属性的目标类型。

### 8.2 新增行

```csharp
await ExecuteGridAsync(
    client,
    AgenticActions.AddRow,
    new()
    {
        ["values"] = new Dictionary<string, object?>
        {
            ["OrderNumber"] = "SO-003",
            ["Product"] = "触摸屏",
            ["Quantity"] = 1,
            ["Amount"] = 2600m
        }
    });
```

成功后返回 `state.rowIndex` 和 `state.row`。WinForms 如果新行不符合当前
过滤条件，`rowIndex` 为 `-1`，`filteredOut` 为 `true`。

### 8.3 删除行

```csharp
await ExecuteGridAsync(
    client,
    AgenticActions.DeleteRow,
    new() { ["row"] = 2 });
```

删除前建议先执行 `getRow` 确认关键业务字段。对删除、启停设备、
下发参数等高风险业务动作，宿主应用仍应执行权限检查和二次确认。

## 9. 排序与过滤

### 9.1 排序

```csharp
await ExecuteGridAsync(
    client,
    AgenticActions.SortByColumn,
    new() { ["column"] = "Amount", ["direction"] = "descending" });
```

`direction` 可以是 `ascending`、`descending`、`asc` 或 `desc`，默认为
`ascending`。每次排序后都应重新执行 `getRows`，因为视图行号可能已改变。

### 9.2 过滤

```csharp
await ExecuteGridAsync(
    client,
    AgenticActions.FilterByColumn,
    new()
    {
        ["column"] = "Product",
        ["value"] = "PLC",
        ["mode"] = "contains"
    });
```

`mode` 支持：

- `contains`：包含，默认值；
- `equals`：完全相等；
- `startsWith`：以指定文本开头。

清除过滤：

```csharp
await ExecuteGridAsync(
    client,
    AgenticActions.FilterByColumn,
    new() { ["column"] = "Product", ["value"] = "" });
```

当 `value` 是空字符串或 `null` 时清除过滤。当前一次只保持一个
AgenticUI 列过滤；WPF 会与应用原有的 `ICollectionView.Filter` 条件合并。

## 10. 选择、滚动与高亮

```csharp
// 选中第 1 行
await ExecuteGridAsync(
    client,
    AgenticActions.SelectRow,
    new() { ["row"] = 0 });

// 滚动到第 21 行
await ExecuteGridAsync(
    client,
    AgenticActions.ScrollToRow,
    new() { ["row"] = 20 });

// 选中单元格
await ExecuteGridAsync(
    client,
    AgenticActions.SelectCell,
    new() { ["row"] = 0, ["column"] = "OrderNumber" });

// 为单元格显示 AI 指引框
await ExecuteGridAsync(
    client,
    AgenticActions.HighlightCell,
    new() { ["row"] = 0, ["column"] = "OrderNumber" });

// 清除表格和单元格高亮
await ExecuteGridAsync(client, AgenticActions.ClearHighlight);
```

`highlightCell` 只表示“引导用户查看或操作这个单元格”，不会修改数据，
也不等于选中或点击。

## 11. 原始 JSON 示例

不使用 `AgenticNamedPipeClient` 时，Named Pipe 每行发送一个 UTF-8 JSON。
执行表格命令的完整包装如下：

```json
{
  "type": "execute",
  "requestId": "execute-1",
  "command": {
    "requestId": "grid-rows-1",
    "controlId": "orders.grid",
    "action": "getRows",
    "arguments": {
      "start": 0,
      "count": 50
    }
  }
}
```

成功响应中，表格数据位于 `result.control.state`。下面是省略了
与表格无关字段的简化示例：

```json
{
  "type": "result",
  "requestId": "execute-1",
  "result": {
    "requestId": "grid-rows-1",
    "succeeded": true,
    "control": {
      "id": "orders.grid",
      "kind": "dataGrid",
      "state": {
        "visible": true,
        "displayable": true,
        "selectedIndex": 0,
        "rowCount": 2,
        "columnCount": 4,
        "start": 0,
        "count": 2,
        "total": 2,
        "rows": [
          {
            "_index": 0,
            "OrderNumber": "SO-001",
            "Product": "伺服电机",
            "Quantity": 2,
            "Amount": 6800
          }
        ]
      }
    }
  }
}
```

WPF 的 `kind` 为 `dataGrid`，WinForms 的 `kind` 为 `dataGridView`。

## 12. 建议的 AI 操作顺序

1. 调用 `listControls`，确认 `orders.grid` 当前可展示且其 `actions`
   包含目标动作。
2. 调用 `getColumns`，获取稳定列名。
3. 调用 `getRows`分页读取，不要盲目读取整张大表。
4. 排序或过滤后重新读取数据，不要沿用旧的行号。
5. 修改前使用 `getRow` 或 `getCell` 核对目标。
6. 需要人工确认时使用 `highlightCell`，确认后再执行 `setCell` 或
   业务操作。
7. 操作后再次读取状态，确认实际结果。

## 13. 常见错误

| 错误 | 原因与处理 |
| --- | --- |
| `Row 'x' is out of range.` | 行号超出当前过滤后视图；重新调用 `getRows` |
| `Column 'x' was not found.` | 列名不匹配；先调用 `getColumns` |
| `getRows count cannot exceed 500.` | 单次请求过大；改为分页读取 |
| `setCell requires a bound DataGrid column.` | WPF 目标列不是可识别的绑定列 |
| `Property 'x' cannot be written.` | WPF 绑定属性或 WinForms 行对象属性不可写 |
| `The DataGrid ItemsSource is read-only.` | WPF 数据源不允许增删 |
| `The bound data source does not support filtering.` | WinForms `BindingSource` 底层数源不支持过滤 |
| `Column 'x' is not sortable.` | WinForms 列的 `SortMode` 是 `NotSortable` |
| `控件当前不可远程操作` | 控件不在活动窗口、被模态弹窗阻挡或当前不可展示 |

执行前应以控件描述中的 `actions` 为准，客户端不应假设所有
DataGrid 数据源都支持全部动作。

## 14. 相关文档

- [快速开始](quickstart.zh-CN.md)
- [本机协议](local-protocol.zh-CN.md)
- [架构说明](architecture.zh-CN.md)
- [安全策略](../SECURITY.md)
