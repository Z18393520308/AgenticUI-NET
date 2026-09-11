using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace AgenticUI.Wpf;

internal static class WpfItemAccess
{
    // ListView/TabControl 保持原动作语义，本次只升级下拉框与普通 ListBox。
    public static bool Supports(FrameworkElement element) => element is ComboBox || element is ListBox and not ListView;

    public static string? Text(Selector selector, object? item)
    {
        if (item is null) return null;
        if (AgenticProperties.GetItemTextProvider(selector) is { } provider) return provider(item);
        object? value = item;
        var path = selector.DisplayMemberPath;
        if (string.IsNullOrEmpty(path)) path = TextSearch.GetTextPath(selector);
        if (!string.IsNullOrEmpty(path)) value = ReadPath(item, path);
        else if (item is ContentControl content) value = content.Content;
        value = value switch { TextBlock text => text.Text, AccessText text => text.Text, _ => value };
        if (value is null) return null;
        return string.IsNullOrEmpty(selector.ItemStringFormat) ? value.ToString() :
            string.Format(CultureInfo.CurrentCulture, selector.ItemStringFormat, value);
    }

    public static string? Key(Selector selector, object? item)
    {
        if (item is null) return null;
        if (AgenticProperties.GetItemKeyProvider(selector) is { } provider) return provider(item);
        return string.IsNullOrEmpty(selector.SelectedValuePath) ? null :
            AgenticItemCollection.ScalarKey(ReadPath(item, selector.SelectedValuePath));
    }

    public static IEnumerable<AgenticItemEntry> Capture(Selector selector)
    {
        for (var index = 0; index < selector.Items.Count; index++)
        {
            var item = selector.Items[index];
            yield return new AgenticItemEntry(item, Text(selector, item), Key(selector, item),
                IsEnabled(selector, item, index), item?.ToString());
        }
    }

    private static bool? IsEnabled(Selector selector, object? item, int index)
    {
        if (!selector.IsEnabled) return false;
        if (item is UIElement element && !element.IsEnabled) return false;
        if (selector.ItemContainerGenerator.ContainerFromIndex(index) is UIElement container) return container.IsEnabled;
        // 未实现的虚拟化容器可能通过样式禁用。不能用 SelectedIndex 绕过未知约束。
        var containerType = selector is ComboBox ? typeof(ComboBoxItem) : typeof(ListBoxItem);
        if (selector.ItemContainerStyle is not null || selector.ItemContainerStyleSelector is not null ||
            selector.TryFindResource(containerType) is Style) return null;
        return true;
    }

    private static object? ReadPath(object item, string path)
    {
        // 使用 WPF 自己的绑定引擎，支持嵌套属性、索引器以及 XML 路径，而不是只反射一级属性。
        var probe = new BindingProbe();
        var binding = new Binding { Source = item, Mode = BindingMode.OneTime };
        if (item is System.Xml.XmlNode) binding.XPath = path;
        else binding.Path = new PropertyPath(path);
        try
        {
            BindingOperations.SetBinding(probe, BindingProbe.ValueProperty, binding);
            var value = probe.GetValue(BindingProbe.ValueProperty);
            if (value is System.Xml.XmlNode node) return node.InnerText;
            return value == DependencyProperty.UnsetValue ? null : value;
        }
        finally { BindingOperations.ClearBinding(probe, BindingProbe.ValueProperty); }
    }

    private sealed class BindingProbe : DependencyObject
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", typeof(object), typeof(BindingProbe), new PropertyMetadata(null));
    }
}
