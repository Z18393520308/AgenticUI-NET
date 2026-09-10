using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AgenticUI.Wpf;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AgenticUI.Wpf.Tests;

public sealed class GuidanceAndInteractionTests
{
    [Fact]
    public void DynamicGuidanceUpdatesWithoutClickOrFocusAndCleansSession() => RunSta(async () =>
    {
        var button = new AgenticButton { AgenticId = "wpf.guidance", Content = "保存", Hint = "预设", Width = 130, Height = 40 };
        var window = new Window { Content = button, Width = 350, Height = 200 };
        try
        {
            window.Show(); window.UpdateLayout();
            var clicks = 0; button.Click += (_, _) => clicks++;
            var focused = Keyboard.FocusedElement;
            var dispatcher = new AgenticCommandDispatcher();
            var command = new AgenticCommand { ControlId = button.AgenticId, Action = AgenticActions.Highlight, SessionId = "one",
                Arguments = { ["showBubble"] = true, ["hint"] = "远程提示", ["instructionNumber"] = 2 } };
            var result = await dispatcher.DispatchAsync(command);
            Assert.True(result.Succeeded, result.Error);
            var overlay = Assert.Single(window.OwnedWindows.Cast<Window>());
            Assert.False(overlay.ShowActivated); Assert.False(overlay.IsHitTestVisible);
            Assert.Same(focused, Keyboard.FocusedElement);
            var field = overlay.GetType().GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.Equal("远程提示", ((AgenticGuidanceOptions)field.GetValue(overlay)!).Hint);
            command.Arguments["showBubble"] = false;
            Assert.True((await dispatcher.DispatchAsync(command)).Succeeded);
            Assert.Same(overlay, Assert.Single(window.OwnedWindows.Cast<Window>()));
            Assert.False(((AgenticGuidanceOptions)field.GetValue(overlay)!).ShowBubble);
            Assert.Equal("预设", button.Hint); Assert.Equal(0, clicks);
            await AgenticControlRegistry.Default.ClearGuidanceAsync("another-session");
            Assert.Single(window.OwnedWindows.Cast<Window>());
            await AgenticControlRegistry.Default.ClearGuidanceAsync("one");
            Assert.Empty(window.OwnedWindows.Cast<Window>());
        }
        finally { window.Close(); }
    });

