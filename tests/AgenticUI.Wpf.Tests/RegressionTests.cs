using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using AgenticUI.Wpf;
using Xunit;

namespace AgenticUI.Wpf.Tests;

public sealed class RegressionTests
{
    [Fact]
    public void SliderValueIgnoresSystemCulture() => RunSta(async () =>
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        var slider = new AgenticSlider { Minimum = 0, Maximum = 100, Width = 200, Height = 30 };
        AgenticProperties.SetId(slider, NewId("slider"));
        var window = Show(slider);
        try
        {
            var result = await Execute(slider, AgenticActions.SetValue, ("value", Json("1.5")));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(1.5, slider.Value);
        }
        finally { CultureInfo.CurrentCulture = previous; window.Close(); }
    });

    [Fact]
    public void NestedMenuItemCanBeClickedWhileSubmenuIsClosed() => RunSta(async () =>
    {
        var menu = new AgenticMenu { Width = 300, Height = 30 };
        AgenticProperties.SetId(menu, NewId("menu"));
        var file = new MenuItem { Header = "文件" };
        var open = new MenuItem { Header = "打开" };
        var locked = new MenuItem { Header = "锁定", IsEnabled = false };
        var clicks = 0;
        open.Click += (_, _) => clicks++;
        locked.Click += (_, _) => clicks += 100;
        file.Items.Add(open); file.Items.Add(locked); menu.Items.Add(file);
        var window = Show(menu);
        try
        {
            Assert.False(file.IsSubmenuOpen);
            var result = await Execute(menu, AgenticActions.Click, ("path", "文件/打开"));
            Assert.True(result.Succeeded, result.Error);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal(1, clicks);
            Assert.False((await Execute(menu, AgenticActions.Click, ("path", "文件/锁定"))).Succeeded);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal(1, clicks);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void DataTableGridSupportsReadFilterAndAdd() => RunSta(async () =>
    {
        var table = new DataTable();
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Score", typeof(int));
        for (var index = 0; index < 200; index++) table.Rows.Add("Row" + index, index);
        table.Rows.Add("50%", 999);
        var grid = new AgenticDataGrid { ItemsSource = table.DefaultView, AutoGenerateColumns = true, Width = 400, Height = 150 };
        AgenticProperties.SetId(grid, NewId("grid"));
        var window = Show(grid);
        try
        {
            // 视口外（未生成行容器）的行也必须读到真实值，而不是 null。
            var rows = await Execute(grid, AgenticActions.GetRows, ("start", 190), ("count", 5));
            Assert.True(rows.Succeeded, rows.Error);
            var page = Assert.IsType<Dictionary<string, object?>[]>(rows.Control!.State["rows"]);
            Assert.Equal("Row190", page[0]["Name"]);
            Assert.Equal(190, page[0]["Score"]);

            Assert.Null(grid.SelectedItem);
            Assert.True((await Execute(grid, AgenticActions.GetCell, ("row", 3), ("column", "Name"))).Succeeded);
            Assert.Null(grid.SelectedItem);

            var filtered = await Execute(grid, AgenticActions.FilterByColumn, ("column", "Name"), ("value", "50%"), ("mode", "equals"));
            Assert.True(filtered.Succeeded, filtered.Error);
            Assert.Equal(1, grid.Items.Cast<object>().Count(item => item is DataRowView));
            Assert.True((await Execute(grid, AgenticActions.FilterByColumn, ("column", "Name"), ("value", ""))).Succeeded);

            var added = await Execute(grid, AgenticActions.AddRow, ("values", Json("""{"Name":"New","Score":7}""")));
            Assert.True(added.Succeeded, added.Error);
            Assert.Equal(202, table.Rows.Count);
            var rejected = await Execute(grid, AgenticActions.AddRow, ("values", Json("""{"Name":"Bad","Score":"x"}""")));
            Assert.False(rejected.Succeeded);
            Assert.Equal(202, table.Rows.Count);
            Assert.False(((IEditableCollectionView)CollectionViewSource.GetDefaultView(table.DefaultView)).IsAddingNew);
        }
        finally { window.Close(); }
    });

    private static string NewId(string kind) => $"regression.{kind}.{Guid.NewGuid():N}";
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static Window Show(FrameworkElement element)
    {
        var window = new Window { Content = element, Width = 500, Height = 300, ShowInTaskbar = false };
        window.Show(); window.UpdateLayout(); return window;
    }

    private static Task<AgenticCommandResult> Execute(FrameworkElement element, string action, params (string Key, object? Value)[] args) =>
        new AgenticCommandDispatcher().DispatchAsync(new AgenticCommand
        { ControlId = AgenticProperties.GetId(element)!, Action = action, Arguments = args.ToDictionary(arg => arg.Key, arg => arg.Value) });

    private static void RunSta(Func<Task> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await test(); } catch (Exception exception) { failure = exception; }
                finally { dispatcher.InvokeShutdown(); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(20))) throw new TimeoutException("WPF regression test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
