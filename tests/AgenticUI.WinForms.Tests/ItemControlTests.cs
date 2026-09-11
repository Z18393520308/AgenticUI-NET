using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using AgenticUI.WinForms;
using Xunit;

namespace AgenticUI.WinForms.Tests;

public sealed class ItemControlTests
{
    [Fact]
    public void BoundComboBoxUsesDisplayMemberForReadSelectStateAndEvents() => RunSta(async () =>
    {
        using var combo = NewCombo();
        combo.DataSource = new[] { new Book("BK-002", "流浪地球"), new Book("BK-001", "三体") };
        using var form = Show(combo);
        var events = new List<AgenticEvent>();
        using var subscription = AgenticEventBus.Default.Subscribe(message =>
        { if (message.ControlId == combo.AgenticId) events.Add(message); return default; });
        var page = await Execute(combo, AgenticActions.GetItems);
        Assert.True(page.Succeeded, page.Error);
        Assert.Contains(AgenticItemCollection.Capability, page.Control!.Capabilities);
        var state = State(page);
        Assert.Equal("三体", state.GetProperty("items")[1].GetProperty("text").GetString());
        Assert.Equal("BK-001", state.GetProperty("items")[1].GetProperty("itemKey").GetString());
        Assert.DoesNotContain("private-business-field", state.ToString());
        var result = await Execute(combo, AgenticActions.SelectItem, ("value", "三体"));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal("BK-001", combo.SelectedValue);
        Assert.Equal("三体", result.Control!.State["text"]);
        Assert.Equal("三体", result.Control.State["selection"]);
        Assert.Equal("BK-001", result.Control.State["itemKey"]);
        var selection = Assert.Single(events.Where(e => e.Name == AgenticEvents.SelectionChanged));
        Assert.Equal(AgenticEventSource.Remote, selection.Source);
        Assert.Equal("三体", selection.Data["selection"]);
        Assert.Equal("BK-001", selection.Data["itemKey"]);
        Assert.False((await Execute(combo, AgenticActions.GetText)).Control!.State.ContainsKey("items"));
    });

