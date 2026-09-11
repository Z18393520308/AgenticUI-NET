using AgenticUI.Remote;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AgenticUI.Samples;

internal static class PairingDialogs
{
    private static Window? _target;
    public static void ShowTarget(Window owner, AgenticPairingService? pairing)
    {
        if (pairing is null) { MessageBox.Show(owner, "网络未启动，请检查 agenticui.json 和状态栏。"); return; }
        _target?.Close();
        var code = pairing.BeginPairing();
        var dialog = Create(owner, "目标端：允许首次配对");
        AddText(dialog, "请在控制端核对以下完整 SHA-256 指纹：\n" + pairing.CertificateFingerprint +
            "\n一次性配对码：" + code + "\n有效 3 分钟，最多 5 次尝试。关闭此窗口即取消配对。");
        var revoke = new Button { Content = "撤销所有已配对控制端", Margin = new Thickness(0, 8, 0, 0) };
        revoke.Click += (_, _) =>
        {
            if (MessageBox.Show(dialog, "确认撤销全部配对并断开网络会话？", "撤销授权", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            pairing.RevokeAllClients(); dialog.Close();
        };
        ((StackPanel)dialog.Content).Children.Add(revoke);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
        timer.Tick += (_, _) => dialog.Close();
        dialog.Closed += (_, _) => { timer.Stop(); pairing.CancelPairing(); _target = null; };
        _target = dialog; timer.Start(); dialog.Show();
    }
    public static string? Confirm(Window owner, AgenticPairingPrompt prompt)
    {
        var dialog = Create(owner, "首次配对：请核验目标身份");
        AddText(dialog, prompt.Endpoint + "\n实际 TLS 证书 SHA-256：\n" + prompt.CertificateFingerprint +
            "\n请与目标软件本地显示逐字核对。UDP 名称和地址不能证明身份。");
        var panel = (StackPanel)dialog.Content;
        var confirmed = new CheckBox { Content = "我已在目标软件上核对完整指纹一致", Margin = new Thickness(0, 8, 0, 8) };
        var code = new TextBox { ToolTip = "输入目标软件给出的一次性配对码" };
        var ok = new Button { Content = "确认配对", IsEnabled = false, IsDefault = true, Margin = new Thickness(0, 8, 0, 8) };
        var cancel = new Button { Content = "取消", IsCancel = true };
        confirmed.Checked += (_, _) => ok.IsEnabled = true;
        confirmed.Unchecked += (_, _) => ok.IsEnabled = false;
        ok.Click += (_, _) => dialog.DialogResult = true;
        panel.Children.Add(confirmed); panel.Children.Add(code); panel.Children.Add(ok); panel.Children.Add(cancel);
        return dialog.ShowDialog() == true ? code.Text : null;
    }
    private static Window Create(Window owner, string title) => new()
    {
        Owner = owner, Title = title, Width = 620, SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
        Content = new StackPanel { Margin = new Thickness(20) }
    };
    private static void AddText(Window dialog, string text) => ((StackPanel)dialog.Content).Children.Add(
        new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140 });
}
