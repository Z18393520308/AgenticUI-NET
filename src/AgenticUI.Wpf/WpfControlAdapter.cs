using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using AgenticUI;

namespace AgenticUI.Wpf;

internal sealed class WpfControlAdapter : IAgenticControl, IAgenticGuidanceControl
{
    private readonly FrameworkElement _element;
    private readonly AgenticControlRegistry _registry;
    private readonly AgenticEventBus _events;
    private string? _registeredId;
    private readonly AgenticGuidanceCollection _highlights = new();
    private readonly AgenticGuidanceCollection _cellHighlights = new();
    private bool _attached;
    private AgenticEventSource _activeSource = AgenticEventSource.User;
    private Dictionary<string, object?>? _lastCellState;
    private TreeViewItem? _lastExpansionNode;
    private Predicate<object>? _originalGridFilter;
    private bool _hasAgenticGridFilter;
    private readonly AgenticItemCollection _items = new();
    private IReadOnlyDictionary<string, object?>? _itemResponse;
    private bool _itemSelectionPending;

    public WpfControlAdapter(
        FrameworkElement element,
        AgenticControlRegistry? registry = null,
        AgenticEventBus? events = null)
    {
        _element = element;
        _registry = registry ?? AgenticControlRegistry.Default;
        _events = events ?? AgenticEventBus.Default;
    }

