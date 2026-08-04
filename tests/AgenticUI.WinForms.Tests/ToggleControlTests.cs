using System.Runtime.ExceptionServices;
using AgenticUI.WinForms;
using Xunit;

namespace AgenticUI.WinForms.Tests;

public sealed class ToggleControlTests
{
    [Fact]
    public void RadioButton_ClickSelectsTheOption()
    {
        RunSta(async () =>
        {
            var safeId = $"test.radio.safe.{Guid.NewGuid():N}";
            var normalId = $"test.radio.normal.{Guid.NewGuid():N}";
            using var form = new Form
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000)
            };
            using var safe = new AgenticRadioButton { AgenticId = safeId, Text = "安全模式" };
            using var normal = new AgenticRadioButton { AgenticId = normalId, Text = "普通模式", Checked = true };
            var clickCount = 0;
            safe.Click += (_, _) => clickCount++;
            form.Controls.Add(safe);
            form.Controls.Add(normal);
            form.Show();
            Application.DoEvents();

            Assert.True(AgenticControlRegistry.Default.TryGet(safeId, out var control));
            Assert.Contains(AgenticActions.Click, control!.Describe().Actions);
            Assert.Contains(AgenticActions.SetChecked, control.Describe().Actions);

            var dispatcher = new AgenticCommandDispatcher();
            var clicked = await dispatcher.DispatchAsync(new AgenticCommand
            {
                ControlId = safeId,
                Action = AgenticActions.Click
            });
            Application.DoEvents();

            Assert.True(clicked.Succeeded, clicked.Error);
            Assert.True(safe.Checked);
            Assert.False(normal.Checked);
            Assert.Equal(1, clickCount);
        });
    }

    [Fact]
    public void TextBox_ClickFocusesTheControl()
    {
        RunSta(async () =>
        {
            var id = $"test.text.{Guid.NewGuid():N}";
            using var form = new Form
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000)
            };
            using var other = new Button { Text = "other", TabIndex = 0 };
            using var textBox = new AgenticTextBox { AgenticId = id, TabIndex = 1 };
            form.Controls.Add(other);
            form.Controls.Add(textBox);
            form.Show();
            other.Focus();
            Application.DoEvents();
            Assert.False(textBox.Focused);

            Assert.True(AgenticControlRegistry.Default.TryGet(id, out var control));
            Assert.Contains(AgenticActions.Click, control!.Describe().Actions);

            var dispatcher = new AgenticCommandDispatcher();
            var clicked = await dispatcher.DispatchAsync(new AgenticCommand
            {
                ControlId = id,
                Action = AgenticActions.Click
            });
            Application.DoEvents();

            Assert.True(clicked.Succeeded, clicked.Error);
            Assert.True(textBox.Focused);
        });
    }

    [Fact]
    public void TextBox_GetTextReturnsCurrentValue()
    {
        RunSta(async () =>
        {
            var id = $"test.text.get.{Guid.NewGuid():N}";
            using var form = new Form
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000)
            };
            using var textBox = new AgenticTextBox { AgenticId = id, Text = "hello-agent" };
            form.Controls.Add(textBox);
            form.Show();
            Application.DoEvents();

            Assert.True(AgenticControlRegistry.Default.TryGet(id, out var control));
            Assert.Contains(AgenticActions.GetText, control!.Describe().Actions);

            var dispatcher = new AgenticCommandDispatcher();
            var result = await dispatcher.DispatchAsync(new AgenticCommand
            {
                ControlId = id,
                Action = AgenticActions.GetText
            });

            Assert.True(result.Succeeded, result.Error);
            Assert.NotNull(result.Control);
            Assert.True(result.Control!.State.TryGetValue("text", out var text));
            Assert.Equal("hello-agent", text?.ToString());
        });
    }

    [Fact]
    public void CheckBox_GetCheckedReturnsCurrentValue()
    {
        RunSta(async () =>
        {
            var id = $"test.check.get.{Guid.NewGuid():N}";
            using var form = new Form
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000)
            };
            using var checkBox = new AgenticCheckBox { AgenticId = id, Text = "记住我", Checked = true };
            form.Controls.Add(checkBox);
            form.Show();
            Application.DoEvents();

            Assert.True(AgenticControlRegistry.Default.TryGet(id, out var control));
            Assert.Contains(AgenticActions.GetChecked, control!.Describe().Actions);
            Assert.Contains(AgenticActions.GetText, control.Describe().Actions);

            var dispatcher = new AgenticCommandDispatcher();
            var result = await dispatcher.DispatchAsync(new AgenticCommand
            {
                ControlId = id,
                Action = AgenticActions.GetChecked
            });

            Assert.True(result.Succeeded, result.Error);
            Assert.NotNull(result.Control);
            Assert.True(result.Control!.State.TryGetValue("checked", out var checkedValue));
            Assert.True(checkedValue is true || string.Equals(checkedValue?.ToString(), "True", StringComparison.OrdinalIgnoreCase));
            Assert.True(result.Control.State.TryGetValue("text", out var text));
            Assert.Equal("记住我", text?.ToString());
        });
    }

    [Fact]
    public void CheckBox_ClickTogglesCheckedState()
    {
        RunSta(async () =>
        {
            var id = $"test.check.{Guid.NewGuid():N}";
            using var form = new Form
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000)
            };
            using var checkBox = new AgenticCheckBox { AgenticId = id, Text = "记住我" };
            var clickCount = 0;
            checkBox.Click += (_, _) => clickCount++;
            var clickedEvent = new TaskCompletionSource<AgenticEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = AgenticEventBus.Default.Subscribe(message =>
            {
                if (message.ControlId == id && message.Name == AgenticEvents.Clicked)
                {
                    clickedEvent.TrySetResult(message);
                }

                return ValueTask.CompletedTask;
            });
            form.Controls.Add(checkBox);
            form.Show();
            Application.DoEvents();

            Assert.True(AgenticControlRegistry.Default.TryGet(id, out var control));
            Assert.Contains(AgenticActions.Click, control!.Describe().Actions);

            var dispatcher = new AgenticCommandDispatcher();
            var first = await dispatcher.DispatchAsync(new AgenticCommand
            {
                ControlId = id,
                Action = AgenticActions.Click
            });
            Application.DoEvents();
            Assert.True(first.Succeeded, first.Error);
            Assert.True(checkBox.Checked);
            Assert.Equal(1, clickCount);
            var firstClickEvent = await clickedEvent.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(AgenticEventSource.Remote, firstClickEvent.Source);

            var second = await dispatcher.DispatchAsync(new AgenticCommand
            {
                ControlId = id,
                Action = AgenticActions.Click
            });
            Application.DoEvents();
            Assert.True(second.Succeeded, second.Error);
            Assert.False(checkBox.Checked);
            Assert.Equal(2, clickCount);
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
