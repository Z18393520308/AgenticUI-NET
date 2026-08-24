using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AgenticUI.Remote;

namespace AgenticUI.Workbench.Wpf;

public partial class MainWindow : Window
{
    private const string PipeName = "AgenticUI.NET.Wpf";
    private static readonly Uri ModernThemeUri =
        new("pack://application:,,,/AgenticUI.Wpf;component/Themes/ModernTheme.xaml");

    private readonly AgenticNamedPipeServer _server;
    private readonly AgenticLogRecorder _recorder;
    private readonly string _pipeStatusText;
    private readonly string _tokenStatusText;
    private readonly DispatcherTimer _statusResetTimer;
    private ResourceDictionary? _modernTheme;
    private bool _themeReady;
    private ConfirmDialog? _confirmDialog;
    private bool _mouseCanvasDragging;
    private int _mouseCanvasClicks;
    private double _mouseMarkerSize = 24;

    public MainWindow()
    {
        InitializeComponent();

        RoleCombo.ItemsSource = new[] { "管理员", "操作员", "访客" };
        RoleCombo.SelectedIndex = 0;
        ThemeCombo.ItemsSource = new[] { "原生外观", "现代主题" };
        ThemeCombo.SelectedIndex = 0;
        DemoGrid.ItemsSource = new ObservableCollection<DemoRow>
        {
            new("Alice", "管理员"),
            new("Bob", "操作员"),
            new("Carol", "访客"),
            new("David", "操作员"),
            new("Eve", "管理员")
        };
        DemoListView.ItemsSource = new[] { new DemoPerson("Alice", "北京"), new DemoPerson("Bob", "上海") };

        var logPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AgenticUI.NET",
            "workbench-wpf-events.jsonl");
        _recorder = new AgenticLogRecorder(logPath);
        _server = new AgenticNamedPipeServer(PipeName);
        _server.Start();
        _pipeStatusText = $"管道 {PipeName}";
        _tokenStatusText = $"令牌 {_server.AuthenticationToken}";
        PipeStatusText.Text = _pipeStatusText;
        TokenStatusText.Text = _tokenStatusText;
        _statusResetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
        _statusResetTimer.Tick += (_, _) =>
        {
            _statusResetTimer.Stop();
            StatusHintText.Text = "";
        };

        _themeReady = true;
        ApplyTheme(modern: false);
        Closed += OnClosed;
    }

    private void PipeStatusText_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        CopyStatusValue(PipeName, "已复制管道名");

    private void TokenStatusText_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        CopyStatusValue(_server.AuthenticationToken, "已复制令牌");

    private void CopyStatusValue(string value, string hint)
    {
        Clipboard.SetText(value);
        StatusHintText.Text = hint;
        _statusResetTimer.Stop();
        _statusResetTimer.Start();
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

    private static Style? TryFindStyle(string key) =>
        Application.Current.TryFindResource(key) as Style;

    private void LoginButton_OnClick(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "登录功能尚未实现。", "AgenticUI.NET");

    private void OpenDialogButton_OnClick(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(ShowConfirmDialog));

    private void ShowConfirmDialog()
    {
        if (_confirmDialog is not null)
        {
            _confirmDialog.Activate();
            return;
        }

        _confirmDialog = new ConfirmDialog { Owner = this };
        _confirmDialog.Closed += (_, _) => _confirmDialog = null;
        _confirmDialog.ShowDialog();
    }

    private void DemoMouseCanvas_OnLoaded(object sender, RoutedEventArgs e) =>
        UpdateMouseCanvas(new Point(DemoMouseCanvas.ActualWidth / 2, DemoMouseCanvas.ActualHeight / 2), false);

    private void DemoMouseCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mouseCanvasDragging = true;
        _mouseCanvasClicks++;
        MouseTrail.Points.Clear();
        DemoMouseCanvas.CaptureMouse();
        var point = e.GetPosition(DemoMouseCanvas);
        UpdateMouseCanvas(point, true);
        MouseCanvasStatus.Text = e.ClickCount > 1
            ? $"状态：双击 ({point.X:F0}, {point.Y:F0})"
            : $"状态：按下并开始拖拽 ({point.X:F0}, {point.Y:F0})，累计点击 {_mouseCanvasClicks} 次";
    }

    private void DemoMouseCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(DemoMouseCanvas);
        UpdateMouseCanvas(point, _mouseCanvasDragging);
        if (!_mouseCanvasDragging)
        {
            MouseCanvasStatus.Text = $"状态：移动到 ({point.X:F0}, {point.Y:F0})";
        }
    }

    private void DemoMouseCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(DemoMouseCanvas);
        UpdateMouseCanvas(point, _mouseCanvasDragging);
        _mouseCanvasDragging = false;
        DemoMouseCanvas.ReleaseMouseCapture();
        MouseCanvasStatus.Text = $"状态：释放，拖拽结束于 ({point.X:F0}, {point.Y:F0})";
    }

    private void DemoMouseCanvas_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _mouseMarkerSize = Math.Clamp(_mouseMarkerSize + Math.Sign(e.Delta) * 4, 12, 64);
        MouseMarker.Width = _mouseMarkerSize;
        MouseMarker.Height = _mouseMarkerSize;
        var point = e.GetPosition(DemoMouseCanvas);
        UpdateMouseCanvas(point, false);
        MouseCanvasStatus.Text = $"状态：滚轮 {e.Delta}，圆点尺寸 {_mouseMarkerSize:F0}";
    }

    private void DemoMouseCanvas_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        MouseTrail.Points.Clear();
        var point = e.GetPosition(DemoMouseCanvas);
        UpdateMouseCanvas(point, false);
        MouseCanvasStatus.Text = $"状态：右键清除轨迹 ({point.X:F0}, {point.Y:F0})";
    }

    private void UpdateMouseCanvas(Point point, bool appendTrail)
    {
        var x = Math.Clamp(point.X, 0, Math.Max(0, DemoMouseCanvas.ActualWidth - 1));
        var y = Math.Clamp(point.Y, 0, Math.Max(0, DemoMouseCanvas.ActualHeight - 1));
        Canvas.SetLeft(MouseMarker, x - MouseMarker.Width / 2);
        Canvas.SetTop(MouseMarker, y - MouseMarker.Height / 2);
        if (appendTrail)
        {
            MouseTrail.Points.Add(new Point(x, y));
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _statusResetTimer.Stop();
        _recorder.Dispose();
        _server.Dispose();
    }

    public sealed class DemoRow
    {
        public DemoRow()
        {
        }

        public DemoRow(string name, string role)
        {
            Name = name;
            Role = role;
        }

        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
    }

    private sealed class DemoPerson
    {
        public DemoPerson(string name, string city)
        {
            Name = name;
            City = city;
        }

        public string Name { get; set; }
        public string City { get; set; }
    }
}
