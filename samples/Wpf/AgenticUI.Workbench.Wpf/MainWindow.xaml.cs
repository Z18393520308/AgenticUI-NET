using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AgenticUI.Remote;

namespace AgenticUI.Workbench.Wpf;

public partial class MainWindow : Window
{
    private const string PipeName = "AgenticUI.NET.Wpf";
    private static readonly Uri ModernThemeUri =
        new("pack://application:,,,/AgenticUI.Wpf;component/Themes/ModernTheme.xaml");

    private readonly AgenticControlRegistry _registry = AgenticControlRegistry.Default;
    private readonly AgenticCommandDispatcher _dispatcher = new();
    private readonly AgenticNamedPipeServer _server;
    private readonly AgenticLogRecorder _recorder;
    private readonly IDisposable _subscription;
    private ResourceDictionary? _modernTheme;
    private bool _themeReady;
    private ConfirmDialog? _confirmDialog;

    public MainWindow()
    {
        InitializeComponent();

        RoleCombo.ItemsSource = new[] { "管理员", "操作员", "访客" };
        RoleCombo.SelectedIndex = 0;
        ThemeCombo.ItemsSource = new[] { "原生外观", "现代主题" };
        ThemeCombo.SelectedIndex = 0;
        DemoGrid.ItemsSource = new[] { new DemoRow("Alice", "管理员"), new DemoRow("Bob", "操作员") };
        DemoListView.ItemsSource = new[] { new DemoPerson("Alice", "北京"), new DemoPerson("Bob", "上海") };

        var logPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AgenticUI.NET",
            "workbench-wpf-events.jsonl");
        _recorder = new AgenticLogRecorder(logPath);
        _server = new AgenticNamedPipeServer(PipeName);
        _server.Start();
        _subscription = AgenticEventBus.Default.Subscribe(OnAgenticEventAsync);

        PipeNameBox.Text = PipeName;
        TokenBox.Text = _server.AuthenticationToken;

        _themeReady = true;
        ApplyTheme(modern: false);

