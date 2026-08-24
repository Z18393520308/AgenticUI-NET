using System.ComponentModel;
using System.Collections;
using System.Drawing;
using System.Globalization;
using System.Text.Json;
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
    private WinFormsHighlight? _cellHighlight;
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
                case AgenticActions.MouseMove:
                case AgenticActions.MouseClick:
                case AgenticActions.MouseDoubleClick:
                case AgenticActions.MouseWheel:
                case AgenticActions.MouseDrag:
                    WinFormsMouseInput.Execute(_control, command);
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
                case AgenticActions.GetRow when _control is DataGridView grid:
                    ReadGridRow(grid, command);
                    break;
                case AgenticActions.GetRows when _control is DataGridView grid:
                    ReadGridRows(grid, command);
                    break;
                case AgenticActions.GetColumns when _control is DataGridView grid:
                    ReadGridColumns(grid);
                    break;
                case AgenticActions.GetCell when _control is DataGridView grid:
                    ReadGridCell(grid, command);
                    break;
                case AgenticActions.SetCell when _control is DataGridView grid:
                    WriteGridCell(grid, command);
                    break;
                case AgenticActions.ScrollToRow when _control is DataGridView grid:
                    ScrollGridToRow(grid, ReadIndex(GetArgument(command, "row")));
                    break;
                case AgenticActions.AddRow when _control is DataGridView grid:
                    AddGridRow(grid, command);
                    break;
                case AgenticActions.DeleteRow when _control is DataGridView grid:
                    DeleteGridRow(grid, ReadIndex(GetArgument(command, "row")));
                    break;
                case AgenticActions.SortByColumn when _control is DataGridView grid:
                    SortGridByColumn(grid, command);
                    break;
                case AgenticActions.FilterByColumn when _control is DataGridView grid:
                    FilterGridByColumn(grid, command);
                    break;
                case AgenticActions.HighlightCell when _control is DataGridView grid:
                    HighlightGridCell(grid, command);
                    break;
                case AgenticActions.SelectCell when _control is DataGridView grid:
                    SelectGridCell(grid, command);
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
            AgenticActions.ClearHighlight,
            AgenticActions.MouseMove,
            AgenticActions.MouseClick,
            AgenticActions.MouseDoubleClick,
            AgenticActions.MouseWheel,
            AgenticActions.MouseDrag
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
            var rows = GetGridRows(grid);
            state["selectedIndex"] = grid.CurrentRow is null ? -1 : rows.IndexOf(grid.CurrentRow);
            state["rowCount"] = rows.Count;
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
        var target = GetGridRow(grid, row);
        grid.ClearSelection();
        target.Selected = true;
        if (grid.Columns.Count > 0) grid.CurrentCell = target.Cells[0];
    }

    private void ReadGridRow(DataGridView grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var target = GetGridRow(grid, row);
        _lastCellState = new Dictionary<string, object?>
        {
            ["rowIndex"] = row,
            ["row"] = CreateGridRowState(grid, target, row)
        };
    }

    private void ReadGridRows(DataGridView grid, AgenticCommand command)
    {
        var start = ReadOptionalNonNegativeIndex(GetArgument(command, "start"), 0, "start");
        var count = ReadOptionalNonNegativeIndex(GetArgument(command, "count"), 50, "count");
        if (count > 500) throw new ArgumentOutOfRangeException(nameof(count), "getRows count cannot exceed 500.");
        var dataRows = GetGridRows(grid);
        var rows = dataRows.Skip(start).Take(count)
            .Select((row, offset) => CreateGridRowState(grid, row, start + offset)).ToArray();
        _lastCellState = new Dictionary<string, object?>
        {
            ["rows"] = rows,
            ["start"] = start,
            ["count"] = rows.Length,
            ["total"] = dataRows.Count
        };
    }

    private void ReadGridColumns(DataGridView grid)
    {
        _lastCellState = new Dictionary<string, object?>
        {
            ["columns"] = grid.Columns.Cast<DataGridViewColumn>().Select(column =>
                new Dictionary<string, object?>
                {
                    ["index"] = column.Index,
                    ["name"] = column.Name,
                    ["header"] = column.HeaderText,
                    ["dataProperty"] = column.DataPropertyName,
                    ["valueType"] = column.ValueType?.FullName,
                    ["readOnly"] = column.ReadOnly,
                    ["visible"] = column.Visible,
                    ["sortDirection"] = column.HeaderCell.SortGlyphDirection.ToString()
                }).ToArray()
        };
    }

    private void ReadGridCell(DataGridView grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var column = ResolveGridColumn(grid, GetArgument(command, "column"));
        var cell = GetGridRow(grid, row).Cells[column];
        grid.CurrentCell = cell;
        _lastCellState = new Dictionary<string, object?>
        {
            ["rowIndex"] = row,
            ["columnIndex"] = column,
            ["text"] = cell.Value?.ToString(),
            ["cell"] = cell.Value
        };
    }

    private void WriteGridCell(DataGridView grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var column = ResolveGridColumn(grid, GetArgument(command, "column"));
        var cell = GetGridRow(grid, row).Cells[column];
        var value = NormalizeJsonValue(GetArgument(command, "value") ?? GetArgument(command, "text"));
        cell.Value = ConvertForType(value, cell.ValueType ?? grid.Columns[column].ValueType);
        grid.CurrentCell = cell;
        _lastCellState = new Dictionary<string, object?>
        {
            ["rowIndex"] = row,
            ["columnIndex"] = column,
            ["text"] = cell.Value?.ToString(),
            ["cell"] = cell.Value
        };
    }

    private static void ScrollGridToRow(DataGridView grid, int row)
    {
        var target = GetGridRow(grid, row);
        grid.FirstDisplayedScrollingRowIndex = target.Index;
    }

    private void AddGridRow(DataGridView grid, AgenticCommand command)
    {
        var values = ReadObjectArgument(GetArgument(command, "values"));
        DataGridViewRow? addedRow;
        if (grid.DataSource is BindingSource source)
        {
            var item = source.AddNew() ?? throw new InvalidOperationException("The bound data source could not create a row.");
            ApplyObjectValues(item, values, grid);
            source.EndEdit();
            grid.Refresh();
            addedRow = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row => ReferenceEquals(row.DataBoundItem, item));
        }
        else if (grid.DataSource is null)
        {
            var sourceIndex = grid.Rows.Add();
            addedRow = grid.Rows[sourceIndex];
            foreach (var pair in values)
            {
                var column = ResolveGridColumn(grid, pair.Key);
                var cell = addedRow.Cells[column];
                cell.Value = ConvertForType(pair.Value, cell.ValueType ?? grid.Columns[column].ValueType);
            }
        }
        else
        {
            throw new InvalidOperationException("addRow requires an unbound grid or a BindingSource data source.");
        }

        var rowIndex = addedRow is null ? -1 : GetGridRows(grid).IndexOf(addedRow);
        if (rowIndex >= 0) SelectGridRow(grid, rowIndex);
        _lastCellState = new Dictionary<string, object?>
        {
            ["rowIndex"] = rowIndex,
            ["filteredOut"] = rowIndex < 0,
            ["row"] = addedRow is null ? null : CreateGridRowState(grid, addedRow, rowIndex)
        };
    }

    private static void DeleteGridRow(DataGridView grid, int row)
    {
        var target = GetGridRow(grid, row);
        if (grid.DataSource is BindingSource source)
        {
            if (target.DataBoundItem is not null) source.Remove(target.DataBoundItem);
            else source.RemoveAt(row);
            return;
        }
        if (grid.DataSource is not null)
            throw new InvalidOperationException("deleteRow requires an unbound grid or a BindingSource data source.");
        grid.Rows.Remove(target);
    }

    private static void SortGridByColumn(DataGridView grid, AgenticCommand command)
    {
        var column = grid.Columns[ResolveGridColumn(grid, GetArgument(command, "column"))];
        var directionText = GetArgument(command, "direction")?.ToString() ?? "ascending";
        var direction = directionText.Equals("descending", StringComparison.OrdinalIgnoreCase) ||
                        directionText.Equals("desc", StringComparison.OrdinalIgnoreCase)
            ? ListSortDirection.Descending
            : directionText.Equals("ascending", StringComparison.OrdinalIgnoreCase) ||
              directionText.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? ListSortDirection.Ascending
                : throw new ArgumentException("direction must be 'ascending'/'asc' or 'descending'/'desc'.");
        if (column.SortMode == DataGridViewColumnSortMode.NotSortable)
            throw new InvalidOperationException($"Column '{column.HeaderText}' is not sortable.");
        grid.Sort(column, direction);
    }

    private static void FilterGridByColumn(DataGridView grid, AgenticCommand command)
    {
        var column = grid.Columns[ResolveGridColumn(grid, GetArgument(command, "column"))];
        var rawValue = NormalizeJsonValue(GetArgument(command, "value"));
        var filter = rawValue?.ToString();
        var mode = GetArgument(command, "mode")?.ToString() ?? "contains";
        if (grid.DataSource is BindingSource source)
        {
            if (!source.SupportsFiltering)
                throw new InvalidOperationException("The bound data source does not support filtering.");
            if (string.IsNullOrEmpty(filter))
            {
                source.RemoveFilter();
                return;
            }
            var property = string.IsNullOrWhiteSpace(column.DataPropertyName) ? column.Name : column.DataPropertyName;
            var escaped = filter!.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
            source.Filter = mode.ToLowerInvariant() switch
            {
                "equals" => $"CONVERT([{property}], 'System.String') = '{escaped}'",
                "startswith" => $"CONVERT([{property}], 'System.String') LIKE '{escaped}%'",
                "contains" => $"CONVERT([{property}], 'System.String') LIKE '%{escaped}%'",
                _ => throw new ArgumentException("mode must be 'contains', 'equals', or 'startsWith'.")
            };
            return;
        }
        if (grid.DataSource is not null)
            throw new InvalidOperationException("filterByColumn requires an unbound grid or a filterable BindingSource.");
        grid.ClearSelection();
        grid.CurrentCell = null;
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            var text = row.Cells[column.Index].Value?.ToString() ?? "";
            row.Visible = string.IsNullOrEmpty(filter) || TextMatches(text, filter!, mode);
        }
    }

    private void HighlightGridCell(DataGridView grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var column = ResolveGridColumn(grid, GetArgument(command, "column"));
        var target = GetGridRow(grid, row);
        ScrollGridToRow(grid, row);
        _cellHighlight?.Dispose();
        _cellHighlight = new WinFormsHighlight(
            grid,
            Options.InstructionNumber,
            Options.Hint,
            () =>
            {
                if (target.Index < 0) return Rectangle.Empty;
                var bounds = grid.GetCellDisplayRectangle(column, target.Index, true);
                return bounds.Width <= 0 || bounds.Height <= 0
                    ? Rectangle.Empty
                    : grid.RectangleToScreen(bounds);
            });
        _cellHighlight.Show();
        _lastCellState = new Dictionary<string, object?> { ["rowIndex"] = row, ["columnIndex"] = column };
    }

    private void SelectGridCell(DataGridView grid, AgenticCommand command)
    {
        var row = ReadIndex(GetArgument(command, "row"));
        var column = ResolveGridColumn(grid, GetArgument(command, "column"));
        var target = GetGridRow(grid, row);
        ScrollGridToRow(grid, row);
        grid.ClearSelection();
        var cell = target.Cells[column];
        grid.CurrentCell = cell;
        cell.Selected = true;
        _lastCellState = new Dictionary<string, object?>
        {
            ["rowIndex"] = row,
            ["columnIndex"] = column,
            ["text"] = cell.Value?.ToString(),
            ["cell"] = cell.Value
        };
    }

    private static Dictionary<string, object?> CreateGridRowState(DataGridView grid, DataGridViewRow row, int viewIndex)
    {
        var result = new Dictionary<string, object?>
        {
            ["_index"] = viewIndex,
            ["_sourceIndex"] = row.Index,
            ["_visible"] = row.Visible
        };
        foreach (DataGridViewColumn column in grid.Columns)
        {
            var key = GetGridColumnKey(column);
            if (result.ContainsKey(key)) key = column.Index.ToString(CultureInfo.InvariantCulture);
            result[key] = row.Cells[column.Index].Value;
        }
        return result;
    }

    private static string GetGridColumnKey(DataGridViewColumn column) =>
        !string.IsNullOrWhiteSpace(column.Name) ? column.Name :
        !string.IsNullOrWhiteSpace(column.DataPropertyName) ? column.DataPropertyName :
        !string.IsNullOrWhiteSpace(column.HeaderText) ? column.HeaderText :
        column.Index.ToString(CultureInfo.InvariantCulture);

    private static List<DataGridViewRow> GetGridRows(DataGridView grid) =>
        grid.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow && row.Visible).ToList();

    private static DataGridViewRow GetGridRow(DataGridView grid, int row)
    {
        var rows = GetGridRows(grid);
        if (row < 0 || row >= rows.Count)
            throw new ArgumentOutOfRangeException(nameof(row), $"Row '{row}' is out of range.");
        return rows[row];
    }

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

    private static object? ConvertForType(object? value, Type? targetType)
    {
        value = NormalizeJsonValue(value);
        if (value is null || targetType is null || targetType == typeof(object)) return value;
        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveType.IsInstanceOfType(value)) return value;
        if (effectiveType.IsEnum) return Enum.Parse(effectiveType, value.ToString()!, true);
        if (effectiveType == typeof(Guid)) return Guid.Parse(value.ToString()!);
        return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
    }

    private static void ApplyObjectValues(object item, IReadOnlyDictionary<string, object?> values, DataGridView grid)
    {
        foreach (var pair in values)
        {
            var column = grid.Columns[ResolveGridColumn(grid, pair.Key)];
            var propertyName = string.IsNullOrWhiteSpace(column.DataPropertyName) ? pair.Key : column.DataPropertyName;
            var property = item.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
            if (property is null || !property.CanWrite)
                throw new InvalidOperationException($"Property '{propertyName}' cannot be written.");
            property.SetValue(item, ConvertForType(pair.Value, property.PropertyType));
        }
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
