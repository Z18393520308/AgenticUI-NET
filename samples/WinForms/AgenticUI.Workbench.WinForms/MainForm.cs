using System.Text.Json;
using AgenticUI.Remote;
using AgenticUI.WinForms;

namespace AgenticUI.Workbench.WinForms;

public partial class MainForm : Form
{
    private readonly AgenticControlRegistry _registry = AgenticControlRegistry.Default;
    private readonly AgenticCommandDispatcher _dispatcher = new();
    private readonly AgenticNamedPipeServer _server;
    private readonly AgenticLogRecorder _recorder;
    private readonly IDisposable _subscription;

    public MainForm()
    {
        InitializeComponent();

        if (roleCombo.Items.Count > 0 && roleCombo.SelectedIndex < 0)
        {
            roleCombo.SelectedIndex = 0;
        }

        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgenticUI.NET", "workbench-events.jsonl");
        _recorder = new AgenticLogRecorder(logPath);
        _server = new AgenticNamedPipeServer();
        _server.Start();
        _subscription = AgenticEventBus.Default.Subscribe(OnAgenticEventAsync);

        pipeNameBox.Text = "AgenticUI.NET";
        tokenBox.Text = _server.AuthenticationToken;
        AgenticControlBinder.Attach(nativeBoundButton, new AgenticControlOptions
        {
            Id = "native.boundButton",
            DisplayName = "通过 Binder 接入的原生按钮",
            InstructionNumber = 5,
            Hint = "无需替换原有控件"
        });
        AddAdvancedDemoControls();
        AddMediumDemoControls();

        // 默认原生外观（索引 0）
        themeCombo.SelectedIndex = 0;
        Shown += (_, _) =>
        {
            // 确保演示区句柄已创建，Agentic 控件完成注册后再给远程枚举。
            demoPanel.CreateControl();
            EnsureDemoPanelWidth();
            RefreshControls();
        };
        FormClosed += OnFormClosed;
    }

    private void ThemeCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // 0 = 原生外观，1 = 现代主题
        if (themeCombo.SelectedIndex == 0)
        {
            WorkbenchTheme.ApplyNative(this);
        }
        else
        {
            WorkbenchTheme.ApplyModern(this);
        }

