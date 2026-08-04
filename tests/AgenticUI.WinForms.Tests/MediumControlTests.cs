using System.Runtime.ExceptionServices;
using AgenticUI.WinForms;
using Xunit;

namespace AgenticUI.WinForms.Tests;

public sealed class MediumControlTests
{
    [Fact]
    public void MediumControls_ExecuteCommands()
    {
        RunSta(async () =>
        {
            using var form = new Form { ShowInTaskbar = false, Location = new Point(-2000, -2000) };
            var label = new AgenticLabel { AgenticId = "medium.label", Text = "就绪" };
            var progress = new AgenticProgressBar { AgenticId = "medium.progress", Value = 35 };
            var tree = new AgenticTreeView { AgenticId = "medium.tree" }; var root = tree.Nodes.Add("根"); root.Nodes.Add("子");
            var grid = new AgenticDataGridView { AgenticId = "medium.grid", AllowUserToAddRows = false }; grid.Columns.Add("Name", "Name"); grid.Rows.Add("Alice");
            var list = new AgenticListView { AgenticId = "medium.list", View = View.Details }; list.Columns.Add("名称"); list.Items.Add("Alice");
            form.Controls.AddRange(new Control[] { label, progress, tree, grid, list }); form.Show(); Application.DoEvents();
            var dispatcher = new AgenticCommandDispatcher();
            Assert.Equal("就绪", (await dispatcher.DispatchAsync(new AgenticCommand { ControlId = "medium.label", Action = AgenticActions.GetText })).Control!.State["text"]);
            Assert.Equal(35, (await dispatcher.DispatchAsync(new AgenticCommand { ControlId = "medium.progress", Action = AgenticActions.GetValue })).Control!.State["value"]);
            Assert.True((await dispatcher.DispatchAsync(new AgenticCommand { ControlId = "medium.tree", Action = AgenticActions.Expand, Arguments = new() { ["path"] = "根" } })).Succeeded);
            Assert.True((await dispatcher.DispatchAsync(new AgenticCommand { ControlId = "medium.tree", Action = AgenticActions.SelectItem, Arguments = new() { ["path"] = "根/子" } })).Succeeded);
            Assert.True((await dispatcher.DispatchAsync(new AgenticCommand { ControlId = "medium.grid", Action = AgenticActions.SetCell, Arguments = new() { ["row"] = 0, ["column"] = 0, ["text"] = "Bob" } })).Succeeded);
            Assert.Equal("Bob", (await dispatcher.DispatchAsync(new AgenticCommand { ControlId = "medium.grid", Action = AgenticActions.GetCell, Arguments = new() { ["row"] = 0, ["column"] = 0 } })).Control!.State["text"]);
            Assert.True((await dispatcher.DispatchAsync(new AgenticCommand { ControlId = "medium.list", Action = AgenticActions.SelectItem, Arguments = new() { ["value"] = "Alice" } })).Succeeded);
        });
    }

    private static void RunSta(Func<Task> test)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { test().GetAwaiter().GetResult(); } catch (Exception exception) { failure = exception; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
