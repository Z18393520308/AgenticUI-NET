using System.Runtime.ExceptionServices;
using AgenticUI.WinForms;
using Xunit;

namespace AgenticUI.WinForms.Tests;

public sealed class ComboBoxTests
{
    [Fact]
    public void ComboBox_CanBeOpenedAndSelectedThroughSemanticCommands()
    {
        RunSta(async () =>
        {
            var id = $"test.combo.{Guid.NewGuid():N}";
            using var form = new Form
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000)
            };
            using var comboBox = new AgenticComboBox
            {
                AgenticId = id,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DataSource = new[] { "first", "second", "third" }
            };
            form.Controls.Add(comboBox);
            form.Show();
            Application.DoEvents();

            Assert.True(AgenticControlRegistry.Default.TryGet(id, out var control));
            var descriptor = control!.Describe();
            Assert.Contains(AgenticActions.Click, descriptor.Actions);
            Assert.Contains(AgenticActions.OpenDropDown, descriptor.Actions);
            Assert.Contains(AgenticActions.CloseDropDown, descriptor.Actions);
            Assert.Contains(AgenticActions.SelectItem, descriptor.Actions);

            var dispatcher = new AgenticCommandDispatcher();
            var highlighted = await dispatcher.DispatchAsync(new AgenticCommand
            {
                ControlId = id,
                Action = AgenticActions.Highlight
            });
            Assert.True(highlighted.Succeeded, highlighted.Error);

            var highlightCleared = await dispatcher.DispatchAsync(new AgenticCommand
            {
                ControlId = id,
                Action = AgenticActions.ClearHighlight
            });
            Assert.True(highlightCleared.Succeeded, highlightCleared.Error);

            var opened = await dispatcher.DispatchAsync(new AgenticCommand
            {
                ControlId = id,
                Action = AgenticActions.Click
            });
            Application.DoEvents();
            Assert.True(opened.Succeeded);
            Assert.True(comboBox.DroppedDown);

            var selected = await dispatcher.DispatchAsync(new AgenticCommand
            {
                ControlId = id,
                Action = AgenticActions.SelectItem,
                Arguments = { ["index"] = 1 }
            });
            Assert.True(selected.Succeeded);
            Assert.Equal(1, comboBox.SelectedIndex);
            Assert.Equal("second", comboBox.SelectedItem);
        });
    }

    private static void RunSta(Func<Task> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                test().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
