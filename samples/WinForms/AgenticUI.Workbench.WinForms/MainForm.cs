using AgenticUI.Remote;
using AgenticUI.WinForms;

namespace AgenticUI.Workbench.WinForms;

public partial class MainForm : Form
{
    private const string PipeName = "AgenticUI.NET";

    private readonly AgenticNamedPipeServer _server;
    private readonly AgenticLogRecorder _recorder;
    private readonly string _pipeStatusText;
    private readonly string _tokenStatusText;
    private System.Windows.Forms.Timer? _statusResetTimer;

    private Label titleLabel = null!;
    private AgenticComboBox themeCombo = null!;
    private AgenticTextBox usernameBox = null!;
    private AgenticTextBox passwordBox = null!;
    private AgenticComboBox roleCombo = null!;
    private AgenticButton loginButton = null!;
    private AgenticButton openDialogButton = null!;
    private Button nativeBoundButton = null!;
    private ConfirmDialogForm? _confirmDialog;

    public MainForm()
    {
        InitializeComponent();

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AgenticUI.NET",
            "workbench-events.jsonl");
        _recorder = new AgenticLogRecorder(logPath);
        _server = new AgenticNamedPipeServer(PipeName);
        _server.Start();

        BuildDemoLayout();
        _pipeStatusText = $"管道 {PipeName}";
        _tokenStatusText = $"令牌 {_server.AuthenticationToken}";
        pipeStatusLabel.Text = _pipeStatusText;
        tokenStatusLabel.Text = _tokenStatusText;