    [Fact]
    public void NativeClickInvokesBusinessCommandExactlyOnce() => RunSta(async () =>
    {
        var action = new CountCommand();
        var button = new AgenticCheckBox { AgenticId = "wpf.toggle", Content = "开启", Command = action };
        var window = new Window { Content = button, Width = 350, Height = 200 };
        try
        {
            window.Show(); window.UpdateLayout();
            var dispatcher = new AgenticCommandDispatcher();
            var result = await dispatcher.DispatchAsync(new AgenticCommand { ControlId = button.AgenticId, Action = "click" });
            Assert.True(result.Succeeded, result.Error); Assert.True(button.IsChecked); Assert.Equal(1, action.Count);
            button.IsEnabled = false;
            Assert.False((await dispatcher.DispatchAsync(new AgenticCommand { ControlId = button.AgenticId, Action = "click" })).Succeeded);
            Assert.Equal(1, action.Count);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void TreeEventsReportFullPathsWithoutConfusingSelectionAndExpansion() => RunSta(async () =>
    {
        var tree = new AgenticTreeView { AgenticId = "wpf.tree.contract" };
        var root = new TreeViewItem { Header = "公司", IsExpanded = true };
        var research = new TreeViewItem { Header = "研发", IsExpanded = true };
        var design = new TreeViewItem { Header = "设计" };
        var sales = new TreeViewItem { Header = "销售" }; sales.Items.Add(new TreeViewItem { Header = "订单" });
        research.Items.Add(design); root.Items.Add(research); root.Items.Add(sales); tree.Items.Add(root);
        var window = new Window { Content = tree, Width = 400, Height = 400 };
        try
        {
            window.Show(); window.UpdateLayout();
            var observed = new List<AgenticEvent>();
            using var subscription = AgenticEventBus.Default.Subscribe(message =>
            { if (message.ControlId == tree.AgenticId) observed.Add(message); return default; });
            design.IsSelected = true;
            var selection = Assert.Single(observed.Where(message => message.Name == "selectionChanged"));
            Assert.Equal("公司/研发/设计", selection.Data["path"]);
            Assert.Equal(AgenticEventSource.User, selection.Source);
            sales.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler((_, args) => args.Handled = true));
            var dispatcher = new AgenticCommandDispatcher();
            var result = await dispatcher.DispatchAsync(new AgenticCommand { ControlId = tree.AgenticId, Action = "expand",
                Arguments = { ["path"] = "公司/销售" } });
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal("公司/研发/设计", result.Control!.State["path"]);
            Assert.Equal("公司/销售", result.Control.State["expansionPath"]);
            Assert.Equal(true, result.Control.State["expanded"]);
            var expansion = Assert.Single(observed.Where(message => message.Name == "expanded"));
            Assert.Equal(AgenticEventSource.Remote, expansion.Source);
            Assert.Equal("公司/销售", expansion.Data["path"]);
            sales.IsExpanded = false;
            var collapse = Assert.Single(observed.Where(message => message.Name == "collapsed"));
            Assert.Equal(AgenticEventSource.User, collapse.Source);
            Assert.Equal(false, collapse.Data["expanded"]);
            root.Items.Add(new TreeViewItem { Header = "销售" }); window.UpdateLayout();
            Assert.False((await dispatcher.DispatchAsync(new AgenticCommand { ControlId = tree.AgenticId, Action = "expand",
                Arguments = { ["path"] = "公司/销售" } })).Succeeded);
            Assert.True(AgenticControlRegistry.Default.TryGet(tree.AgenticId!, out var control));
            Assert.Null(control!.Describe().State["expansionPath"]);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void DataBoundTreeContainersUseSamePathContract() => RunSta(async () =>
    {
        var template = new HierarchicalDataTemplate(typeof(TreeData)) { ItemsSource = new Binding(nameof(TreeData.Children)) };
        var text = new FrameworkElementFactory(typeof(TextBlock)); text.SetBinding(TextBlock.TextProperty, new Binding(nameof(TreeData.Name)));
        template.VisualTree = text;
        var style = new Style(typeof(TreeViewItem));
        style.Setters.Add(new Setter(System.Windows.Automation.AutomationProperties.NameProperty, new Binding(nameof(TreeData.Name))));
        var tree = new AgenticTreeView { AgenticId = "wpf.bound.tree", ItemTemplate = template, ItemContainerStyle = style,
            ItemsSource = new[] { new TreeData { Name = "仓库", Children = new[] { new TreeData { Name = "入库" } } } } };
        var window = new Window { Content = tree, Width = 400, Height = 350 };
        try
        {
            window.Show(); window.UpdateLayout();
            var dispatcher = new AgenticCommandDispatcher();
            var expanded = await dispatcher.DispatchAsync(new AgenticCommand { ControlId = tree.AgenticId, Action = "expand",
                Arguments = { ["path"] = "仓库" } });
            Assert.True(expanded.Succeeded, expanded.Error); window.UpdateLayout();
            var selected = await dispatcher.DispatchAsync(new AgenticCommand { ControlId = tree.AgenticId, Action = "selectItem",
                Arguments = { ["path"] = "仓库/入库" } });
            Assert.True(selected.Succeeded, selected.Error);
            Assert.Equal("仓库/入库", selected.Control!.State["path"]);
        }
        finally { window.Close(); }
    });

    public sealed class TreeData
    {
        public string Name { get; set; } = "";
        public TreeData[] Children { get; set; } = Array.Empty<TreeData>();
    }

    [Fact]
    public void TextEditPreservesBindingAndRespectsReadOnly() => RunSta(async () =>
    {
        var model = new TextModel();
        var text = new AgenticTextBox { AgenticId = "wpf.text", Height = 30 };
        text.SetBinding(TextBox.TextProperty, new Binding(nameof(TextModel.Value)) { Source = model, Mode = BindingMode.TwoWay });
        var window = new Window { Content = text, Width = 350, Height = 200 };
        try
        {
            window.Show(); window.UpdateLayout();
            var command = new AgenticCommand { ControlId = text.AgenticId, Action = "setText", Arguments = { ["text"] = "new" } };
            var dispatcher = new AgenticCommandDispatcher();
            Assert.True((await dispatcher.DispatchAsync(command)).Succeeded);
            Assert.True(BindingOperations.IsDataBound(text, TextBox.TextProperty)); Assert.Equal("new", model.Value);
            text.IsReadOnly = true; command.Arguments["text"] = "blocked";
            Assert.True(AgenticControlRegistry.Default.TryGet(text.AgenticId, out var adapter));
            Assert.Equal(true, adapter!.Describe().State["readOnly"]);
            Assert.False((await dispatcher.DispatchAsync(command)).Succeeded); Assert.Equal("new", model.Value);
        }
        finally { window.Close(); }
    });

    public sealed class TextModel { public string Value { get; set; } = "old"; }
    private sealed class CountCommand : ICommand
    {
        public int Count { get; private set; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Count++;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
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
        if (!thread.Join(TimeSpan.FromSeconds(20))) throw new TimeoutException("WPF UI test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
