namespace AgenticUI.Workbench.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private Panel demoPanel = null!;
    private AgenticUI.WinForms.AgenticStatusStrip statusStrip = null!;
    private ToolStripStatusLabel pipeStatusLabel = null!;
    private ToolStripStatusLabel statusSeparator = null!;
    private ToolStripStatusLabel tokenStatusLabel = null!;
    private ToolStripStatusLabel statusHintLabel = null!;

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
        demoPanel = new Panel();
        statusStrip = new AgenticUI.WinForms.AgenticStatusStrip();
        pipeStatusLabel = new ToolStripStatusLabel();
        statusSeparator = new ToolStripStatusLabel();
        tokenStatusLabel = new ToolStripStatusLabel();
        statusHintLabel = new ToolStripStatusLabel();
        SuspendLayout();
        // 
        // demoPanel
        // 
        demoPanel.AutoScroll = true;
        demoPanel.BackColor = SystemColors.Window;
        demoPanel.Dock = DockStyle.Fill;
        demoPanel.Location = new Point(0, 0);
        demoPanel.Name = "demoPanel";
        demoPanel.Padding = new Padding(24);
        demoPanel.TabIndex = 0;
        // 
        // statusStrip
        // 
        statusStrip.AgenticDisplayName = "状态栏";
        statusStrip.AgenticId = "demo.statusBar";
        statusStrip.Items.AddRange(new ToolStripItem[]
        {
            pipeStatusLabel,
            statusSeparator,
            tokenStatusLabel,
            statusHintLabel
        });
        statusStrip.Location = new Point(0, 738);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(1080, 22);
        statusStrip.TabIndex = 1;
        // 
        // pipeStatusLabel
        // 
        pipeStatusLabel.IsLink = true;
        pipeStatusLabel.LinkBehavior = LinkBehavior.HoverUnderline;
        pipeStatusLabel.Name = "pipeStatusLabel";
        pipeStatusLabel.Text = "管道";
        pipeStatusLabel.ToolTipText = "点击复制管道名";
        pipeStatusLabel.Click += PipeStatusLabel_Click;
        // 
        // statusSeparator
        // 
        statusSeparator.Name = "statusSeparator";
        statusSeparator.Text = "  ·  ";
        // 
        // tokenStatusLabel
        // 
        tokenStatusLabel.IsLink = true;
        tokenStatusLabel.LinkBehavior = LinkBehavior.HoverUnderline;
        tokenStatusLabel.Name = "tokenStatusLabel";
        tokenStatusLabel.Text = "令牌";
        tokenStatusLabel.ToolTipText = "点击复制令牌";
        tokenStatusLabel.Click += TokenStatusLabel_Click;
        // 
        // statusHintLabel
        // 
        statusHintLabel.Name = "statusHintLabel";
        statusHintLabel.Spring = true;
        statusHintLabel.Text = "";
        statusHintLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1080, 760);
        Controls.Add(demoPanel);
        Controls.Add(statusStrip);
        MinimumSize = new Size(900, 640);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "AgenticUI.NET Workbench";
        ResumeLayout(false);
        PerformLayout();
    }
}
