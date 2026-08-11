using System.Runtime.ExceptionServices;
using AgenticUI.WinForms;
using Xunit;

namespace AgenticUI.WinForms.Tests;

public sealed class DisplayabilityTests
{
    [Fact]
    public void InactiveTabPage_ControlsAreNotRemotelyDiscoverable()
    {
        RunSta(() =>
        {
            var visibleId = $"demo.visible.{Guid.NewGuid():N}";
            var hiddenId = $"demo.hidden.{Guid.NewGuid():N}";
            using var form = new Form
            {
                ShowInTaskbar = false,
                TopMost = true,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(120, 120),
                Size = new Size(420, 320)
            };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var pageA = new TabPage("A");
            var pageB = new TabPage("B");
            var visibleButton = new AgenticButton
            {
                AgenticId = visibleId,
                Text = "可见",
                Location = new Point(16, 16),
                Size = new Size(100, 30)
            };
            var hiddenButton = new AgenticButton
            {
                AgenticId = hiddenId,
                Text = "隐藏",
                Location = new Point(16, 16),
                Size = new Size(100, 30)
            };
            pageA.Controls.Add(visibleButton);
            pageB.Controls.Add(hiddenButton);
            tabs.TabPages.Add(pageA);
            tabs.TabPages.Add(pageB);
            form.Controls.Add(tabs);
            form.Show();
            form.Activate();

            // Inactive TabPage children may not create handles until selected once.
            tabs.SelectedTab = pageB;
            Application.DoEvents();
            tabs.SelectedTab = pageA;
            Application.DoEvents();

            Assert.True(AgenticControlRegistry.Default.TryGet(visibleId, out var visible));
            Assert.True(AgenticControlRegistry.Default.TryGet(hiddenId, out var hidden));
            Assert.True(visible!.IsRemotelyDiscoverable());
            Assert.False(hidden!.IsRemotelyDiscoverable());

            var all = AgenticControlRegistry.Default.Snapshot();
            var discoverable = AgenticControlRegistry.Default.Snapshot(remotelyDiscoverableOnly: true);
            Assert.Contains(all, item => item.Id == hiddenId);
            Assert.DoesNotContain(discoverable, item => item.Id == hiddenId);
            Assert.Contains(discoverable, item => item.Id == visibleId);
        });
    }

    [Fact]
    public void ModalDialog_RestrictsRemoteDiscoveryToDialogControls()
    {
        RunSta(() =>
        {
            var mainId = $"main.action.{Guid.NewGuid():N}";
            var dialogId = $"dialog.ok.{Guid.NewGuid():N}";
            using var main = new Form
            {
                ShowInTaskbar = false,
                TopMost = true,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(100, 100),
                Size = new Size(480, 360)
            };
            var mainButton = new AgenticButton
            {
                AgenticId = mainId,
                Text = "主窗按钮",
                Location = new Point(20, 20),
                Size = new Size(120, 32)
            };
            main.Controls.Add(mainButton);
            main.Show();
            main.Activate();
            Application.DoEvents();

            Assert.True(AgenticControlRegistry.Default.TryGet(mainId, out var mainControl));
            Assert.True(mainControl!.IsRemotelyDiscoverable());

            var asserted = false;
            using var dialog = new Form
            {
                ShowInTaskbar = false,
                TopMost = true,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(180, 180),
                Size = new Size(320, 200),
                FormBorderStyle = FormBorderStyle.FixedDialog
            };
            var okButton = new AgenticButton
            {
                AgenticId = dialogId,
                Text = "确定",
                Location = new Point(40, 40),
                Size = new Size(100, 32)
            };
            dialog.Controls.Add(okButton);
            dialog.Shown += (_, _) =>
            {
                dialog.Activate();
                Application.DoEvents();
                Assert.True(AgenticControlRegistry.Default.TryGet(dialogId, out var dialogControl));
                Assert.True(dialogControl!.IsRemotelyDiscoverable());
                Assert.False(mainControl.IsRemotelyDiscoverable());

                var discoverable = AgenticControlRegistry.Default.Snapshot(remotelyDiscoverableOnly: true);
                Assert.Contains(discoverable, item => item.Id == dialogId);
                Assert.DoesNotContain(discoverable, item => item.Id == mainId);
                asserted = true;
                dialog.Close();
            };

            dialog.ShowDialog(main);
            Assert.True(asserted);
            Application.DoEvents();
            Assert.True(mainControl.IsRemotelyDiscoverable());
        });
    }

    private static void RunSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
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
