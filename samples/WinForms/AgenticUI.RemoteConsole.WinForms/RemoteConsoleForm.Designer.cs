namespace AgenticUI.RemoteConsole.WinForms;

partial class RemoteConsoleForm
{
    private System.ComponentModel.IContainer components = null!;

    private TableLayoutPanel rootLayout = null!;
    private FlowLayoutPanel connectionPanel = null!;
    private Label pipeNameLabel = null!;
    private TextBox pipeNameBox = null!;
    private Label tokenLabel = null!;
    private TextBox tokenBox = null!;
    private Button connectButton = null!;
    private Label statusLabel = null!;
    private SplitContainer mainSplit = null!;
    private ListView controlsList = null!;
    private ColumnHeader columnId = null!;
    private ColumnHeader columnKind = null!;
    private ColumnHeader columnName = null!;
    private ColumnHeader columnActions = null!;
    private ListBox eventsList = null!;
    private FlowLayoutPanel actionsPanel = null!;
    private Button refreshButton = null!;
    private Button highlightButton = null!;
    private Button clearHighlightButton = null!;
    private Button clickButton = null!;
    private Button selectNextButton = null!;
    private Button focusButton = null!;
    private Label inputLabel = null!;
    private TextBox inputTextBox = null!;
    private Button readButton = null!;
    private Label readResultLabel = null!;
    private Button setTextButton = null!;

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
        rootLayout = new TableLayoutPanel();
        connectionPanel = new FlowLayoutPanel();
        pipeNameLabel = new Label();
        pipeNameBox = new TextBox();
        tokenLabel = new Label();
        tokenBox = new TextBox();
        connectButton = new Button();
        statusLabel = new Label();
        mainSplit = new SplitContainer();
        controlsList = new ListView();
        columnId = new ColumnHeader();
        columnKind = new ColumnHeader();
        columnName = new ColumnHeader();
        columnActions = new ColumnHeader();
        eventsList = new ListBox();
        actionsPanel = new FlowLayoutPanel();
        refreshButton = new Button();
        highlightButton = new Button();
        clearHighlightButton = new Button();
        clickButton = new Button();
        selectNextButton = new Button();
        focusButton = new Button();
        inputLabel = new Label();
        inputTextBox = new TextBox();
        readButton = new Button();
        readResultLabel = new Label();
        setTextButton = new Button();
        rootLayout.SuspendLayout();
        connectionPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)mainSplit).BeginInit();
        mainSplit.Panel1.SuspendLayout();
        mainSplit.Panel2.SuspendLayout();
        mainSplit.SuspendLayout();
        actionsPanel.SuspendLayout();
        SuspendLayout();
        //
        // rootLayout
        //
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(connectionPanel, 0, 0);
        rootLayout.Controls.Add(mainSplit, 0, 1);
        rootLayout.Controls.Add(actionsPanel, 0, 2);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Location = new Point(0, 0);
        rootLayout.Name = "rootLayout";
        rootLayout.Padding = new Padding(12);
        rootLayout.RowCount = 3;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.Size = new Size(1100, 720);
        rootLayout.TabIndex = 0;
        //
        // connectionPanel
        //
        connectionPanel.AutoSize = true;
        connectionPanel.Controls.Add(pipeNameLabel);
        connectionPanel.Controls.Add(pipeNameBox);
        connectionPanel.Controls.Add(tokenLabel);
        connectionPanel.Controls.Add(tokenBox);
        connectionPanel.Controls.Add(connectButton);
        connectionPanel.Controls.Add(statusLabel);
        connectionPanel.Dock = DockStyle.Fill;
        connectionPanel.Location = new Point(15, 15);
        connectionPanel.Margin = new Padding(3, 3, 3, 10);
        connectionPanel.Name = "connectionPanel";
        connectionPanel.Size = new Size(1070, 35);
        connectionPanel.TabIndex = 0;
        connectionPanel.WrapContents = true;
        //
        // pipeNameLabel
        //
        pipeNameLabel.Anchor = AnchorStyles.Left;
        pipeNameLabel.AutoSize = true;
        pipeNameLabel.Location = new Point(3, 8);
        pipeNameLabel.Margin = new Padding(3, 8, 3, 3);
        pipeNameLabel.Name = "pipeNameLabel";
        pipeNameLabel.Size = new Size(32, 17);
        pipeNameLabel.TabIndex = 0;
        pipeNameLabel.Text = "管道";
        //
        // pipeNameBox
        //
        pipeNameBox.Location = new Point(41, 3);
        pipeNameBox.Name = "pipeNameBox";
        pipeNameBox.Size = new Size(180, 23);
        pipeNameBox.TabIndex = 1;
        pipeNameBox.Text = "AgenticUI.NET";
        //
        // tokenLabel
        //
        tokenLabel.Anchor = AnchorStyles.Left;
        tokenLabel.AutoSize = true;
        tokenLabel.Location = new Point(241, 8);
        tokenLabel.Margin = new Padding(14, 8, 3, 3);
        tokenLabel.Name = "tokenLabel";
        tokenLabel.Size = new Size(32, 17);
        tokenLabel.TabIndex = 2;
        tokenLabel.Text = "令牌";
        //
        // tokenBox
        //
        tokenBox.Location = new Point(279, 3);
        tokenBox.Name = "tokenBox";
        tokenBox.Size = new Size(390, 23);
        tokenBox.TabIndex = 3;
        //
        // connectButton
        //
        connectButton.AutoSize = true;
        connectButton.Location = new Point(675, 3);
        connectButton.Name = "connectButton";
        connectButton.Size = new Size(52, 27);
        connectButton.TabIndex = 4;
        connectButton.Text = "连接";
        connectButton.UseVisualStyleBackColor = true;
        connectButton.Click += Connect_Click;
        //
        // statusLabel
        //
        statusLabel.Anchor = AnchorStyles.Left;
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(733, 8);
        statusLabel.Margin = new Padding(3, 8, 3, 3);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(44, 17);
        statusLabel.TabIndex = 5;
        statusLabel.Text = "未连接";
        //
        // mainSplit
        //
        mainSplit.Dock = DockStyle.Fill;
        mainSplit.Location = new Point(15, 63);
        mainSplit.Name = "mainSplit";
        //
        // mainSplit.Panel1
        //
        mainSplit.Panel1.Controls.Add(controlsList);
        //
        // mainSplit.Panel2
        //
        mainSplit.Panel2.Controls.Add(eventsList);
        mainSplit.Size = new Size(1070, 568);
        mainSplit.SplitterDistance = 700;
        mainSplit.TabIndex = 1;
        //
        // controlsList
        //
        controlsList.Columns.AddRange(new ColumnHeader[] { columnId, columnKind, columnName, columnActions });
        controlsList.Dock = DockStyle.Fill;
        controlsList.FullRowSelect = true;
        controlsList.Location = new Point(0, 0);
        controlsList.MultiSelect = false;
        controlsList.Name = "controlsList";
        controlsList.Size = new Size(700, 568);
        controlsList.TabIndex = 0;
        controlsList.UseCompatibleStateImageBehavior = false;
        controlsList.View = View.Details;
        controlsList.SelectedIndexChanged += ControlsList_SelectedIndexChanged;
        //
        // columnId
        //
        columnId.Text = "ID";
        columnId.Width = 260;
        //
        // columnKind
        //
        columnKind.Text = "类型";
        columnKind.Width = 110;
        //
        // columnName
        //
        columnName.Text = "名称";
        columnName.Width = 180;
        //
        // columnActions
        //
        columnActions.Text = "可执行动作";
        columnActions.Width = 360;
        //
        // eventsList
        //
        eventsList.Dock = DockStyle.Fill;
        eventsList.FormattingEnabled = true;
        eventsList.ItemHeight = 17;
        eventsList.Location = new Point(0, 0);
        eventsList.Name = "eventsList";
        eventsList.Size = new Size(366, 568);
        eventsList.TabIndex = 0;
        //
        // actionsPanel
        //
        actionsPanel.AutoSize = true;
        actionsPanel.Controls.Add(refreshButton);
        actionsPanel.Controls.Add(highlightButton);
        actionsPanel.Controls.Add(clearHighlightButton);
        actionsPanel.Controls.Add(clickButton);
        actionsPanel.Controls.Add(selectNextButton);
        actionsPanel.Controls.Add(focusButton);
        actionsPanel.Controls.Add(inputLabel);
        actionsPanel.Controls.Add(inputTextBox);
        actionsPanel.Controls.Add(readButton);
        actionsPanel.Controls.Add(readResultLabel);
        actionsPanel.Controls.Add(setTextButton);
        actionsPanel.Dock = DockStyle.Fill;
        actionsPanel.Location = new Point(15, 640);
        actionsPanel.Name = "actionsPanel";
        actionsPanel.Padding = new Padding(0, 8, 0, 0);
        actionsPanel.Size = new Size(1070, 65);
        actionsPanel.TabIndex = 2;
        actionsPanel.WrapContents = true;
        //
        // refreshButton
        //
        refreshButton.AutoSize = true;
        refreshButton.Location = new Point(4, 12);
        refreshButton.Margin = new Padding(4);
        refreshButton.Name = "refreshButton";
        refreshButton.Size = new Size(76, 27);
        refreshButton.TabIndex = 0;
        refreshButton.Text = "刷新控件";
        refreshButton.UseVisualStyleBackColor = true;
        refreshButton.Click += Refresh_Click;
        //
        // highlightButton
        //
        highlightButton.AutoSize = true;
        highlightButton.Location = new Point(88, 12);
        highlightButton.Margin = new Padding(4);
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
        clearHighlightButton.Location = new Point(148, 12);
        clearHighlightButton.Margin = new Padding(4);
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
        clickButton.Location = new Point(232, 12);
        clickButton.Margin = new Padding(4);
        clickButton.Name = "clickButton";
        clickButton.Size = new Size(76, 27);
        clickButton.TabIndex = 3;
        clickButton.Text = "点击/打开";
        clickButton.UseVisualStyleBackColor = true;
        clickButton.Click += Click_Click;
        //
        // selectNextButton
        //
        selectNextButton.AutoSize = true;
        selectNextButton.Location = new Point(316, 12);
        selectNextButton.Margin = new Padding(4);
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
        focusButton.Location = new Point(412, 12);
        focusButton.Margin = new Padding(4);
        focusButton.Name = "focusButton";
        focusButton.Size = new Size(52, 27);
        focusButton.TabIndex = 5;
        focusButton.Text = "聚焦";
        focusButton.UseVisualStyleBackColor = true;
        focusButton.Click += Focus_Click;
        //
        // inputLabel
        //
        inputLabel.Anchor = AnchorStyles.Left;
        inputLabel.AutoSize = true;
        inputLabel.Location = new Point(482, 17);
        inputLabel.Margin = new Padding(14, 8, 3, 3);
        inputLabel.Name = "inputLabel";
        inputLabel.Size = new Size(56, 17);
        inputLabel.TabIndex = 6;
        inputLabel.Text = "输入文本";
        //
        // inputTextBox
        //
        inputTextBox.Location = new Point(544, 12);
        inputTextBox.Margin = new Padding(3, 4, 3, 3);
        inputTextBox.Name = "inputTextBox";
        inputTextBox.Size = new Size(220, 23);
        inputTextBox.TabIndex = 7;
        inputTextBox.KeyDown += InputText_KeyDown;
        //
        // readButton
        //
        readButton.AutoSize = true;
        readButton.Location = new Point(770, 12);
        readButton.Margin = new Padding(4);
        readButton.Name = "readButton";
        readButton.Size = new Size(52, 27);
        readButton.TabIndex = 8;
        readButton.Text = "读取";
        readButton.UseVisualStyleBackColor = true;
        readButton.Click += Read_Click;
        //
        // readResultLabel
        //
        readResultLabel.Anchor = AnchorStyles.Left;
        readResultLabel.AutoSize = true;
        readResultLabel.ForeColor = Color.DimGray;
        readResultLabel.Location = new Point(830, 17);
        readResultLabel.Margin = new Padding(8, 10, 3, 3);
        readResultLabel.Name = "readResultLabel";
        readResultLabel.Size = new Size(80, 17);
        readResultLabel.TabIndex = 9;
        readResultLabel.Text = "读取结果：—";
        //
        // setTextButton
        //
        setTextButton.AutoSize = true;
        setTextButton.Location = new Point(917, 12);
        setTextButton.Margin = new Padding(4);
        setTextButton.Name = "setTextButton";
        setTextButton.Size = new Size(76, 27);
        setTextButton.TabIndex = 10;
        setTextButton.Text = "设置文本";
        setTextButton.UseVisualStyleBackColor = true;
        setTextButton.Click += SetText_Click;
        //
        // RemoteConsoleForm
        //
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1100, 720);
        Controls.Add(rootLayout);
        MinimumSize = new Size(850, 520);
        Name = "RemoteConsoleForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "AgenticUI.NET Remote Console";
        Shown += RemoteConsoleForm_Shown;
        FormClosed += RemoteConsoleForm_FormClosed;
        rootLayout.ResumeLayout(false);
        rootLayout.PerformLayout();
        connectionPanel.ResumeLayout(false);
        connectionPanel.PerformLayout();
        mainSplit.Panel1.ResumeLayout(false);
        mainSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)mainSplit).EndInit();
        mainSplit.ResumeLayout(false);
        actionsPanel.ResumeLayout(false);
        actionsPanel.PerformLayout();
        ResumeLayout(false);
    }
}