    public void Attach()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _element.Loaded += OnLoaded;
        _element.Unloaded += OnUnloaded;
        if (_element.IsLoaded)
        {
            Register();
        }
    }

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        _element.Loaded -= OnLoaded;
        _element.Unloaded -= OnUnloaded;
        UnhookEvents();
        RemoveHighlight();
        Unregister();
    }

    public void RefreshRegistration()
    {
        void Refresh()
        {
            if (!_element.IsLoaded)
            {
                return;
            }

            var requestedId = AgenticProperties.GetId(_element);
            if (string.IsNullOrWhiteSpace(requestedId) ||
                string.Equals(requestedId, _registeredId, StringComparison.Ordinal))
            {
                return;
            }

            Unregister();
            Register();
        }

        if (_element.Dispatcher.CheckAccess())
        {
            Refresh();
        }
        else
        {
            _element.Dispatcher.Invoke(Refresh);
        }
    }

    public AgenticControlDescriptor Describe()
    {
        if (_element.Dispatcher.CheckAccess())
        {
            return DescribeOnUiThread();
        }

        return _element.Dispatcher.Invoke(DescribeOnUiThread);
    }

    public bool IsRemotelyDiscoverable()
    {
        if (_element.Dispatcher.CheckAccess())
        {
            return WpfDisplayability.IsDisplayable(_element);
        }

        return _element.Dispatcher.Invoke(() => WpfDisplayability.IsDisplayable(_element));
    }

    public async Task<AgenticCommandResult> ExecuteAsync(
        AgenticCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var operation = _element.Dispatcher.InvokeAsync(
                () => ExecuteOnUiThreadAsync(command, cancellationToken),
                System.Windows.Threading.DispatcherPriority.Normal,
                cancellationToken);
            return await (await operation).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return AgenticCommandResult.Failure(command.RequestId, exception.Message);
        }
    }

    private async Task<AgenticCommandResult> ExecuteOnUiThreadAsync(AgenticCommand command, CancellationToken token)
    {
        if (_itemSelectionPending && !AgenticActionPolicy.IsObservation(command.Action))
            throw new InvalidOperationException("该控件正在准备选项，请等待本次选择结束后再执行修改动作。");
        var selecting = command.Action == AgenticActions.SelectItem && WpfItemAccess.Supports(_element);
        try
        {
            if (selecting)
            {
                _itemSelectionPending = true;
                await PrepareAndSelectItemAsync((Selector)_element, command, token);
            }
            else ExecuteOnUiThread(command);
            return AgenticCommandResult.Success(command.RequestId, DescribeOnUiThread());
        }
        finally
        {
            if (selecting) _itemSelectionPending = false;
            _itemResponse = null;
        }
    }

    private AgenticControlDescriptor DescribeOnUiThread()
    {
        var id = _registeredId ?? AgenticProperties.GetId(_element) ?? "";
        return new AgenticControlDescriptor
        {
            Id = id,
            Name = AgenticProperties.GetDisplayName(_element) ?? _element.Name ?? id,
            Kind = GetKind(),
            IsTemporaryId = id.StartsWith("temporary.", StringComparison.OrdinalIgnoreCase),
            IsSensitive = AgenticProperties.GetSensitive(_element) || _element is PasswordBox,
            IsEnabled = _element.IsEnabled,
            Capabilities = WpfItemAccess.Supports(_element)
                ? new[] { AgenticGuidanceOptions.Capability, AgenticItemCollection.Capability }
                : new[] { AgenticGuidanceOptions.Capability },
            Actions = GetActions(),
            State = GetState()
        };
    }

    private void ExecuteOnUiThread(AgenticCommand command)
    {
        if (command.Action == AgenticActions.ClearHighlight) { ClearHighlight(command); return; }
        ValidateCommandTarget(command);
        ExecuteActionOnUiThread(command);
    }

    private void ValidateCommandTarget(AgenticCommand command)
    {
        if (!AgenticActionPolicy.IsObservation(command.Action))
        {
            if (!_element.IsEnabled || !WpfDisplayability.IsDisplayable(_element))
                throw new InvalidOperationException("目标控件不可交互，禁止远程绕过禁用或隐藏状态。");
        }
        var window = Window.GetWindow(_element);
        if (window is null ||
            !window.IsVisible ||
            !WpfDisplayability.IsInActiveInteractionScope(window))
        {
            throw new InvalidOperationException(
                "控件当前不可远程操作（被模态弹窗阻挡或不在活动窗口）。");
        }
    }

    private void ExecuteActionOnUiThread(AgenticCommand command)
    {
        var previousSource = _activeSource;
        _activeSource = AgenticEventSource.Remote;
        try
        {
            switch (command.Action)
            {
                case AgenticActions.Focus:
                    _element.Focus();
                    break;
                case AgenticActions.Highlight:
                    ShowHighlight(command);
                    break;
                case AgenticActions.ClearHighlight:
                    ClearHighlight(command);
                    break;
                case AgenticActions.MouseMove:
                case AgenticActions.MouseClick:
                case AgenticActions.MouseDoubleClick:
                case AgenticActions.MouseWheel:
                case AgenticActions.MouseDrag:
                    WpfMouseInput.Execute(_element, command);
                    break;
                case AgenticActions.Click when _element is ButtonBase button:
                    InvokeNativeClick(button);
                    break;
                case AgenticActions.Click when _element is TextBox:
                case AgenticActions.Click when _element is PasswordBox:
                    _element.Focus();
                    break;
                case AgenticActions.Click when _element is ComboBox:
                case AgenticActions.OpenDropDown when _element is ComboBox:
                    ((ComboBox)_element).IsDropDownOpen = true;
                    break;
                case AgenticActions.CloseDropDown when _element is ComboBox closedComboBox:
                    closedComboBox.IsDropDownOpen = false;
                    break;
                case AgenticActions.SetText when _element is TextBox textBox:
                    if (textBox.IsReadOnly) throw new InvalidOperationException("文本框为只读。");
                    var text = GetArgument(command, "text")?.ToString() ?? "";
                    if (textBox.MaxLength > 0 && text.Length > textBox.MaxLength)
                        throw new ArgumentException("文本超过控件 MaxLength 限制。");
                    textBox.SetCurrentValue(TextBox.TextProperty, text);
                    textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    if (Validation.GetHasError(textBox)) throw new InvalidOperationException("文本未通过控件绑定校验。");
                    break;
                case AgenticActions.SetText when _element is PasswordBox passwordBox:
                    passwordBox.Password = GetArgument(command, "text")?.ToString() ?? "";
                    break;
                case AgenticActions.GetText when _element is TextBox:
                    break;
                case AgenticActions.GetText when _element is PasswordBox:
                    break;
                case AgenticActions.GetText when _element is ComboBox:
                    break;
                case AgenticActions.GetText when _element is ToggleButton:
                case AgenticActions.GetText when _element is ListBox:
                case AgenticActions.GetText when _element is TabControl:
                    break;
                case AgenticActions.GetText when _element is TextBlock:
                case AgenticActions.GetText when _element is Label:
                case AgenticActions.GetText when _element is ListView:
                    break;
                case AgenticActions.SetValue when _element is DatePicker datePicker:
                    datePicker.SetCurrentValue(DatePicker.SelectedDateProperty, DateTime.Parse(GetArgument(command, "value")?.ToString() ?? throw new ArgumentException("setValue requires a 'value' argument.")));
                    break;
                case AgenticActions.GetValue when _element is DatePicker:
                    break;
                case AgenticActions.SetValue when _element is Slider slider:
                    slider.SetCurrentValue(Slider.ValueProperty, double.Parse(GetArgument(command, "value")?.ToString() ?? throw new ArgumentException("setValue requires a 'value' argument.")));
                    break;
                case AgenticActions.GetValue when _element is Slider:
                    break;
                case AgenticActions.SetChecked when _element is ToggleButton toggle:
                    toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, ReadBoolean(GetArgument(command, "checked")));
                    break;
                case AgenticActions.GetChecked when _element is ToggleButton:
                    break;
                case AgenticActions.GetItems when WpfItemAccess.Supports(_element):
                    var itemSelector = (Selector)_element;
                    _items.Update(WpfItemAccess.Capture(itemSelector));
                    _itemResponse = _items.ReadPage(command, itemSelector.SelectedIndex);
                    break;
                case AgenticActions.SelectItem when WpfItemAccess.Supports(_element):
                    SelectItem((Selector)_element, command);
                    break;
                case AgenticActions.SelectItem when _element is Selector selector:
                    Select(selector, GetArgument(command, "index"), GetArgument(command, "value"));
                    break;
                case AgenticActions.SelectRow when _element is DataGrid grid:
                    SelectGridRow(grid, ReadIndex(GetArgument(command, "row")));
                    break;
                case AgenticActions.GetRow when _element is DataGrid grid:
                    ReadGridRow(grid, command);
                    break;
                case AgenticActions.GetRows when _element is DataGrid grid:
                    ReadGridRows(grid, command);
                    break;
                case AgenticActions.GetColumns when _element is DataGrid grid:
                    ReadGridColumns(grid);
                    break;
                case AgenticActions.GetCell when _element is DataGrid grid:
                    ReadGridCell(grid, command);
                    break;
                case AgenticActions.SetCell when _element is DataGrid grid:
                    WriteGridCell(grid, command);
                    break;
                case AgenticActions.ScrollToRow when _element is DataGrid grid:
                    ScrollGridToRow(grid, ReadIndex(GetArgument(command, "row")));
                    break;
                case AgenticActions.AddRow when _element is DataGrid grid:
                    AddGridRow(grid, command);
                    break;
                case AgenticActions.DeleteRow when _element is DataGrid grid:
                    DeleteGridRow(grid, ReadIndex(GetArgument(command, "row")));
                    break;
                case AgenticActions.SortByColumn when _element is DataGrid grid:
                    SortGridByColumn(grid, command);
                    break;
                case AgenticActions.FilterByColumn when _element is DataGrid grid:
                    FilterGridByColumn(grid, command);
                    break;
                case AgenticActions.HighlightCell when _element is DataGrid grid:
                    HighlightGridCell(grid, command);
                    break;
                case AgenticActions.SelectCell when _element is DataGrid grid:
                    SelectGridCell(grid, command);
                    break;
                case AgenticActions.SelectItem when _element is TreeView tree:
                    WpfTreeState.Find(tree, command).SetCurrentValue(TreeViewItem.IsSelectedProperty, true);
                    break;
                case AgenticActions.Expand when _element is TreeView tree:
                    WpfTreeState.Find(tree, command).SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
                    break;
                case AgenticActions.Collapse when _element is TreeView tree:
                    WpfTreeState.Find(tree, command).SetCurrentValue(TreeViewItem.IsExpandedProperty, false);
                    break;
                case AgenticActions.Click when _element is Menu menu:
                    InvokeNativeMenuClick(FindMenuItem(menu.Items, command));
                    break;
                case AgenticActions.Click when _element is ToolBar toolBar:
                    InvokeNativeClick(FindToolBarItem(toolBar, command));
                    break;
                case AgenticActions.GetValue when _element is ProgressBar:
                    break;
                default:
                    throw new InvalidOperationException($"Action '{command.Action}' is not valid for {GetKind()}.");
            }
        }
        finally
        {
            _activeSource = previousSource;
        }
    }

    private static void InvokeNativeClick(ButtonBase button)
    {
        if (!button.IsEnabled || !button.IsVisible) throw new InvalidOperationException("按钮不可交互。");
        button.Focus();
        // 调用真实控件的虚方法，复用 OnToggle、Click、ICommand；仅 RaiseEvent 会遗漏 ICommand。
        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(button, null);
    }

    private static void InvokeNativeMenuClick(MenuItem item)
    {
        if (!item.IsEnabled || !item.IsVisible) throw new InvalidOperationException("菜单项不可交互。");
        typeof(MenuItem).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(item, null);
    }

    private void Register()
    {
        if (_registeredId is not null)
        {
            return;
        }

        _registeredId = _registry.Register(this, AgenticProperties.GetId(_element));
        HookEvents();
    }

    private void Unregister()
    {
        if (_registeredId is null)
        {
            return;
        }

        _registry.Unregister(_registeredId, this);
        _registeredId = null;
    }

    private void HookEvents()
    {
        _element.PreviewMouseDown += OnMouseDown;
        _element.PreviewMouseUp += OnMouseUp;
        _element.GotKeyboardFocus += OnGotFocus;
        _element.LostKeyboardFocus += OnLostFocus;
        if (_element is ButtonBase button)
        {
            button.Click += OnClick;
        }
        if (_element is TextBox text)
        {
            text.TextChanged += OnTextChanged;
        }
        if (_element is PasswordBox passwordBox)
        {
            passwordBox.PasswordChanged += OnPasswordChanged;
        }
        if (_element is ToggleButton toggle)
        {
            toggle.Checked += OnCheckedChanged;
            toggle.Unchecked += OnCheckedChanged;
        }
        if (_element is Selector selector)
        {
            selector.SelectionChanged += OnSelectionChanged;
        }
        if (_element is ComboBox comboBox)
        {
            comboBox.DropDownOpened += OnDropDownOpened;
            comboBox.DropDownClosed += OnDropDownClosed;
        }
        if (_element is Slider slider) slider.ValueChanged += OnValueChanged;
        if (_element is ProgressBar progressBar) progressBar.ValueChanged += OnProgressValueChanged;
        if (_element is TreeView treeView)
        {
            treeView.SelectedItemChanged += OnTreeSelectionChanged;
            // 在树根监听冒泡事件，涵盖动态/绑定节点；handledEventsToo 避免业务处理后丢失审计。
            treeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnTreeExpanded), true);
            treeView.AddHandler(TreeViewItem.CollapsedEvent, new RoutedEventHandler(OnTreeCollapsed), true);
        }
        if (_element is DatePicker datePicker) datePicker.SelectedDateChanged += OnDateChanged;
    }

    private void UnhookEvents()
    {
        _element.PreviewMouseDown -= OnMouseDown;
        _element.PreviewMouseUp -= OnMouseUp;
        _element.GotKeyboardFocus -= OnGotFocus;
        _element.LostKeyboardFocus -= OnLostFocus;
        if (_element is ButtonBase button)
        {
            button.Click -= OnClick;
        }
        if (_element is TextBox text)
        {
            text.TextChanged -= OnTextChanged;
        }
        if (_element is PasswordBox passwordBox)
        {
            passwordBox.PasswordChanged -= OnPasswordChanged;
        }
        if (_element is ToggleButton toggle)
        {
            toggle.Checked -= OnCheckedChanged;
            toggle.Unchecked -= OnCheckedChanged;
        }
        if (_element is Selector selector)
        {
            selector.SelectionChanged -= OnSelectionChanged;
        }
        if (_element is ComboBox comboBox)
        {
            comboBox.DropDownOpened -= OnDropDownOpened;
            comboBox.DropDownClosed -= OnDropDownClosed;
        }
        if (_element is Slider slider) slider.ValueChanged -= OnValueChanged;
        if (_element is ProgressBar progressBar) progressBar.ValueChanged -= OnProgressValueChanged;
        if (_element is TreeView treeView)
        {
            treeView.SelectedItemChanged -= OnTreeSelectionChanged;
            treeView.RemoveHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnTreeExpanded));
            treeView.RemoveHandler(TreeViewItem.CollapsedEvent, new RoutedEventHandler(OnTreeCollapsed));
            _lastExpansionNode = null;
        }
        if (_element is DatePicker datePicker) datePicker.SelectedDateChanged -= OnDateChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => Register();
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        UnhookEvents();
        RemoveHighlight();
        Unregister();
    }

    private void OnClick(object sender, RoutedEventArgs args) => Publish(AgenticEvents.Clicked);
    private void OnMouseDown(object sender, MouseButtonEventArgs args) => Publish(AgenticEvents.Pressed);
    private void OnMouseUp(object sender, MouseButtonEventArgs args) => Publish(AgenticEvents.Released);
    private void OnGotFocus(object sender, KeyboardFocusChangedEventArgs args) =>
        Publish(AgenticEvents.FocusChanged, data: new Dictionary<string, object?> { ["focused"] = true });
    private void OnLostFocus(object sender, KeyboardFocusChangedEventArgs args) =>
        Publish(AgenticEvents.FocusChanged, data: new Dictionary<string, object?> { ["focused"] = false });

    private void OnTextChanged(object sender, TextChangedEventArgs args)
    {
        var value = _element is TextBox textBox ? textBox.Text : null;
        Publish(AgenticEvents.TextChanged, data: new Dictionary<string, object?> { ["text"] = value });
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs args) =>
        Publish(AgenticEvents.TextChanged, data: new Dictionary<string, object?> { ["text"] = null });

    private void OnCheckedChanged(object sender, RoutedEventArgs args)
    {
        Publish(
            AgenticEvents.CheckedChanged,
            data: new Dictionary<string, object?> { ["checked"] = ((ToggleButton)_element).IsChecked });
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        var selector = (Selector)_element;
        Publish(
            AgenticEvents.SelectionChanged,
            data: new Dictionary<string, object?>
            {
                ["index"] = selector.SelectedIndex,
                ["selection"] = WpfItemAccess.Supports(selector) ? WpfItemAccess.Text(selector, selector.SelectedItem) :
                    selector is TabControl ? GetTabText(selector.SelectedItem) : selector.SelectedItem?.ToString() ?? selector.SelectedValue?.ToString(),
                ["itemKey"] = WpfItemAccess.Supports(selector) ? WpfItemAccess.Key(selector, selector.SelectedItem) : null
            });
    }

    private void OnDropDownOpened(object? sender, EventArgs args) => Publish(AgenticEvents.DropDownOpened);
    private void OnDropDownClosed(object? sender, EventArgs args) => Publish(AgenticEvents.DropDownClosed);
    private void OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args) =>
        Publish(AgenticEvents.ValueChanged, data: new Dictionary<string, object?> { ["value"] = args.NewValue });
    private void OnProgressValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args) =>
        Publish(AgenticEvents.ValueChanged, data: new Dictionary<string, object?> { ["value"] = args.NewValue });
    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
    {
        // 嵌套 TreeView 的选择事件会冒泡到外树，不得将其内容作为外树的事件（或绕过内树脱敏）。
        if (!ReferenceEquals(args.OriginalSource, _element)) return;
        Publish(AgenticEvents.SelectionChanged, data: new Dictionary<string, object?>
        {
            ["selection"] = WpfTreeState.Header(WpfTreeState.Selected((TreeView)_element)) ?? GetTreeHeader(args.NewValue),
            ["path"] = WpfTreeState.Path((TreeView)_element, WpfTreeState.Selected((TreeView)_element))
        });
    }
    private void OnTreeExpanded(object sender, RoutedEventArgs args) => OnTreeExpansion(args, AgenticEvents.Expanded);
    private void OnTreeCollapsed(object sender, RoutedEventArgs args) => OnTreeExpansion(args, AgenticEvents.Collapsed);
    private void OnTreeExpansion(RoutedEventArgs args, string eventName)
    {
        if (_element is not TreeView tree || args.OriginalSource is not TreeViewItem node || !WpfTreeState.BelongsTo(tree, node)) return;
        _lastExpansionNode = node;
        Publish(eventName, data: new Dictionary<string, object?>
        {
            ["path"] = WpfTreeState.Path(tree, node), ["expanded"] = node.IsExpanded
        });
    }
    private void OnDateChanged(object? sender, SelectionChangedEventArgs args) =>
        Publish(AgenticEvents.ValueChanged, data: new Dictionary<string, object?> { ["value"] = ((DatePicker)_element).SelectedDate?.ToString("O") });

    private void Publish(
        string eventName,
        AgenticEventSource? source = null,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        if (_registeredId is not null)
        {
            var message = _events.Create(_registeredId, eventName, source ?? _activeSource, data);
            message.IsSensitive = AgenticProperties.GetSensitive(_element) || _element is PasswordBox;
            _ = _events.PublishAsync(message);
        }
    }

    private string GetKind() => _element switch
    {
        DataGrid => "dataGrid",
        TreeView => "treeView",
        Menu => "menu",
        ToolBar => "toolBar",
        ProgressBar => "progressBar",
        TextBlock => "textBlock",
        Label => "label",
        ListView => "listView",
        RadioButton => "radioButton",
        CheckBox => "checkBox",
        ToggleButton => "toggleButton",
        ButtonBase => "button",
        PasswordBox => "passwordBox",
        Canvas => "canvas",
        TextBox => "textBox",
        DatePicker => "datePicker",
        ComboBox => "comboBox",
        ListBox => "listBox",
        TabControl => "tabControl",
        Slider => "slider",
        Selector => "selector",
        _ => _element.GetType().Name
    };

    private IReadOnlyList<string> GetActions()
    {
        var actions = new List<string>
        {
            AgenticActions.Focus,
            AgenticActions.Highlight,
            AgenticActions.ClearHighlight,
            AgenticActions.MouseMove,
            AgenticActions.MouseClick,
            AgenticActions.MouseDoubleClick,
            AgenticActions.MouseWheel,
            AgenticActions.MouseDrag
        };
        if (_element is Button or ToggleButton or TextBox or PasswordBox)
        {
            actions.Add(AgenticActions.Click);
        }

        if (_element is TextBox or PasswordBox)
        {
            actions.Add(AgenticActions.SetText);
            actions.Add(AgenticActions.GetText);
        }

        if (_element is ToggleButton)
        {
            actions.Add(AgenticActions.SetChecked);
            actions.Add(AgenticActions.GetChecked);
            actions.Add(AgenticActions.GetText);
        }

        if (_element is ComboBox)
        {
            actions.Add(AgenticActions.Click);
            actions.Add(AgenticActions.OpenDropDown);
            actions.Add(AgenticActions.CloseDropDown);
            actions.Add(AgenticActions.GetText);
        }

        if (_element is Selector)
        {
            actions.Add(AgenticActions.SelectItem);
        }
        if (WpfItemAccess.Supports(_element)) actions.Add(AgenticActions.GetItems);
        if (_element is ListBox or TabControl)
        {
            actions.Add(AgenticActions.GetText);
        }
        if (_element is Slider or DatePicker)
        {
            actions.Add(AgenticActions.SetValue);
            actions.Add(AgenticActions.GetValue);
        }
        if (_element is ProgressBar) actions.Add(AgenticActions.GetValue);
        if (_element is TextBlock or Label or ListView) actions.Add(AgenticActions.GetText);
        if (_element is DataGrid)
        {
            actions.Add(AgenticActions.SelectRow);
            actions.Add(AgenticActions.GetRow);
            actions.Add(AgenticActions.GetRows);
            actions.Add(AgenticActions.GetColumns);
            actions.Add(AgenticActions.GetCell);
            actions.Add(AgenticActions.SetCell);
            actions.Add(AgenticActions.ScrollToRow);
            actions.Add(AgenticActions.AddRow);
            actions.Add(AgenticActions.DeleteRow);
            actions.Add(AgenticActions.SortByColumn);
            actions.Add(AgenticActions.FilterByColumn);
            actions.Add(AgenticActions.HighlightCell);
            actions.Add(AgenticActions.SelectCell);
        }
        if (_element is TreeView)
        {
            actions.Add(AgenticActions.SelectItem);
            actions.Add(AgenticActions.Expand);
            actions.Add(AgenticActions.Collapse);
        }
        if (_element is Menu or ToolBar) actions.Add(AgenticActions.Click);

        return actions;
    }

    private IReadOnlyDictionary<string, object?> GetState()
    {
        var state = new Dictionary<string, object?>
        {
            ["visible"] = _element.IsVisible,
            ["displayable"] = WpfDisplayability.IsDisplayable(_element),
            ["focused"] = _element.IsKeyboardFocusWithin
        };
        if (_element is TextBox textBox)
        {
            state["text"] = AgenticProperties.GetSensitive(_element) ? null : textBox.Text;
            state["readOnly"] = textBox.IsReadOnly;
        }

        if (_element is PasswordBox)
        {
            state["text"] = null;
        }

        if (_element is ToggleButton toggle)
        {
            state["checked"] = toggle.IsChecked;
            state["text"] = toggle switch
            {
                ContentControl content => content.Content?.ToString(),
                _ => null
            };
        }
        if (_element is Selector selector)
        {
            state["selectedIndex"] = selector.SelectedIndex;
            state["selection"] = WpfItemAccess.Supports(selector) ? WpfItemAccess.Text(selector, selector.SelectedItem) :
                selector is TabControl ? GetTabText(selector.SelectedItem) : selector.SelectedItem?.ToString() ?? selector.SelectedValue?.ToString();
            state["itemCount"] = selector.Items.Count;
            if (WpfItemAccess.Supports(selector))
            {
                state["itemKey"] = WpfItemAccess.Key(selector, selector.SelectedItem);
                state["text"] = state["selection"];
            }
        }
        if (_element is ComboBox comboBox)
        {
            state["text"] = WpfItemAccess.Text(comboBox, comboBox.SelectedItem) ?? comboBox.Text;
            state["isDropDownOpen"] = comboBox.IsDropDownOpen;
        }
        if (_element is DatePicker datePicker)
        {
            state["value"] = datePicker.SelectedDate?.ToString("O");
            state["text"] = datePicker.Text;
        }
        if (_element is Slider slider)
        {
            state["value"] = slider.Value;
            state["minimum"] = slider.Minimum;
            state["maximum"] = slider.Maximum;
        }
        if (_element is ProgressBar progressBar)
        {
            state["value"] = progressBar.Value;
            state["minimum"] = progressBar.Minimum;
            state["maximum"] = progressBar.Maximum;
        }
        if (_element is TextBlock textBlock) state["text"] = textBlock.Text;
        if (_element is Label label) state["text"] = label.Content?.ToString();
        if (_element is DataGrid grid)
        {
            state["readOnly"] = grid.IsReadOnly;
            state["selectedIndex"] = grid.SelectedIndex;
            state["rowCount"] = grid.Items.Count;
            state["columnCount"] = grid.Columns.Count;
        }
        if (_element is TreeView treeView)
        {
            var selected = WpfTreeState.Selected(treeView);
            state["text"] = WpfTreeState.Header(selected) ?? GetTreeHeader(treeView.SelectedItem);
            state["selection"] = state["text"];
            state["path"] = WpfTreeState.Path(treeView, selected);
            state["treeStateVersion"] = 1;
            var expansionPath = WpfTreeState.Path(treeView, _lastExpansionNode);
            state["expansionPath"] = expansionPath;
            state["expanded"] = expansionPath is null ? null : _lastExpansionNode!.IsExpanded;
            state["itemCount"] = treeView.Items.Count;
        }
        if (_lastCellState is not null)
            foreach (var pair in _lastCellState) state[pair.Key] = pair.Value;
        if (_element is ListBox listBox) state["text"] = WpfItemAccess.Supports(listBox)
            ? WpfItemAccess.Text(listBox, listBox.SelectedItem) : listBox.SelectedItem?.ToString();
        if (_element is TabControl tabControl) state["text"] = GetTabText(tabControl.SelectedItem);
        if (_itemResponse is not null)
            foreach (var pair in _itemResponse) state[pair.Key] = pair.Value;
        return state;
    }

    private void ShowHighlight(AgenticCommand command)
    {
        var options = AgenticGuidanceOptions.FromCommand(command, AgenticProperties.GetInstructionNumber(_element), AgenticProperties.GetHint(_element));
        _highlights.Show(command, options, () => new WpfHighlight(_element));
    }

    private void ClearHighlight(AgenticCommand command)
    {
        var id = AgenticGuidanceOptions.ReadGuidanceId(command);
        _highlights.Clear(command.SessionId, id);
        _cellHighlights.Clear(command.SessionId, id);
    }

    private void RemoveHighlight()
    {
        _highlights.Dispose();
        _cellHighlights.Dispose();
    }

    public async Task ClearGuidanceAsync(string sessionId)
    {
        if (_element.Dispatcher.HasShutdownStarted) return;
        await _element.Dispatcher.InvokeAsync(() =>
        {
            _highlights.Clear(sessionId);
            _cellHighlights.Clear(sessionId);
        });
    }

    private static object? GetArgument(AgenticCommand command, string key) =>
        command.Arguments.TryGetValue(key, out var value) ? value : null;

    private static bool ReadBoolean(object? value)
    {
        if (value is bool boolean)
        {
            return boolean;
        }

        if (bool.TryParse(value?.ToString(), out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"'{value}' is not a valid Boolean value.");
    }

    private async Task PrepareAndSelectItemAsync(Selector selector, AgenticCommand command, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ValidateCommandTarget(command);
        _items.Update(WpfItemAccess.Capture(selector));
        var index = _items.LocateIndex(command);
        var version = _items.Version;
        var registration = _registeredId;
        var window = Window.GetWindow(selector);
        void ValidatePreparedTarget()
        {
            ValidateCommandTarget(command);
            if (!selector.IsLoaded || !_attached || _registeredId != registration || Window.GetWindow(selector) != window)
                throw new InvalidOperationException("目标控件已卸载或重新注册，已停止本次选择。");
            if (_items.Version != version)
                throw new InvalidOperationException("Items changed during preparation. Read getItems again.");
        }
        void CommitPreparedSelection()
        {
            // 等待时只检查生命周期和集合通知，不每 30 ms 遍历整个绑定数据源。
            // 提交前重新捕获，仍可发现未发集合通知的文字/键变化，且不接受等待期间的新目标。
            _items.Update(WpfItemAccess.Capture(selector));
            ValidatePreparedTarget();
            if (_items.LocateIndex(command) != index)
                throw new InvalidOperationException("Items changed during preparation. Read getItems again.");
            token.ThrowIfCancellationRequested();
            ExecuteOnUiThread(command);
        }
        void AsRemote(Action action)
        {
            var previousSource = _activeSource;
            _activeSource = AgenticEventSource.Remote;
            try { action(); }
            finally { _activeSource = previousSource; }
        }

        await WpfItemRealization.WithContainerAsync(selector, index, ValidatePreparedTarget,
            CommitPreparedSelection, AsRemote, token);
    }

    private void SelectItem(Selector selector, AgenticCommand command)
    {
        _items.Update(WpfItemAccess.Capture(selector));
        var index = _items.ResolveIndex(command);
        var target = selector.Items[index];
        // 保留原绑定及原生 SelectionChanged，不旁路写业务对象。
        selector.SetCurrentValue(Selector.SelectedIndexProperty, index);
        selector.GetBindingExpression(Selector.SelectedIndexProperty)?.UpdateSource();
        selector.GetBindingExpression(Selector.SelectedItemProperty)?.UpdateSource();
        selector.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateSource();
        if (Validation.GetHasError(selector)) throw new InvalidOperationException("选择未通过控件绑定校验。");
        if (selector.SelectedIndex != index || !Equals(selector.SelectedItem, target))
            throw new InvalidOperationException("Selection changed during the operation. Read the current state before retrying.");
        _items.Update(WpfItemAccess.Capture(selector));
        _itemResponse = new Dictionary<string, object?> { ["itemsVersion"] = _items.Version };
    }

    private static void Select(Selector selector, object? index, object? value)
    {
        if (index is not null && int.TryParse(index.ToString(), out var parsedIndex))
        {
            if (parsedIndex < 0 || parsedIndex >= selector.Items.Count) throw new ArgumentOutOfRangeException(nameof(index));
            selector.SetCurrentValue(Selector.SelectedIndexProperty, parsedIndex);
            return;
        }

        if (value is null)
        {
            throw new ArgumentException("selectItem requires an 'index' or 'value' argument.");
        }

        foreach (var item in (IEnumerable)selector.Items)
        {
            if (string.Equals(selector is TabControl ? GetTabText(item) : item?.ToString(), value.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (item is UIElement child && !child.IsEnabled) throw new InvalidOperationException("目标选项已禁用。");
                selector.SetCurrentValue(Selector.SelectedItemProperty, item);
                return;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(value), $"Item '{value}' was not found.");
    }

    private static string? GetTabText(object? item) => item is TabItem tab ? tab.Header?.ToString() : item?.ToString();

    private static int ReadIndex(object? value)
    {
        if (value is not null && int.TryParse(value.ToString(), out var index)) return index;
        throw new ArgumentException("Action requires an index argument.");
    }

    private static void SelectGridRow(DataGrid grid, int row)
    {
        var item = GetGridItem(grid, row);
        grid.SelectedItem = item;
        grid.ScrollIntoView(item);
    }

    private void ReadGridRow(DataGrid grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var item = GetGridItem(grid, row);
        _lastCellState = new Dictionary<string, object?>
        {
            ["rowIndex"] = row,
            ["row"] = CreateGridRowState(grid, item, row)
        };
    }

    private void ReadGridRows(DataGrid grid, AgenticCommand command)
    {
        var start = ReadOptionalNonNegativeIndex(GetArgument(command, "start"), 0, "start");
        var count = ReadOptionalNonNegativeIndex(GetArgument(command, "count"), 50, "count");
        if (count > 500) throw new ArgumentOutOfRangeException(nameof(count), "getRows count cannot exceed 500.");
        var items = GetGridItems(grid);
        var rows = items.Skip(start).Take(count)
            .Select((item, offset) => CreateGridRowState(grid, item, start + offset)).ToArray();
        _lastCellState = new Dictionary<string, object?>
        {
            ["rows"] = rows,
            ["start"] = start,
            ["count"] = rows.Length,
            ["total"] = items.Count
        };
    }

    private void ReadGridColumns(DataGrid grid)
    {
        _lastCellState = new Dictionary<string, object?>
        {
            ["columns"] = grid.Columns.Select((column, index) =>
                new Dictionary<string, object?>
                {
                    ["index"] = index,
                    ["name"] = GetBindingPath(column) ?? column.Header?.ToString() ?? index.ToString(CultureInfo.InvariantCulture),
                    ["header"] = column.Header?.ToString(),
                    ["bindingPath"] = GetBindingPath(column),
                    ["readOnly"] = column.IsReadOnly,
                    ["visible"] = column.Visibility == Visibility.Visible,
                    ["sortDirection"] = column.SortDirection?.ToString()
                }).ToArray()
        };
    }

    private void ReadGridCell(DataGrid grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var column = ResolveGridColumn(grid, GetArgument(command, "column"));
        var item = GetGridItem(grid, row);
        grid.SelectedItem = item;
        var value = ReadGridValue(grid, grid.Columns[column], item);
        _lastCellState = new Dictionary<string, object?>
        {
            ["rowIndex"] = row,
            ["columnIndex"] = column,
            ["text"] = value?.ToString(),
            ["cell"] = value
        };
    }

    private void WriteGridCell(DataGrid grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var column = ResolveGridColumn(grid, GetArgument(command, "column"));
        if (grid.IsReadOnly || grid.Columns[column].IsReadOnly)
            throw new InvalidOperationException("目标单元格为只读。");
        var cell = GetGridCell(grid, row, column);
        grid.CurrentCell = new DataGridCellInfo(GetGridItem(grid, row), grid.Columns[column]);
        if (!grid.BeginEdit()) throw new InvalidOperationException("表格拒绝进入编辑状态。");
        try
        {
            if (cell.Content is not TextBox editor)
                throw new InvalidOperationException("setCell 当前只支持原生文本编辑列，其他列请使用其真实编辑控件。");
            var text = NormalizeJsonValue(GetArgument(command, "value") ?? GetArgument(command, "text"))?.ToString() ?? "";
            editor.SetCurrentValue(TextBox.TextProperty, text);
            editor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (Validation.GetHasError(editor) || !grid.CommitEdit(DataGridEditingUnit.Cell, true) ||
                !grid.CommitEdit(DataGridEditingUnit.Row, true))
                throw new InvalidOperationException("单元格或行编辑未通过原有校验。");
        }
        catch
        {
            grid.CancelEdit(DataGridEditingUnit.Cell);
            grid.CancelEdit(DataGridEditingUnit.Row);
            throw;
        }
        ReadGridCell(grid, command);
    }

    private static void ScrollGridToRow(DataGrid grid, int row)
    {
        var item = GetGridItem(grid, row);
        grid.ScrollIntoView(item);
        grid.UpdateLayout();
    }

    private void AddGridRow(DataGrid grid, AgenticCommand command)
    {
        if (grid.IsReadOnly || !grid.CanUserAddRows) throw new InvalidOperationException("表格不允许用户新增行。");
        var values = ReadObjectArgument(GetArgument(command, "values"));
        var existingItems = GetGridItems(grid);
        var itemType = GetCollectionItemType(grid.ItemsSource?.GetType()) ?? existingItems.FirstOrDefault()?.GetType();
        if (itemType is null || itemType == typeof(object))
            throw new InvalidOperationException("addRow could not infer the row type. Use a typed, writable ItemsSource.");
        var item = Activator.CreateInstance(itemType) ?? throw new InvalidOperationException($"Could not create row type '{itemType.FullName}'.");
        ApplyObjectValues(item, values, grid);
        if (grid.ItemsSource is IList source)
        {
            if (source.IsReadOnly || source.IsFixedSize)
                throw new InvalidOperationException("The DataGrid ItemsSource is read-only.");
            source.Add(item);
        }
        else if (grid.ItemsSource is null)
        {
            grid.Items.Add(item);
        }
        else
        {
            throw new InvalidOperationException("addRow requires an ItemsSource implementing IList.");
        }
        CollectionViewSource.GetDefaultView(grid.ItemsSource ?? grid.Items)?.Refresh();
        var rowIndex = GetGridItems(grid).IndexOf(item);
        grid.SelectedItem = item;
        _lastCellState = new Dictionary<string, object?>
        {
            ["rowIndex"] = rowIndex,
            ["row"] = CreateGridRowState(grid, item, rowIndex)
        };
    }

    private static void DeleteGridRow(DataGrid grid, int row)
    {
        if (grid.IsReadOnly || !grid.CanUserDeleteRows) throw new InvalidOperationException("表格不允许用户删除行。");
        var item = GetGridItem(grid, row);
        if (grid.ItemsSource is IList source)
        {
            if (source.IsReadOnly || source.IsFixedSize)
                throw new InvalidOperationException("The DataGrid ItemsSource is read-only.");
            source.Remove(item);
        }
        else if (grid.ItemsSource is null)
        {
            grid.Items.Remove(item);
        }
        else
        {
            throw new InvalidOperationException("deleteRow requires an ItemsSource implementing IList.");
        }
        CollectionViewSource.GetDefaultView(grid.ItemsSource ?? grid.Items).Refresh();
    }

    private static void SortGridByColumn(DataGrid grid, AgenticCommand command)
    {
        var column = grid.Columns[ResolveGridColumn(grid, GetArgument(command, "column"))];
        if (!grid.CanUserSortColumns || !column.CanUserSort) throw new InvalidOperationException("该列禁止用户排序。");
        var property = GetBindingPath(column);
        if (string.IsNullOrWhiteSpace(property))
            throw new InvalidOperationException("sortByColumn requires a bound DataGrid column.");
        var directionText = GetArgument(command, "direction")?.ToString() ?? "ascending";
        var direction = directionText.Equals("descending", StringComparison.OrdinalIgnoreCase) ||
                        directionText.Equals("desc", StringComparison.OrdinalIgnoreCase)
            ? ListSortDirection.Descending
            : directionText.Equals("ascending", StringComparison.OrdinalIgnoreCase) ||
              directionText.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? ListSortDirection.Ascending
                : throw new ArgumentException("direction must be 'ascending'/'asc' or 'descending'/'desc'.");
        var view = CollectionViewSource.GetDefaultView(grid.ItemsSource ?? grid.Items);
        if (!view.CanSort) throw new InvalidOperationException("The DataGrid view does not support sorting.");
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(property!, direction));
        }
        column.SortDirection = direction;
    }

    private void FilterGridByColumn(DataGrid grid, AgenticCommand command)
    {
        var column = grid.Columns[ResolveGridColumn(grid, GetArgument(command, "column"))];
        var property = GetBindingPath(column);
        if (string.IsNullOrWhiteSpace(property))
            throw new InvalidOperationException("filterByColumn requires a bound DataGrid column.");
        var view = CollectionViewSource.GetDefaultView(grid.ItemsSource ?? grid.Items);
        if (!view.CanFilter) throw new InvalidOperationException("The DataGrid view does not support filtering.");
        var filter = NormalizeJsonValue(GetArgument(command, "value"))?.ToString();
        var mode = GetArgument(command, "mode")?.ToString() ?? "contains";
        if (!_hasAgenticGridFilter)
        {
            _originalGridFilter = view.Filter;
            _hasAgenticGridFilter = true;
        }
        if (string.IsNullOrEmpty(filter))
        {
            view.Filter = _originalGridFilter;
            _originalGridFilter = null;
            _hasAgenticGridFilter = false;
            return;
        }
        var original = _originalGridFilter;
        view.Filter = item => (original?.Invoke(item) ?? true) &&
                              TextMatches(ReadBoundPropertyValue(column, item)?.ToString() ?? "", filter!, mode);
    }

    private void HighlightGridCell(DataGrid grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var column = ResolveGridColumn(grid, GetArgument(command, "column"));
        var cell = GetGridCell(grid, row, column);
        var options = AgenticGuidanceOptions.FromCommand(command, AgenticProperties.GetInstructionNumber(_element), AgenticProperties.GetHint(_element));
        var item = cell.DataContext;
        var targetColumn = cell.Column;
        _cellHighlights.Show(command, options, () => new WpfHighlight(cell),
            visual => ((WpfHighlight)visual).Retarget(cell,
                () => ReferenceEquals(cell.DataContext, item) && ReferenceEquals(cell.Column, targetColumn)));
        _lastCellState = new Dictionary<string, object?> { ["rowIndex"] = row, ["columnIndex"] = column };
    }

    private void SelectGridCell(DataGrid grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var column = ResolveGridColumn(grid, GetArgument(command, "column"));
        var item = GetGridItem(grid, row);
        ScrollGridToRow(grid, row);
        grid.SelectedCells.Clear();
        grid.SelectedItem = item;
        grid.CurrentCell = new DataGridCellInfo(item, grid.Columns[column]);
        grid.SelectedCells.Add(grid.CurrentCell);
        var value = ReadGridValue(grid, grid.Columns[column], item);
        _lastCellState = new Dictionary<string, object?>
        {
            ["rowIndex"] = row,
            ["columnIndex"] = column,
            ["text"] = value?.ToString(),
            ["cell"] = value
        };
    }

    private static DataGridCell GetGridCell(DataGrid grid, int row, int column)
    {
        var item = GetGridItem(grid, row);
        grid.ScrollIntoView(item, grid.Columns[column]);
        grid.UpdateLayout();
        var content = grid.Columns[column].GetCellContent(item)
            ?? throw new InvalidOperationException($"Cell ({row}, {column}) could not be realized.");
        for (DependencyObject? current = content; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is DataGridCell cell) return cell;
        throw new InvalidOperationException($"Cell ({row}, {column}) could not be located.");
    }

    private static List<object> GetGridItems(DataGrid grid) =>
        grid.Items.Cast<object>().Where(item => item != CollectionView.NewItemPlaceholder).ToList();

    private static object GetGridItem(DataGrid grid, int row)
    {
        var items = GetGridItems(grid);
        if (row < 0 || row >= items.Count)
            throw new ArgumentOutOfRangeException(nameof(row), $"Row '{row}' is out of range.");
        return items[row];
    }

    private static Dictionary<string, object?> CreateGridRowState(DataGrid grid, object item, int row)
    {
        var result = new Dictionary<string, object?> { ["_index"] = row };
        for (var columnIndex = 0; columnIndex < grid.Columns.Count; columnIndex++)
        {
            var column = grid.Columns[columnIndex];
            var key = GetBindingPath(column) ?? column.Header?.ToString() ?? columnIndex.ToString(CultureInfo.InvariantCulture);
            if (result.ContainsKey(key)) key = columnIndex.ToString(CultureInfo.InvariantCulture);
            result[key] = ReadGridValue(grid, column, item);
        }
        return result;
    }

    private static object? ReadGridValue(DataGrid grid, DataGridColumn column, object item)
    {
        var bound = ReadBoundPropertyValue(column, item);
        if (bound is not null) return bound;
        var content = column.GetCellContent(item);
        return content switch
        {
            TextBlock text => text.Text,
            ContentControl control => control.Content,
            _ => null
        };
    }

    private static int ResolveGridColumn(DataGrid grid, object? value)
    {
        if (value is not null && int.TryParse(value.ToString(), out var index) && index >= 0 && index < grid.Columns.Count) return index;
        var column = grid.Columns.FirstOrDefault(item =>
            string.Equals(item.Header?.ToString(), value?.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetBindingPath(item), value?.ToString(), StringComparison.OrdinalIgnoreCase));
        return column is null ? throw new ArgumentOutOfRangeException(nameof(value), $"Column '{value}' was not found.") : grid.Columns.IndexOf(column);
    }

    private static object? ReadBoundPropertyValue(DataGridColumn column, object item)
    {
        var path = GetBindingPath(column);
        return path is null ? null : item.GetType().GetProperty(path, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(item);
    }

    private static string? GetBindingPath(DataGridColumn column) =>
        column is DataGridBoundColumn { Binding: System.Windows.Data.Binding binding } ? binding.Path?.Path : null;

    private static int ReadOptionalNonNegativeIndex(object? value, int defaultValue, string name)
    {
        if (value is null) return defaultValue;
        if (int.TryParse(value.ToString(), out var parsed) && parsed >= 0) return parsed;
        throw new ArgumentException($"'{name}' must be a non-negative integer.");
    }

    private static bool TextMatches(string text, string filter, string mode) => mode.ToLowerInvariant() switch
    {
        "equals" => string.Equals(text, filter, StringComparison.OrdinalIgnoreCase),
        "startswith" => text.StartsWith(filter, StringComparison.OrdinalIgnoreCase),
        "contains" => text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0,
        _ => throw new ArgumentException("mode must be 'contains', 'equals', or 'startsWith'.")
    };

    private static IReadOnlyDictionary<string, object?> ReadObjectArgument(object? value)
    {
        if (value is null) return new Dictionary<string, object?>();
        if (value is IReadOnlyDictionary<string, object?> readOnly) return readOnly;
        if (value is IDictionary<string, object?> dictionary) return new Dictionary<string, object?>(dictionary);
        if (value is JsonElement { ValueKind: JsonValueKind.Object } json)
            return json.EnumerateObject().ToDictionary(property => property.Name, property => NormalizeJsonValue(property.Value));
        throw new ArgumentException("'values' must be a JSON object keyed by column name.");
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is not JsonElement json) return value;
        return json.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => json.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when json.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when json.TryGetDecimal(out var number) => number,
            _ => json.ToString()
        };
    }

    private static object? ConvertForType(object? value, Type targetType)
    {
        value = NormalizeJsonValue(value);
        if (value is null) return null;
        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveType.IsInstanceOfType(value)) return value;
        if (effectiveType.IsEnum) return Enum.Parse(effectiveType, value.ToString()!, true);
        if (effectiveType == typeof(Guid)) return Guid.Parse(value.ToString()!);
        return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
    }

    private static Type? GetCollectionItemType(Type? collectionType)
    {
        if (collectionType is null) return null;
        if (collectionType.IsGenericType && collectionType.GetGenericArguments().Length == 1)
            return collectionType.GetGenericArguments()[0];
        return collectionType.GetInterfaces()
            .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
            ?.GetGenericArguments()[0];
    }

    private static void ApplyObjectValues(object item, IReadOnlyDictionary<string, object?> values, DataGrid grid)
    {
        foreach (var pair in values)
        {
            var column = grid.Columns[ResolveGridColumn(grid, pair.Key)];
            var propertyName = GetBindingPath(column) ?? pair.Key;
            var property = item.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is null || !property.CanWrite)
                throw new InvalidOperationException($"Property '{propertyName}' cannot be written.");
            property.SetValue(item, ConvertForType(pair.Value, property.PropertyType));
        }
    }

    private static string? GetTreeHeader(object? item) => WpfTreeState.Header(item);

    private static MenuItem FindMenuItem(ItemCollection items, AgenticCommand command) =>
        FindMenuItemCore(items.OfType<MenuItem>(), command);

    private static MenuItem FindMenuItemCore(IEnumerable<MenuItem> items, AgenticCommand command)
    {
        var path = GetArgument(command, "path")?.ToString();
        if (!string.IsNullOrWhiteSpace(path))
        {
            IEnumerable<MenuItem> current = items;
            MenuItem? found = null;
            foreach (var part in path!.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                found = current.FirstOrDefault(item => TextEquals(item.Header?.ToString(), part))
                    ?? throw new ArgumentOutOfRangeException(nameof(path), $"Menu item '{path}' was not found.");
                current = found.Items.OfType<MenuItem>();
            }
            return found!;
        }
        var value = GetArgument(command, "value")?.ToString() ?? throw new ArgumentException("click requires path or value.");
        return FlattenMenus(items).FirstOrDefault(item => TextEquals(item.Header?.ToString(), value))
            ?? throw new ArgumentOutOfRangeException(nameof(value), $"Menu item '{value}' was not found.");
    }

    private static IEnumerable<MenuItem> FlattenMenus(IEnumerable<MenuItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in FlattenMenus(item.Items.OfType<MenuItem>())) yield return child;
        }
    }

    private static ButtonBase FindToolBarItem(ToolBar toolBar, AgenticCommand command)
    {
        var value = GetArgument(command, "path")?.ToString() ?? GetArgument(command, "value")?.ToString()
            ?? throw new ArgumentException("click requires path or value.");
        return toolBar.Items.OfType<ButtonBase>().FirstOrDefault(item => TextEquals((item as ContentControl)?.Content?.ToString(), value))
            ?? throw new ArgumentOutOfRangeException(nameof(value), $"ToolBar item '{value}' was not found.");
    }

    private static bool TextEquals(string? text, string value) =>
        string.Equals((text ?? "").Replace("_", ""), value.Replace("_", ""), StringComparison.OrdinalIgnoreCase);
}
