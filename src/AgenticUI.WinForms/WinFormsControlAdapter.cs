using System.ComponentModel;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;
using AgenticUI;

namespace AgenticUI.WinForms;

internal sealed class WinFormsControlAdapter : IAgenticControl
{
    private readonly Control _control;
    private readonly AgenticControlRegistry _registry;
    private readonly AgenticEventBus _events;
    private string? _registeredId;
    private WinFormsHighlight? _highlight;
    private bool _attached;
    private AgenticEventSource _activeSource = AgenticEventSource.User;
    private Dictionary<string, object?>? _lastCellState;
    private string? _lastStatusText;

    public WinFormsControlAdapter(
        Control control,
        AgenticControlOptions options,
        AgenticControlRegistry? registry = null,
        AgenticEventBus? events = null)
    {
        _control = control;
        Options = options;
        _registry = registry ?? AgenticControlRegistry.Default;
        _events = events ?? AgenticEventBus.Default;
    }

    public AgenticControlOptions Options { get; private set; }

    public void UpdateOptions(AgenticControlOptions options)
    {
        Options = options;
        RefreshRegistration();
    }

    public void Attach()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _control.HandleCreated += OnHandleCreated;
        _control.HandleDestroyed += OnHandleDestroyed;
        _control.Disposed += OnDisposed;
        if (_control.IsHandleCreated)
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
        _control.HandleCreated -= OnHandleCreated;
        _control.HandleDestroyed -= OnHandleDestroyed;
        _control.Disposed -= OnDisposed;
        UnhookEvents();
        RemoveHighlight();
        Unregister();
    }

    public void RefreshRegistration()
    {
        if (!_control.IsHandleCreated)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Options.Id) ||
            string.Equals(Options.Id, _registeredId, StringComparison.Ordinal))
        {
            return;
        }

        Unregister();
        Register();
    }

    public AgenticControlDescriptor Describe()
    {
        if (_control.IsDisposed)
        {
            var id = _registeredId ?? Options.Id ?? "";
            return new AgenticControlDescriptor
            {
                Id = id,
                Name = Options.DisplayName ?? id,
                Kind = "disposed",
                IsTemporaryId = id.StartsWith("temporary.", StringComparison.OrdinalIgnoreCase),
                IsSensitive = Options.IsSensitive,
                IsEnabled = false
            };
        }

        if (_control.InvokeRequired)
        {
            return (AgenticControlDescriptor)_control.Invoke(DescribeOnUiThread);
        }

        return DescribeOnUiThread();
    }

    public bool IsRemotelyDiscoverable()
    {
        if (_control.IsDisposed)
        {
            return false;
        }

        if (_control.InvokeRequired)
        {
            return (bool)_control.Invoke(new Func<bool>(() => WinFormsDisplayability.IsDisplayable(_control)));
        }

        return WinFormsDisplayability.IsDisplayable(_control);
    }

    public Task<AgenticCommandResult> ExecuteAsync(
        AgenticCommand command,
        CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<AgenticCommandResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void Execute()
        {
            try
            {
                ExecuteOnUiThread(command);
                completion.SetResult(AgenticCommandResult.Success(command.RequestId, DescribeOnUiThread()));
            }
            catch (Exception exception)
            {
                completion.SetResult(AgenticCommandResult.Failure(command.RequestId, exception.Message));
            }
        }

        if (_control.InvokeRequired)
        {
            _control.BeginInvoke((Action)Execute);
        }
        else
        {
            Execute();
        }

        return completion.Task;
    }

    private AgenticControlDescriptor DescribeOnUiThread()
    {
        var id = _registeredId ?? Options.Id ?? "";
        return new AgenticControlDescriptor
        {
            Id = id,
            Name = Options.DisplayName ?? _control.AccessibleName ?? _control.Name ?? id,
            Kind = GetKind(),
            IsTemporaryId = id.StartsWith("temporary.", StringComparison.OrdinalIgnoreCase),
            IsSensitive = Options.IsSensitive || _control is TextBox { UseSystemPasswordChar: true },
            IsEnabled = _control.Enabled,
            Actions = GetActions(),
            State = GetState()
        };
    }

    private void ExecuteOnUiThread(AgenticCommand command)
    {
        var form = _control.FindForm();
        if (form is null ||
            form.IsDisposed ||
            !WinFormsDisplayability.IsInActiveInteractionScope(form))
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
                    _control.Focus();
                    break;
                case AgenticActions.Highlight:
                    ShowHighlight();
                    break;
                case AgenticActions.ClearHighlight:
                    RemoveHighlight();
                    break;
                case AgenticActions.Click when _control is Button button:
                    button.PerformClick();
                    break;
                case AgenticActions.Click when _control is CheckBox clickedCheckBox:
                    clickedCheckBox.AccessibilityObject.DoDefaultAction();
                    break;
                case AgenticActions.Click when _control is RadioButton clickedRadioButton:
                    clickedRadioButton.PerformClick();
                    break;
                case AgenticActions.Click when _control is TextBoxBase:
                    _control.Focus();
                    break;
                case AgenticActions.Click when _control is ComboBox:
                case AgenticActions.OpenDropDown when _control is ComboBox:
                    ((ComboBox)_control).DroppedDown = true;
                    break;
                case AgenticActions.Click when _control is CheckedListBox checkedListClick:
                    ToggleCheckedListItem(checkedListClick, GetArgument(command, "index"), GetArgument(command, "value"));
                    break;
                case AgenticActions.CloseDropDown when _control is ComboBox closedComboBox:
                    closedComboBox.DroppedDown = false;
                    break;
                case AgenticActions.SetText when _control is TextBoxBase textBox:
                    textBox.Text = GetArgument(command, "text")?.ToString() ?? "";
                    break;
                case AgenticActions.GetText when _control is TextBoxBase:
                    break;
                case AgenticActions.GetText when _control is ComboBox:
                case AgenticActions.GetText when _control is ListBox:
                case AgenticActions.GetText when _control is CheckedListBox:
                    break;
                case AgenticActions.GetText when _control is CheckBox:
                    break;
                case AgenticActions.GetText when _control is RadioButton:
                    break;
                case AgenticActions.GetText when _control is StatusStrip statusStrip:
                    var statusIndex = GetArgument(command, "index");
                    _lastStatusText = statusIndex is null
                        ? string.Join(" ", statusStrip.Items.OfType<ToolStripStatusLabel>().Select(item => item.Text))
                        : (statusStrip.Items[ReadIndex(statusIndex)] as ToolStripStatusLabel)?.Text;
                    break;
                case AgenticActions.GetText when _control is Label:
                case AgenticActions.GetText when _control is ListView:
                    break;
                case AgenticActions.SetChecked when _control is CheckBox checkBox:
                    checkBox.Checked = ReadBoolean(GetArgument(command, "checked"));
                    break;
                case AgenticActions.SetChecked when _control is RadioButton radioButton:
                    radioButton.Checked = ReadBoolean(GetArgument(command, "checked"));
                    break;
                case AgenticActions.GetChecked when _control is CheckBox:
                    break;
                case AgenticActions.GetChecked when _control is RadioButton:
                    break;
                case AgenticActions.SelectItem when _control is ComboBox comboBox:
                    Select(comboBox.Items, index => comboBox.SelectedIndex = index, GetArgument(command, "index"), GetArgument(command, "value"));
                    break;
                case AgenticActions.SelectItem when _control is ListBox listBox and not CheckedListBox:
                    Select(listBox.Items, index => listBox.SelectedIndex = index, GetArgument(command, "index"), GetArgument(command, "value"));
                    break;
                case AgenticActions.SelectItem when _control is CheckedListBox checkedListSelect:
                    Select(
                        checkedListSelect.Items,
                        index => checkedListSelect.SelectedIndex = index,
                        GetArgument(command, "index"),
                        GetArgument(command, "value"));
                    break;
                case AgenticActions.SetChecked when _control is CheckedListBox checkedListBox:
                    SetCheckedListItem(
                        checkedListBox,
                        GetArgument(command, "index"),
                        GetArgument(command, "value"),
                        ReadBoolean(GetArgument(command, "checked")));
                    break;
                case AgenticActions.GetChecked when _control is CheckedListBox:
                    break;
                case AgenticActions.SelectItem when _control is TabControl tabControl:
                    Select(tabControl.TabPages, index => tabControl.SelectedIndex = index, GetArgument(command, "index"), GetArgument(command, "value"));
                    break;
                case AgenticActions.SelectItem when _control is ListView listView:
                    SelectListViewItem(listView, GetArgument(command, "index"), GetArgument(command, "value"));
                    break;
                case AgenticActions.SelectRow when _control is DataGridView grid:
                    SelectGridRow(grid, ReadIndex(GetArgument(command, "row")));
                    break;
                case AgenticActions.GetCell when _control is DataGridView grid:
                    ReadGridCell(grid, command);
                    break;
                case AgenticActions.SetCell when _control is DataGridView grid:
                    WriteGridCell(grid, command);
                    break;
                case AgenticActions.SelectItem when _control is TreeView tree:
                    SelectTreeNode(tree, command);
                    break;
                case AgenticActions.Expand when _control is TreeView tree:
                    FindTreeNode(tree, command).Expand();
                    break;
                case AgenticActions.Collapse when _control is TreeView tree:
                    FindTreeNode(tree, command).Collapse();
                    break;
                case AgenticActions.Click when _control is MenuStrip menu:
                    FindToolStripItem(menu.Items, command).PerformClick();
                    break;
                case AgenticActions.Click when _control is ToolStrip strip && _control is not StatusStrip and not MenuStrip:
                    FindToolStripItem(strip.Items, command).PerformClick();
                    break;
                case AgenticActions.GetValue when _control is ProgressBar:
                    break;
                case AgenticActions.SetValue when _control is DateTimePicker dateTimePicker:
                    dateTimePicker.Value = DateTime.Parse(GetArgument(command, "value")?.ToString() ?? throw new ArgumentException("setValue requires a 'value' argument."));
                    break;
                case AgenticActions.GetValue when _control is DateTimePicker:
                    break;
                case AgenticActions.SetValue when _control is NumericUpDown numericUpDown:
                    numericUpDown.Value = decimal.Parse(GetArgument(command, "value")?.ToString() ?? throw new ArgumentException("setValue requires a 'value' argument."));
                    break;
                case AgenticActions.GetValue when _control is NumericUpDown:
                    break;
                case AgenticActions.SetValue when _control is TrackBar trackBar:
                    trackBar.Value = int.Parse(GetArgument(command, "value")?.ToString() ?? throw new ArgumentException("setValue requires a 'value' argument."));
                    break;
                case AgenticActions.GetValue when _control is TrackBar:
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
        if (_registeredId is not null || IsDesignMode(_control))
        {
            return;
        }

        _registeredId = _registry.Register(this, Options.Id);
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
        _control.MouseDown += OnMouseDown;
        _control.MouseUp += OnMouseUp;
        _control.Enter += OnFocusEntered;
        _control.Leave += OnFocusLeft;
        _control.Click += OnClick;
        _control.TextChanged += OnTextChanged;
        if (_control is CheckBox checkBox) checkBox.CheckedChanged += OnCheckedChanged;
        if (_control is RadioButton radioButton) radioButton.CheckedChanged += OnCheckedChanged;
        if (_control is ComboBox comboBox)
        {
            comboBox.SelectedIndexChanged += OnSelectionChanged;
            comboBox.DropDown += OnDropDownOpened;
            comboBox.DropDownClosed += OnDropDownClosed;
        }
        if (_control is ListBox listBox) listBox.SelectedIndexChanged += OnSelectionChanged;
        if (_control is TabControl tabControl) tabControl.SelectedIndexChanged += OnSelectionChanged;
        if (_control is DateTimePicker dateTimePicker) dateTimePicker.ValueChanged += OnValueChanged;
        if (_control is NumericUpDown numericUpDown) numericUpDown.ValueChanged += OnValueChanged;
        if (_control is TrackBar trackBar) trackBar.ValueChanged += OnValueChanged;
        if (_control is ListView listView) listView.SelectedIndexChanged += OnSelectionChanged;
        if (_control is TreeView treeView)
        {
            treeView.AfterSelect += OnTreeSelectionChanged;
            treeView.AfterExpand += OnTreeExpanded;
            treeView.AfterCollapse += OnTreeCollapsed;
        }
    }

    private void UnhookEvents()
    {
        _control.MouseDown -= OnMouseDown;
        _control.MouseUp -= OnMouseUp;
        _control.Enter -= OnFocusEntered;
        _control.Leave -= OnFocusLeft;
        _control.Click -= OnClick;
        _control.TextChanged -= OnTextChanged;
        if (_control is CheckBox checkBox) checkBox.CheckedChanged -= OnCheckedChanged;
        if (_control is RadioButton radioButton) radioButton.CheckedChanged -= OnCheckedChanged;
        if (_control is ComboBox comboBox)
        {
            comboBox.SelectedIndexChanged -= OnSelectionChanged;
            comboBox.DropDown -= OnDropDownOpened;
            comboBox.DropDownClosed -= OnDropDownClosed;
        }
        if (_control is ListBox listBox) listBox.SelectedIndexChanged -= OnSelectionChanged;
        if (_control is TabControl tabControl) tabControl.SelectedIndexChanged -= OnSelectionChanged;
        if (_control is DateTimePicker dateTimePicker) dateTimePicker.ValueChanged -= OnValueChanged;
        if (_control is NumericUpDown numericUpDown) numericUpDown.ValueChanged -= OnValueChanged;
        if (_control is TrackBar trackBar) trackBar.ValueChanged -= OnValueChanged;
        if (_control is ListView listView) listView.SelectedIndexChanged -= OnSelectionChanged;
        if (_control is TreeView treeView)
        {
            treeView.AfterSelect -= OnTreeSelectionChanged;
            treeView.AfterExpand -= OnTreeExpanded;
            treeView.AfterCollapse -= OnTreeCollapsed;
        }
    }

    private void OnHandleCreated(object? sender, EventArgs args) => Register();
    private void OnHandleDestroyed(object? sender, EventArgs args)
    {
        UnhookEvents();
        RemoveHighlight();
        Unregister();
    }
    private void OnDisposed(object? sender, EventArgs args) => Detach();

    private static bool IsDesignMode(Control control) =>
        LicenseManager.UsageMode == LicenseUsageMode.Designtime ||
        control.Site?.DesignMode == true;
    private void OnClick(object? sender, EventArgs args)
    {
        if (_control is ButtonBase)
        {
            Publish(AgenticEvents.Clicked);
        }
    }
    private void OnMouseDown(object? sender, MouseEventArgs args) => Publish(AgenticEvents.Pressed);
    private void OnMouseUp(object? sender, MouseEventArgs args) => Publish(AgenticEvents.Released);
    private void OnFocusEntered(object? sender, EventArgs args) =>
        Publish(AgenticEvents.FocusChanged, data: new Dictionary<string, object?> { ["focused"] = true });
    private void OnFocusLeft(object? sender, EventArgs args) =>
        Publish(AgenticEvents.FocusChanged, data: new Dictionary<string, object?> { ["focused"] = false });

    private void OnTextChanged(object? sender, EventArgs args)
    {
        if (_control is TextBoxBase textBox)
        {
            Publish(
                AgenticEvents.TextChanged,
                data: new Dictionary<string, object?> { ["text"] = textBox.Text });
        }
    }

    private void OnCheckedChanged(object? sender, EventArgs args)
    {
        var value = _control switch
        {
            CheckBox checkBox => checkBox.Checked,
            RadioButton radioButton => radioButton.Checked,
            _ => false
        };
        Publish(AgenticEvents.CheckedChanged, data: new Dictionary<string, object?> { ["checked"] = value });
    }

    private void OnSelectionChanged(object? sender, EventArgs args)
    {
        var (index, selection) = _control switch
        {
            ComboBox comboBox => (comboBox.SelectedIndex, comboBox.SelectedItem?.ToString()),
            ListBox listBox => (listBox.SelectedIndex, listBox.SelectedItem?.ToString()),
            TabControl tabControl => (tabControl.SelectedIndex, tabControl.SelectedTab?.Text),
            ListView listView => (listView.SelectedIndices.Count > 0 ? listView.SelectedIndices[0] : -1, listView.SelectedItems.Count > 0 ? listView.SelectedItems[0].Text : null),
            _ => (-1, null)
        };
        Publish(
            AgenticEvents.SelectionChanged,
            data: new Dictionary<string, object?>
            {
                ["index"] = index,
                ["selection"] = selection
            });
    }
    private void OnValueChanged(object? sender, EventArgs args) =>
        Publish(AgenticEvents.ValueChanged, data: new Dictionary<string, object?> { ["value"] = GetValue() });
    private void OnTreeSelectionChanged(object? sender, TreeViewEventArgs args) =>
        Publish(AgenticEvents.SelectionChanged, data: new Dictionary<string, object?> { ["selection"] = args.Node?.Text });
    private void OnTreeExpanded(object? sender, TreeViewEventArgs args) => Publish(AgenticEvents.Expanded, data: new Dictionary<string, object?> { ["path"] = args.Node is null ? null : GetTreePath(args.Node) });
    private void OnTreeCollapsed(object? sender, TreeViewEventArgs args) => Publish(AgenticEvents.Collapsed, data: new Dictionary<string, object?> { ["path"] = args.Node is null ? null : GetTreePath(args.Node) });

    private void OnDropDownOpened(object? sender, EventArgs args) => Publish(AgenticEvents.DropDownOpened);
    private void OnDropDownClosed(object? sender, EventArgs args) => Publish(AgenticEvents.DropDownClosed);

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

    private string GetKind() => _control switch
    {
        DataGridView => "dataGridView",
        TreeView => "treeView",
        ListView => "listView",
        MenuStrip => "menuStrip",
        StatusStrip => "statusStrip",
        ToolStrip => "toolStrip",
        ProgressBar => "progressBar",
        Label => "label",
        RadioButton => "radioButton",
        CheckBox => "checkBox",
        Button => "button",
        TextBox => "textBox",
        ComboBox => "comboBox",
        DateTimePicker => "dateTimePicker",
        NumericUpDown => "numericUpDown",
        CheckedListBox => "checkedListBox",
        ListBox => "listBox",
        TabControl => "tabControl",
        TrackBar => "trackBar",
        _ => _control.GetType().Name
    };

    private IReadOnlyList<string> GetActions()
    {
        var actions = new List<string>
        {
            AgenticActions.Focus,
            AgenticActions.Highlight,
            AgenticActions.ClearHighlight
        };
        if (_control is Button or CheckBox or RadioButton or TextBoxBase) actions.Add(AgenticActions.Click);
        if (_control is TextBoxBase)
        {
            actions.Add(AgenticActions.SetText);
            actions.Add(AgenticActions.GetText);
        }
        if (_control is CheckBox or RadioButton)
        {
            actions.Add(AgenticActions.SetChecked);
            actions.Add(AgenticActions.GetChecked);
            actions.Add(AgenticActions.GetText);
        }
        if (_control is ComboBox)
        {
            actions.Add(AgenticActions.Click);
            actions.Add(AgenticActions.OpenDropDown);
            actions.Add(AgenticActions.CloseDropDown);
            actions.Add(AgenticActions.SelectItem);
            actions.Add(AgenticActions.GetText);
        }
        if (_control is ListBox and not CheckedListBox or TabControl)
        {
            actions.Add(AgenticActions.SelectItem);
            actions.Add(AgenticActions.GetText);
        }
        if (_control is CheckedListBox)
        {
            actions.Add(AgenticActions.Click);
            actions.Add(AgenticActions.SelectItem);
            actions.Add(AgenticActions.GetText);
            actions.Add(AgenticActions.SetChecked);
            actions.Add(AgenticActions.GetChecked);
        }
        if (_control is DateTimePicker or NumericUpDown or TrackBar)
        {
            actions.Add(AgenticActions.SetValue);
            actions.Add(AgenticActions.GetValue);
        }
        if (_control is ProgressBar) actions.Add(AgenticActions.GetValue);
        if (_control is Label or StatusStrip) actions.Add(AgenticActions.GetText);
        if (_control is ListView)
        {
            actions.Add(AgenticActions.SelectItem);
            actions.Add(AgenticActions.GetText);
        }
        if (_control is DataGridView)
        {
            actions.Add(AgenticActions.SelectRow);
            actions.Add(AgenticActions.GetCell);
            actions.Add(AgenticActions.SetCell);
        }
        if (_control is TreeView)
        {
            actions.Add(AgenticActions.SelectItem);
            actions.Add(AgenticActions.Expand);
            actions.Add(AgenticActions.Collapse);
        }
        if (_control is MenuStrip || (_control is ToolStrip and not StatusStrip and not MenuStrip))
        {
            actions.Add(AgenticActions.Click);
        }
        return actions;
    }

    private IReadOnlyDictionary<string, object?> GetState()
    {
        var state = new Dictionary<string, object?>
        {
            ["visible"] = _control.Visible,
            ["displayable"] = WinFormsDisplayability.IsDisplayable(_control),
            ["focused"] = _control.Focused
        };
        if (_control is TextBoxBase textBox) state["text"] = Options.IsSensitive ? null : textBox.Text;
        if (_control is CheckBox checkBox)
        {
            state["checked"] = checkBox.Checked;
            state["text"] = checkBox.Text;
        }
        if (_control is RadioButton radioButton)
        {
            state["checked"] = radioButton.Checked;
            state["text"] = radioButton.Text;
        }
        if (_control is ComboBox comboBox)
        {
            state["text"] = comboBox.SelectedItem?.ToString() ?? comboBox.Text;
            state["selectedIndex"] = comboBox.SelectedIndex;
            state["selection"] = comboBox.SelectedItem?.ToString();
            state["itemCount"] = comboBox.Items.Count;
            state["isDropDownOpen"] = comboBox.DroppedDown;
        }
        if (_control is DateTimePicker dateTimePicker)
        {
            state["value"] = dateTimePicker.Value.ToString("O");
            state["text"] = dateTimePicker.Text;
        }
        if (_control is NumericUpDown numericUpDown)
        {
            state["value"] = numericUpDown.Value;
            state["minimum"] = numericUpDown.Minimum;
            state["maximum"] = numericUpDown.Maximum;
        }
        if (_control is TrackBar trackBar)
        {
            state["value"] = trackBar.Value;
            state["minimum"] = trackBar.Minimum;
            state["maximum"] = trackBar.Maximum;
        }
        if (_control is ProgressBar progressBar)
        {
            state["value"] = progressBar.Value;
            state["minimum"] = progressBar.Minimum;
            state["maximum"] = progressBar.Maximum;
        }
        if (_control is Label label) state["text"] = label.Text;
        if (_control is StatusStrip statusStrip)
        {
            state["text"] = _lastStatusText ?? string.Join(" ", statusStrip.Items.OfType<ToolStripStatusLabel>().Select(item => item.Text));
            state["itemCount"] = statusStrip.Items.Count;
        }
        if (_control is ListView listView)
        {
            var selected = listView.SelectedItems.Count > 0 ? listView.SelectedItems[0] : null;
            state["selectedIndex"] = selected?.Index ?? -1;
            state["selection"] = selected?.Text;
            state["text"] = selected?.Text;
            state["itemCount"] = listView.Items.Count;
            state["subItems"] = selected?.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(item => item.Text).ToArray();
        }
        if (_control is TreeView treeView)
        {
            state["text"] = treeView.SelectedNode?.Text;
            state["selection"] = treeView.SelectedNode?.Text;
            state["path"] = treeView.SelectedNode is null ? null : GetTreePath(treeView.SelectedNode);
            state["itemCount"] = treeView.Nodes.Count;
        }
        if (_control is DataGridView grid)
        {
            state["selectedIndex"] = grid.CurrentRow?.Index ?? -1;
            state["rowCount"] = grid.Rows.Cast<DataGridViewRow>().Count(row => !row.IsNewRow);
            state["columnCount"] = grid.Columns.Count;
        }
        if (_lastCellState is not null)
        {
            foreach (var pair in _lastCellState) state[pair.Key] = pair.Value;
        }
        if (_control is ListBox listBox)
        {
            state["selectedIndex"] = listBox.SelectedIndex;
            state["selection"] = listBox.SelectedItem?.ToString();
            state["itemCount"] = listBox.Items.Count;
            state["text"] = listBox.SelectedItem?.ToString();
        }
        if (_control is CheckedListBox checkedListBox)
        {
            state["checkedIndices"] = checkedListBox.CheckedIndices.Cast<int>().ToArray();
            state["checked"] = checkedListBox.SelectedIndex >= 0 && checkedListBox.GetItemChecked(checkedListBox.SelectedIndex);
        }
        if (_control is TabControl tabControl)
        {
            state["selectedIndex"] = tabControl.SelectedIndex;
            state["selection"] = tabControl.SelectedTab?.Text;
            state["itemCount"] = tabControl.TabPages.Count;
            state["text"] = tabControl.SelectedTab?.Text;
        }
        return state;
    }

    private void ShowHighlight()
    {
        _highlight ??= new WinFormsHighlight(_control, Options.InstructionNumber, Options.Hint);
        _highlight.Show();
    }

    private void RemoveHighlight()
    {
        _highlight?.Dispose();
        _highlight = null;
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

    private object? GetValue() => _control switch
    {
        DateTimePicker date => date.Value.ToString("O"),
        NumericUpDown numeric => numeric.Value,
        TrackBar track => track.Value,
        _ => null
    };

    private static int ReadIndex(object? value)
    {
        if (value is not null && int.TryParse(value.ToString(), out var index)) return index;
        throw new ArgumentException("Action requires an 'index' argument.");
    }

    private static void Select(IList items, Action<int> selectIndex, object? index, object? value)
    {
        if (index is not null && int.TryParse(index.ToString(), out var parsedIndex))
        {
            selectIndex(parsedIndex);
            return;
        }

        if (value is null)
        {
            throw new ArgumentException("selectItem requires an 'index' or 'value' argument.");
        }

        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            if (string.Equals(
                    items[itemIndex]?.ToString(),
                    value.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                selectIndex(itemIndex);
                return;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(value), $"Item '{value}' was not found.");
    }

    private static void Select(TabControl.TabPageCollection pages, Action<int> selectIndex, object? index, object? value)
    {
        if (index is not null) { selectIndex(ReadIndex(index)); return; }
        if (value is null) throw new ArgumentException("selectItem requires an 'index' or 'value' argument.");
        for (var i = 0; i < pages.Count; i++)
            if (string.Equals(pages[i].Text, value.ToString(), StringComparison.OrdinalIgnoreCase)) { selectIndex(i); return; }
        throw new ArgumentOutOfRangeException(nameof(value), $"Item '{value}' was not found.");
    }

    private static void SelectListViewItem(ListView listView, object? index, object? value)
    {
        var target = index is not null
            ? listView.Items[ReadIndex(index)]
            : listView.Items.Cast<ListViewItem>().FirstOrDefault(item => string.Equals(item.Text, value?.ToString(), StringComparison.OrdinalIgnoreCase))
              ?? throw new ArgumentOutOfRangeException(nameof(value), $"Item '{value}' was not found.");
        target.Selected = true;
        target.Focused = true;
        target.EnsureVisible();
    }

    private static int ResolveCheckedListIndex(CheckedListBox list, object? index, object? value)
    {
        if (index is not null)
        {
            return ReadIndex(index);
        }

        if (value is not null)
        {
            for (var i = 0; i < list.Items.Count; i++)
            {
                if (string.Equals(list.Items[i]?.ToString(), value.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(value), $"Item '{value}' was not found.");
        }

        if (list.SelectedIndex >= 0)
        {
            return list.SelectedIndex;
        }

        if (list.Items.Count > 0)
        {
            return 0;
        }

        throw new InvalidOperationException("CheckedListBox has no items.");
    }

    private static void SetCheckedListItem(CheckedListBox list, object? index, object? value, bool isChecked)
    {
        var target = ResolveCheckedListIndex(list, index, value);
        if (target < 0 || target >= list.Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index '{target}' is out of range.");
        }

        list.SetItemChecked(target, isChecked);
        list.SelectedIndex = target;
    }

    private static void ToggleCheckedListItem(CheckedListBox list, object? index, object? value)
    {
        var target = ResolveCheckedListIndex(list, index, value);
        if (target < 0 || target >= list.Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index '{target}' is out of range.");
        }

        list.SetItemChecked(target, !list.GetItemChecked(target));
        list.SelectedIndex = target;
    }

    private static void SelectGridRow(DataGridView grid, int row)
    {
        if (row < 0 || row >= grid.Rows.Count || grid.Rows[row].IsNewRow) throw new ArgumentOutOfRangeException(nameof(row));
        grid.ClearSelection();
        grid.Rows[row].Selected = true;
        if (grid.Columns.Count > 0) grid.CurrentCell = grid.Rows[row].Cells[0];
    }

    private void ReadGridCell(DataGridView grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var column = ResolveGridColumn(grid, GetArgument(command, "column"));
        var cell = grid.Rows[row].Cells[column];
        grid.CurrentCell = cell;
        _lastCellState = new Dictionary<string, object?> { ["text"] = cell.Value?.ToString(), ["cell"] = cell.Value };
    }

    private void WriteGridCell(DataGridView grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var column = ResolveGridColumn(grid, GetArgument(command, "column"));
        var cell = grid.Rows[row].Cells[column];
        cell.Value = GetArgument(command, "value") ?? GetArgument(command, "text");
        grid.CurrentCell = cell;
        _lastCellState = new Dictionary<string, object?> { ["text"] = cell.Value?.ToString(), ["cell"] = cell.Value };
    }

    private static int ResolveGridColumn(DataGridView grid, object? value)
    {
        if (value is not null && int.TryParse(value.ToString(), out var index) && index >= 0 && index < grid.Columns.Count) return index;
        var column = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(item =>
            string.Equals(item.Name, value?.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.HeaderText, value?.ToString(), StringComparison.OrdinalIgnoreCase));
        return column?.Index ?? throw new ArgumentOutOfRangeException(nameof(value), $"Column '{value}' was not found.");
    }

    private static TreeNode FindTreeNode(TreeView tree, AgenticCommand command)
    {
        var path = GetArgument(command, "path")?.ToString();
        if (!string.IsNullOrWhiteSpace(path))
        {
            TreeNodeCollection nodes = tree.Nodes;
            TreeNode? current = null;
            foreach (var part in path!.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = nodes.Cast<TreeNode>().FirstOrDefault(node => string.Equals(node.Text, part, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentOutOfRangeException(nameof(path), $"Tree node '{path}' was not found.");
                nodes = current.Nodes;
            }
            return current!;
        }
        if (GetArgument(command, "index") is not null) return tree.Nodes[ReadIndex(GetArgument(command, "index"))];
        var value = GetArgument(command, "value")?.ToString() ?? throw new ArgumentException("Tree action requires 'path', 'value', or 'index'.");
        return tree.Nodes.Cast<TreeNode>().SelectMany(Flatten).FirstOrDefault(node => string.Equals(node.Text, value, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentOutOfRangeException(nameof(value), $"Tree node '{value}' was not found.");
    }

    private static IEnumerable<TreeNode> Flatten(TreeNode node)
    {
        yield return node;
        foreach (TreeNode child in node.Nodes)
            foreach (var descendant in Flatten(child)) yield return descendant;
    }

    private static void SelectTreeNode(TreeView tree, AgenticCommand command)
    {
        var node = FindTreeNode(tree, command);
        tree.SelectedNode = node;
        node.EnsureVisible();
    }

    private static string GetTreePath(TreeNode node)
    {
        var parts = new Stack<string>();
        for (var current = node; current is not null; current = current.Parent) parts.Push(current.Text);
        return string.Join("/", parts);
    }

    private static ToolStripItem FindToolStripItem(ToolStripItemCollection items, AgenticCommand command)
    {
        var path = GetArgument(command, "path")?.ToString();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ToolStripItemCollection current = items;
            ToolStripItem? found = null;
            var parts = path!.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                found = current.Cast<ToolStripItem>().FirstOrDefault(item => TextEquals(item.Text, part))
                    ?? throw new ArgumentOutOfRangeException(nameof(path), $"Menu item '{path}' was not found.");
                if (i < parts.Length - 1)
                    current = found is ToolStripDropDownItem dropDown ? dropDown.DropDownItems : throw new ArgumentException($"'{part}' has no submenu.");
            }
            return found!;
        }
        var value = GetArgument(command, "value")?.ToString() ?? throw new ArgumentException("click requires a 'path' or 'value' argument.");
        return FlattenItems(items).FirstOrDefault(item => TextEquals(item.Text, value))
            ?? throw new ArgumentOutOfRangeException(nameof(value), $"Menu item '{value}' was not found.");
    }

    private static IEnumerable<ToolStripItem> FlattenItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            yield return item;
            if (item is ToolStripDropDownItem dropDown)
                foreach (var child in FlattenItems(dropDown.DropDownItems)) yield return child;
        }
    }

    private static bool TextEquals(string? text, string value) =>
        string.Equals((text ?? "").Replace("&", ""), value.Replace("&", ""), StringComparison.OrdinalIgnoreCase);
}
