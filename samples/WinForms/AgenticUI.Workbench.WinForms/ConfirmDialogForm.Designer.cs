namespace AgenticUI.Workbench.WinForms;

partial class ConfirmDialogForm
{
    private System.ComponentModel.IContainer components = null!;
    private Label messageLabel = null!;
    private AgenticUI.WinForms.AgenticButton okButton = null!;
    private AgenticUI.WinForms.AgenticButton cancelButton = null!;

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
        messageLabel = new Label();
        okButton = new AgenticUI.WinForms.AgenticButton();
        cancelButton = new AgenticUI.WinForms.AgenticButton();
        SuspendLayout();
        //
        // messageLabel
        //
        messageLabel.Location = new Point(24, 24);
        messageLabel.Name = "messageLabel";
        messageLabel.Size = new Size(360, 72);
        messageLabel.TabIndex = 0;
        messageLabel.Text = "这是正常的模态确认弹窗。\r\n弹窗打开后请在远程端刷新列表，即可 click「确定」或「取消」。";
        //
        // okButton
        //
        okButton.AgenticDisplayName = "确认弹窗-确定";
        okButton.AgenticId = "dialog.ok";
        okButton.Hint = "确认并关闭弹窗";
        okButton.InstructionNumber = 10;
        okButton.Location = new Point(168, 120);
        okButton.Name = "okButton";
        okButton.Size = new Size(100, 36);
        okButton.TabIndex = 1;
        okButton.Text = "确定";
        okButton.UseVisualStyleBackColor = true;
        okButton.Click += OkButton_Click;
        //
        // cancelButton
        //
        cancelButton.AgenticDisplayName = "确认弹窗-取消";
        cancelButton.AgenticId = "dialog.cancel";
        cancelButton.Hint = "取消并关闭弹窗";
        cancelButton.InstructionNumber = 11;
        cancelButton.Location = new Point(284, 120);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new Size(100, 36);
        cancelButton.TabIndex = 2;
        cancelButton.Text = "取消";
        cancelButton.UseVisualStyleBackColor = true;
        cancelButton.Click += CancelButton_Click;
        //
        // ConfirmDialogForm
        //
        AcceptButton = okButton;
        CancelButton = cancelButton;
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(408, 180);
        Controls.Add(cancelButton);
        Controls.Add(okButton);
        Controls.Add(messageLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ConfirmDialogForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "确认操作";
        ResumeLayout(false);
    }
}
