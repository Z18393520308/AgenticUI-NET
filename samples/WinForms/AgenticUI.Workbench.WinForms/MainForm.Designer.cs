namespace AgenticUI.Workbench.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    private SplitContainer mainSplit = null!;
    private Panel demoPanel = null!;
    private Label titleLabel = null!;
    private Label usernameLabel = null!;
    private AgenticUI.WinForms.AgenticTextBox usernameBox = null!;
    private Label passwordLabel = null!;
    private AgenticUI.WinForms.AgenticTextBox passwordBox = null!;
    private Label roleLabel = null!;
    private AgenticUI.WinForms.AgenticComboBox roleCombo = null!;
    private AgenticUI.WinForms.AgenticCheckBox rememberCheck = null!;
    private AgenticUI.WinForms.AgenticRadioButton safeModeRadio = null!;
    private AgenticUI.WinForms.AgenticButton loginButton = null!;
    private AgenticUI.WinForms.AgenticButton openDialogButton = null!;
    private Button nativeBoundButton = null!;
    private Label themeLabel = null!;
    private AgenticUI.WinForms.AgenticComboBox themeCombo = null!;
    private TabControl inspectorTabs = null!;
    private TabPage controlsTab = null!;
    private TabPage eventsTab = null!;
    private Label tokenLabel = null!;
    private TextBox tokenBox = null!;
    private Label pipeNameLabel = null!;
    private TextBox pipeNameBox = null!;
    private ListBox controlsList = null!;
    private Label inputLabel = null!;
    private TextBox inputTextBox = null!;
    private Button setTextButton = null!;
    private Button getTextButton = null!;
    private FlowLayoutPanel commandButtons = null!;
    private Button refreshButton = null!;
    private Button highlightButton = null!;
    private Button clearHighlightButton = null!;
    private Button clickButton = null!;
    private Button selectNextButton = null!;
    private Button focusButton = null!;
    private ListBox eventsList = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        mainSplit = new SplitContainer();
        demoPanel = new Panel();
        nativeBoundButton = new Button();
        openDialogButton = new AgenticUI.WinForms.AgenticButton();
        loginButton = new AgenticUI.WinForms.AgenticButton();
        safeModeRadio = new AgenticUI.WinForms.AgenticRadioButton();
        rememberCheck = new AgenticUI.WinForms.AgenticCheckBox();
        roleCombo = new AgenticUI.WinForms.AgenticComboBox();
        roleLabel = new Label();
        passwordBox = new AgenticUI.WinForms.AgenticTextBox();
        passwordLabel = new Label();
        usernameBox = new AgenticUI.WinForms.AgenticTextBox();
        usernameLabel = new Label();
        themeCombo = new AgenticUI.WinForms.AgenticComboBox();
        themeLabel = new Label();
        titleLabel = new Label();
        inspectorTabs = new TabControl();
        controlsTab = new TabPage();
        commandButtons = new FlowLayoutPanel();
        refreshButton = new Button();
        highlightButton = new Button();
        clearHighlightButton = new Button();
        clickButton = new Button();
        selectNextButton = new Button();
        focusButton = new Button();
        setTextButton = new Button();
        getTextButton = new Button();
        inputTextBox = new TextBox();
        inputLabel = new Label();
        controlsList = new ListBox();
        tokenBox = new TextBox();
        tokenLabel = new Label();
        pipeNameBox = new TextBox();
        pipeNameLabel = new Label();
        eventsTab = new TabPage();
        eventsList = new ListBox();
        ((System.ComponentModel.ISupportInitialize)mainSplit).BeginInit();
        mainSplit.Panel1.SuspendLayout();
        mainSplit.Panel2.SuspendLayout();
        mainSplit.SuspendLayout();
        demoPanel.SuspendLayout();
        inspectorTabs.SuspendLayout();
        controlsTab.SuspendLayout();
        commandButtons.SuspendLayout();
        eventsTab.SuspendLayout();
        SuspendLayout();
        // 
        // mainSplit
        // 
        mainSplit.Dock = DockStyle.Fill;
        mainSplit.FixedPanel = FixedPanel.Panel1;
        mainSplit.Location = new Point(0, 0);
        mainSplit.Name = "mainSplit";
        // 
        // mainSplit.Panel1
        // 
        mainSplit.Panel1.Controls.Add(demoPanel);
        // 
        // mainSplit.Panel2
        // 
        mainSplit.Panel2.Controls.Add(inspectorTabs);
        mainSplit.Panel2.Padding = new Padding(16);
        mainSplit.Size = new Size(1180, 760);
        mainSplit.SplitterDistance = 480;
        mainSplit.TabIndex = 0;
        // 
        // demoPanel
        // 
        demoPanel.AutoScroll = true;
        demoPanel.Controls.Add(nativeBoundButton);
        demoPanel.Controls.Add(openDialogButton);
        demoPanel.Controls.Add(loginButton);
        demoPanel.Controls.Add(safeModeRadio);
        demoPanel.Controls.Add(rememberCheck);
        demoPanel.Controls.Add(roleCombo);
        demoPanel.Controls.Add(roleLabel);
        demoPanel.Controls.Add(passwordBox);
        demoPanel.Controls.Add(passwordLabel);
        demoPanel.Controls.Add(usernameBox);
        demoPanel.Controls.Add(usernameLabel);
        demoPanel.Controls.Add(themeCombo);
        demoPanel.Controls.Add(themeLabel);
        demoPanel.Controls.Add(titleLabel);
        demoPanel.Dock = DockStyle.Fill;
        demoPanel.Location = new Point(0, 0);
        demoPanel.Name = "demoPanel";
        demoPanel.Padding = new Padding(28);
        demoPanel.Size = new Size(480, 760);
        demoPanel.TabIndex = 0;
        // 
        // nativeBoundButton
        // 
        nativeBoundButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        nativeBoundButton.Location = new Point(28, 566);
        nativeBoundButton.Name = "nativeBoundButton";
        nativeBoundButton.Size = new Size(420, 38);
        nativeBoundButton.TabIndex = 10;
        nativeBoundButton.Text = "原生按钮 + Binder";
        nativeBoundButton.UseVisualStyleBackColor = true;
        // 
        // openDialogButton
        // 
        openDialogButton.AgenticDisplayName = "打开确认弹窗";
        openDialogButton.AgenticId = "dialog.open";
        openDialogButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        openDialogButton.Hint = "打开可远程控制的模态确认弹窗";
        openDialogButton.InstructionNumber = 4;
        openDialogButton.IsSensitive = false;
        openDialogButton.Location = new Point(28, 508);
        openDialogButton.Name = "openDialogButton";
        openDialogButton.Size = new Size(420, 42);
        openDialogButton.TabIndex = 9;
        openDialogButton.Text = "打开确认弹窗";
        openDialogButton.UseVisualStyleBackColor = true;
        openDialogButton.Click += openDialogButton_Click;
        // 
        // loginButton
        // 
        loginButton.AgenticDisplayName = "登录";
        loginButton.AgenticId = "login.submit";
        loginButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        loginButton.Hint = "请点击登录";
        loginButton.InstructionNumber = 3;
        loginButton.IsSensitive = false;
        loginButton.Location = new Point(28, 450);
        loginButton.Name = "loginButton";
        loginButton.Size = new Size(420, 42);
        loginButton.TabIndex = 8;
        loginButton.Text = "登录";
        loginButton.UseVisualStyleBackColor = true;
        loginButton.Click += loginButton_Click;
        // 
        // safeModeRadio
        // 
        safeModeRadio.AgenticDisplayName = null;
        safeModeRadio.AgenticId = "login.mode.safe";
        safeModeRadio.AutoSize = true;
        safeModeRadio.Checked = true;
        safeModeRadio.Hint = null;
        safeModeRadio.InstructionNumber = 0;
        safeModeRadio.IsSensitive = false;
        safeModeRadio.Location = new Point(28, 406);
        safeModeRadio.Name = "safeModeRadio";
        safeModeRadio.Size = new Size(74, 21);
        safeModeRadio.TabIndex = 7;
        safeModeRadio.TabStop = true;
        safeModeRadio.Text = "安全模式";
        safeModeRadio.UseVisualStyleBackColor = true;
        // 
        // rememberCheck
        // 
        rememberCheck.AgenticDisplayName = null;
        rememberCheck.AgenticId = "login.remember";
        rememberCheck.AutoSize = true;
        rememberCheck.Hint = null;
        rememberCheck.InstructionNumber = 0;
        rememberCheck.IsSensitive = false;
        rememberCheck.Location = new Point(28, 370);
        rememberCheck.Name = "rememberCheck";
        rememberCheck.Size = new Size(63, 21);
        rememberCheck.TabIndex = 6;
        rememberCheck.Text = "记住我";
        rememberCheck.UseVisualStyleBackColor = true;
        // 
        // roleCombo
        // 
        roleCombo.AgenticDisplayName = "用户角色";
        roleCombo.AgenticId = "login.role";
        roleCombo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        roleCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        roleCombo.FormattingEnabled = true;
        roleCombo.Hint = null;
        roleCombo.InstructionNumber = 0;
        roleCombo.IsSensitive = false;
        roleCombo.Items.AddRange(new object[] { "管理员", "操作员", "访客" });
        roleCombo.Location = new Point(28, 326);
        roleCombo.Name = "roleCombo";
        roleCombo.Size = new Size(420, 25);
        roleCombo.TabIndex = 5;
        // 
        // roleLabel
        // 
        roleLabel.AutoSize = true;
        roleLabel.Location = new Point(28, 302);
        roleLabel.Name = "roleLabel";
        roleLabel.Size = new Size(56, 17);
        roleLabel.TabIndex = 14;
        roleLabel.Text = "用户角色";
        // 
        // passwordBox
        // 
        passwordBox.AgenticDisplayName = "登录密码";
        passwordBox.AgenticId = "login.password";
        passwordBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        passwordBox.Hint = "请输入密码";
        passwordBox.InstructionNumber = 2;
        passwordBox.IsSensitive = true;
        passwordBox.Location = new Point(28, 274);
        passwordBox.Name = "passwordBox";
        passwordBox.Size = new Size(420, 23);
        passwordBox.TabIndex = 4;
        passwordBox.UseSystemPasswordChar = true;
        // 
        // passwordLabel
        // 
        passwordLabel.AutoSize = true;
        passwordLabel.Location = new Point(28, 250);
        passwordLabel.Name = "passwordLabel";
        passwordLabel.Size = new Size(128, 17);
        passwordLabel.TabIndex = 3;
        passwordLabel.Text = "密码（日志默认脱敏）";
        // 
        // usernameBox
        // 
        usernameBox.AgenticDisplayName = "登录账号";
        usernameBox.AgenticId = "login.username";
        usernameBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        usernameBox.Hint = "请输入账号";
        usernameBox.InstructionNumber = 1;
        usernameBox.IsSensitive = false;
        usernameBox.Location = new Point(28, 210);
        usernameBox.Name = "usernameBox";
        usernameBox.Size = new Size(420, 23);
        usernameBox.TabIndex = 2;
        // 
        // usernameLabel
        // 
        usernameLabel.AutoSize = true;
        usernameLabel.Location = new Point(28, 186);
        usernameLabel.Name = "usernameLabel";
        usernameLabel.Size = new Size(32, 17);
        usernameLabel.TabIndex = 1;
        usernameLabel.Text = "账号";
        // 
        // themeCombo
        // 
        themeCombo.AgenticDisplayName = "界面主题";
        themeCombo.AgenticId = "ui.theme";
        themeCombo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        themeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        themeCombo.FormattingEnabled = true;
        themeCombo.Hint = "切换原生外观或现代主题";
        themeCombo.InstructionNumber = 0;
        themeCombo.IsSensitive = false;
        themeCombo.Items.AddRange(new object[] { "原生外观", "现代主题" });
        themeCombo.Location = new Point(28, 102);
        themeCombo.Name = "themeCombo";
        themeCombo.Size = new Size(420, 25);
        themeCombo.TabIndex = 13;
        themeCombo.SelectedIndexChanged += ThemeCombo_SelectedIndexChanged;
        // 
        // themeLabel
        // 
        themeLabel.AutoSize = true;
        themeLabel.Location = new Point(28, 78);
        themeLabel.Name = "themeLabel";
        themeLabel.Size = new Size(56, 17);
        themeLabel.TabIndex = 12;
        themeLabel.Text = "界面主题";
        // 
        // titleLabel
        // 
        titleLabel.AutoSize = true;
        titleLabel.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        titleLabel.Location = new Point(28, 28);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(226, 37);
        titleLabel.TabIndex = 0;
        titleLabel.Text = "AI 原生控件演示";
        // 
        // inspectorTabs
        // 
        inspectorTabs.Controls.Add(controlsTab);
        inspectorTabs.Controls.Add(eventsTab);
        inspectorTabs.Dock = DockStyle.Fill;
        inspectorTabs.Location = new Point(16, 16);
        inspectorTabs.Name = "inspectorTabs";
        inspectorTabs.SelectedIndex = 0;
        inspectorTabs.Size = new Size(664, 728);
        inspectorTabs.TabIndex = 0;
        // 
        // controlsTab
        // 
        controlsTab.Controls.Add(commandButtons);
        controlsTab.Controls.Add(setTextButton);
        controlsTab.Controls.Add(getTextButton);
        controlsTab.Controls.Add(inputTextBox);
        controlsTab.Controls.Add(inputLabel);
        controlsTab.Controls.Add(controlsList);
        controlsTab.Controls.Add(tokenBox);
        controlsTab.Controls.Add(tokenLabel);
        controlsTab.Controls.Add(pipeNameBox);
        controlsTab.Controls.Add(pipeNameLabel);
        controlsTab.Location = new Point(4, 26);
        controlsTab.Name = "controlsTab";
        controlsTab.Padding = new Padding(8);
        controlsTab.Size = new Size(656, 698);
        controlsTab.TabIndex = 0;
        controlsTab.Text = "控件树";
        controlsTab.UseVisualStyleBackColor = true;
        // 
        // commandButtons
        // 
        commandButtons.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        commandButtons.Controls.Add(refreshButton);
        commandButtons.Controls.Add(highlightButton);
        commandButtons.Controls.Add(clearHighlightButton);
        commandButtons.Controls.Add(clickButton);
        commandButtons.Controls.Add(selectNextButton);
        commandButtons.Controls.Add(focusButton);
        commandButtons.Location = new Point(11, 620);
        commandButtons.Name = "commandButtons";
        commandButtons.Size = new Size(629, 64);
        commandButtons.TabIndex = 6;
        // 
        // refreshButton
        // 
        refreshButton.AutoSize = true;
        refreshButton.Location = new Point(3, 3);
        refreshButton.Name = "refreshButton";
        refreshButton.Size = new Size(52, 27);
        refreshButton.TabIndex = 0;
        refreshButton.Text = "刷新";
        refreshButton.UseVisualStyleBackColor = true;
        refreshButton.Click += Refresh_Click;
        // 
        // highlightButton
        // 
        highlightButton.AutoSize = true;
        highlightButton.Location = new Point(61, 3);
        highlightButton.Name = "highlightButton";
        highlightButton.Size = new Size(52, 27);
        highlightButton.TabIndex = 1;
        highlightButton.Text = "高亮";
        highlightButton.UseVisualStyleBackColor = true;
        highlightButton.Click += Highlight_Click;
        // 
        // clearHighlightButton
        // 
        clearHighlightButton.AutoSize = true;
        clearHighlightButton.Location = new Point(119, 3);
        clearHighlightButton.Name = "clearHighlightButton";
        clearHighlightButton.Size = new Size(76, 27);
        clearHighlightButton.TabIndex = 2;
        clearHighlightButton.Text = "取消高亮";
        clearHighlightButton.UseVisualStyleBackColor = true;
        clearHighlightButton.Click += ClearHighlight_Click;
        // 
        // clickButton
        // 
        clickButton.AutoSize = true;
        clickButton.Location = new Point(201, 3);
        clickButton.Name = "clickButton";
        clickButton.Size = new Size(52, 27);
        clickButton.TabIndex = 3;
        clickButton.Text = "点击";
        clickButton.UseVisualStyleBackColor = true;
        clickButton.Click += Click_Click;
        // 
        // selectNextButton
        // 
        selectNextButton.AutoSize = true;
        selectNextButton.Location = new Point(259, 3);
        selectNextButton.Name = "selectNextButton";
        selectNextButton.Size = new Size(88, 27);
        selectNextButton.TabIndex = 4;
        selectNextButton.Text = "选择下一项";
        selectNextButton.UseVisualStyleBackColor = true;
        selectNextButton.Click += SelectNext_Click;
        // 
        // focusButton
        // 
        focusButton.AutoSize = true;
        focusButton.Location = new Point(353, 3);
        focusButton.Name = "focusButton";
        focusButton.Size = new Size(52, 27);
        focusButton.TabIndex = 5;
        focusButton.Text = "聚焦";
        focusButton.UseVisualStyleBackColor = true;
        focusButton.Click += Focus_Click;
        // 
        // setTextButton
        // 
        setTextButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        setTextButton.AutoSize = true;
        setTextButton.Location = new Point(552, 579);
        setTextButton.Name = "setTextButton";
        setTextButton.Size = new Size(88, 27);
        setTextButton.TabIndex = 6;
        setTextButton.Text = "设置文本";
        setTextButton.UseVisualStyleBackColor = true;
        setTextButton.Click += SetText_Click;
        // 
        // getTextButton
        // 
        getTextButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        getTextButton.AutoSize = true;
        getTextButton.Location = new Point(452, 579);
        getTextButton.Name = "getTextButton";
        getTextButton.Size = new Size(88, 27);
        getTextButton.TabIndex = 5;
        getTextButton.Text = "读取";
        getTextButton.UseVisualStyleBackColor = true;
        getTextButton.Click += Read_Click;
        // 
        // inputTextBox
        // 
        inputTextBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        inputTextBox.Location = new Point(86, 581);
        inputTextBox.Name = "inputTextBox";
        inputTextBox.Size = new Size(354, 23);
        inputTextBox.TabIndex = 4;
        inputTextBox.KeyDown += InputText_KeyDown;
        // 
        // inputLabel
        // 
        inputLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        inputLabel.AutoSize = true;
        inputLabel.Location = new Point(11, 584);
        inputLabel.Name = "inputLabel";
        inputLabel.Size = new Size(56, 17);
        inputLabel.TabIndex = 3;
        inputLabel.Text = "输入文本";
        // 
        // controlsList
        // 
        controlsList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        controlsList.DisplayMember = "Id";
        controlsList.FormattingEnabled = true;
        controlsList.ItemHeight = 17;
        controlsList.Location = new Point(11, 84);
        controlsList.Name = "controlsList";
        controlsList.Size = new Size(629, 480);
        controlsList.TabIndex = 4;
        controlsList.SelectedIndexChanged += ControlsList_SelectedIndexChanged;
        // 
        // tokenBox
        // 
        tokenBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        tokenBox.Location = new Point(110, 48);
        tokenBox.Name = "tokenBox";
        tokenBox.ReadOnly = true;
        tokenBox.Size = new Size(530, 23);
        tokenBox.TabIndex = 3;
        // 
        // tokenLabel
        // 
        tokenLabel.AutoSize = true;
        tokenLabel.Location = new Point(11, 51);
        tokenLabel.Name = "tokenLabel";
        tokenLabel.Size = new Size(80, 17);
        tokenLabel.TabIndex = 2;
        tokenLabel.Text = "远程连接令牌";
        // 
        // pipeNameBox
        // 
        pipeNameBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pipeNameBox.Location = new Point(110, 15);
        pipeNameBox.Name = "pipeNameBox";
        pipeNameBox.ReadOnly = true;
        pipeNameBox.Size = new Size(530, 23);
        pipeNameBox.TabIndex = 1;
        // 
        // pipeNameLabel
        // 
        pipeNameLabel.AutoSize = true;
        pipeNameLabel.Location = new Point(11, 18);
        pipeNameLabel.Name = "pipeNameLabel";
        pipeNameLabel.Size = new Size(56, 17);
        pipeNameLabel.TabIndex = 0;
        pipeNameLabel.Text = "管道名称";
        // 
        // eventsTab
        // 
        eventsTab.Controls.Add(eventsList);
        eventsTab.Location = new Point(4, 26);
        eventsTab.Name = "eventsTab";
        eventsTab.Padding = new Padding(8);
        eventsTab.Size = new Size(656, 698);
        eventsTab.TabIndex = 1;
        eventsTab.Text = "事件流";
        eventsTab.UseVisualStyleBackColor = true;
        // 
        // eventsList
        // 
        eventsList.Dock = DockStyle.Fill;
        eventsList.FormattingEnabled = true;
        eventsList.ItemHeight = 17;
        eventsList.Location = new Point(8, 8);
        eventsList.Name = "eventsList";
        eventsList.Size = new Size(640, 682);
        eventsList.TabIndex = 0;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1180, 760);
        Controls.Add(mainSplit);
        MinimumSize = new Size(940, 600);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "AgenticUI.NET Workbench";
        Load += MainForm_Load;
        mainSplit.Panel1.ResumeLayout(false);
        mainSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)mainSplit).EndInit();
        mainSplit.ResumeLayout(false);
        demoPanel.ResumeLayout(false);
        demoPanel.PerformLayout();
        inspectorTabs.ResumeLayout(false);
        controlsTab.ResumeLayout(false);
        controlsTab.PerformLayout();
        commandButtons.ResumeLayout(false);
        commandButtons.PerformLayout();
        eventsTab.ResumeLayout(false);
        ResumeLayout(false);
    }
}