        themeCombo.SelectedIndex = 0;
        FormClosed += OnFormClosed;
    }

    private void PipeStatusLabel_Click(object? sender, EventArgs e) =>
        CopyStatusValue(PipeName, "已复制管道名");

    private void TokenStatusLabel_Click(object? sender, EventArgs e) =>
        CopyStatusValue(_server.AuthenticationToken, "已复制令牌");

    private void CopyStatusValue(string value, string hint)
    {
        Clipboard.SetText(value);
        statusHintLabel.Text = $"  {hint}";
        _statusResetTimer?.Stop();
        _statusResetTimer ??= new System.Windows.Forms.Timer { Interval = 1600 };
        _statusResetTimer.Tick -= StatusResetTimer_Tick;
        _statusResetTimer.Tick += StatusResetTimer_Tick;
        _statusResetTimer.Start();
    }

    private void StatusResetTimer_Tick(object? sender, EventArgs e)
    {
        _statusResetTimer?.Stop();
        if (!IsDisposed)
        {
            statusHintLabel.Text = "";
        }
    }

    private void BuildDemoLayout()
    {
        demoPanel.Controls.Clear();
        demoPanel.Padding = new Padding(16, 16, 16, 8);
        demoPanel.AutoScroll = false;

        const int margin = 20;
        const int colGap = 24;
        const int colWidth = 440;
        var leftCol = margin;
        var rightCol = margin + colWidth + colGap;

        titleLabel = new Label
        {
            Text = "AI 原生控件演示",
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(margin, 8)
        };
        demoPanel.Controls.Add(titleLabel);

        demoPanel.Controls.Add(new Label
        {
            Text = "界面主题",
            AutoSize = true,
            Location = new Point(margin, 56)
        });
        themeCombo = new AgenticComboBox
        {
            AgenticId = "ui.theme",
            AgenticDisplayName = "界面主题",
            Hint = "切换原生外观或现代主题",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(margin, 78),
            Width = 280
        };
        themeCombo.Items.AddRange(new object[] { "原生外观", "现代主题" });
        themeCombo.SelectedIndexChanged += ThemeCombo_SelectedIndexChanged;
        demoPanel.Controls.Add(themeCombo);

        var rootTabs = new AgenticTabControl
        {
            Name = "demoRootTabs",
            AgenticId = "ui.rootTabs",
            AgenticDisplayName = "主界面标签页",
            Hint = "切换登录、高优先级、中优先级演示页",
            Location = new Point(margin, 118),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Size = new Size(
                Math.Max(200, demoPanel.ClientSize.Width - margin * 2),
                Math.Max(200, demoPanel.ClientSize.Height - 130))
        };
        demoPanel.Resize += (_, _) =>
        {
            rootTabs.Size = new Size(
                Math.Max(200, demoPanel.ClientSize.Width - margin * 2),
                Math.Max(200, demoPanel.ClientSize.Height - 130));
        };

        var (loginTab, loginHost) = CreateScrollTab("登录与操作");
        var (highTab, highHost) = CreateScrollTab("高优先级");
        var (mediumTab, mediumHost) = CreateScrollTab("中优先级");
        BuildLoginTab(loginHost, leftCol, rightCol, colWidth);
        BuildHighPriorityTab(highHost, leftCol, rightCol, colWidth, colGap);
        BuildMediumPriorityTab(mediumHost, leftCol, rightCol, colWidth, colGap);
        rootTabs.TabPages.AddRange([loginTab, highTab, mediumTab]);
        demoPanel.Controls.Add(rootTabs);
    }

    private static (TabPage page, Panel host) CreateScrollTab(string title)
    {
        var page = new TabPage(title) { Padding = new Padding(0), UseVisualStyleBackColor = true };
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(8)
        };
        page.Controls.Add(host);
        return (page, host);
    }

    private void BuildLoginTab(Panel host, int leftCol, int rightCol, int colWidth)
    {
        var y = 16;
        AddSection(host, "登录", leftCol, y);
        AddSection(host, "操作", rightCol, y);
        y += 30;

        var loginY = y;
        AddCaption(host, "账号", leftCol, loginY);
        usernameBox = new AgenticTextBox
        {
            AgenticId = "login.username",
            AgenticDisplayName = "登录账号",
            InstructionNumber = 1,
            Hint = "请输入账号",
            Location = new Point(leftCol, loginY + 22),
            Width = colWidth
        };
        host.Controls.Add(usernameBox);
        loginY += 56;

        AddCaption(host, "密码（日志默认脱敏）", leftCol, loginY);
        passwordBox = new AgenticTextBox
        {
            AgenticId = "login.password",
            AgenticDisplayName = "登录密码",
            InstructionNumber = 2,
            Hint = "请输入密码",
            IsSensitive = true,
            UseSystemPasswordChar = true,
            Location = new Point(leftCol, loginY + 22),
            Width = colWidth
        };
        host.Controls.Add(passwordBox);
        loginY += 56;

        AddCaption(host, "用户角色", leftCol, loginY);
        roleCombo = new AgenticComboBox
        {
            AgenticId = "login.role",
            AgenticDisplayName = "用户角色",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(leftCol, loginY + 22),
            Width = colWidth
        };
        roleCombo.Items.AddRange(new object[] { "管理员", "操作员", "访客" });
        roleCombo.SelectedIndex = 0;
        host.Controls.Add(roleCombo);
        loginY += 56;

        host.Controls.Add(new AgenticCheckBox
        {
            AgenticId = "login.remember",
            Text = "记住我",
            AutoSize = true,
            Location = new Point(leftCol, loginY)
        });
        host.Controls.Add(new AgenticRadioButton
        {
            AgenticId = "login.mode.safe",
            Text = "安全模式",
            AutoSize = true,
            Checked = true,
            Location = new Point(leftCol + 110, loginY)
        });
        loginY += 40;

        var actionY = y;
        loginButton = new AgenticButton
        {
            AgenticId = "login.submit",
            AgenticDisplayName = "登录",
            InstructionNumber = 3,
            Hint = "请点击登录",
            Text = "登录",
            Location = new Point(rightCol, actionY),
            Size = new Size(colWidth, 40)
        };
        loginButton.Click += (_, _) => MessageBox.Show(this, "登录功能尚未实现。", "AgenticUI.NET");
        host.Controls.Add(loginButton);
        actionY += 52;

        openDialogButton = new AgenticButton
        {
            AgenticId = "dialog.open",
            AgenticDisplayName = "打开确认弹窗",
            InstructionNumber = 4,
            Hint = "打开可远程控制的模态确认弹窗",
            Text = "打开确认弹窗",
            Location = new Point(rightCol, actionY),
            Size = new Size(colWidth, 40)
        };
        openDialogButton.Click += (_, _) => BeginInvoke(ShowConfirmDialog);
        host.Controls.Add(openDialogButton);
        actionY += 52;

        nativeBoundButton = new Button
        {
            Text = "原生按钮 + Binder",
            Location = new Point(rightCol, actionY),
            Size = new Size(colWidth, 38)
        };
        AgenticControlBinder.Attach(nativeBoundButton, new AgenticControlOptions
        {
            Id = "native.boundButton",
            DisplayName = "通过 Binder 接入的原生按钮",
            InstructionNumber = 5,
            Hint = "无需替换原有控件"
        });
        host.Controls.Add(nativeBoundButton);
        actionY += 52;

        host.AutoScrollMinSize = new Size(rightCol + colWidth + 20, Math.Max(loginY, actionY) + 24);
    }

    private static void BuildHighPriorityTab(Panel host, int leftCol, int rightCol, int colWidth, int colGap)
    {
        var y = 16;
        var rowTop = y;
        AddCaption(host, "日期", leftCol, rowTop);
        host.Controls.Add(new AgenticDateTimePicker
        {
            AgenticId = "demo.date",
            AgenticDisplayName = "演示日期",
            Location = new Point(leftCol, rowTop + 22),
            Width = colWidth
        });

        AddCaption(host, "数量", rightCol, rowTop);
        host.Controls.Add(new AgenticNumericUpDown
        {
            AgenticId = "demo.quantity",
            AgenticDisplayName = "演示数量",
            Location = new Point(rightCol, rowTop + 22),
            Width = 160,
            Minimum = 1,
            Maximum = 100,
            Value = 1
        });
        y = rowTop + 60;

        rowTop = y;
        AddCaption(host, "列表", leftCol, rowTop);
        var list = new AgenticListBox
        {
            AgenticId = "demo.list",
            AgenticDisplayName = "演示列表",
            Location = new Point(leftCol, rowTop + 22),
            Size = new Size(colWidth, 100)
        };
        list.Items.AddRange(new object[] { "北京", "上海", "广州" });
        host.Controls.Add(list);

        AddCaption(host, "多选", rightCol, rowTop);
        var checkedList = new AgenticCheckedListBox
        {
            CheckOnClick = true,
            AgenticId = "demo.checkedList",
            AgenticDisplayName = "演示多选",
            Location = new Point(rightCol, rowTop + 22),
            Size = new Size(colWidth, 100)
        };
        checkedList.Items.AddRange(new object[] { "选项A", "选项B", "选项C" });
        host.Controls.Add(checkedList);
        y = rowTop + 140;

        AddCaption(host, "音量", leftCol, y);
        host.Controls.Add(new AgenticTrackBar
        {
            AgenticId = "demo.volume",
            AgenticDisplayName = "演示音量",
            Location = new Point(leftCol, y + 22),
            Width = colWidth * 2 + colGap,
            Minimum = 0,
            Maximum = 100,
            Value = 40,
            TickFrequency = 10
        });
        y += 84;

        var tabs = new AgenticTabControl
        {
            AgenticId = "demo.tabs",
            AgenticDisplayName = "演示标签页",
            Location = new Point(leftCol, y),
            Size = new Size(colWidth * 2 + colGap, 110)
        };
        tabs.TabPages.AddRange([new TabPage("概览"), new TabPage("详情"), new TabPage("设置")]);
        host.Controls.Add(tabs);
        y += 130;

        host.AutoScrollMinSize = new Size(rightCol + colWidth + 20, y + 24);
    }

    private static void BuildMediumPriorityTab(Panel host, int leftCol, int rightCol, int colWidth, int colGap)
    {
        var y = 16;
        host.Controls.Add(new AgenticLabel
        {
            AgenticId = "demo.statusText",
            AgenticDisplayName = "状态文本",
            Text = "状态：就绪",
            AutoSize = true,
            Location = new Point(leftCol, y)
        });
        y += 28;

        host.Controls.Add(new AgenticProgressBar
        {
            AgenticId = "demo.progress",
            AgenticDisplayName = "演示进度",
            Location = new Point(leftCol, y),
            Width = colWidth * 2 + colGap,
            Value = 35,
            Maximum = 100
        });
        y += 36;

        var listView = new AgenticListView
        {
            AgenticId = "demo.listView",
            AgenticDisplayName = "演示列表视图",
            Location = new Point(leftCol, y),
            Size = new Size(colWidth * 2 + colGap, 110),
            View = View.Details,
            FullRowSelect = true
        };
        listView.Columns.Add("名称", 200);
        listView.Columns.Add("城市", 200);
        listView.Items.Add(new ListViewItem(["Alice", "北京"]));
        listView.Items.Add(new ListViewItem(["Bob", "上海"]));
        host.Controls.Add(listView);
        y += 126;

        var rowTop = y;
        var tree = new AgenticTreeView
        {
            AgenticId = "demo.tree",
            AgenticDisplayName = "演示树",
            Location = new Point(leftCol, rowTop),
            Size = new Size(colWidth, 160)
        };
        var company = tree.Nodes.Add("公司");
        company.Nodes.Add("研发");
        company.Nodes.Add("销售");
        company.Expand();
        host.Controls.Add(tree);

        var grid = new AgenticDataGridView
        {
            AgenticId = "demo.grid",
            AgenticDisplayName = "演示表格",
            Location = new Point(rightCol, rowTop),
            Size = new Size(colWidth, 160),
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        grid.Columns.Add("Name", "姓名");
        grid.Columns.Add("Role", "角色");
        grid.Rows.Add("Alice", "管理员");
        grid.Rows.Add("Bob", "操作员");
        host.Controls.Add(grid);
        y = rowTop + 176;

        var menu = new AgenticMenuStrip
        {
            AgenticId = "demo.menu",
            AgenticDisplayName = "演示菜单",
            Location = new Point(leftCol, y),
            Dock = DockStyle.None,
            GripStyle = ToolStripGripStyle.Hidden
        };
        var file = new ToolStripMenuItem("文件");
        file.DropDownItems.Add("打开");
        file.DropDownItems.Add("退出");
        menu.Items.Add(file);
        host.Controls.Add(menu);
        y += 36;

        var toolbar = new AgenticToolStrip
        {
            AgenticId = "demo.toolbar",
            AgenticDisplayName = "演示工具栏",
            Location = new Point(leftCol, y),
            Dock = DockStyle.None,
            GripStyle = ToolStripGripStyle.Hidden
        };
        toolbar.Items.Add("刷新");
        toolbar.Items.Add("导出");
        host.Controls.Add(toolbar);
        y += 48;

        host.AutoScrollMinSize = new Size(rightCol + colWidth + 20, y + 24);
    }

    private static void AddSection(Control host, string title, int x, int y) =>
        host.Controls.Add(new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(x, y)
        });

    private static void AddCaption(Control host, string text, int x, int y) =>
        host.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y)
        });

    private void ThemeCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
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

    private void ShowConfirmDialog()
    {
        if (_confirmDialog is not null && !_confirmDialog.IsDisposed)
        {
            _confirmDialog.Activate();
            return;
        }

        using var dialog = new ConfirmDialogForm();
        _confirmDialog = dialog;
        dialog.FormClosed += (_, _) => _confirmDialog = null;
        dialog.ShowDialog(this);
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs args)
    {
        _statusResetTimer?.Stop();
        _statusResetTimer?.Dispose();
        _recorder.Dispose();
        _server.Dispose();
    }
}
