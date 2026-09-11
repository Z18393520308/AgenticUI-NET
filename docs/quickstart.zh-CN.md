# AgenticUI.NET 使用指南

面向第一次使用控件库的应用开发者。本文件是唯一的接入和使用指南；保留 `quickstart`
路径以兼容官网和 NuGet 的已有链接。维护控件库、开发控制端或查询完整协议，请阅读
[开发文档](development.zh-CN.md)。许可证、安全策略和修改历史仍由根目录专用文件维护。

## 目录

- [1. 产品与版本](#start)
- [2. 运行现成演示](#demo)
- [3. WPF 从零接入](#wpf)
- [4. WinForms 从零接入](#winforms)
- [5. 连接并执行第一条命令](#client)
- [6. 控件身份与类型](#controls)
- [7. 下拉框和列表](#items)
- [8. DataGrid 表格](#grid)
- [9. 动态描边、编号与气泡](#guidance)
- [10. 树、菜单与应用内鼠标](#other-controls)
- [11. 日志、敏感数据与录制](#audit)
- [12. 网络控制与首次配对](#network)
- [13. 排错与上线检查](#troubleshooting)

<a id="start"></a>
## 1. 产品与版本

AgenticUI.NET 为真实控件增加稳定身份、状态读取、语义动作、事件和可视化引导。
它不包含大模型、自然语言理解或任务规划，也不会自动连接 AI 服务。
人和外部控制端使用同一套控件、原生事件和业务处理；未启动通信服务时仍可人工使用。
常见场景包括工业上位机、仪器软件、CAD/设计工具和企业桌面客户端。

| 角色 | 职责 |
| --- | --- |
| 被控应用 | 注册真实界面；需要进程外访问时启动通信服务；保留业务权限检查 |
| 控制端 | 连接、读取控件、发送动作、核验结果；可以是普通程序，不必含 AI |
| AI/Agent | 决定任务步骤、气泡内容、何时等待人工和何时继续；在控件库之外实现 |

### 版本范围

本文对应 [v0.7.0](https://github.com/Z18393520308/AgenticUI-NET/releases/tag/v0.7.0)（2026-09-11）。
0.7.0 正式包含应用内网关、首次配对与 items.v1；旧 0.6.1 包不包含这些 API。
后续 main 新增但未发布的能力必须另行标注，安装时以对应 Release/tag 的文档为准。

| 能力 | 旧版 0.6.1 | 正式包 0.7.0 |
| --- | --- | --- |
| WPF/WinForms、ID、Named Pipe、日志、录制 | 支持 | 支持 |
| 动态引导、DataGrid、树状态、应用内鼠标 | 支持；仍需检查实际 actions/capabilities | 支持 |
| 配置驱动 AgenticApplicationHost、内嵌 WSS、自动证书、首次配对 | 不包含本次新接入 API | 支持，网络默认关闭 |
| items.v1、getItems、业务键选择、WPF 自动准备选项容器 | 不包含 | 支持 |
| Web/React/Vue 控件 | 未实现 | 仅预留跨框架协议边界 |

从零示例使用 .NET 8 和正式包的本机链路。库也支持 .NET Framework 4.8；现成 Demo 是
.NET 8 Windows 应用。开发机建议安装 .NET 8 SDK 和 Visual Studio“.NET 桌面开发”工作负载。
运行 WPF/WinForms 必须使用 Windows；依赖框架的 .NET 8 部署需要 Windows Desktop Runtime。

安装 Wpf 或 WinForms 包会引入 Core；进程外访问再装 Remote。四个包保持同版本组合。
仅协议/注册表/日志可单独安装 Core。授权以 [LICENSING.md](../LICENSING.md) 为准，
不能把“开源”理解为无条件闭源集成许可。

<a id="demo"></a>
## 2. 运行现成演示

Windows 终端进入仓库根目录：

```powershell
dotnet restore AgenticUI.NET.sln
dotnet build AgenticUI.NET.sln -c Release --no-restore
```

WPF：在两个终端分别执行下面两条命令。

```powershell
dotnet run --project samples/Wpf/AgenticUI.Workbench.Wpf -c Release --no-build
dotnet run --project samples/Wpf/AgenticUI.RemoteConsole.Wpf -c Release --no-build
```

WinForms：同样在两个终端分别运行。

```powershell
dotnet run --project samples/WinForms/AgenticUI.Workbench.WinForms -c Release --no-build
dotnet run --project samples/WinForms/AgenticUI.RemoteConsole.WinForms -c Release --no-build
```

Workbench 是被控端，RemoteConsole 是控制端。当前源码配置默认只开本机 Pipe。

| 示例 | 管道名 | 可选网络端口，默认关闭 |
| --- | --- | --- |
| WinForms Workbench | AgenticUI.NET | 7443 |
| WPF Workbench | AgenticUI.NET.Wpf | 7444 |

1. 保持 Workbench 打开，显示目标所在标签页。
2. 将其本地显示的管道名和实际随机令牌填到 Console，选择 Pipe 后连接。
3. 刷新列表，选择 login.username 设置文本并读取确认；选择 login.submit 高亮。
4. 测试 login.role 下拉框、demo.grid 表格和 demo.mouseSurface 鼠标画布。
5. 用 dialog.open 打开自定义模态弹窗，刷新后操作 dialog.ok / dialog.cancel。

令牌不要出现在公开聊天、Issue 或截图中。网络身份按钮不是 AI 控件，不能远程开启配对。
跨电脑扫描先完成[网络配置](#network)。

<a id="wpf"></a>
## 3. WPF 从零接入

Windows 创建项目；已有项目只安装包、合并相应代码，不覆盖业务文件。

```powershell
dotnet new wpf -n WpfQuickStart -f net8.0
cd WpfQuickStart
dotnet add package AgenticUI.Wpf --version 0.7.0
dotnet add package AgenticUI.Remote --version 0.7.0
```

### 3.1 同一套人机共用控件

新项目 MainWindow.xaml 使用以下完整内容：

<!-- verify-file: wpf/MainWindow.xaml -->
```xml
<Window x:Class="WpfQuickStart.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:aui="clr-namespace:AgenticUI.Wpf;assembly=AgenticUI.Wpf"
        Title="AgenticUI 接入示例" Width="460" Height="260">
    <StackPanel Margin="24">
        <TextBlock Text="账号" />
        <aui:AgenticTextBox x:Name="UserNameBox"
                           AgenticId="login.username" AgenticDisplayName="账号"
                           Height="32" Margin="0,8,0,16" />
        <aui:AgenticButton AgenticId="login.submit" AgenticDisplayName="登录"
                          Content="登录" Height="36" Click="OnLogin" />
        <TextBlock x:Name="ResultText" Margin="0,12,0,0" />
    </StackPanel>
</Window>
```

MainWindow.xaml.cs：

<!-- verify-file: wpf/MainWindow.xaml.cs -->
```csharp
using System.Windows;

namespace WpfQuickStart;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnLogin(object sender, RoutedEventArgs e)
    {
        // 人工点击和远程 click 都进入这里；真实项目保留权限与业务校验。
        ResultText.Text = $"已收到 {UserNameBox.Text} 的登录请求";
    }
}
```

### 3.2 服务跟随应用生命周期

保留模板 App.xaml 的 StartupUri="MainWindow.xaml"，App.xaml.cs 改为：

<!-- verify-file: wpf/App.xaml.cs -->
```csharp
using System.Windows;
using AgenticUI.Remote;

namespace WpfQuickStart;

public partial class App : Application
{
    private AgenticNamedPipeServer? _server;
    public string LocalAuthenticationToken => _server?.AuthenticationToken ?? "";

    protected override void OnStartup(StartupEventArgs e)
    {
        _server = new AgenticNamedPipeServer("MyProduct.Wpf");
        _server.Start();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _server?.Dispose();
        base.OnExit(e);
    }
}
```

调试器读取 `((App)Application.Current).LocalAuthenticationToken` 交给 Console，管道名填
MyProduct.Wpf。生产通过仅授权本机用户可见的管理 UI 交付，不写普通日志。
不要在短暂初始化方法中 using 服务后立即返回，否则方法结束服务就被释放。

### 3.3 保留原生控件

在现有 XAML 增加上述 aui 命名空间，再添加附加属性，不用为 AI 另写界面：

```xml
<Button aui:AgenticProperties.Enabled="True"
        aui:AgenticProperties.Id="login.submit"
        aui:AgenticProperties.DisplayName="登录"
        Content="登录" Click="OnLogin" />
```

这是替代写法，不要同时保留两个相同 ID 的活动控件。保留原有 Command、绑定和 Click。
只设置 Id 不会启用原生控件接入，必须 Enabled=True。

<a id="winforms"></a>
## 4. WinForms 从零接入

```powershell
dotnet new winforms -n WinFormsQuickStart -f net8.0
cd WinFormsQuickStart
dotnet add package AgenticUI.WinForms --version 0.7.0
dotnet add package AgenticUI.Remote --version 0.7.0
```

新项目 Program.cs 使用以下完整内容；原模板 Form1 可保留，不参与启动。

<!-- verify-file: winforms/Program.cs -->
```csharp
using System.Drawing;
using System.Windows.Forms;
using AgenticUI.Remote;
using AgenticUI.WinForms;

namespace WinFormsQuickStart;

internal static class Program
{
    internal static string LocalAuthenticationToken { get; private set; } = "";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var server = new AgenticNamedPipeServer("MyProduct.WinForms");
        server.Start();
        LocalAuthenticationToken = server.AuthenticationToken;
        Application.Run(new LoginForm());
    }
}

public sealed class LoginForm : Form
{
    public LoginForm()
    {
        Text = "AgenticUI 接入示例";
        ClientSize = new Size(460, 240);
        var input = new AgenticTextBox
        {
            AgenticId = "login.username", AgenticDisplayName = "账号",
            Location = new Point(24, 36), Width = 380
        };
        var button = new AgenticButton
        {
            AgenticId = "login.submit", AgenticDisplayName = "登录",
            Text = "登录", Location = new Point(24, 84), Size = new Size(380, 36)
        };
        var result = new Label { Location = new Point(24, 140), AutoSize = true };
        button.Click += (_, _) => result.Text = $"已收到 {input.Text} 的登录请求";
        Controls.AddRange(new Control[] { input, button, result });
    }
}
```

调试器读取 Program.LocalAuthenticationToken；Console 管道名填 MyProduct.WinForms。
net48 保留原有 STAThread Main，用 Application.EnableVisualStyles() 和
Application.SetCompatibleTextRenderingDefault(false) 初始化；不要复制 .NET 8 专用的
ApplicationConfiguration.Initialize()。控件与服务 API 使用方式相同。

已有原生控件在 InitializeComponent() 后附加：

```csharp
AgenticControlBinder.Attach(existingButton, new AgenticControlOptions
{
    Id = "login.submit", DisplayName = "登录"
});
```

已经附加的控件用 GetOptions(control) 修改选项，更改 Id 后调用 Refresh(control)。
解除接入用 AgenticControlBinder.Detach(control)，不删除原控件。

<a id="client"></a>
## 5. 连接并执行第一条命令

先用 Console 验证，再写自己的控制端。新建 .NET 8 控制台项目，安装同版本 AgenticUI.Remote。
以下 Program.cs 接收本机输入，不把令牌放进源码或命令行参数。

<!-- verify-file: client/Program.cs -->
```csharp
using AgenticUI;
using AgenticUI.Remote;

Console.Write("请输入本机管道名：");
var pipeName = Console.ReadLine() ?? "";
Console.Write("请输入本机令牌（输入不回显）：");
var token = "";
for (var key = Console.ReadKey(true); key.Key != ConsoleKey.Enter; key = Console.ReadKey(true))
{
    if (key.Key == ConsoleKey.Backspace) { if (token.Length > 0) token = token[..^1]; }
    else if (!char.IsControl(key.KeyChar)) token += key.KeyChar;
}
Console.WriteLine();
using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var client = await AgenticNamedPipeClient.ConnectAsync(token, pipeName,
    clientName: "MyControlClient", cancellationToken: lifetime.Token);

var discovery = await client.ListControlsAsync(lifetime.Token);
if (discovery.Controls is null) throw new InvalidOperationException(discovery.Error ?? "发现失败");
foreach (var control in discovery.Controls)
    Console.WriteLine($"{control.Id} | {control.Kind} | {string.Join(", ", control.Actions)}");

var response = await client.ExecuteAsync(new AgenticCommand
{
    ControlId = "login.username", Action = AgenticActions.SetText,
    Arguments = { ["text"] = "alice" }
}, lifetime.Token);
if (response.Result?.Succeeded != true)
    throw new InvalidOperationException(response.Result?.Error ?? response.Error ?? "设置失败");

var readBack = await client.ExecuteAsync(new AgenticCommand
{
    ControlId = "login.username", Action = AgenticActions.GetText
}, lifetime.Token);
if (readBack.Result?.Succeeded != true)
    throw new InvalidOperationException(readBack.Result?.Error ?? readBack.Error ?? "读取失败");
Console.WriteLine("读取结果：" + readBack.Result.Control?.State["text"]);
```

必须检查 response.Result?.Succeeded，不能只看“请求已发送”。成功只表示控件动作成功，
不是登录、库存提交或设备动作完成凭据；要核验业务状态。失败/超时后不要盲目重试非幂等动作。
同进程可直接 `await new AgenticCommandDispatcher().DispatchAsync(command)`，不启动服务。
参数放 Arguments，不是与 Action 同级的 Value；完整 JSON 见[开发文档](development.zh-CN.md#protocol)。

输入后跳下一框属于控制端编排：核验输入，再向下一控件发 focus。
控件不会把 setText 擅自解释成 Tab、Enter、失焦或提交，也不会决定任务下一步。

<a id="controls"></a>
## 6. 控件身份与类型

| 元数据 | 替换控件 | WPF 附加属性 | WinForms Options |
| --- | --- | --- | --- |
| 稳定 ID | AgenticId | Id | Id |
| 人可读名称 | AgenticDisplayName | DisplayName | DisplayName |
| 敏感标记 | IsSensitive | Sensitive | IsSensitive |
| 本地默认编号 | InstructionNumber | InstructionNumber | InstructionNumber |
| 本地默认提示 | Hint | Hint | Hint |

业务 ID 用 login.username、orders.grid 等语义名称，不用显示文字或屏幕坐标。
活动控件不重号；动态子控件要包含业务身份并处理卸载。未设置时生成 temporary.N，
不保证跨启动稳定。编号是任务步骤，不是控件 ID。

| 类别 | WPF 类名，均以 Agentic 开头 | WinForms 类名，均以 Agentic 开头 |
| --- | --- | --- |
| 基础 | Button、TextBox、CheckBox、RadioButton、ComboBox | 同左 |
| 列表 | ListBox、TabControl、ListView | ListBox、CheckedListBox、TabControl、ListView |
| 值 | DatePicker、Slider、ToggleButton | DateTimePicker、NumericUpDown、TrackBar |
| 数据/导航 | DataGrid、TreeView、Menu、ToolBar | DataGridView、TreeView、MenuStrip、ToolStrip、StatusStrip |
| 展示/绘图 | ProgressBar、TextBlock、Label、Canvas | ProgressBar、Label、Panel |

WPF 密码框用原生 PasswordBox + 附加属性，没有 AgenticPasswordBox。
类型支持某动作，不等于当前数据源/只读/列类型/权限允许执行；先读 actions 和 capabilities。
默认原生外观。WPF 可选合并 `/AgenticUI.Wpf;component/Themes/ModernTheme.xaml`；WinForms 可用
AgenticModernTheme.Apply(root, AgenticUiTheme.Modern)，切回 Native 恢复。高亮不要求额外 AdornerDecorator。

<a id="items"></a>
## 7. 下拉框和列表

本节 items.v1 从 0.7.0 起提供。0.6.1 旧参数为 index/value，不保证对象 ToString 与界面相同。
“设置选中项”用 selectItem，没有 setItem 动作，也不是修改候选数据源。

WPF：Books 每项有公开 Id、Name 属性，使用普通绑定即可。

```xml
<aui:AgenticComboBox AgenticId="inbound.line.material"
                     ItemsSource="{Binding Books}" DisplayMemberPath="Name"
                     SelectedValuePath="Id"
                     SelectedValue="{Binding SelectedBookId, Mode=TwoWay}" />
```

WinForms：

```csharp
var booksCombo = new AgenticComboBox
{
    AgenticId = "inbound.line.material", DropDownStyle = ComboBoxStyle.DropDownList,
    DisplayMember = "Name", ValueMember = "Id", DataSource = books
};
```

普通绑定不用 AI 专用 DTO；复杂模板才配置可选解析器，见[选项协议](development.zh-CN.md#items-contract)。

先确认 actions 含 getItems，capabilities 含 items.v1，再发送以下 command 对象：

```json
{"controlId":"inbound.line.material","action":"getItems","arguments":{"start":0,"count":50}}
```

读取 result.control.state.items，每项含 index、text、itemKey、isSelected、isEnabled；
同一响应还有 itemsVersion。假如实际返回业务键 BK-001：

```json
{"controlId":"inbound.line.material","action":"selectItem","arguments":{"itemKey":"BK-001"}}
```

无键时用实际 index 和原样返回的 itemsVersion。value 不做包含匹配：“三体”不会自动选择
“BK-001 三体”。重名、重复键、过期版本拒绝猜测；完成后核对 selectedIndex/selection/itemKey。

0.7.0 的 WPF 自动展开/定位虚拟化选项，异步等真实容器再检查禁用状态。普通样式造成
isEnabled:null 不再立即拒绝，真正禁用仍不可选择。等待预算 2 秒、可取消；人工改选、关闭、
卸载或列表变化会中止。自动展开的结束时关闭，原本展开的保持展开，绑定继续保留。

getItems 只读已加载数据，不展开、不查数据库。如果业务在 DropDownOpened 加载/重排，
先 openDropDown，等业务完成再 getItems 和选择；自动准备中发生这种变化也会失败，不能偷偷改选。
业务同步事件阻塞 UI 的问题仍需宿主修复，容器等待超时不能抢占阻塞线程。

<a id="grid"></a>
## 8. DataGrid 表格

WPF 用 AgenticDataGrid，WinForms 用 AgenticDataGridView，或给原表格附加接入。设置稳定 ID
orders.grid。row 是当前排序/过滤后视图索引，从 0 开始，不是数据库主键；column 优先用
getColumns 返回的稳定列名。

WPF 在已有窗口使用；Orders 可为 ObservableCollection<OrderRow>，行模型有公开无参构造函数
和公开可写的 OrderNumber、Quantity 属性。业务程序更新数据时仍按普通 WPF 规则实现通知。

```xml
<aui:AgenticDataGrid AgenticId="orders.grid" ItemsSource="{Binding Orders}"
                     AutoGenerateColumns="False" IsReadOnly="False"
                     CanUserAddRows="True" CanUserDeleteRows="True">
    <DataGrid.Columns>
        <DataGridTextColumn Header="订单号" Binding="{Binding OrderNumber}" />
        <DataGridTextColumn Header="数量" Binding="{Binding Quantity}" />
    </DataGrid.Columns>
</aui:AgenticDataGrid>
```

WinForms 未绑定表格，放在 Form 初始化中：

```csharp
var grid = new AgenticDataGridView
{
    AgenticId = "orders.grid", Dock = DockStyle.Fill, ReadOnly = false,
    AllowUserToAddRows = true, AllowUserToDeleteRows = true
};
grid.Columns.Add(new DataGridViewTextBoxColumn
{
    Name = "OrderNumber", HeaderText = "订单号", SortMode = DataGridViewColumnSortMode.Automatic
});
grid.Columns.Add(new DataGridViewTextBoxColumn
{
    Name = "Quantity", HeaderText = "数量", ValueType = typeof(int),
    SortMode = DataGridViewColumnSortMode.Automatic
});
grid.Rows.Add("SO-001", 2);
Controls.Add(grid);
```

这里显式允许人工增删是为了演示；生产若不允许就设为 false，远程也不能绕过。

| 用途 | action | arguments 示例 |
| --- | --- | --- |
| 读取列 | getColumns | `{}` |
| 读多行/一行 | getRows / getRow | `{"start":0,"count":50}` / `{"row":0}` |
| 读/写单元格 | getCell / setCell | `{"row":0,"column":"Quantity"}`；写另加 `"value":5` |
| 选择行/滚动 | selectRow / scrollToRow | `{"row":0}` |
| 选择/高亮单元格 | selectCell / highlightCell | `{"row":0,"column":"Quantity"}` |
| 新增 | addRow | `{"values":{"OrderNumber":"SO-002","Quantity":1}}` |
| 删除 | deleteRow | `{"row":0}` |
| 排序 | sortByColumn | `{"column":"Quantity","direction":"descending"}` |
| 过滤 | filterByColumn | `{"column":"OrderNumber","value":"SO-","mode":"startsWith"}` |

结果在 result.control.state 的 columns/rows/row/cell；分页含 start、实际 count、total，
单次最多 500 行。过滤 value 为空字符串或 null 清除。关键限制：

- 排序/过滤/增删后重读，不沿用旧行号；目前没有 DataGrid 行版本/稳定行键协议。
- getCell 当前可能改变表格选中项/当前单元格；仅观察且需要保持选择时优先 getRow/getRows。
- setCell 走支持的原生文本编辑器及校验，不保证模板、复选、下拉编辑列支持。
- WPF 增删需可写 IList 数据源和可创建行类型；排序/过滤需可识别绑定属性。
- WinForms 增删支持未绑定表格或 BindingSource；绑定排序/过滤依赖底层能力。
  普通 BindingList 套上 BindingSource 不会自动支持过滤。
- 失败不保证业务回滚；非文本编辑器和高风险操作继续走原应用授权和事件。

完整字段、列匹配与约束见[表格协议](development.zh-CN.md#grid-contract)。

<a id="guidance"></a>
## 9. 动态描边、编号与气泡

独立展示，不是按钮按下，不会点击或改数据。先检查 dynamicGuidance.v1。文字和显隐由控制端
决定，不要求控件写死 Hint。

```json
{
  "controlId":"login.username", "action":"highlight",
  "arguments":{
    "guidanceId":"task-1-step-1", "showOutline":true,
    "showNumber":true, "instructionNumber":1,
    "showBubble":true, "hint":"请填写账号，核对后继续。",
    "placement":"auto", "durationMs":0
  }
}
```

同连接、同控件、同 guidanceId 再发即更新。每次是完整展示快照，不是局部补丁；建议始终
显式发送三个开关。showBubble:false 不回退显示旧 Hint。提示最多 1000 字符纯文本，
不执行 HTML/Markdown/脚本；禁止放入密码、令牌和隐私数据。

```json
{"controlId":"login.username","action":"clearHighlight","arguments":{"guidanceId":"task-1-step-1"}}
```

不传 guidanceId 清除此连接在该控件上的全部引导；断线、卸载和到期也清理，不影响其他连接。
单元格用 highlightCell，加 row/column，其余参数相同。范围见[引导协议](development.zh-CN.md#guidance-contract)。

<a id="other-controls"></a>
## 10. 树、菜单与应用内鼠标

树用 selectItem/expand/collapse，优先完整 path，如公司/研发/设计。路径不是永久主键，
改名、重名、移除或未生成容器要重新发现。当前选择是 state.path，最近展开/折叠是
expansionPath/expanded，二者不同。菜单/工具栏用注册控件的 click + path，如文件/打开。
系统 MessageBox 内部按钮没有自动注册；远程确认用软件自己的可接入对话框。

画布用 AgenticCanvas/AgenticPanel 或已注册绘图区域。坐标是控件内部 0～1 比例：

```json
{"controlId":"editor.canvas","action":"mouseDrag","arguments":{"startXRatio":0.2,"startYRatio":0.3,"endXRatio":0.8,"endYRatio":0.7,"button":"left","steps":12}}
```

还支持 mouseMove/mouseClick/mouseDoubleClick/mouseWheel。不移动系统真实指针，不操作桌面、
任务栏或其他进程；控件、坐标和路径必须可交互。普通按钮优先语义 click，比例坐标不能
替代业务目标校验，也不保证适配所有第三方绘图库。

<a id="audit"></a>
## 11. 日志、敏感数据与录制

日志不自动上传，但需要宿主创建记录器并保持生命周期。WinForms 可放在 Application.Run
外层；WPF 保存 App 字段并在退出时 Dispose，不放在短暂返回的方法中。

```csharp
using AgenticUI;

var logDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyProduct", "audit");
using var audit = new AgenticLogRecorder(Path.Combine(logDirectory, "events.jsonl"),
    options: new AgenticLogOptions
    {
        Level = AgenticLogLevel.Semantic,
        RedactSensitiveValues = true
    });
```

默认过滤 pressed/released/focusChanged，排查时可改 Detailed。这只是记录器过滤，事件总线仍
广播事件。本地用 AgenticEventBus.Default.Subscribe，远程用 client.EventReceived 订阅。
回调不保证 UI 线程；更新界面用 Dispatcher/BeginInvoke，订阅处理要快速返回。

### 脱敏不等于自动识别一切秘密

密码框会识别为敏感；普通账号、手机号或业务参数不会自动识别。需要保护的控件主动标记：

```xml
<aui:AgenticTextBox AgenticId="settings.secret" IsSensitive="True" />
```

```csharp
AgenticControlBinder.GetOptions(secretTextBox).IsSensitive = true;
```

敏感控件远程状态除严格布尔 readOnly 外全部置 null，候选/行列也不例外。默认日志替换
敏感事件数据。关闭 RedactSensitiveValues 只影响该本地记录器，不关闭远程脱敏。
ID、名称、气泡也不要含秘密。

### 录制与回放

```csharp
using var recording = new AgenticInteractionRecorder(
    Path.Combine(logDirectory, "recording-" + Guid.NewGuid().ToString("N") + ".jsonl"));
```

默认跳过 Remote/Replay 来源和敏感文本；RecordSensitiveText=true 是单独的风险选项。
同路径会覆盖旧录制，因此每次用新文件名；审计日志则追加。0.7.0 的选项优先录业务键，无键
仍可能依赖索引。这不是完整鼠标轨迹或任意自定义动作录制器。

```csharp
var commands = await AgenticReplay.LoadCommandsAsync(recordingPath);
var results = await new AgenticReplay(new AgenticCommandDispatcher())
    .ReplayAsync(commands, TimeSpan.FromMilliseconds(250));
```

recordingPath 替换为已有文件。上例同进程回放按顺序执行，但不自动在失败项停止，没有业务
幂等保证。高风险流程控制端应逐条检查再决定继续。JSONL 不是防篡改审计系统，滚动、保留
周期、磁盘权限和业务完成凭据仍由宿主负责。

<a id="network"></a>
## 12. 网络控制与首次配对

本节 API 从正式 NuGet 0.7.0 起提供，升级时统一更新相关包。当前没有独立 Gateway，
也不需要手动 HTTP.sys 授权、netsh 绑定或安装系统证书，
加密与首次核验仍保留。

### 12.1 每个应用初始化一次

用 AgenticApplicationHost 替换前述 NamedPipeServer，不能重复启动同名服务。
不是每个控件构造时自动监听；应用入口明确初始化，配置决定服务开关。WinForms UI 初始化后：

```csharp
using var host = AgenticApplicationHost.StartFromConfiguration();
Application.Run(new MainForm());
```

WPF 在 App.OnStartup 保存 StartFromConfiguration 返回值，在 App.OnExit Dispose。
用 LocalRunning/NetworkRunning/DiscoveryRunning/LastError 显示诊断，不只看配置开关。

### 12.2 配置

EXE 同目录放 agenticui.json，SDK 风格项目在 ItemGroup 添加：

```xml
<None Update="agenticui.json" CopyToOutputDirectory="PreserveNewest"
      CopyToPublishDirectory="PreserveNewest" />
```

仅本机测试 WSS/扫描的完整配置，只放行读取和引导：

```json
{
  "AgenticUI": {
    "AutoStart": true,
    "Local": { "Enabled": true, "PipeName": "MyProduct.Wpf" },
    "Network": {
      "Enabled": true, "BindAddress": "127.0.0.1",
      "ListenUrl": "https://localhost:7444", "WebSocketPath": "/agenticui",
      "AllowedActions": ["getText", "getItems", "getRows", "getColumns", "highlight", "clearHighlight"]
    },
    "Discovery": { "Enabled": true, "ServiceName": "MyProduct WPF" }
  }
}
```

- Network.Enabled=false 关网络，Discovery.Enabled=false 关发现，AutoStart=false 关全部通信。
- 缺失文件、拼错字段和非法配置会失败，不静默忽略；查看实际 EXE 目录配置与 LastError。
- 网络要求 Local.Enabled=true；发现依赖网络成功，不会只开 Discovery 就开网络。
- 改配置要重启。不同产品/多实例用不同管道、端口及身份目录。
- 写操作按需逐项加入 AllowedActions，如 setText/selectItem/click；不允许 *。
  配对不绕过白名单，UI 禁用/只读也不是完整业务授权系统。

跨电脑将 BindAddress 改为目标局域网 IP 或 0.0.0.0，ListenUrl 改为该电脑可访问地址，
例如 https://192.168.1.50:7444（替换示例 IP）。公告默认由监听 URL 生成，不向其他电脑公告
localhost/0.0.0.0；必要时设置 Discovery.PublicWebSocketUrl，路径保持一致。
防火墙按需对可信网络放行目标 TCP 端口及扫描端 UDP 47731，本库不自动修改防火墙。

### 12.3 配对

1. 目标确认网络成功，本地点击“网络身份 / 开启首次配对”。
2. Console 选 WSS，扫描选目标后连接，或手工输入完整 WSS 地址。
3. 将控制端完整 SHA-256 证书指纹与目标端本地显示值独立核对。
4. 确认后输入一次性码；成功保存本地凭据，普通重启不必重配。

码有效 3 分钟，最多 5 次尝试，只能成功一次；关闭目标配对窗口取消。
目标撤销所有客户端使配对失效并断开连接；控制端“忘记”只删除自身记录。
UDP 公告不可信，扫描成功不是身份认证；证书变化不能自动接受。

目标应用自己提供本地管理 UI，调用 host.Pairing.BeginPairing()/CancelPairing()/RevokeAllClients()，
不要把管理入口注册成远程动作。可参考 [WPF 配对界面](../samples/Shared/PairingDialogs.Wpf.cs)
和 [WinForms 配对界面](../samples/Shared/PairingDialogs.WinForms.cs)。控制端用
AgenticWebSocketClient.ConnectPairedAsync，回调切自己的 UI 线程核验和输入。

Windows 身份私钥/凭据用当前用户 DPAPI 保护，升级安装不能清空；换账户不能直接复制使用。
证书过期/损坏不自动换身份，需核实后显式处理并重配。浏览器不能照搬 .NET 指纹配对客户端，
未来 Web 控件不等于已支持浏览器直连。完整配置、兼容令牌和边界见[开发文档](development.zh-CN.md#hosting)。

<a id="troubleshooting"></a>
## 13. 排错与上线检查

| 现象 | 首先核对 |
| --- | --- |
| NETSDK1141 | 仓库没有锁死补丁版本的 global.json；检查父目录旧文件和 dotnet --list-sdks |
| 找不到控件 | UI 已加载、附加接入已启用、ID 不冲突、目标在当前标签页/可视区 |
| 隐藏控件可枚举却不可操作 | includeHidden 只诊断，不授予绕过隐藏、禁用、模态的权限 |
| Pipe 失败 | 目标进程、实际管道/令牌、同 Windows 用户、服务生命周期、重复服务 |
| 扫描不到 | 默认网络/发现关闭；核对实际配置、启动状态、端口、网卡、防火墙、广播隔离 |
| 扫描成功但连接失败 | UDP 不验证 WSS 可达性；地址可能仍是 localhost，或端口/TLS/配对失败 |
| 能高亮不能输入 | WSS 白名单未放行写动作，或目标只读/禁用/被模态窗口挡住 |
| Item not found / Ambiguous | 0.7.0 起先 getItems，再用真实 text 或唯一 itemKey，不做模糊书名匹配 |
| 仍报容器未就绪 | 检查实际 DLL；更新 Core/Wpf 并重启。缺少真实容器的自定义模板仍可能超时 |
| itemsVersion 过期 | 重读后决策，不去掉校验强行按旧索引选择 |
| 填完不跳下一框 | 控制端核验后发 focus，setText 不隐含 Tab/Enter |
| 高亮缺边/透明异常/卡顿 | 排除旧 DLL，提供最小窗口、缩放信息和脱敏日志复现 |
| DataGrid 操作失败 | 只读/人工增删开关、列类型和数据源能力；先 getColumns/getRows |
| 换源码还是旧行为 | 区分 ProjectReference/NuGet，核对输出 DLL，关闭旧进程后从新目录启动 |

交付前完成[Windows 验收](development.zh-CN.md#verification)：人工使用不依赖 AI，不能绕过
只读/禁用/模态，日志不泄密，气泡不抢焦点或挡点击，业务危险动作有授权，断线/超时不误报成功。
问题报告附版本、框架、controlId、action、最小复现和脱敏结果，不附真实令牌或业务秘密。