        titleLabel.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
    }

    private void Refresh_Click(object? sender, EventArgs e) => RefreshControls();

    private async void Highlight_Click(object? sender, EventArgs e) =>
        await ExecuteSelectedAsync(AgenticActions.Highlight);

    private async void ClearHighlight_Click(object? sender, EventArgs e) =>
        await ExecuteSelectedAsync(AgenticActions.ClearHighlight);

    private async void Click_Click(object? sender, EventArgs e) =>
        await ExecuteSelectedAsync(AgenticActions.Click);

    private async void SelectNext_Click(object? sender, EventArgs e) =>
        await SelectNextItemAsync();

    private async void Focus_Click(object? sender, EventArgs e) =>
        await ExecuteSelectedAsync(AgenticActions.Focus);

    private async void SetText_Click(object? sender, EventArgs e) =>
        await SetSelectedTextAsync();

    private async void Read_Click(object? sender, EventArgs e) =>
        await ReadSelectedAsync();

    private void ControlsList_SelectedIndexChanged(object? sender, EventArgs e) =>
        SyncInputTextFromSelection();

    private async void InputText_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            await SetSelectedTextAsync();
        }
    }

    private void RefreshControls()
    {
        var selectedId = (controlsList.SelectedItem as AgenticControlDescriptor)?.Id;
        controlsList.BeginUpdate();
        controlsList.Items.Clear();
        foreach (var descriptor in _registry.Snapshot())
        {
            controlsList.Items.Add(descriptor);
        }
        controlsList.EndUpdate();
        if (selectedId is not null)
        {
            controlsList.SelectedItem = controlsList.Items
                .Cast<AgenticControlDescriptor>()
                .FirstOrDefault(item => item.Id == selectedId);
        }
        if (controlsList.SelectedIndex < 0 && controlsList.Items.Count > 0)
        {
            controlsList.SelectedIndex = 0;
        }

        SyncInputTextFromSelection();
    }

    private void SyncInputTextFromSelection()
    {
        if (controlsList.SelectedItem is not AgenticControlDescriptor descriptor)
        {
            inputTextBox.Enabled = false;
            return;
        }

        var supportsText = descriptor.Actions.Contains(AgenticActions.SetText, StringComparer.OrdinalIgnoreCase) ||
                           descriptor.Actions.Contains(AgenticActions.SetValue, StringComparer.OrdinalIgnoreCase) ||
                           descriptor.Actions.Contains(AgenticActions.GetText, StringComparer.OrdinalIgnoreCase) ||
                           descriptor.Actions.Contains(AgenticActions.GetValue, StringComparer.OrdinalIgnoreCase);
        inputTextBox.Enabled = supportsText;
        if (!supportsText)
        {
            return;
        }

        if (descriptor.State.TryGetValue("text", out var text) && text is not null)
        {
            inputTextBox.Text = text.ToString() ?? "";
        }
        else if (descriptor.State.TryGetValue("value", out var value) && value is not null)
        {
            inputTextBox.Text = value.ToString() ?? "";
        }
    }

    private async Task ReadSelectedAsync()
    {
        if (controlsList.SelectedItem is not AgenticControlDescriptor descriptor)
        {
            MessageBox.Show(this, "请先选择一个控件。", "AgenticUI.NET");
            return;
        }

        var supportsGetChecked = descriptor.Actions.Contains(
            AgenticActions.GetChecked,
            StringComparer.OrdinalIgnoreCase);
        var supportsGetText = descriptor.Actions.Contains(
            AgenticActions.GetText,
            StringComparer.OrdinalIgnoreCase);
        var supportsGetValue = descriptor.Actions.Contains(
            AgenticActions.GetValue,
            StringComparer.OrdinalIgnoreCase);
        if (!supportsGetChecked && !supportsGetValue && !supportsGetText)
        {
            MessageBox.Show(this, "该控件不支持读取（需要 getChecked、getValue 或 getText）。", "AgenticUI.NET");
            return;
        }

        await ExecuteSelectedAsync(supportsGetChecked ? AgenticActions.GetChecked : supportsGetValue ? AgenticActions.GetValue : AgenticActions.GetText);
        RefreshControls();
        if (controlsList.SelectedItem is not AgenticControlDescriptor updated)
        {
            return;
        }

        updated.State.TryGetValue("text", out var text);
        updated.State.TryGetValue("checked", out var checkedValue);
        updated.State.TryGetValue("value", out var value);
        if (text is not null)
        {
            inputTextBox.Text = text.ToString() ?? "";
        }

        MessageBox.Show(
            this,
            supportsGetChecked
                ? $"checked={checkedValue}\r\ntext={text}"
                : supportsGetValue
                    ? $"读取结果：{value}"
                : updated.IsSensitive
                    ? "敏感字段内容已脱敏。"
                    : $"读取结果：{inputTextBox.Text}",
            "AgenticUI.NET");
    }

    private async Task SetSelectedTextAsync()
    {
        if (controlsList.SelectedItem is not AgenticControlDescriptor descriptor)
        {
            MessageBox.Show(this, "请选择一个支持输入文本的控件。", "AgenticUI.NET");
            return;
        }

        var useText = descriptor.Actions.Contains(AgenticActions.SetText, StringComparer.OrdinalIgnoreCase);
        if (!useText && !descriptor.Actions.Contains(AgenticActions.SetValue, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "请选择一个支持文本或数值输入的控件。", "AgenticUI.NET");
            return;
        }
        await ExecuteSelectedAsync(useText ? AgenticActions.SetText : AgenticActions.SetValue,
            new Dictionary<string, object?> { [useText ? "text" : "value"] = inputTextBox.Text });
        RefreshControls();
    }

    private async Task ExecuteSelectedAsync(
        string action,
        Dictionary<string, object?>? arguments = null)
    {
        if (controlsList.SelectedItem is not AgenticControlDescriptor descriptor)
        {
            return;
        }

        var result = await _dispatcher.DispatchAsync(new AgenticCommand
        {
            ControlId = descriptor.Id,
            Action = action,
            Arguments = arguments ?? new Dictionary<string, object?>()
        });
        if (!result.Succeeded)
        {
            MessageBox.Show(this, result.Error, "命令执行失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task SelectNextItemAsync()
    {
        if (controlsList.SelectedItem is not AgenticControlDescriptor descriptor ||
            !descriptor.Actions.Contains(AgenticActions.SelectItem, StringComparer.OrdinalIgnoreCase) ||
            !_registry.TryGet(descriptor.Id, out var control) ||
            control is null)
        {
            MessageBox.Show(this, "请选择一个支持选择项目的下拉列表。", "AgenticUI.NET");
            return;
        }

        var current = control.Describe();
        var selectedIndex = ReadInt(current.State, "selectedIndex", -1);
        var itemCount = ReadInt(current.State, "itemCount", 0);
        if (itemCount == 0)
        {
            MessageBox.Show(this, "下拉列表中没有可选择的项目。", "AgenticUI.NET");
            return;
        }

        await ExecuteSelectedAsync(
            AgenticActions.SelectItem,
            new Dictionary<string, object?> { ["index"] = (selectedIndex + 1) % itemCount });
        RefreshControls();
    }

    private void EnsureDemoPanelWidth()
    {
        mainSplit.Panel1MinSize = 340;
        mainSplit.Panel2MinSize = 480;
        var maximum = Math.Max(340, mainSplit.Width - 488);
        mainSplit.SplitterDistance = Math.Min(480, maximum);
    }

    private void AddAdvancedDemoControls()
    {
        demoPanel.AutoScroll = true;
        var y = nativeBoundButton.Bottom + 24;
        const int left = 28;
        const int fieldLeft = 100;
        const int fieldWidth = 300;

        void Section(string title)
        {
            demoPanel.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(left, y),
                AutoSize = true
            });
            y += 28;
        }

        void FieldLabel(string text) =>
            demoPanel.Controls.Add(new Label { Text = text, Location = new Point(left, y + 4), AutoSize = true });

        Section("高优先级控件");
        FieldLabel("日期");
        demoPanel.Controls.Add(new AgenticDateTimePicker
        {
            AgenticId = "demo.date",
            AgenticDisplayName = "演示日期",
            Location = new Point(fieldLeft, y),
            Width = fieldWidth
        });
        y += 36;

        FieldLabel("数量");
        demoPanel.Controls.Add(new AgenticNumericUpDown
        {
            AgenticId = "demo.quantity",
            AgenticDisplayName = "演示数量",
            Location = new Point(fieldLeft, y),
            Width = 120,
            Minimum = 1,
            Maximum = 100,
            Value = 1
        });
        y += 36;

        FieldLabel("列表");
        var list = new AgenticListBox
        {
            AgenticId = "demo.list",
            AgenticDisplayName = "演示列表",
            Location = new Point(fieldLeft, y),
            Size = new Size(fieldWidth, 64)
        };
        list.Items.AddRange(new object[] { "北京", "上海", "广州" });
        demoPanel.Controls.Add(list);
        y += 72;

        FieldLabel("多选");
        var checkedList = new AgenticCheckedListBox
        {
            CheckOnClick = true,
            AgenticId = "demo.checkedList",
            AgenticDisplayName = "演示多选",
            Location = new Point(fieldLeft, y),
            Size = new Size(fieldWidth, 64)
        };
        checkedList.Items.AddRange(new object[] { "选项A", "选项B", "选项C" });
        demoPanel.Controls.Add(checkedList);
        y += 72;

        FieldLabel("音量");
        demoPanel.Controls.Add(new AgenticTrackBar
        {
            AgenticId = "demo.volume",
            AgenticDisplayName = "演示音量",
            Location = new Point(fieldLeft, y),
            Width = fieldWidth,
            Minimum = 0,
            Maximum = 100,
            Value = 40,
            TickFrequency = 10
        });
        y += 52;

        var tabs = new AgenticTabControl
        {
            AgenticId = "demo.tabs",
            AgenticDisplayName = "演示标签页",
            Location = new Point(left, y),
            Size = new Size(fieldLeft + fieldWidth - left, 88)
        };
        tabs.TabPages.AddRange([new TabPage("概览"), new TabPage("详情"), new TabPage("设置")]);
        demoPanel.Controls.Add(tabs);
        y += 104;

        Section("中优先级控件");
        demoPanel.Controls.Add(new AgenticLabel
        {
            AgenticId = "demo.statusText",
            AgenticDisplayName = "状态文本",
            Text = "状态：就绪",
            Location = new Point(left, y),
            AutoSize = true
        });
        y += 28;

        demoPanel.Controls.Add(new AgenticProgressBar
        {
            AgenticId = "demo.progress",
            AgenticDisplayName = "演示进度",
            Location = new Point(left, y),
            Width = fieldLeft + fieldWidth - left,
            Value = 35,
            Maximum = 100
        });
        y += 36;

        var listView = new AgenticListView
        {
            AgenticId = "demo.listView",
            AgenticDisplayName = "演示列表视图",
            Location = new Point(left, y),
            Size = new Size(fieldLeft + fieldWidth - left, 80),
            View = View.Details,
            FullRowSelect = true
        };
        listView.Columns.Add("名称", 140);
        listView.Columns.Add("城市", 140);
        listView.Items.Add(new ListViewItem(["Alice", "北京"]));
        listView.Items.Add(new ListViewItem(["Bob", "上海"]));
        demoPanel.Controls.Add(listView);
        y += 92;

        var tree = new AgenticTreeView
        {
            AgenticId = "demo.tree",
            AgenticDisplayName = "演示树",
            Location = new Point(left, y),
            Size = new Size(160, 110)
        };
        var company = tree.Nodes.Add("公司");
        company.Nodes.Add("研发");
        company.Nodes.Add("销售");
        company.Expand();
        demoPanel.Controls.Add(tree);

        var grid = new AgenticDataGridView
        {
            AgenticId = "demo.grid",
            AgenticDisplayName = "演示表格",
            Location = new Point(left + 172, y),
            Size = new Size(fieldLeft + fieldWidth - left - 172, 110),
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        grid.Columns.Add("Name", "姓名");
        grid.Columns.Add("Role", "角色");
        grid.Rows.Add("Alice", "管理员");
        grid.Rows.Add("Bob", "操作员");
        demoPanel.Controls.Add(grid);
        y += 122;

        var menu = new AgenticMenuStrip
        {
            AgenticId = "demo.menu",
            AgenticDisplayName = "演示菜单",
            Location = new Point(left, y),
            Dock = DockStyle.None,
            GripStyle = ToolStripGripStyle.Hidden
        };
        var file = new ToolStripMenuItem("文件");
        file.DropDownItems.Add("打开");
        file.DropDownItems.Add("退出");
        menu.Items.Add(file);
        demoPanel.Controls.Add(menu);
        y += 36;

        var toolbar = new AgenticToolStrip
        {
            AgenticId = "demo.toolbar",
            AgenticDisplayName = "演示工具栏",
            Location = new Point(left, y),
            Dock = DockStyle.None,
            GripStyle = ToolStripGripStyle.Hidden
        };
        toolbar.Items.Add("刷新");
        toolbar.Items.Add("导出");
        demoPanel.Controls.Add(toolbar);
        y += 48;

        demoPanel.AutoScrollMinSize = new Size(0, y + 24);

        var status = new AgenticStatusStrip { AgenticId = "demo.statusBar", AgenticDisplayName = "状态栏" };
        status.Items.Add(new ToolStripStatusLabel("Workbench"));
        Controls.Add(status);
    }

    private void AddMediumDemoControls()
    {
        // 中优先级控件已合并进 AddAdvancedDemoControls，保持调用点兼容。
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> state, string key, int fallback) =>
        state.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : fallback;

    private ValueTask OnAgenticEventAsync(AgenticEvent message)
    {
        if (IsDisposed)
        {
            return ValueTask.CompletedTask;
        }

        BeginInvoke(() =>
        {
            eventsList.Items.Insert(
                0,
                $"#{message.Sequence}  {message.Timestamp:HH:mm:ss.fff}  {message.ControlId}  {message.Name}  [{message.Source}]  {JsonSerializer.Serialize(message.Data)}");
            while (eventsList.Items.Count > 500)
            {
                eventsList.Items.RemoveAt(eventsList.Items.Count - 1);
            }
        });
        return ValueTask.CompletedTask;
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs args)
    {
        _subscription.Dispose();
        _recorder.Dispose();
        _server.Dispose();
    }

    private void MainForm_Load(object sender, EventArgs e)
    {

    }

    private ConfirmDialogForm? _confirmDialog;

    private void loginButton_Click(object sender, EventArgs e)
    {
        MessageBox.Show(this, "登录功能尚未实现。", "AgenticUI.NET");
    }

    private void openDialogButton_Click(object sender, EventArgs e)
    {
        // 延后弹出，避免远程 click 卡在 ShowDialog 上无法返回。
        BeginInvoke(ShowConfirmDialog);
    }

    private void ShowConfirmDialog()
    {
        if (_confirmDialog is not null && !_confirmDialog.IsDisposed)
        {
            _confirmDialog.Activate();
            return;
        }

        using var dialog = new ConfirmDialogForm();
        _confirmDialog = dialog;
        dialog.FormClosed += (_, _) =>
        {
            _confirmDialog = null;
            RefreshControls();
        };
        // 模态弹窗；嵌套消息循环中仍可处理远程对 dialog.ok / dialog.cancel 的 click。
        dialog.ShowDialog(this);
        RefreshControls();
    }
}
