using System.Reflection;
using System.Runtime.ExceptionServices;
using AgenticUI.WinForms;
using Xunit;

namespace AgenticUI.WinForms.Tests;

public sealed class GuidanceAndSafetyTests
{
    [Fact]
    public void BubbleUpdatesWithoutChangingControlAndSessionCleanupIsIsolated() => RunSta(() =>
    {
        using var form = new Form { ClientSize = new Size(500, 300), StartPosition = FormStartPosition.CenterScreen };
        var button = new AgenticButton { AgenticId = "forms.guidance", Hint = "预设", Text = "保存", Location = new Point(40, 40) };
        form.Controls.Add(button); form.Show(); Application.DoEvents();
        var clicks = 0; button.Click += (_, _) => clicks++;
        var command = new AgenticCommand { ControlId = button.AgenticId!, Action = "highlight", SessionId = "one",
            Arguments = { ["showBubble"] = true, ["hint"] = "远程提示" } };
        var dispatcher = new AgenticCommandDispatcher();
        var result = dispatcher.DispatchAsync(command).GetAwaiter().GetResult();
        Assert.True(result.Succeeded, result.Error);
        var overlay = Assert.Single(form.OwnedForms);
        var field = overlay.GetType().GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal("远程提示", ((AgenticGuidanceOptions)field.GetValue(overlay)!).Hint);
        command.Arguments["showBubble"] = false;
        Assert.True(dispatcher.DispatchAsync(command).GetAwaiter().GetResult().Succeeded);
        Assert.Same(overlay, Assert.Single(form.OwnedForms));
        Assert.False(((AgenticGuidanceOptions)field.GetValue(overlay)!).ShowBubble);
        Assert.Equal("预设", button.Hint); Assert.Equal(0, clicks);
        AgenticControlRegistry.Default.ClearGuidanceAsync("another").GetAwaiter().GetResult();
        Assert.Single(form.OwnedForms);
        AgenticControlRegistry.Default.ClearGuidanceAsync("one").GetAwaiter().GetResult();
        Assert.Empty(form.OwnedForms);
    });

    [Fact]
    public void CellGuidanceRetargetsInPlaceAndClosesWhenTargetColumnIsHidden() => RunSta(() =>
    {
        using var form = new Form { ClientSize = new Size(650, 350), StartPosition = FormStartPosition.CenterScreen };
        var grid = new AgenticDataGridView { AgenticId = "forms.cell.guidance", Dock = DockStyle.Fill, AllowUserToAddRows = false };
        grid.Columns.Add("Name", "名称"); grid.Columns.Add("Value", "值");
        grid.Rows.Add("第一行", "1"); grid.Rows.Add("第二行", "2");
        form.Controls.Add(grid); form.Show(); Application.DoEvents();
        var dispatcher = new AgenticCommandDispatcher();
        var command = new AgenticCommand { ControlId = grid.AgenticId!, Action = "highlightCell", SessionId = "cell-session",
            Arguments = { ["row"] = 0, ["column"] = "Name", ["showBubble"] = true, ["hint"] = "第一处" } };
        var result = dispatcher.DispatchAsync(command).GetAwaiter().GetResult();
        Assert.True(result.Succeeded, result.Error);
        var overlay = Assert.Single(form.OwnedForms);
        var oldLocation = overlay.Location;
        command.Arguments["row"] = 1; command.Arguments["column"] = "Value"; command.Arguments["hint"] = "第二处";
        result = dispatcher.DispatchAsync(command).GetAwaiter().GetResult();
        Assert.True(result.Succeeded, result.Error);
        Assert.Same(overlay, Assert.Single(form.OwnedForms));
        Assert.NotEqual(oldLocation, overlay.Location);
        grid.Columns["Value"].Visible = false; Application.DoEvents();
        Assert.Empty(form.OwnedForms);
    });

    [Fact]
    public void TreeReportsSelectionAndExpansionAsSeparateLiveStates() => RunSta(() =>
    {
        using var form = new Form { ClientSize = new Size(500, 350), StartPosition = FormStartPosition.CenterScreen };
        var tree = new AgenticTreeView { AgenticId = "forms.tree.contract", Dock = DockStyle.Fill };
        var root = tree.Nodes.Add("公司"); var research = root.Nodes.Add("研发");
        var design = research.Nodes.Add("设计"); var sales = root.Nodes.Add("销售"); sales.Nodes.Add("订单");
        form.Controls.Add(tree); form.Show(); root.Expand(); research.Expand(); Application.DoEvents();
        var observed = new List<AgenticEvent>();
        using var subscription = AgenticEventBus.Default.Subscribe(message =>
        { if (message.ControlId == tree.AgenticId) observed.Add(message); return default; });
        tree.SelectedNode = design;
        Assert.Equal("公司/研发/设计", Assert.Single(observed.Where(message => message.Name == "selectionChanged")).Data["path"]);
        var result = new AgenticCommandDispatcher().DispatchAsync(new AgenticCommand { ControlId = tree.AgenticId!, Action = "expand",
            Arguments = { ["path"] = "公司/销售" } }).GetAwaiter().GetResult();
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("公司/研发/设计", result.Control!.State["path"]);
        Assert.Equal("公司/销售", result.Control.State["expansionPath"]);
        Assert.Equal(true, result.Control.State["expanded"]);
        Assert.Equal(AgenticEventSource.Remote, Assert.Single(observed.Where(message => message.Name == "expanded")).Source);
        sales.Collapse();
        Assert.Equal(AgenticEventSource.User, Assert.Single(observed.Where(message => message.Name == "collapsed")).Source);
        root.Nodes.Add("销售");
        Assert.True(AgenticControlRegistry.Default.TryGet(tree.AgenticId!, out var control));
        Assert.Null(control!.Describe().State["expansionPath"]);
    });

    [Fact]
    public void PasswordMaskIsSensitiveAndReadOnlyTextCannotBeWritten() => RunSta(() =>
    {
        using var form = new Form();
        var text = new AgenticTextBox { AgenticId = "forms.password", PasswordChar = '*', Text = "secret", ReadOnly = true };
        form.Controls.Add(text); form.Show(); Application.DoEvents();
        Assert.True(AgenticControlRegistry.Default.TryGet(text.AgenticId!, out var control));
        Assert.True(control!.Describe().IsSensitive); Assert.Null(control.Describe().State["text"]);
        Assert.Equal(true, AgenticPrivacy.SanitizeDescriptor(control.Describe()).State["readOnly"]);
        var result = new AgenticCommandDispatcher().DispatchAsync(new AgenticCommand { ControlId = text.AgenticId!,
            Action = "setText", Arguments = { ["text"] = "new" } }).GetAwaiter().GetResult();
        Assert.False(result.Succeeded); Assert.Equal("secret", text.Text);
    });

    private static void RunSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { test(); } catch (Exception exception) { failure = exception; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(20))) throw new TimeoutException("WinForms UI test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
