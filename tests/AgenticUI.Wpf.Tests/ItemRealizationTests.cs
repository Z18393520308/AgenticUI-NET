using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using AgenticUI.Wpf;
using Xunit;

namespace AgenticUI.Wpf.Tests;

public sealed partial class ItemControlTests
{
    [Theory]
    [InlineData("value", false)]
    [InlineData("index", true)]
    [InlineData("itemKey", false)]
    [InlineData("itemKey", true)]
    public void StyledClosedComboIsRealizedWithoutInvalidatingReadVersion(string argument, bool implicitStyle) => RunSta(async () =>
    {
        var combo = NewCombo();
        combo.ItemsSource = new[] { new Book("BK-002", "流浪地球"), new Book("BK-001", "三体") };
        var style = ItemStyle();
        if (implicitStyle) combo.Resources[typeof(ComboBoxItem)] = style;
        else combo.ItemContainerStyle = style;
        var openState = new OpenState();
        combo.SetBinding(ComboBox.IsDropDownOpenProperty, new Binding(nameof(OpenState.IsOpen))
        { Source = openState, Mode = BindingMode.TwoWay });
        var window = Show(combo);
        try
        {
            var events = new List<AgenticEvent>();
            using var subscription = AgenticEventBus.Default.Subscribe(message =>
            { if (message.ControlId == combo.AgenticId) events.Add(message); return default; });
            var page = State(await Execute(combo, AgenticActions.GetItems));
            var version = page.GetProperty("itemsVersion").GetString();
            Assert.False(combo.IsDropDownOpen);
            Assert.DoesNotContain(events, e => e.Name == AgenticEvents.DropDownOpened);
            object value = argument == "index" ? 1 : argument == "value" ? "三体" : "BK-001";
            var selected = await Execute(combo, AgenticActions.SelectItem, (argument, value), ("itemsVersion", version));
            Assert.True(selected.Succeeded, selected.Error);
            Assert.Equal(1, combo.SelectedIndex);
            Assert.Equal("三体", selected.Control!.State["selection"]);
            Assert.Equal(version, selected.Control.State["itemsVersion"]);
            Assert.False(combo.IsDropDownOpen);
            Assert.True(BindingOperations.IsDataBound(combo, ComboBox.IsDropDownOpenProperty));
            Assert.Equal(AgenticEventSource.Remote, Assert.Single(events.Where(e => e.Name == AgenticEvents.DropDownOpened)).Source);
            Assert.Equal(AgenticEventSource.Remote, Assert.Single(events.Where(e => e.Name == AgenticEvents.SelectionChanged)).Source);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void OriginallyOpenComboIsNotClosedByPreparation() => RunSta(async () =>
    {
        var combo = NewCombo(); combo.ItemContainerStyle = ItemStyle();
        combo.ItemsSource = new[] { new Book("BK-001", "三体") };
        var window = Show(combo);
        try
        {
            combo.IsDropDownOpen = true;
            var result = await Execute(combo, AgenticActions.SelectItem, ("itemKey", "BK-001"));
            Assert.True(result.Succeeded, result.Error);
            Assert.True(combo.IsDropDownOpen);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void VirtualizedComboCanRealizeAnOffscreenIndex() => RunSta(async () =>
    {
        var combo = NewCombo(); combo.ItemContainerStyle = ItemStyle(); combo.MaxDropDownHeight = 120;
        combo.ItemsSource = Enumerable.Range(0, 400).Select(i => new Book("BK-" + i, "Book " + i)).ToArray();
        combo.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        VirtualizingPanel.SetIsVirtualizing(combo, true);
        VirtualizingPanel.SetVirtualizationMode(combo, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(combo, true);
        var window = Show(combo);
        try
        {
            var page = State(await Execute(combo, AgenticActions.GetItems, ("start", 320), ("count", 1)));
            var result = await Execute(combo, AgenticActions.SelectItem,
                ("index", 320), ("itemsVersion", page.GetProperty("itemsVersion").GetString()));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal("BK-320", combo.SelectedValue);
            Assert.False(combo.IsDropDownOpen);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void VirtualizedListBoxCanRealizeAnOffscreenItemWithoutLosingVersion() => RunSta(async () =>
    {
        var list = new AgenticListBox
        {
            AgenticId = "wpf.items." + Guid.NewGuid().ToString("N"),
            DisplayMemberPath = "Details.Name", SelectedValuePath = "Id",
            ItemsSource = Enumerable.Range(0, 400).Select(i => new Book("BK-" + i, "Book " + i)).ToArray(),
            ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel))),
            ItemContainerStyle = new Style(typeof(ListBoxItem))
        };
        list.ItemContainerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4)));
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(list, true);
        var window = Show(list);
        try
        {
            var page = State(await Execute(list, AgenticActions.GetItems, ("start", 320), ("count", 1)));
            var result = await Execute(list, AgenticActions.SelectItem,
                ("itemKey", "BK-320"), ("itemsVersion", page.GetProperty("itemsVersion").GetString()));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(320, list.SelectedIndex);
            Assert.Equal("BK-320", list.SelectedValue);
        }
        finally { Close(list, window); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedDisabledStyleStillRejectsSelection(bool dataTrigger) => RunSta(async () =>
    {
        var combo = NewCombo(); combo.ItemsSource = new[] { new Book("BK-001", "三体") };
        var style = ItemStyle();
        if (dataTrigger)
        {
            var trigger = new DataTrigger { Binding = new Binding("Id"), Value = "BK-001" };
            trigger.Setters.Add(new Setter(UIElement.IsEnabledProperty, false)); style.Triggers.Add(trigger);
        }
        else style.Setters.Add(new Setter(UIElement.IsEnabledProperty, false));
        combo.ItemContainerStyle = style;
        var window = Show(combo);
        try
        {
            var result = await Execute(combo, AgenticActions.SelectItem, ("itemKey", "BK-001"));
            Assert.False(result.Succeeded); Assert.Contains("禁用", result.Error);
            Assert.Equal(-1, combo.SelectedIndex); Assert.False(combo.IsDropDownOpen);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void DropDownLoadingCannotSilentlyChangeWhichItemAnIndexSelects() => RunSta(async () =>
    {
        var books = new ObservableCollection<Book> { new("BK-001", "三体"), new("BK-002", "流浪地球") };
        var combo = NewCombo(); combo.ItemsSource = books; combo.ItemContainerStyle = ItemStyle();
        combo.DropDownOpened += (_, _) => books.Move(0, 1);
        var window = Show(combo);
        try
        {
            var page = State(await Execute(combo, AgenticActions.GetItems));
            var result = await Execute(combo, AgenticActions.SelectItem,
                ("index", 0), ("itemsVersion", page.GetProperty("itemsVersion").GetString()));
            Assert.False(result.Succeeded); Assert.Contains("Items changed", result.Error);
            Assert.Equal(-1, combo.SelectedIndex); Assert.False(combo.IsDropDownOpen);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void PropertyChangeWithoutCollectionNotificationIsDetectedBeforeSelection() => RunSta(async () =>
    {
        var book = new Book("BK-001", "三体");
        var combo = NewCombo(); combo.ItemsSource = new[] { book }; combo.ItemContainerStyle = ItemStyle();
        combo.DropDownOpened += (_, _) => book.Details.Name = "三体（修订版）";
        var window = Show(combo);
        try
        {
            // 即使旧客户端没带 itemsVersion，也不能在准备期间悄悄换成新候选快照。
            var result = await Execute(combo, AgenticActions.SelectItem, ("index", 0));
            Assert.False(result.Succeeded); Assert.Contains("Items changed", result.Error);
            Assert.Equal(-1, combo.SelectedIndex); Assert.False(combo.IsDropDownOpen);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void HumanSelectionDuringPreparationIsNotMarkedRemoteOrOverwritten() => RunSta(async () =>
    {
        var combo = NewCombo(); combo.ItemContainerStyle = ItemStyle();
        combo.ItemsSource = new[] { new Book("BK-001", "三体"), new Book("BK-002", "流浪地球") };
        combo.DropDownOpened += (_, _) => combo.Dispatcher.BeginInvoke(
            new Action(() => combo.SelectedIndex = 0), DispatcherPriority.Background);
        var window = Show(combo);
        try
        {
            var events = new List<AgenticEvent>();
            using var subscription = AgenticEventBus.Default.Subscribe(message =>
            { if (message.ControlId == combo.AgenticId && message.Name == AgenticEvents.SelectionChanged) events.Add(message); return default; });
            var result = await Execute(combo, AgenticActions.SelectItem, ("itemKey", "BK-002"));
            Assert.False(result.Succeeded); Assert.Contains("交互", result.Error);
            Assert.Equal("BK-001", combo.SelectedValue);
            Assert.Equal(AgenticEventSource.User, Assert.Single(events).Source);
        }
        finally { Close(combo, window); }
    });

    [Fact]
    public void MissingItemsPresenterTimesOutWithoutBlockingDispatcher() => RunSta(async () =>
    {
        var combo = NewCombo(); combo.ItemsSource = new[] { new Book("BK-001", "三体") };
        combo.Template = TemplateWithoutItemsPresenter();
        var window = Show(combo);
        var ticks = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) => ticks++;
        try
        {
            timer.Start(); var watch = Stopwatch.StartNew();
            var result = await Execute(combo, AgenticActions.SelectItem, ("itemKey", "BK-001"));
            Assert.False(result.Succeeded); Assert.Contains("超时", result.Error);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5)); Assert.True(ticks > 2);
            Assert.Equal(-1, combo.SelectedIndex); Assert.False(combo.IsDropDownOpen);
        }
        finally { timer.Stop(); Close(combo, window); }
    });

    [Fact]
    public void PendingSelectionAllowsReadsRejectsSecondMutationAndCleansUpOnCancellation() => RunSta(async () =>
    {
        var combo = NewCombo(); combo.ItemsSource = new[] { new Book("BK-001", "三体") };
        combo.Template = TemplateWithoutItemsPresenter();
        // 自定义模板不一定触发 DropDownOpened，使用依赖属性值确认准备已开始。
        var window = Show(combo);
        using var cancellation = new CancellationTokenSource();
        try
        {
            var pending = new AgenticCommandDispatcher().DispatchAsync(new AgenticCommand
            { ControlId = combo.AgenticId!, Action = AgenticActions.SelectItem, Arguments = { ["itemKey"] = "BK-001" } }, cancellation.Token);
            var watch = Stopwatch.StartNew();
            while (!combo.IsDropDownOpen && !pending.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(1)) await Task.Delay(10);
            Assert.True(combo.IsDropDownOpen);
            Assert.True((await Execute(combo, AgenticActions.GetItems)).Succeeded);
            var second = await Execute(combo, AgenticActions.SelectItem, ("index", 0));
            Assert.False(second.Succeeded); Assert.Contains("正在准备", second.Error);
            cancellation.Cancel();
            Assert.False((await pending).Succeeded);
            Assert.False(combo.IsDropDownOpen);
            // 换回原生模板后仍能开始新命令，证明等待标记和事件订阅已释放。
            combo.ClearValue(Control.TemplateProperty); window.UpdateLayout();
            Assert.True((await Execute(combo, AgenticActions.SelectItem, ("index", 0))).Succeeded);
        }
        finally { cancellation.Cancel(); Close(combo, window); }
    });

    private static Style ItemStyle()
    {
        var style = new Style(typeof(ComboBoxItem));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
        return style;
    }
    private static ControlTemplate TemplateWithoutItemsPresenter()
    {
        var root = new FrameworkElementFactory(typeof(Border));
        root.SetValue(Border.BackgroundProperty, Brushes.White);
        return new ControlTemplate(typeof(ComboBox)) { VisualTree = root };
    }
    public sealed class OpenState { public bool IsOpen { get; set; } }
}
