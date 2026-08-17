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

internal sealed class WpfControlAdapter : IAgenticControl
{
    private readonly FrameworkElement _element;
    private readonly AgenticControlRegistry _registry;
    private readonly AgenticEventBus _events;
    private string? _registeredId;
    private WpfHighlight? _highlight;
    private WpfHighlight? _cellHighlight;
    private bool _attached;
    private AgenticEventSource _activeSource = AgenticEventSource.User;
    private Dictionary<string, object?>? _lastCellState;
    private Predicate<object>? _originalGridFilter;
    private bool _hasAgenticGridFilter;

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
            return await _element.Dispatcher.InvokeAsync(
                () =>
                {
                    ExecuteOnUiThread(command);
                    return AgenticCommandResult.Success(command.RequestId, DescribeOnUiThread());
                },
                System.Windows.Threading.DispatcherPriority.Normal,
                cancellationToken);
        }
        catch (Exception exception)
        {
            return AgenticCommandResult.Failure(command.RequestId, exception.Message);
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
            Actions = GetActions(),
            State = GetState()
        };
    }

    private void ExecuteOnUiThread(AgenticCommand command)
    {
        var window = Window.GetWindow(_element);
        if (window is null ||
            !window.IsVisible ||
            !WpfDisplayability.IsInActiveInteractionScope(window))
        {
            throw new InvalidOperationException(
                "控件当前不可远程操作（被模态弹窗阻挡或不在活动窗口）。");
        }

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
                    ShowHighlight();
                    break;
                case AgenticActions.ClearHighlight:
                    RemoveHighlight();
                    break;
                case AgenticActions.Click when _element is Button button:
                    var peer = new ButtonAutomationPeer(button);
                    ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
                    break;
                case AgenticActions.Click when _element is RadioButton radioButton:
                    radioButton.IsChecked = true;
                    radioButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, radioButton));
                    break;
                case AgenticActions.Click when _element is CheckBox checkBox:
                    checkBox.IsChecked = checkBox.IsChecked switch
                    {
                        true => checkBox.IsThreeState ? null : false,
                        false => true,
                        null => false
                    };
                    checkBox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, checkBox));
                    break;
                case AgenticActions.Click when _element is ToggleButton toggleButton:
                    toggleButton.IsChecked = toggleButton.IsChecked != true;
                    toggleButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, toggleButton));
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
                    textBox.Text = GetArgument(command, "text")?.ToString() ?? "";
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
                    datePicker.SelectedDate = DateTime.Parse(GetArgument(command, "value")?.ToString() ?? throw new ArgumentException("setValue requires a 'value' argument."));
                    break;
                case AgenticActions.GetValue when _element is DatePicker:
                    break;
                case AgenticActions.SetValue when _element is Slider slider:
                    slider.Value = double.Parse(GetArgument(command, "value")?.ToString() ?? throw new ArgumentException("setValue requires a 'value' argument."));
                    break;
                case AgenticActions.GetValue when _element is Slider:
                    break;
                case AgenticActions.SetChecked when _element is ToggleButton toggle:
                    toggle.IsChecked = ReadBoolean(GetArgument(command, "checked"));
                    break;
                case AgenticActions.GetChecked when _element is ToggleButton:
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
                    FindTreeItem(tree, command).IsSelected = true;
                    break;
                case AgenticActions.Expand when _element is TreeView tree:
                    FindTreeItem(tree, command).IsExpanded = true;
                    break;
                case AgenticActions.Collapse when _element is TreeView tree:
                    FindTreeItem(tree, command).IsExpanded = false;
                    break;
                case AgenticActions.Click when _element is Menu menu:
                    FindMenuItem(menu.Items, command).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    break;
                case AgenticActions.Click when _element is ToolBar toolBar:
                    FindToolBarItem(toolBar, command).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
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
        if (_element is TreeView treeView) treeView.SelectedItemChanged += OnTreeSelectionChanged;
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
        if (_element is TreeView treeView) treeView.SelectedItemChanged -= OnTreeSelectionChanged;
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
                ["selection"] = selector is TabControl ? GetTabText(selector.SelectedItem) : selector.SelectedItem?.ToString() ?? selector.SelectedValue?.ToString()
            });
    }

    private void OnDropDownOpened(object? sender, EventArgs args) => Publish(AgenticEvents.DropDownOpened);
    private void OnDropDownClosed(object? sender, EventArgs args) => Publish(AgenticEvents.DropDownClosed);
    private void OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args) =>
        Publish(AgenticEvents.ValueChanged, data: new Dictionary<string, object?> { ["value"] = args.NewValue });
    private void OnProgressValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args) =>
        Publish(AgenticEvents.ValueChanged, data: new Dictionary<string, object?> { ["value"] = args.NewValue });
    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> args) =>
        Publish(AgenticEvents.SelectionChanged, data: new Dictionary<string, object?> { ["selection"] = GetTreeHeader(args.NewValue) });
    private void OnDateChanged(object? sender, SelectionChangedEventArgs args) =>
        Publish(AgenticEvents.ValueChanged, data: new Dictionary<string, object?> { ["value"] = ((DatePicker)_element).SelectedDate?.ToString("O") });

    private void Publish(
        string eventName,
        AgenticEventSource? source = null,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        if (_registeredId is not null)
        {
            _ = _events.PublishAsync(_registeredId, eventName, source ?? _activeSource, data);
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
            AgenticActions.ClearHighlight
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
            state["selection"] = selector is TabControl ? GetTabText(selector.SelectedItem) : selector.SelectedItem?.ToString() ?? selector.SelectedValue?.ToString();
            state["itemCount"] = selector.Items.Count;
        }
        if (_element is ComboBox comboBox)
        {
            state["text"] = comboBox.SelectedItem?.ToString() ?? comboBox.Text;
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
            state["selectedIndex"] = grid.SelectedIndex;
            state["rowCount"] = grid.Items.Count;
            state["columnCount"] = grid.Columns.Count;
        }
        if (_element is TreeView treeView)
        {
            state["text"] = GetTreeHeader(treeView.SelectedItem);
            state["selection"] = GetTreeHeader(treeView.SelectedItem);
            state["itemCount"] = treeView.Items.Count;
        }
        if (_lastCellState is not null)
            foreach (var pair in _lastCellState) state[pair.Key] = pair.Value;
        if (_element is ListBox listBox) state["text"] = listBox.SelectedItem?.ToString();
        if (_element is TabControl tabControl) state["text"] = GetTabText(tabControl.SelectedItem);
        return state;
    }

    private void ShowHighlight()
    {
        if (_highlight is not null)
        {
            return;
        }

        _highlight = new WpfHighlight(
            _element,
            AgenticProperties.GetInstructionNumber(_element),
            AgenticProperties.GetHint(_element));
        _highlight.Show();
    }

    private void RemoveHighlight()
    {
        _highlight?.Dispose();
        _highlight = null;
        _cellHighlight?.Dispose();
        _cellHighlight = null;
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

    private static void Select(Selector selector, object? index, object? value)
    {
        if (index is not null && int.TryParse(index.ToString(), out var parsedIndex))
        {
            selector.SelectedIndex = parsedIndex;
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
                selector.SelectedItem = item;
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
        var item = GetGridItem(grid, row);
        var property = GetBindingPath(grid.Columns[column]);
        if (string.IsNullOrWhiteSpace(property)) throw new InvalidOperationException("setCell requires a bound DataGrid column.");
        var member = item.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (member is null || !member.CanWrite) throw new InvalidOperationException($"Property '{property}' cannot be written.");
        var value = NormalizeJsonValue(GetArgument(command, "value") ?? GetArgument(command, "text") ?? "");
        member.SetValue(item, ConvertForType(value, member.PropertyType));
        grid.Items.Refresh();
        grid.SelectedItem = item;
        var updated = member.GetValue(item);
        _lastCellState = new Dictionary<string, object?>
        {
            ["rowIndex"] = row,
            ["columnIndex"] = column,
            ["text"] = updated?.ToString(),
            ["cell"] = updated
        };
    }

    private static void ScrollGridToRow(DataGrid grid, int row)
    {
        var item = GetGridItem(grid, row);
        grid.ScrollIntoView(item);
        grid.UpdateLayout();
    }

    private void AddGridRow(DataGrid grid, AgenticCommand command)
    {
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
        _cellHighlight?.Dispose();
        _cellHighlight = new WpfHighlight(cell, AgenticProperties.GetInstructionNumber(_element), AgenticProperties.GetHint(_element));
        _cellHighlight.Show();
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

    private static TreeViewItem FindTreeItem(TreeView tree, AgenticCommand command)
    {
        var path = GetArgument(command, "path")?.ToString();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ItemCollection items = tree.Items;
            TreeViewItem? found = null;
            foreach (var part in path!.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                found = items.OfType<TreeViewItem>().FirstOrDefault(item => string.Equals(GetTreeHeader(item), part, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentOutOfRangeException(nameof(path), $"Tree item '{path}' was not found.");
                items = found.Items;
            }
            return found!;
        }
        if (GetArgument(command, "index") is not null) return (TreeViewItem)tree.Items[ReadIndex(GetArgument(command, "index"))];
        var value = GetArgument(command, "value")?.ToString() ?? throw new ArgumentException("Tree action requires path, value, or index.");
        return tree.Items.OfType<TreeViewItem>().SelectMany(FlattenTree).FirstOrDefault(item => string.Equals(GetTreeHeader(item), value, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentOutOfRangeException(nameof(value), $"Tree item '{value}' was not found.");
    }

    private static IEnumerable<TreeViewItem> FlattenTree(TreeViewItem item)
    {
        yield return item;
        foreach (var child in item.Items.OfType<TreeViewItem>())
            foreach (var descendant in FlattenTree(child)) yield return descendant;
    }

    private static string? GetTreeHeader(object? item) => item is TreeViewItem treeItem ? treeItem.Header?.ToString() : item?.ToString();

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
