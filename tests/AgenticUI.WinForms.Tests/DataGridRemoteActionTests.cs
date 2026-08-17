using System.Runtime.ExceptionServices;
using AgenticUI.WinForms;
using Xunit;

namespace AgenticUI.WinForms.Tests;

public sealed class DataGridRemoteActionTests
{
    [Fact]
    public void DataGridView_ExecutesExtendedRemoteActions()
    {
        RunSta(async () =>
        {
            using var form = new Form { ShowInTaskbar = false, Location = new Point(-2000, -2000) };
            var grid = new AgenticDataGridView
            {
                AgenticId = "grid.extended",
                AllowUserToAddRows = false,
                Width = 400,
                Height = 200
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name", SortMode = DataGridViewColumnSortMode.Automatic });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Score", HeaderText = "Score", ValueType = typeof(int), SortMode = DataGridViewColumnSortMode.Automatic });
            grid.Rows.Add("Bob", 20);
            grid.Rows.Add("Alice", 30);
            form.Controls.Add(grid);
            form.Show();
            Application.DoEvents();

            var dispatcher = new AgenticCommandDispatcher();
            var columns = await Dispatch(dispatcher, AgenticActions.GetColumns);
            var descriptor = columns.Control!;
            Assert.Contains(AgenticActions.GetRow, descriptor.Actions);
            Assert.Contains(AgenticActions.GetRows, descriptor.Actions);
            Assert.Contains(AgenticActions.GetColumns, descriptor.Actions);
            Assert.Contains(AgenticActions.ScrollToRow, descriptor.Actions);
            Assert.Contains(AgenticActions.AddRow, descriptor.Actions);
            Assert.Contains(AgenticActions.DeleteRow, descriptor.Actions);
            Assert.Contains(AgenticActions.SortByColumn, descriptor.Actions);
            Assert.Contains(AgenticActions.FilterByColumn, descriptor.Actions);
            Assert.Contains(AgenticActions.HighlightCell, descriptor.Actions);
            Assert.Contains(AgenticActions.SelectCell, descriptor.Actions);

            Assert.Equal(2, Assert.IsType<Dictionary<string, object?>[]>(columns.Control!.State["columns"]).Length);

            var row = await Dispatch(dispatcher, AgenticActions.GetRow, ("row", 1));
            Assert.Equal("Alice", Assert.IsType<Dictionary<string, object?>>(row.Control!.State["row"])["Name"]);

            var rows = await Dispatch(dispatcher, AgenticActions.GetRows, ("start", 0), ("count", 1));
            Assert.Equal(1, rows.Control!.State["count"]);
            Assert.Single(Assert.IsType<Dictionary<string, object?>[]>(rows.Control.State["rows"]));

            var added = await Dispatch(
                dispatcher,
                AgenticActions.AddRow,
                ("values", new Dictionary<string, object?> { ["Name"] = "Carol", ["Score"] = 10 }));
            Assert.Equal(3, added.Control!.State["rowCount"]);

            Assert.True((await Dispatch(dispatcher, AgenticActions.SortByColumn, ("column", "Score"), ("direction", "ascending"))).Succeeded);
            Assert.Equal("Carol", grid.Rows[0].Cells["Name"].Value);

            Assert.True((await Dispatch(dispatcher, AgenticActions.FilterByColumn, ("column", "Name"), ("value", "ali"))).Succeeded);
            Assert.False(grid.Rows.Cast<DataGridViewRow>().Single(item => Equals(item.Cells["Name"].Value, "Bob")).Visible);
            Assert.True((await Dispatch(dispatcher, AgenticActions.FilterByColumn, ("column", "Name"), ("value", ""))).Succeeded);

            Assert.True((await Dispatch(dispatcher, AgenticActions.ScrollToRow, ("row", 1))).Succeeded);
            Assert.True((await Dispatch(dispatcher, AgenticActions.SelectCell, ("row", 1), ("column", "Score"))).Succeeded);
            Assert.Equal(1, grid.CurrentCell!.RowIndex);
            Assert.Equal(1, grid.CurrentCell.ColumnIndex);

            Assert.True((await Dispatch(dispatcher, AgenticActions.HighlightCell, ("row", 1), ("column", "Name"))).Succeeded);
            Assert.True((await Dispatch(dispatcher, AgenticActions.ClearHighlight)).Succeeded);

            Assert.True((await Dispatch(dispatcher, AgenticActions.DeleteRow, ("row", 2))).Succeeded);
            Assert.Equal(2, grid.Rows.Count);
        });
    }

    private static Task<AgenticCommandResult> Dispatch(
        AgenticCommandDispatcher dispatcher,
        string action,
        params (string Key, object? Value)[] arguments)
    {
        return dispatcher.DispatchAsync(new AgenticCommand
        {
            ControlId = "grid.extended",
            Action = action,
            Arguments = arguments.ToDictionary(argument => argument.Key, argument => argument.Value)
        });
    }

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
