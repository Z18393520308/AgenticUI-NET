using AgenticUI.Remote;

namespace AgenticUI.RemoteConsole.WinForms;

public partial class RemoteConsoleForm : Form
{
    private AgenticNamedPipeClient? _client;

    public RemoteConsoleForm()
    {
        InitializeComponent();
    }

    private async void Connect_Click(object? sender, EventArgs e)
    {
        connectButton.Enabled = false;
        try
        {
            _client?.Dispose();
            _client = await AgenticNamedPipeClient.ConnectAsync(
                tokenBox.Text,
                pipeNameBox.Text,
                "AgenticUI Remote Console");
            _client.EventReceived += OnEventReceived;
            _client.ConnectionFaulted += OnConnectionFaulted;
            statusLabel.Text = "已认证连接";
            statusLabel.ForeColor = Color.Green;
            await RefreshControlsCoreAsync();
        }
        catch (Exception exception)
        {
            statusLabel.Text = $"连接失败：{exception.Message}";
            statusLabel.ForeColor = Color.Firebrick;
        }
        finally
        {
            connectButton.Enabled = true;
        }
    }

    private async void Refresh_Click(object? sender, EventArgs e) =>
        await RefreshControlsCoreAsync();

    private async void Highlight_Click(object? sender, EventArgs e) =>
        await ExecuteAsync(AgenticActions.Highlight);

    private async void ClearHighlight_Click(object? sender, EventArgs e) =>
        await ExecuteAsync(AgenticActions.ClearHighlight);

    private async void Click_Click(object? sender, EventArgs e) =>
        await ExecuteAsync(AgenticActions.Click);

    private async void SelectNext_Click(object? sender, EventArgs e) =>
        await SelectNextItemAsync();

    private async void Focus_Click(object? sender, EventArgs e) =>
        await ExecuteAsync(AgenticActions.Focus);

    private async void Read_Click(object? sender, EventArgs e) =>
        await ReadSelectedAsync();

    private async void SetText_Click(object? sender, EventArgs e) =>
        await SetSelectedTextAsync();

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

    private void RemoteConsoleForm_Shown(object? sender, EventArgs e) =>
        EnsureSplitterWidth();

    private void RemoteConsoleForm_FormClosed(object? sender, FormClosedEventArgs e) =>
        _client?.Dispose();

