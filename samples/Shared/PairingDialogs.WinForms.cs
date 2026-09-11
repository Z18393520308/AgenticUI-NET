using AgenticUI.Remote;
using System.Windows.Forms;
using System.Drawing;

namespace AgenticUI.Samples;

internal static class PairingDialogs
{
    private static Form? _target;
    public static void ShowTarget(Form owner, AgenticPairingService? pairing)
    {
        if (pairing is null) { MessageBox.Show(owner, "网络未启动，请检查 agenticui.json 和状态栏。"); return; }
        _target?.Close();
        var code = pairing.BeginPairing();
        var dialog = Create("目标端：允许首次配对");
        AddText(dialog, "请在控制端核对以下完整 SHA-256 指纹：\r\n" + pairing.CertificateFingerprint +
            "\r\n一次性配对码：" + code + "\r\n有效 3 分钟，最多 5 次尝试。关闭此窗口即取消配对。");
        var revoke = new Button { Text = "撤销所有已配对控制端", AutoSize = true };
        revoke.Click += (_, _) =>
        {
            if (MessageBox.Show(dialog, "确认撤销全部配对并断开网络会话？", "撤销授权", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            pairing.RevokeAllClients(); dialog.Close();
        };
        ((FlowLayoutPanel)dialog.Controls[0]).Controls.Add(revoke);
        var timer = new System.Windows.Forms.Timer { Interval = 180000 };
        timer.Tick += (_, _) => dialog.Close();
        dialog.FormClosed += (_, _) => { timer.Dispose(); pairing.CancelPairing(); _target = null; };
        _target = dialog; timer.Start(); dialog.Show(owner);
    }

    public static string? Confirm(Form owner, AgenticPairingPrompt prompt)
    {
        using var dialog = Create("首次配对：请核验目标身份");
        AddText(dialog, prompt.Endpoint + "\r\n实际 TLS 证书 SHA-256：\r\n" + prompt.CertificateFingerprint +
            "\r\n请与目标软件本地显示逐字核对。UDP 名称和地址不能证明身份。");
        var panel = (FlowLayoutPanel)dialog.Controls[0];
        var confirmed = new CheckBox { Text = "我已在目标软件上核对完整指纹一致", AutoSize = true };
        var code = new TextBox { Width = 420, PlaceholderText = "输入目标软件给出的一次性配对码" };
        var ok = new Button { Text = "确认配对", Enabled = false, DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        confirmed.CheckedChanged += (_, _) => ok.Enabled = confirmed.Checked;
        panel.Controls.AddRange([confirmed, code, ok, cancel]);
        dialog.AcceptButton = ok; dialog.CancelButton = cancel;
        return dialog.ShowDialog(owner) == DialogResult.OK ? code.Text : null;
    }

    private static Form Create(string title)
    {
        var dialog = new Form { Text = title, ClientSize = new Size(600, 330), StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog, AutoScaleMode = AutoScaleMode.Dpi };
        dialog.Controls.Add(new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), FlowDirection = FlowDirection.TopDown, WrapContents = false });
        return dialog;
    }
    private static void AddText(Form dialog, string text) => ((FlowLayoutPanel)dialog.Controls[0]).Controls.Add(
        new TextBox { Text = text, Multiline = true, ReadOnly = true, Width = 555, Height = 160, ScrollBars = ScrollBars.Vertical });
}
