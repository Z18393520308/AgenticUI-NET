using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using AgenticUI.WinForms;
using Xunit;

namespace AgenticUI.WinForms.Tests;

public sealed class RegressionTests
{
    [Fact]
    public void Grid_HiddenFirstColumnAndPureGetCell() => RunSta(async () =>
    {
        using var form = NewForm();
        var grid = NewGrid();
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name" });
        grid.Rows.Add("1", "Alice");
        grid.Rows.Add("2", "Bob");
        form.Controls.Add(grid); form.Show(); Application.DoEvents();

        var selected = await Dispatch(grid, AgenticActions.SelectRow, ("row", 1));
        Assert.True(selected.Succeeded, selected.Error);
        Assert.Equal(1, grid.CurrentCell!.RowIndex);
        Assert.Equal(1, grid.CurrentCell.ColumnIndex);

        var hidden = await Dispatch(grid, AgenticActions.GetCell, ("row", 0), ("column", "Id"));
        Assert.True(hidden.Succeeded, hidden.Error);
        Assert.Equal("1", hidden.Control!.State["text"]);
        Assert.Equal(1, grid.CurrentCell!.RowIndex);
        // 读取结果只随本次命令返回，后续描述不再携带旧的行/单元格数据。
        Assert.False((await Dispatch(grid, AgenticActions.Focus)).Control!.State.ContainsKey("cell"));

        var added = await Dispatch(grid, AgenticActions.AddRow, ("values", new Dictionary<string, object?> { ["Id"] = "3", ["Name"] = "Carol" }));
        Assert.True(added.Succeeded, added.Error);
        Assert.Equal(3, grid.Rows.Cast<DataGridViewRow>().Count(row => !row.IsNewRow));
    });

    [Fact]
    public void Grid_FailedAddRowLeavesNoRow() => RunSta(async () =>
    {
        using var form = NewForm();
        var unbound = NewGrid();
        unbound.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name" });
        unbound.Columns.Add(new DataGridViewTextBoxColumn { Name = "Score", ValueType = typeof(int) });
        unbound.Rows.Add("Alice", 1);
        var people = new BindingList<Person> { new() { Name = "Alice", Score = 1 } };
        var bound = NewGrid();
        bound.AutoGenerateColumns = true;
        bound.DataSource = new BindingSource { DataSource = people };
        bound.Top = 210;
        form.Controls.Add(unbound); form.Controls.Add(bound); form.Show(); Application.DoEvents();

        var badColumn = await Dispatch(unbound, AgenticActions.AddRow, ("values", new Dictionary<string, object?> { ["Name"] = "Bob", ["Missing"] = 1 }));
        Assert.False(badColumn.Succeeded);
        Assert.Equal(1, unbound.Rows.Cast<DataGridViewRow>().Count(row => !row.IsNewRow));

        var badValue = await Dispatch(bound, AgenticActions.AddRow, ("values", new Dictionary<string, object?> { ["Name"] = "Bob", ["Score"] = "not-a-number" }));
        Assert.False(badValue.Succeeded);
        Assert.Single(people);
        bound.CurrentCell = bound.Rows[0].Cells[0];
        Application.DoEvents();
        Assert.Single(people);
    });

    [Fact]
    public void Grid_DataTableBindingSourceSupportsAddRow() => RunSta(async () =>
    {
        using var form = NewForm();
        var table = new DataTable();
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Score", typeof(int));
        table.Rows.Add("Alice", 1);
        var grid = NewGrid();
        grid.AutoGenerateColumns = true;
        grid.DataSource = new BindingSource { DataSource = table };
        form.Controls.Add(grid); form.Show(); Application.DoEvents();

        var added = await Dispatch(grid, AgenticActions.AddRow, ("values", Json("""{"Name":"Bob","Score":2}""")));
        Assert.True(added.Succeeded, added.Error);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.Rows[1]["Score"]);
    });

    [Fact]
    public void Menu_DisabledItemIsNotReportedAsClicked() => RunSta(async () =>
    {
        using var form = NewForm();
        var menu = new AgenticMenuStrip { AgenticId = "regression.menu." + Guid.NewGuid().ToString("N") };
        var file = new ToolStripMenuItem("&File");
        var open = new ToolStripMenuItem("&Open");
        var save = new ToolStripMenuItem("&Save") { Enabled = false };
        var clicks = 0;
        open.Click += (_, _) => clicks++;
        save.Click += (_, _) => clicks += 100;
        file.DropDownItems.Add(open); file.DropDownItems.Add(save);
        menu.Items.Add(file);
        form.Controls.Add(menu); form.MainMenuStrip = menu; form.Show(); Application.DoEvents();

        Assert.True((await Dispatch(menu, AgenticActions.Click, ("path", "File/Open"))).Succeeded);
        Assert.False((await Dispatch(menu, AgenticActions.Click, ("path", "File/Save"))).Succeeded);
        Assert.Equal(1, clicks);
    });

    [Fact]
    public void NumericValueIgnoresSystemCulture() => RunSta(async () =>
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            using var form = NewForm();
            var numeric = new AgenticNumericUpDown { AgenticId = "regression.numeric." + Guid.NewGuid().ToString("N"), DecimalPlaces = 1, Maximum = 100 };
            form.Controls.Add(numeric); form.Show(); Application.DoEvents();
            var result = await Dispatch(numeric, AgenticActions.SetValue, ("value", Json("1.5")));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(1.5m, numeric.Value);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    });

    [Fact]
    public void CommandForDisposedControlCompletes() => RunSta(async () =>
    {
        using var form = NewForm();
        var button = new AgenticButton { AgenticId = "regression.button." + Guid.NewGuid().ToString("N"), Text = "OK" };
        form.Controls.Add(button); form.Show(); Application.DoEvents();
        var id = button.AgenticId!;
        Assert.True(AgenticControlRegistry.Default.TryGet(id, out var adapter));
        form.Close();
        button.Dispose();
        var result = await Task.Run(() => adapter!.ExecuteAsync(new AgenticCommand { ControlId = id, Action = AgenticActions.Click }))
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Succeeded);
    });

    public sealed class Person
    {
        public string Name { get; set; } = "";
        public int Score { get; set; }
    }

    private static Form NewForm() => new() { ShowInTaskbar = false, Location = new Point(20, 20), Width = 500, Height = 500 };
    private static AgenticDataGridView NewGrid() => new()
    {
        AgenticId = "regression.grid." + Guid.NewGuid().ToString("N"),
        AllowUserToAddRows = true, Width = 400, Height = 200
    };
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static Task<AgenticCommandResult> Dispatch(IAgenticWinFormsControl control, string action, params (string Key, object? Value)[] arguments) =>
        new AgenticCommandDispatcher().DispatchAsync(new AgenticCommand
        {
            ControlId = control.AgenticId!,
            Action = action,
            Arguments = arguments.ToDictionary(argument => argument.Key, argument => argument.Value)
        });

    private static void RunSta(Func<Task> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { test().GetAwaiter().GetResult(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