        ContentRendered += (_, _) => RefreshControls();
        Closed += OnClosed;
    }

    private void ThemeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_themeReady)
        {
            return;
        }

        ApplyTheme(modern: ThemeCombo.SelectedIndex == 1);
    }

    private void ApplyTheme(bool modern)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        if (modern)
        {
            _modernTheme ??= new ResourceDictionary { Source = ModernThemeUri };
            if (!dictionaries.Contains(_modernTheme))
            {
                dictionaries.Add(_modernTheme);
            }

            Background = new SolidColorBrush(Color.FromRgb(246, 248, 252));
            UsernameBox.Style = TryFindStyle("AgenticModernTextBox");
            PasswordBox.Style = TryFindStyle("AgenticModernPasswordBox");
            ThemeCombo.Style = TryFindStyle("AgenticModernComboBox");
            RoleCombo.Style = TryFindStyle("AgenticModernComboBox");
            LoginButton.Style = TryFindStyle("AgenticModernButton");
            OpenDialogButton.Style = TryFindStyle("AgenticModernButton");
            NativeBoundButton.Style = TryFindStyle("AgenticModernButton");
        }
        else
        {
            if (_modernTheme is not null)
            {
                dictionaries.Remove(_modernTheme);
            }

            Background = SystemColors.ControlBrush;
            UsernameBox.ClearValue(StyleProperty);
            PasswordBox.ClearValue(StyleProperty);
            ThemeCombo.ClearValue(StyleProperty);
            RoleCombo.ClearValue(StyleProperty);
            LoginButton.ClearValue(StyleProperty);
            OpenDialogButton.ClearValue(StyleProperty);
            NativeBoundButton.ClearValue(StyleProperty);
        }

        TitleText.FontSize = 22;
        TitleText.FontWeight = FontWeights.Bold;
    }

    private static Style? TryFindStyle(string key)
    {
        return Application.Current.TryFindResource(key) as Style;
    }

    private void LoginButton_OnClick(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "登录功能尚未实现。", "AgenticUI.NET");

    private void OpenDialogButton_OnClick(object sender, RoutedEventArgs e)
    {
        // 延后弹出，避免远程 click 卡在 ShowDialog 上无法返回。
        Dispatcher.BeginInvoke(new Action(ShowConfirmDialog));
    }

    private void ShowConfirmDialog()
    {
        if (_confirmDialog is not null)
        {
            _confirmDialog.Activate();
            return;
        }

        _confirmDialog = new ConfirmDialog { Owner = this };
        _confirmDialog.Closed += (_, _) =>
        {
            _confirmDialog = null;
            RefreshControls();
        };
        // 模态弹窗；嵌套消息循环中仍可处理远程对 dialog.ok / dialog.cancel 的 click。
        _confirmDialog.ShowDialog();
        RefreshControls();
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => RefreshControls();

    private async void Highlight_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteSelectedAsync(AgenticActions.Highlight);

    private async void ClearHighlight_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteSelectedAsync(AgenticActions.ClearHighlight);

    private async void Click_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteSelectedAsync(AgenticActions.Click);

    private async void SelectNext_OnClick(object sender, RoutedEventArgs e) =>
        await SelectNextItemAsync();

    private async void Focus_OnClick(object sender, RoutedEventArgs e) =>
        await ExecuteSelectedAsync(AgenticActions.Focus);

    private async void SetText_OnClick(object sender, RoutedEventArgs e) =>
        await SetSelectedTextAsync();

    private async void Read_OnClick(object sender, RoutedEventArgs e) =>
        await ReadSelectedAsync();

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

    private void RefreshControls()
    {
        var selectedId = (ControlsList.SelectedItem as AgenticControlDescriptor)?.Id;
        ControlsList.Items.Clear();
        foreach (var descriptor in _registry.Snapshot())
        {
            ControlsList.Items.Add(descriptor);
        }

        if (selectedId is not null)
        {
            ControlsList.SelectedItem = ControlsList.Items
                .Cast<AgenticControlDescriptor>()
                .FirstOrDefault(item => item.Id == selectedId);
        }

        if (ControlsList.SelectedIndex < 0 && ControlsList.Items.Count > 0)
        {
            ControlsList.SelectedIndex = 0;
        }

        SyncInputTextFromSelection();
    }

    private void SyncInputTextFromSelection()
    {
        if (ControlsList.SelectedItem is not AgenticControlDescriptor descriptor)
        {
            InputTextBox.IsEnabled = false;
            return;
        }

        var supportsText = descriptor.Actions.Contains(AgenticActions.SetText, StringComparer.OrdinalIgnoreCase) ||
                           descriptor.Actions.Contains(AgenticActions.SetValue, StringComparer.OrdinalIgnoreCase) ||
                           descriptor.Actions.Contains(AgenticActions.GetText, StringComparer.OrdinalIgnoreCase) ||
                           descriptor.Actions.Contains(AgenticActions.GetValue, StringComparer.OrdinalIgnoreCase);
        InputTextBox.IsEnabled = supportsText;
        if (!supportsText)
        {
            return;
        }

        if (descriptor.State.TryGetValue("text", out var text) && text is not null)
        {
            InputTextBox.Text = text.ToString() ?? "";
        }
        else if (descriptor.State.TryGetValue("value", out var value) && value is not null)
        {
            InputTextBox.Text = value.ToString() ?? "";
        }
    }

    private async Task ReadSelectedAsync()
    {
        if (ControlsList.SelectedItem is not AgenticControlDescriptor descriptor)
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
        if (ControlsList.SelectedItem is not AgenticControlDescriptor updated)
        {
            return;
        }

        updated.State.TryGetValue("text", out var text);
        updated.State.TryGetValue("checked", out var checkedValue);
        updated.State.TryGetValue("value", out var value);
        if (text is not null)
        {
            InputTextBox.Text = text.ToString() ?? "";
        }
        else if (value is not null)
        {
            InputTextBox.Text = value.ToString() ?? "";
        }

        MessageBox.Show(
            this,
            supportsGetChecked
                ? $"checked={checkedValue}\r\ntext={text}"
                : supportsGetValue
                    ? $"读取结果：{value}"
                    : updated.IsSensitive
                        ? "敏感字段内容已脱敏。"
                        : $"读取结果：{InputTextBox.Text}",
            "AgenticUI.NET");
    }

    private async Task SetSelectedTextAsync()
    {
        if (ControlsList.SelectedItem is not AgenticControlDescriptor descriptor)
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

        await ExecuteSelectedAsync(
            useText ? AgenticActions.SetText : AgenticActions.SetValue,
            new Dictionary<string, object?> { [useText ? "text" : "value"] = InputTextBox.Text });
        RefreshControls();
    }

    private async Task ExecuteSelectedAsync(
        string action,
        Dictionary<string, object?>? arguments = null)
    {
        if (ControlsList.SelectedItem is not AgenticControlDescriptor descriptor)
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
            MessageBox.Show(this, result.Error, "命令执行失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task SelectNextItemAsync()
    {
        if (ControlsList.SelectedItem is not AgenticControlDescriptor descriptor ||
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

    private static int ReadInt(IReadOnlyDictionary<string, object?> state, string key, int fallback) =>
        state.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : fallback;

    private ValueTask OnAgenticEventAsync(AgenticEvent message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => AppendEvent(message));
            return ValueTask.CompletedTask;
        }

        AppendEvent(message);
        return ValueTask.CompletedTask;
    }

    private void AppendEvent(AgenticEvent message)
    {
        EventsList.Items.Insert(
            0,
            $"#{message.Sequence}  {message.Timestamp:HH:mm:ss.fff}  {message.ControlId}  {message.Name}  [{message.Source}]  {JsonSerializer.Serialize(message.Data)}");
        while (EventsList.Items.Count > 500)
        {
            EventsList.Items.RemoveAt(EventsList.Items.Count - 1);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _subscription.Dispose();
        _recorder.Dispose();
        _server.Dispose();
    }

    private sealed class DemoRow
    {
        public DemoRow(string name, string role) { Name = name; Role = role; }
        public string Name { get; set; }
        public string Role { get; set; }
    }

    private sealed class DemoPerson
    {
        public DemoPerson(string name, string city) { Name = name; City = city; }
        public string Name { get; set; }
        public string City { get; set; }
    }
}
