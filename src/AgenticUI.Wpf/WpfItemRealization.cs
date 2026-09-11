using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace AgenticUI.Wpf;

/// <summary>让真实选项容器就绪，不通过改选中项、反射或临时假容器绕过原控件样式。</summary>
internal static class WpfItemRealization
{
    public static async Task WithContainerAsync(Selector selector, int index, Action validate,
        Action commit, Action<Action> asRemote, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var combo = selector as ComboBox;
        var openedByUs = false;
        var interrupted = false;
        var itemsChanged = false;
        var popupClosed = false;
        var items = (INotifyCollectionChanged)selector.Items;
        void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs args) => itemsChanged = true;
        void OnSelectionChanged(object sender, SelectionChangedEventArgs args) => interrupted = true;
        void OnUnloaded(object sender, RoutedEventArgs args) => interrupted = true;
        void OnMouse(object sender, MouseButtonEventArgs args) => interrupted = true;
        void OnKey(object sender, KeyEventArgs args) => interrupted = true;
        void OnClosed(object? sender, EventArgs args) => popupClosed = true;
        void Check()
        {
            timeout.Token.ThrowIfCancellationRequested();
            if (itemsChanged) throw new InvalidOperationException("Items changed during preparation. Read getItems again.");
            if (interrupted || popupClosed)
                throw new InvalidOperationException("选项准备期间界面发生交互、选择变化或下拉框被关闭，已停止本次选择。");
            validate();
        }

        items.CollectionChanged += OnItemsChanged;
        selector.SelectionChanged += OnSelectionChanged;
        selector.Unloaded += OnUnloaded;
        selector.PreviewMouseDown += OnMouse;
        selector.PreviewKeyDown += OnKey;
        if (combo is not null) combo.DropDownClosed += OnClosed;
        try
        {
            Check();
            selector.ApplyTemplate();
            if (combo is not null && !combo.IsDropDownOpen)
            {
                // 标记只包住同步属性修改，等待期间的人工输入仍必须标记为 User。
                openedByUs = true;
                asRemote(() => combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, true));
            }

            VirtualizingPanel? requestedPanel = null;
            FrameworkElement? requestedContainer = null;
            var listScrollRequested = false;
            while (true)
            {
                Check();
                var container = selector.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                if (container is not null && !ReferenceEquals(container, requestedContainer))
                {
                    requestedContainer = container;
                    container.BringIntoView();
                }
                else if (container is { IsLoaded: true, IsVisible: true } &&
                    container.ActualWidth > 0 && container.ActualHeight > 0)
                {
                    // 上一轮已请求定位，并给绑定/布局留出处理时间；提交前仍需再次验证数据和权限。
                    Check();
                    commit();
                    return;
                }

                if (selector is ListBox list && !listScrollRequested)
                {
                    listScrollRequested = true;
                    list.ScrollIntoView(list.Items[index]);
                }
                else if (combo is not null && FindItemsHost(combo) is VirtualizingPanel { IsLoaded: true } panel &&
                    !ReferenceEquals(panel, requestedPanel))
                {
                    requestedPanel = panel;
                    panel.BringIndexIntoViewPublic(index);
                }

                // 不用同步等待、DoEvents 或反复 UpdateLayout；UI 可处理输入、取消及其他只读命令。
                await Task.Delay(30, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("等待目标选项容器就绪超时（2 秒），未执行选择。请检查控件模板或目标项可见性。");
        }
        finally
        {
            items.CollectionChanged -= OnItemsChanged;
            selector.SelectionChanged -= OnSelectionChanged;
            selector.Unloaded -= OnUnloaded;
            selector.PreviewMouseDown -= OnMouse;
            selector.PreviewKeyDown -= OnKey;
            if (combo is not null)
            {
                combo.DropDownClosed -= OnClosed;
                // 原本已展开的列表不关闭；人工关闭又重新打开的列表也不夺回控制权。
                if (openedByUs && !popupClosed && combo.IsDropDownOpen)
                    asRemote(() => combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false));
            }
        }
    }

    private static Panel? FindItemsHost(ComboBox combo)
    {
        // 只定位本控件弹出层的 ItemsHost，不误操作模板中的其他滚动区域。
        if (combo.Template?.FindName("PART_Popup", combo) is not Popup { Child: { } child }) return null;
        var pending = new Stack<DependencyObject>(); pending.Push(child);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is Panel { IsItemsHost: true } panel && ItemsControl.GetItemsOwner(panel) == combo) return panel;
            for (var i = VisualTreeHelper.GetChildrenCount(current) - 1; i >= 0; i--)
                pending.Push(VisualTreeHelper.GetChild(current, i));
        }
        return null;
    }
}