    [Fact]
    public void ListRefreshRejectsOldIndexAndKeySurvivesReordering() => RunSta(async () =>
    {
        using var combo = NewCombo();
        var books = new BindingList<Book> { new("BK-001", "三体"), new("BK-002", "流浪地球") };
        combo.DataSource = books;
        using var form = Show(combo);
        var version = State(await Execute(combo, AgenticActions.GetItems)).GetProperty("itemsVersion").GetString();
        books.Insert(0, new Book("BK-003", "球状闪电"));
        var rejected = await Execute(combo, AgenticActions.SelectItem, ("index", 0), ("itemsVersion", version));
        Assert.False(rejected.Succeeded);
        Assert.Contains("stale", rejected.Error);
        var result = await Execute(combo, AgenticActions.SelectItem, ("itemKey", "BK-001"));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, combo.SelectedIndex);
    });

    [Fact]
    public void EmptyThenLoadedAndDuplicateLabelsAreHandledWithoutGuessing() => RunSta(async () =>
    {
        using var combo = NewCombo();
        var books = new BindingList<Book>();
        combo.DataSource = books;
        using var form = Show(combo);
        Assert.Equal(0, (await Execute(combo, AgenticActions.GetItems)).Control!.State["total"]);
        books.Add(new Book("first-edition", "三体"));
        books.Add(new Book("second-edition", "三体"));
        var original = combo.SelectedIndex;
        var rejected = await Execute(combo, AgenticActions.SelectItem, ("value", "三体"));
        Assert.False(rejected.Succeeded); Assert.Contains("Ambiguous", rejected.Error);
        Assert.Equal(original, combo.SelectedIndex);
        Assert.True((await Execute(combo, AgenticActions.SelectItem, ("itemKey", "second-edition"))).Succeeded);
        Assert.Equal(1, combo.SelectedIndex);
    });

    [Fact]
    public void FormattedTextMatchesWhatTheUserSees() => RunSta(async () =>
    {
        using var combo = NewCombo();
        combo.FormattingEnabled = true;
        combo.Format += (_, e) => { if (e.ListItem is Book book) e.Value = book.Id + " " + book.Name; };
        combo.DataSource = new[] { new Book("BK-001", "三体") };
        using var form = Show(combo);
        Assert.Equal("BK-001 三体", State(await Execute(combo, AgenticActions.GetItems)).GetProperty("items")[0].GetProperty("text").GetString());
        Assert.False((await Execute(combo, AgenticActions.SelectItem, ("value", "三体"))).Succeeded);
        Assert.True((await Execute(combo, AgenticActions.SelectItem, ("value", "BK-001 三体"))).Succeeded);
        Assert.True((await Execute(combo, AgenticActions.SelectItem, ("itemKey", "BK-001"))).Succeeded);
    });

    [Fact]
    public void DataTableBindingExposesOnlyDisplayAndScalarValueMembers() => RunSta(async () =>
    {
        using var table = new System.Data.DataTable();
        table.Columns.Add("Id", typeof(int)); table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Secret", typeof(string)); table.Rows.Add(42, "三体", "private-data-row");
        using var combo = NewCombo(); combo.DataSource = table;
        using var form = Show(combo);
        var state = State(await Execute(combo, AgenticActions.GetItems));
        Assert.Equal("三体", state.GetProperty("items")[0].GetProperty("text").GetString());
        Assert.Equal("42", state.GetProperty("items")[0].GetProperty("itemKey").GetString());
        Assert.DoesNotContain("private-data-row", state.ToString());
        Assert.True((await Execute(combo, AgenticActions.SelectItem, ("itemKey", "42"))).Succeeded);
        Assert.Equal(42, combo.SelectedValue);
    });

    [Fact]
    public void NativeBinderSupportsOptionalSemanticProviders() => RunSta(async () =>
    {
        using var combo = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        var id = "native.items." + Guid.NewGuid().ToString("N");
        AgenticControlBinder.Attach(combo, new AgenticControlOptions
        {
            Id = id, ItemTextProvider = item => ((Book)item!).Id + " / " + ((Book)item).Name,
            ItemKeyProvider = item => ((Book)item!).Id
        });
        combo.Items.Add(new Book("BK-001", "三体"));
        using var form = Show(combo);
        var result = await new AgenticCommandDispatcher().DispatchAsync(new AgenticCommand
        { ControlId = id, Action = AgenticActions.SelectItem, Arguments = { ["itemKey"] = "BK-001" } });
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("BK-001 / 三体", result.Control!.State["selection"]);
    });

    [Fact]
    public void ListBoxSharesItemProtocolAndDisabledControlsStayReadOnly() => RunSta(async () =>
    {
        using var list = new AgenticListBox { AgenticId = "list.items." + Guid.NewGuid().ToString("N"),
            DisplayMember = "Name", ValueMember = "Id", DataSource = new[] { new Book("BK-001", "三体") }, Width = 200 };
        using var form = Show(list);
        var dispatcher = new AgenticCommandDispatcher();
        var selected = await dispatcher.DispatchAsync(new AgenticCommand
        { ControlId = list.AgenticId, Action = AgenticActions.SelectItem, Arguments = { ["itemKey"] = "BK-001" } });
        Assert.True(selected.Succeeded, selected.Error); Assert.Equal("三体", selected.Control!.State["text"]);
        list.Enabled = false;
        Assert.True((await dispatcher.DispatchAsync(new AgenticCommand
        { ControlId = list.AgenticId, Action = AgenticActions.GetItems })).Succeeded);
        Assert.False((await dispatcher.DispatchAsync(new AgenticCommand
        { ControlId = list.AgenticId, Action = AgenticActions.SelectItem, Arguments = { ["index"] = 0 } })).Succeeded);
    });

    public sealed class Book
    {
        public Book(string id, string name) { Id = id; Name = name; }
        public string Id { get; }
        public string Name { get; }
        public string Secret => "private-business-field";
        // 故意不覆盖 ToString：这是原先绑定显示正常、远程却找不到项目的回归用例。
    }
    private static AgenticComboBox NewCombo() => new() { AgenticId = "combo.items." + Guid.NewGuid().ToString("N"),
        DisplayMember = "Name", ValueMember = "Id", Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    private static Form Show(Control control)
    {
        var form = new Form { ShowInTaskbar = false, ClientSize = new Size(350, 180),
            StartPosition = FormStartPosition.Manual, Location = new Point(20, 20) };
        control.Location = new Point(20, 20); form.Controls.Add(control); form.Show(); Application.DoEvents(); return form;
    }
    private static JsonElement State(AgenticCommandResult result)
    { Assert.True(result.Succeeded, result.Error); return JsonSerializer.SerializeToElement(result.Control!.State, AgenticJson.Options); }
    private static Task<AgenticCommandResult> Execute(AgenticComboBox combo, string action, params (string Key, object? Value)[] args) =>
        new AgenticCommandDispatcher().DispatchAsync(new AgenticCommand
        { ControlId = combo.AgenticId!, Action = action, Arguments = args.ToDictionary(arg => arg.Key, arg => arg.Value) });
    private static void RunSta(Func<Task> test)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { test().GetAwaiter().GetResult(); } catch (Exception exception) { failure = exception; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(20))) throw new TimeoutException("WinForms item test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
