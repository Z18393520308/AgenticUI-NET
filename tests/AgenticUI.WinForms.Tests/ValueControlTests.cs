using System.Runtime.ExceptionServices;
using AgenticUI.WinForms;
using Xunit;

namespace AgenticUI.WinForms.Tests;

public sealed class ValueControlTests
{
    [Fact]
    public void NumericUpDown_SetValueAndGetValue()
    {
        RunSta(async () =>
        {
            var id = $"test.numeric.{Guid.NewGuid():N}";
            using var form = CreateForm();
            using var numeric = new AgenticNumericUpDown { AgenticId = id, Minimum = 0, Maximum = 100 };
            form.Controls.Add(numeric); form.Show(); Application.DoEvents();
            var result = await Dispatch(id, AgenticActions.SetValue, 42);
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(42, numeric.Value);
            Assert.Equal(42m, Assert.IsType<decimal>(result.Control!.State["value"]));
        });
    }

    [Fact]
    public void ListBox_SelectItem()
    {
        RunSta(async () =>
        {
            var id = $"test.list.{Guid.NewGuid():N}";
            using var form = CreateForm();
            using var list = new AgenticListBox { AgenticId = id };
            list.Items.AddRange(new object[] { "北京", "上海" }); form.Controls.Add(list); form.Show(); Application.DoEvents();
            var result = await Dispatch(id, AgenticActions.SelectItem, "上海", "value");
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(1, list.SelectedIndex);
        });
    }

    [Fact]
    public void DateTimePicker_SetValue()
    {
        RunSta(async () =>
        {
            var id = $"test.date.{Guid.NewGuid():N}";
            using var form = CreateForm();
            using var picker = new AgenticDateTimePicker { AgenticId = id };
            form.Controls.Add(picker); form.Show(); Application.DoEvents();
            var result = await Dispatch(id, AgenticActions.SetValue, "2026-08-01");
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(new DateTime(2026, 8, 1), picker.Value.Date);
        });
    }

    private static Form CreateForm() => new() { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) };
    private static Task<AgenticCommandResult> Dispatch(string id, string action, object? value, string key = "value") =>
        new AgenticCommandDispatcher().DispatchAsync(new AgenticCommand { ControlId = id, Action = action, Arguments = new() { [key] = value } });
    private static void RunSta(Func<Task> test)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { test().GetAwaiter().GetResult(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
