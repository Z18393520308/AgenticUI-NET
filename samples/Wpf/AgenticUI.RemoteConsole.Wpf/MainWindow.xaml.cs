using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AgenticUI.Remote;

namespace AgenticUI.RemoteConsole.Wpf;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ControlRow> _controls = new();
    private AgenticNamedPipeClient? _client;

    public MainWindow()
    {
        InitializeComponent();
        ControlsList.ItemsSource = _controls;
        Closed += (_, _) => _client?.Dispose();
    }

    private async void Connect_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button connectButton)
        {
            connectButton.IsEnabled = false;
        }

        try
        {
            _client?.Dispose();
            _client = await AgenticNamedPipeClient.ConnectAsync(
                TokenBox.Text,
                PipeNameBox.Text,
                "AgenticUI Remote Console (WPF)");
            _client.EventReceived += OnEventReceived;
            _client.ConnectionFaulted += OnConnectionFaulted;
            StatusText.Text = "已认证连接";
            StatusText.Foreground = Brushes.Green;
            await RefreshControlsCoreAsync();
        }
        catch (Exception exception)
        {
            StatusText.Text = $"连接失败：{exception.Message}";
            StatusText.Foreground = Brushes.Firebrick;
        }
        finally
        {
            if (sender is Button button)
            {
                button.IsEnabled = true;
            }
        }
    }

    private async void Refresh_OnClick(object sender, RoutedEventArgs e) =>
        await RefreshControlsCoreAsync();

    private async void Highlight_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(AgenticActions.Highlight);

    private async void ClearHighlight_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(AgenticActions.ClearHighlight);

    private async void Click_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(AgenticActions.Click);

    private async void SelectNext_OnClick(object sender, RoutedEventArgs e) =>
        await SelectNextItemAsync();

    private async void Focus_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(AgenticActions.Focus);

    private async void Read_OnClick(object sender, RoutedEventArgs e) =>
        await ReadSelectedAsync();

    private async void SetText_OnClick(object sender, RoutedEventArgs e) =>
        await SetSelectedTextAsync();

    private async void MouseMove_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(
            AgenticActions.MouseMove,
            new Dictionary<string, object?> { ["xRatio"] = 0.5, ["yRatio"] = 0.5 });

    private async void MouseClick_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(
            AgenticActions.MouseClick,
            new Dictionary<string, object?> { ["xRatio"] = 0.35, ["yRatio"] = 0.55 });

    private async void MouseDoubleClick_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(
            AgenticActions.MouseDoubleClick,
            new Dictionary<string, object?> { ["xRatio"] = 0.65, ["yRatio"] = 0.4 });

    private async void MouseWheel_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(
            AgenticActions.MouseWheel,
            new Dictionary<string, object?> { ["xRatio"] = 0.5, ["yRatio"] = 0.5, ["delta"] = 120 });

    private async void MouseDrag_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(
            AgenticActions.MouseDrag,
            new Dictionary<string, object?>
            {
                ["startXRatio"] = 0.2,
                ["startYRatio"] = 0.25,
                ["endXRatio"] = 0.8,
                ["endYRatio"] = 0.75,
                ["steps"] = 16
            });

    private async void GridRows_OnClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(
            AgenticActions.GetRows,
            new Dictionary<string, object?> { ["start"] = 0, ["count"] = 5 });
        ShowSelectedState("rows", "表格行");
    }

    private async void GridHighlightCell_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(
            AgenticActions.HighlightCell,
            new Dictionary<string, object?> { ["row"] = 0, ["column"] = "Name" });

    private async void GridAddRow_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(
            AgenticActions.AddRow,
            new Dictionary<string, object?>
            {
                ["values"] = new Dictionary<string, object?>
                {
                    ["Name"] = "New User",
                    ["Role"] = "访客"
                }
            });

    private async void GridSort_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(
            AgenticActions.SortByColumn,
            new Dictionary<string, object?> { ["column"] = "Name", ["direction"] = "ascending" });

    private async void InputTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SetSelectedTextAsync();
        }
    }

    private void ControlsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SyncInputTextFromSelection();

    private async Task RefreshControlsCoreAsync()
    {
        if (_client is null)
        {
            MessageBox.Show(this, "请先连接 Workbench（管道名 + 令牌）。", "AgenticUI.NET");
            return;
        }

        try
        {
            var selectedId = (ControlsList.SelectedItem as ControlRow)?.Id;
            var response = await _client.ListControlsAsync();
            if (response.Type == RemoteMessageTypes.Error)
            {
                throw new InvalidOperationException(response.Error ?? "枚举控件失败。");
            }

            if (response.Type != RemoteMessageTypes.Controls)
            {
                throw new InvalidOperationException($"意外响应类型：{response.Type}");
            }

            _controls.Clear();
            foreach (var control in response.Controls ?? Array.Empty<AgenticControlDescriptor>())
            {
                _controls.Add(ControlRow.From(control));
            }

            if (selectedId is not null)
            {
                ControlsList.SelectedItem = _controls.FirstOrDefault(item => item.Id == selectedId);
            }

            if (ControlsList.SelectedIndex < 0 && _controls.Count > 0)
            {
                ControlsList.SelectedIndex = 0;
            }

            SyncInputTextFromSelection();

            var count = _controls.Count;
            var hasDialogButtons = _controls.Any(item =>
                string.Equals(item.Id, "dialog.ok", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Id, "dialog.cancel", StringComparison.OrdinalIgnoreCase));
            StatusText.Text = count == 0
                ? "已连接，但未枚举到控件（确认 WPF Workbench 已完全显示后再点刷新）"
                : hasDialogButtons
                    ? $"已连接，共 {count} 个控件（含弹窗按钮，可 click dialog.ok / dialog.cancel）"
                    : $"已连接，共 {count} 个控件（click dialog.open 打开弹窗后请再刷新）";
            StatusText.Foreground = count == 0 ? Brushes.DarkOrange : Brushes.Green;
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void SyncInputTextFromSelection()
    {
        if (ControlsList.SelectedItem is not ControlRow row)
        {
            InputTextBox.IsEnabled = false;
            return;
        }

        var supportsText = row.Descriptor.Actions.Contains(AgenticActions.SetText, StringComparer.OrdinalIgnoreCase) ||
                           row.Descriptor.Actions.Contains(AgenticActions.SetValue, StringComparer.OrdinalIgnoreCase) ||
                           row.Descriptor.Actions.Contains(AgenticActions.GetText, StringComparer.OrdinalIgnoreCase) ||
                           row.Descriptor.Actions.Contains(AgenticActions.GetValue, StringComparer.OrdinalIgnoreCase);
        InputTextBox.IsEnabled = supportsText;
        if (!supportsText)
        {
            return;
        }

        if (row.Descriptor.State.TryGetValue("text", out var text) && text is not null)
        {
            InputTextBox.Text = text.ToString() ?? "";
        }
        else if (row.Descriptor.State.TryGetValue("value", out var value) && value is not null)
        {
            InputTextBox.Text = value.ToString() ?? "";
        }
    }

    private async Task ReadSelectedAsync()
    {
        if (ControlsList.SelectedItem is not ControlRow row)
        {
            MessageBox.Show(this, "请先选择一个控件。", "AgenticUI.NET");
            return;
        }

        var supportsGetChecked = row.Descriptor.Actions.Contains(
            AgenticActions.GetChecked,
            StringComparer.OrdinalIgnoreCase);
        var supportsGetText = row.Descriptor.Actions.Contains(
            AgenticActions.GetText,
            StringComparer.OrdinalIgnoreCase);
        var supportsGetValue = row.Descriptor.Actions.Contains(
            AgenticActions.GetValue,
            StringComparer.OrdinalIgnoreCase);
        if (!supportsGetChecked && !supportsGetValue && !supportsGetText)
        {
            MessageBox.Show(this, "该控件不支持读取（需要 getChecked、getValue 或 getText）。", "AgenticUI.NET");
            return;
        }

        await ExecuteAsync(supportsGetChecked ? AgenticActions.GetChecked : supportsGetValue ? AgenticActions.GetValue : AgenticActions.GetText);
        if (ControlsList.SelectedItem is not ControlRow updated)
        {
            return;
        }

        updated.Descriptor.State.TryGetValue("text", out var text);
        updated.Descriptor.State.TryGetValue("checked", out var checkedValue);
        updated.Descriptor.State.TryGetValue("value", out var value);
        if (text is not null)
        {
            InputTextBox.Text = text.ToString() ?? "";
        }
        else if (value is not null)
        {
            InputTextBox.Text = value.ToString() ?? "";
        }

        ReadResultText.Text = supportsGetChecked
            ? $"读取结果：checked={checkedValue}，text={text}"
            : supportsGetValue
                ? $"读取结果：{value}"
                : updated.Descriptor.IsSensitive
                    ? "读取结果：敏感字段已脱敏"
                    : $"读取结果：{InputTextBox.Text}";
        ReadResultText.Foreground = Brushes.Green;
    }

    private async Task SetSelectedTextAsync()
    {
        if (ControlsList.SelectedItem is not ControlRow row)
        {
            MessageBox.Show(this, "请选择一个支持输入文本的控件。", "AgenticUI.NET");
            return;
        }

        var useText = row.Descriptor.Actions.Contains(AgenticActions.SetText, StringComparer.OrdinalIgnoreCase);
        if (!useText && !row.Descriptor.Actions.Contains(AgenticActions.SetValue, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "请选择一个支持文本或数值输入的控件。", "AgenticUI.NET");
            return;
        }

        await ExecuteAsync(
            useText ? AgenticActions.SetText : AgenticActions.SetValue,
            new Dictionary<string, object?> { [useText ? "text" : "value"] = InputTextBox.Text });
    }

    private async Task ExecuteAsync(
        string action,
        Dictionary<string, object?>? arguments = null)
    {
        if (_client is null || ControlsList.SelectedItem is not ControlRow row)
        {
            return;
        }

        if (!row.Descriptor.Actions.Contains(action, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, $"控件不支持动作 {action}。", "AgenticUI.NET");
            return;
        }

        try
        {
            var response = await _client.ExecuteAsync(new AgenticCommand
            {
                ControlId = row.Id,
                Action = action,
                Arguments = arguments ?? new Dictionary<string, object?>()
            });
            if (response.Result?.Succeeded != true)
            {
                throw new InvalidOperationException(response.Result?.Error ?? response.Error ?? "命令执行失败。");
            }

            if (response.Result.Control is { } updated)
            {
                var index = ControlsList.SelectedIndex;
                if (index >= 0)
                {
                    _controls[index] = ControlRow.From(updated);
                    ControlsList.SelectedIndex = index;
                }
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
        if (ControlsList.SelectedItem is not ControlRow row ||
            !row.Descriptor.Actions.Contains(AgenticActions.SelectItem, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "请选择一个支持选择项目的下拉列表。", "AgenticUI.NET");
            return;
        }

        var selectedIndex = ReadInt(row.Descriptor.State, "selectedIndex", -1);
        var itemCount = ReadInt(row.Descriptor.State, "itemCount", 0);
        if (itemCount == 0)
        {
            MessageBox.Show(this, "下拉列表中没有可选择的项目。", "AgenticUI.NET");
            return;
        }

        await ExecuteAsync(
            AgenticActions.SelectItem,
            new Dictionary<string, object?> { ["index"] = (selectedIndex + 1) % itemCount });
    }

    private void ShowSelectedState(string key, string label)
    {
        if (ControlsList.SelectedItem is ControlRow row &&
            row.Descriptor.State.TryGetValue(key, out var value))
        {
            ReadResultText.Text = $"{label}：{value}";
            ReadResultText.Foreground = Brushes.Green;
        }
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> state, string key, int fallback) =>
        state.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : fallback;

    private void OnEventReceived(AgenticEvent message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => AppendEvent(message));
            return;
        }

        AppendEvent(message);
    }

    private void AppendEvent(AgenticEvent message)
    {
        EventsList.Items.Insert(
            0,
            $"#{message.Sequence} {message.Timestamp:HH:mm:ss.fff} {message.ControlId} {message.Name} [{message.Source}]");
        while (EventsList.Items.Count > 1000)
        {
            EventsList.Items.RemoveAt(EventsList.Items.Count - 1);
        }
    }

    private void OnConnectionFaulted(Exception exception)
    {
        void ShowFault()
        {
            StatusText.Text = $"连接中断：{exception.Message}";
            StatusText.Foreground = Brushes.Firebrick;
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(ShowFault);
            return;
        }

        ShowFault();
    }

    private void ShowError(Exception exception) =>
        MessageBox.Show(this, exception.Message, "AgenticUI.NET", MessageBoxButton.OK, MessageBoxImage.Warning);

    private sealed class ControlRow
    {
        public required string Id { get; init; }
        public required string Kind { get; init; }
        public required string Name { get; init; }
        public required string ActionsText { get; init; }
        public required AgenticControlDescriptor Descriptor { get; init; }

        public static ControlRow From(AgenticControlDescriptor control) => new()
        {
            Id = control.Id,
            Kind = control.Kind,
            Name = control.Name,
            ActionsText = string.Join(", ", control.Actions),
            Descriptor = control
        };
    }
}