    private async Task RefreshControlsCoreAsync()
    {
        if (_client is null)
        {
            MessageBox.Show(this, "请先连接 Workbench（管道名 + 令牌）。", "AgenticUI.NET");
            return;
        }

        try
        {
            var selectedId = controlsList.SelectedItems.Count > 0
                ? controlsList.SelectedItems[0].Text
                : null;
            var response = await _client.ListControlsAsync();
            if (response.Type == RemoteMessageTypes.Error)
            {
                throw new InvalidOperationException(response.Error ?? "枚举控件失败。");
            }

            if (response.Type != RemoteMessageTypes.Controls)
            {
                throw new InvalidOperationException($"意外响应类型：{response.Type}");
            }

            controlsList.BeginUpdate();
            controlsList.Items.Clear();
            ListViewItem? restoreItem = null;
            foreach (var control in response.Controls ?? Array.Empty<AgenticControlDescriptor>())
            {
                var item = new ListViewItem(control.Id) { Tag = control };
                item.SubItems.Add(control.Kind);
                item.SubItems.Add(control.Name);
                item.SubItems.Add(string.Join(", ", control.Actions));
                controlsList.Items.Add(item);
                if (selectedId is not null &&
                    string.Equals(control.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                {
                    restoreItem = item;
                }
            }

            controlsList.EndUpdate();
            if (restoreItem is not null)
            {
                restoreItem.Selected = true;
                restoreItem.EnsureVisible();
            }
            else if (controlsList.Items.Count > 0)
            {
                controlsList.Items[0].Selected = true;
            }

            SyncInputTextFromSelection();

            var count = controlsList.Items.Count;
            var hasDialogButtons = controlsList.Items.Cast<ListViewItem>().Any(item =>
                string.Equals(item.Text, "dialog.ok", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Text, "dialog.cancel", StringComparison.OrdinalIgnoreCase));
            statusLabel.Text = count == 0
                ? "已连接，但未枚举到控件（确认 Workbench 窗口已完全显示后再点刷新）"
                : hasDialogButtons
                    ? $"已连接，共 {count} 个控件（含弹窗按钮，可 click dialog.ok / dialog.cancel）"
                    : $"已连接，共 {count} 个控件（click dialog.open 打开弹窗后请再刷新）";
            statusLabel.ForeColor = count == 0 ? Color.DarkOrange : Color.Green;
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void SyncInputTextFromSelection()
    {
        if (controlsList.SelectedItems.Count == 0 ||
            controlsList.SelectedItems[0].Tag is not AgenticControlDescriptor descriptor)
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
        if (controlsList.SelectedItems.Count == 0 ||
            controlsList.SelectedItems[0].Tag is not AgenticControlDescriptor descriptor)
        {
            MessageBox.Show(this, "请先选择一个控件。", "AgenticUI.NET");
            return;
        }

        var supportsGetChecked = descriptor.Actions.Contains(AgenticActions.GetChecked, StringComparer.OrdinalIgnoreCase);
        var supportsGetText = descriptor.Actions.Contains(AgenticActions.GetText, StringComparer.OrdinalIgnoreCase);
        var supportsGetValue = descriptor.Actions.Contains(AgenticActions.GetValue, StringComparer.OrdinalIgnoreCase);
        if (!supportsGetChecked && !supportsGetValue && !supportsGetText)
        {
            MessageBox.Show(this, "该控件不支持读取（需要 getChecked、getValue 或 getText）。", "AgenticUI.NET");
            return;
        }

        await ExecuteAsync(supportsGetChecked ? AgenticActions.GetChecked : supportsGetValue ? AgenticActions.GetValue : AgenticActions.GetText);
        if (controlsList.SelectedItems.Count == 0 ||
            controlsList.SelectedItems[0].Tag is not AgenticControlDescriptor updated)
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
        else if (value is not null)
        {
            inputTextBox.Text = value.ToString() ?? "";
        }

        readResultLabel.Text = supportsGetChecked
            ? $"读取结果：checked={checkedValue}，text={text}"
            : supportsGetValue
                ? $"读取结果：{value}"
                : updated.IsSensitive
                    ? "读取结果：敏感字段已脱敏"
                    : $"读取结果：{inputTextBox.Text}";
        readResultLabel.ForeColor = Color.Green;
    }

    private async Task SetSelectedTextAsync()
    {
        if (controlsList.SelectedItems.Count == 0 ||
            controlsList.SelectedItems[0].Tag is not AgenticControlDescriptor descriptor)
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

        await ExecuteAsync(
            useText ? AgenticActions.SetText : AgenticActions.SetValue,
            new Dictionary<string, object?> { [useText ? "text" : "value"] = inputTextBox.Text });
    }

    private async Task ExecuteAsync(
        string action,
        Dictionary<string, object?>? arguments = null)
    {
        if (_client is null || controlsList.SelectedItems.Count == 0)
        {
            return;
        }

        if (controlsList.SelectedItems[0].Tag is not AgenticControlDescriptor descriptor)
        {
            return;
        }

        if (!descriptor.Actions.Contains(action, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, $"控件不支持动作 {action}。", "AgenticUI.NET");
            return;
        }

        try
        {
            var response = await _client.ExecuteAsync(new AgenticCommand
            {
                ControlId = descriptor.Id,
                Action = action,
                Arguments = arguments ?? new Dictionary<string, object?>()
            });
            if (response.Result?.Succeeded != true)
            {
                throw new InvalidOperationException(response.Result?.Error ?? response.Error ?? "命令执行失败。");
            }

            if (response.Result.Control is { } updated)
            {
                var item = controlsList.SelectedItems[0];
                item.Tag = updated;
                item.SubItems[1].Text = updated.Kind;
                item.SubItems[2].Text = updated.Name;
                item.SubItems[3].Text = string.Join(", ", updated.Actions);
            }
            else
            {
                await RefreshControlsCoreAsync();
            }
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async Task SelectNextItemAsync()
    {
        if (controlsList.SelectedItems.Count == 0 ||
            controlsList.SelectedItems[0].Tag is not AgenticControlDescriptor descriptor ||
            !descriptor.Actions.Contains(AgenticActions.SelectItem, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "请选择一个支持选择项目的下拉列表。", "AgenticUI.NET");
            return;
        }

        var selectedIndex = ReadInt(descriptor.State, "selectedIndex", -1);
        var itemCount = ReadInt(descriptor.State, "itemCount", 0);
        if (itemCount == 0)
        {
            MessageBox.Show(this, "下拉列表中没有可选择的项目。", "AgenticUI.NET");
            return;
        }

        await ExecuteAsync(
            AgenticActions.SelectItem,
            new Dictionary<string, object?> { ["index"] = (selectedIndex + 1) % itemCount });
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> state, string key, int fallback) =>
        state.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : fallback;

    private void EnsureSplitterWidth() =>
        mainSplit.SplitterDistance = Math.Max(420, (int)(mainSplit.Width * 0.65));

    private void OnEventReceived(AgenticEvent message)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(() =>
        {
            eventsList.Items.Insert(
                0,
                $"#{message.Sequence} {message.Timestamp:HH:mm:ss.fff} {message.ControlId} {message.Name} [{message.Source}]");
            while (eventsList.Items.Count > 1000)
            {
                eventsList.Items.RemoveAt(eventsList.Items.Count - 1);
            }
        });
    }

    private void OnConnectionFaulted(Exception exception)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(() =>
        {
            statusLabel.Text = $"连接中断：{exception.Message}";
            statusLabel.ForeColor = Color.Firebrick;
        });
    }

    private void ShowError(Exception exception) =>
        MessageBox.Show(this, exception.Message, "AgenticUI.NET", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
