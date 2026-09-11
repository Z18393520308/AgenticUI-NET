using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using AgenticUI.Remote;
using AgenticUI.Wpf;
using Xunit;

namespace AgenticUI.Wpf.Tests;

public sealed partial class ItemControlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObjectBindingUsesDisplayPathAndPreservesSelectionBinding(bool useListBox) => RunSta(async () =>
    {
        Selector selector = useListBox ? new AgenticListBox() : new AgenticComboBox();
        AgenticProperties.SetId(selector, "wpf.items." + Guid.NewGuid().ToString("N"));
        selector.DisplayMemberPath = "Details.Name"; selector.SelectedValuePath = "Id";
        selector.ItemsSource = new[] { new Book("BK-002", "流浪地球"), new Book("BK-001", "三体") };
        var model = new SelectionModel();
        selector.SetBinding(Selector.SelectedValueProperty, new Binding(nameof(SelectionModel.BookId))
        { Source = model, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit });
        var window = Show(selector);
        try
        {
            var events = new List<AgenticEvent>();
            using var subscription = AgenticEventBus.Default.Subscribe(message =>
            { if (message.ControlId == AgenticProperties.GetId(selector)) events.Add(message); return default; });
            var page = await Execute(selector, AgenticActions.GetItems);
            var state = State(page);
            Assert.Contains(AgenticItemCollection.Capability, page.Control!.Capabilities);
            Assert.Equal("三体", state.GetProperty("items")[1].GetProperty("text").GetString());
            Assert.Equal("BK-001", state.GetProperty("items")[1].GetProperty("itemKey").GetString());
            Assert.DoesNotContain("private-business-field", state.ToString());
            var selected = await Execute(selector, AgenticActions.SelectItem, ("value", "三体"));
            Assert.True(selected.Succeeded, selected.Error);
            Assert.Equal(1, selector.SelectedIndex);
            Assert.Equal("BK-001", model.BookId);
            Assert.True(BindingOperations.IsDataBound(selector, Selector.SelectedValueProperty));
            Assert.Equal("三体", selected.Control!.State["text"]);
            Assert.Equal("三体", selected.Control.State["selection"]);
            Assert.Equal("BK-001", selected.Control.State["itemKey"]);
            var selection = Assert.Single(events.Where(e => e.Name == AgenticEvents.SelectionChanged));
            Assert.Equal(AgenticEventSource.Remote, selection.Source);
            Assert.Equal("三体", selection.Data["selection"]); Assert.Equal("BK-001", selection.Data["itemKey"]);
            Assert.False((await Execute(selector, AgenticActions.GetText)).Control!.State.ContainsKey("items"));
        }
        finally { Close(selector, window); }
    });

    [Fact]
    public void LateDataAndViewSortingInvalidateOldIndexButNotBusinessKey() => RunSta(async () =>
    {
        var books = new ObservableCollection<Book>();
        var combo = NewCombo(); combo.ItemsSource = books;
        var window = Show(combo);
        try
        {
            Assert.Equal(0, (await Execute(combo, AgenticActions.GetItems)).Control!.State["total"]);
            // 模拟界面显示后才收到数据；读取动作本身不触发业务加载或打开下拉框。
            await Task.Yield();
            books.Add(new Book("BK-002", "流浪地球")); books.Add(new Book("BK-001", "三体"));
            var version = State(await Execute(combo, AgenticActions.GetItems)).GetProperty("itemsVersion").GetString();
            CollectionViewSource.GetDefaultView(books).SortDescriptions.Add(new SortDescription("Id", ListSortDirection.Ascending));
            var failed = await Execute(combo, AgenticActions.SelectItem, ("index", 1), ("itemsVersion", version));
            Assert.False(failed.Succeeded); Assert.Contains("stale", failed.Error);
            Assert.True((await Execute(combo, AgenticActions.SelectItem, ("itemKey", "BK-001"))).Succeeded);
            Assert.Equal(0, combo.SelectedIndex);
            books.Add(new Book("BK-003", "三体"));
            failed = await Execute(combo, AgenticActions.SelectItem, ("value", "三体"));
            Assert.False(failed.Succeeded); Assert.Contains("Ambiguous", failed.Error);
            Assert.Equal("BK-001", combo.SelectedValue);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void NativeItemContentAndDisabledItemsAreRespected() => RunSta(async () =>
    {
        var combo = new AgenticComboBox { AgenticId = "wpf.native.items." + Guid.NewGuid().ToString("N") };
        combo.Items.Add(new ComboBoxItem { Content = "三体" });
        combo.Items.Add(new ComboBoxItem { Content = "不能选择", IsEnabled = false });
        var window = Show(combo);
        try
        {
            Assert.True((await Execute(combo, AgenticActions.SelectItem, ("value", "三体"))).Succeeded);
            Assert.Equal(0, combo.SelectedIndex);
            Assert.False((await Execute(combo, AgenticActions.SelectItem, ("index", 1))).Succeeded);
            Assert.False((await Execute(combo, AgenticActions.SelectItem, ("value", "不能选择"))).Succeeded);
            Assert.Equal(0, combo.SelectedIndex);
            combo.IsEnabled = false;
            Assert.True((await Execute(combo, AgenticActions.GetItems)).Succeeded);
            Assert.False((await Execute(combo, AgenticActions.SelectItem, ("index", 0))).Succeeded);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void UnrealizedStyledItemsCannotBypassDisabledContainerRules() => RunSta(async () =>
    {
        var combo = NewCombo(); combo.ItemsSource = new[] { new Book("BK-001", "三体") };
        var style = new Style(typeof(ComboBoxItem)); style.Setters.Add(new Setter(UIElement.IsEnabledProperty, false));
        combo.ItemContainerStyle = style;
        var window = Show(combo);
        try
        {
            Assert.False((await Execute(combo, AgenticActions.SelectItem, ("index", 0))).Succeeded);
            Assert.Equal(-1, combo.SelectedIndex);
            await Execute(combo, AgenticActions.OpenDropDown); window.UpdateLayout();
            await combo.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.False((await Execute(combo, AgenticActions.SelectItem, ("itemKey", "BK-001"))).Succeeded);
            Assert.Equal(-1, combo.SelectedIndex);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void TemplateCanOptIntoSemanticTextWithoutChangingItsDataSource() => RunSta(async () =>
    {
        var combo = new ComboBox { ItemsSource = new[] { new Book("BK-001", "三体") } };
        var visual = new FrameworkElementFactory(typeof(TextBlock));
        var textBinding = new MultiBinding { StringFormat = "{0} / {1}" };
        textBinding.Bindings.Add(new Binding("Id")); textBinding.Bindings.Add(new Binding("Details.Name"));
        visual.SetBinding(TextBlock.TextProperty, textBinding);
        var template = new DataTemplate { VisualTree = visual };
        combo.ItemTemplate = template;
        AgenticProperties.SetId(combo, "wpf.template.items." + Guid.NewGuid().ToString("N"));
        AgenticProperties.SetEnabled(combo, true);
        AgenticProperties.SetItemTextProvider(combo, item => ((Book)item!).Id + " / " + ((Book)item).Details.Name);
        AgenticProperties.SetItemKeyProvider(combo, item => ((Book)item!).Id);
        var window = Show(combo);
        try
        {
            var result = await Execute(combo, AgenticActions.SelectItem, ("itemKey", "BK-001"));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal("BK-001 / 三体", result.Control!.State["selection"]);
            Assert.Same(template, combo.ItemTemplate);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void StringFormattingUsesTheSameLabelForReadAndSelect() => RunSta(async () =>
    {
        var combo = NewCombo(); combo.ItemsSource = new[] { new Book("BK-001", "三体") };
        combo.ItemStringFormat = "图书：{0}";
        var window = Show(combo);
        try
        {
            Assert.Equal("图书：三体", State(await Execute(combo, AgenticActions.GetItems)).GetProperty("items")[0].GetProperty("text").GetString());
            Assert.True((await Execute(combo, AgenticActions.SelectItem, ("value", "图书：三体"))).Succeeded);
            Assert.Equal("图书：三体", (await Execute(combo, AgenticActions.GetText)).Control!.State["text"]);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void NamedPipeRoundTripsOptionsAndRedactsSensitiveCandidates() => RunSta(async () =>
    {
        var combo = NewCombo(); combo.ItemsSource = new[] { new Book("BK-001", "三体") };
        var window = Show(combo);
        var pipeName = "aui-wpf-items-" + Guid.NewGuid().ToString("N");
        using var server = new AgenticNamedPipeServer(pipeName);
        try
        {
            server.Start();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            using var client = await AgenticNamedPipeClient.ConnectAsync(server.AuthenticationToken,
                pipeName: pipeName, cancellationToken: timeout.Token);
            var response = await client.ExecuteAsync(new AgenticCommand
            { ControlId = combo.AgenticId!, Action = AgenticActions.GetItems }, timeout.Token);
            var state = State(response.Result!);
            Assert.Equal("BK-001", state.GetProperty("items")[0].GetProperty("itemKey").GetString());
            var selected = await client.ExecuteAsync(new AgenticCommand
            { ControlId = combo.AgenticId!, Action = AgenticActions.SelectItem, Arguments = { ["itemKey"] = "BK-001" } }, timeout.Token);
            Assert.True(selected.Result?.Succeeded == true, selected.Result?.Error);
            Assert.Equal("BK-001", combo.SelectedValue);
            AgenticProperties.SetSensitive(combo, true);
            response = await client.ExecuteAsync(new AgenticCommand
            { ControlId = combo.AgenticId!, Action = AgenticActions.GetItems }, timeout.Token);
            state = State(response.Result!);
            Assert.Equal(JsonValueKind.Null, state.GetProperty("items").ValueKind);
            Assert.DoesNotContain("BK-001", state.ToString());
        }
        finally { Close(combo, window); }
    });

    public sealed class Details { public string Name { get; set; } = ""; }
    public sealed class Book
    {
        public Book(string id, string name) { Id = id; Details.Name = name; }
        public string Id { get; }
        public Details Details { get; } = new();
        public string Secret => "private-business-field";
    }
    public sealed class SelectionModel { public string? BookId { get; set; } }
    private static AgenticComboBox NewCombo() => new() { AgenticId = "wpf.items." + Guid.NewGuid().ToString("N"),
        DisplayMemberPath = "Details.Name", SelectedValuePath = "Id" };
    private static Window Show(Selector selector)
    {
        selector.Width = 220; selector.Height = 35;
        var window = new Window { Content = selector, Width = 350, Height = 200, ShowInTaskbar = false };
        window.Show(); window.UpdateLayout(); return window;
    }
    private static void Close(Selector selector, Window window)
    { try { AgenticProperties.SetEnabled(selector, false); } finally { window.Close(); } }
    private static JsonElement State(AgenticCommandResult result)
    { Assert.True(result.Succeeded, result.Error); return JsonSerializer.SerializeToElement(result.Control!.State, AgenticJson.Options); }
    private static Task<AgenticCommandResult> Execute(Selector selector, string action, params (string Key, object? Value)[] args) =>
        new AgenticCommandDispatcher().DispatchAsync(new AgenticCommand
        { ControlId = AgenticProperties.GetId(selector)!, Action = action, Arguments = args.ToDictionary(arg => arg.Key, arg => arg.Value) });
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
        if (!thread.Join(TimeSpan.FromSeconds(20))) throw new TimeoutException("WPF item test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
