using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using AgenticUI;

namespace AgenticUI.Wpf;

/// <summary>从真实节点容器读取路径。不为发现动作展开树、不猜测业务模型属性。</summary>
internal static class WpfTreeState
{
    public static string? Header(object? item)
    {
        if (item is not TreeViewItem node) return item?.ToString();
        var name = AutomationProperties.GetName(node);
        if (!string.IsNullOrWhiteSpace(name)) return name;
        if (node.Header is string text) return text;
        if (node.Header is TextBlock textBlock) return textBlock.Text;
        if (node.Header is AccessText accessText) return accessText.Text;
        // 数据绑定模板的实际标题优先于模型类型名。复杂多文本模板请设置原生 AutomationProperties.Name。
        if (node.Template?.FindName("PART_Header", node) is DependencyObject presenter)
        {
            var labels = Texts(presenter).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().Take(2).ToArray();
            if (labels.Length == 1) return labels[0];
            if (labels.Length > 1) return null;
        }
        return node.Header?.ToString();
    }

    private static IEnumerable<string> Texts(DependencyObject parent)
    {
        if (parent is TextBlock text) { yield return text.Text; yield break; }
        if (parent is AccessText access) { yield return access.Text; yield break; }
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            foreach (var label in Texts(VisualTreeHelper.GetChild(parent, index))) yield return label;
    }

    private static ItemsControl? Parent(TreeViewItem node) =>
        ItemsControl.ItemsControlFromItemContainer(node) ?? node.Parent as ItemsControl;

    private static IEnumerable<TreeViewItem> Children(ItemsControl owner)
    {
        for (var index = 0; index < owner.Items.Count; index++)
            if ((owner.Items[index] as TreeViewItem ?? owner.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem) is { } node)
                yield return node;
    }

    public static bool BelongsTo(TreeView tree, TreeViewItem node)
    {
        for (var depth = 0; depth < 128; depth++)
        {
            var parent = Parent(node);
            if (ReferenceEquals(parent, tree)) return true;
            if (parent is not TreeViewItem ancestor) return false;
            node = ancestor;
        }
        return false;
    }

    public static string? Path(TreeView tree, TreeViewItem? node)
    {
        if (node is null) return null;
        var parts = new Stack<string>();
        for (var depth = 0; depth < 128; depth++)
        {
            var parent = Parent(node);
            var label = Header(node);
            // 旧协议用 / 分层。不能把同名兄弟或含 / 的标题编成会指向别处的假路径。
            if (parent is null || string.IsNullOrWhiteSpace(label) || label!.Contains('/') || label.Any(char.IsControl)) return null;
            var siblings = Children(parent).ToArray();
            if (siblings.Length != parent.Items.Count || siblings.Count(other =>
                    string.Equals(Header(other), label, StringComparison.OrdinalIgnoreCase)) != 1) return null;
            parts.Push(label!);
            if (ReferenceEquals(parent, tree)) return string.Join("/", parts);
            if (parent is not TreeViewItem ancestor) return null;
            node = ancestor;
        }
        return null;
    }

    public static TreeViewItem? Selected(TreeView tree) => tree.SelectedItem switch
    {
        null => null,
        TreeViewItem node when BelongsTo(tree, node) => node,
        _ => Nodes(tree, 0).FirstOrDefault(node => node.IsSelected)
    };

    private static IEnumerable<TreeViewItem> Nodes(ItemsControl owner, int depth)
    {
        if (depth >= 128) yield break;
        foreach (var node in Children(owner))
        {
            yield return node;
            foreach (var descendant in Nodes(node, depth + 1)) yield return descendant;
        }
    }

    public static TreeViewItem Find(TreeView tree, AgenticCommand command)
    {
        command.Arguments.TryGetValue("path", out var pathValue);
        var path = pathValue?.ToString();
        TreeViewItem? found;
        if (!string.IsNullOrWhiteSpace(path))
        {
            var parts = path!.Split('/');
            if (parts.Length > 128 || parts.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("树路径必须由非空标题按 / 分隔。");
            ItemsControl parent = tree;
            found = null;
            foreach (var part in parts)
            {
                var matches = Children(parent).Where(node => string.Equals(Header(node), part, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("树路径不存在、存在同名节点或容器尚未生成，请先展开父节点并重新读取状态。");
                found = matches[0]; parent = found;
            }
            if (Path(tree, found) is null) throw new InvalidOperationException("节点没有可唯一定位的完整路径。");
        }
        else if (command.Arguments.TryGetValue("index", out var indexValue))
        {
            if (!int.TryParse(indexValue?.ToString(), out var index) || index < 0 || index >= tree.Items.Count)
                throw new ArgumentOutOfRangeException("index");
            found = tree.Items[index] as TreeViewItem ?? tree.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
        }
        else
        {
            var value = command.Arguments.TryGetValue("value", out var item) ? item?.ToString() : null;
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Tree action requires path, value, or index.");
            var matches = Nodes(tree, 0).Where(node => string.Equals(Header(node), value, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("树节点不存在或名称不唯一，请使用完整路径。");
            found = matches[0];
        }
        if (found is null) throw new InvalidOperationException("节点容器尚未生成，请先展开父节点。");
        if (!found.IsEnabled || !found.IsVisible) throw new InvalidOperationException("树节点被禁用或隐藏，请先通过界面展开父节点。");
        return found;
    }
}
