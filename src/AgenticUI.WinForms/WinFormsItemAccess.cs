using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace AgenticUI.WinForms;

/// <summary>候选读取、选择、状态和日志共用原控件的显示规则，不修改原数据源。</summary>
internal static class WinFormsItemAccess
{
    public static IList Items(ListControl control) => control switch
    {
        ComboBox combo => combo.Items,
        ListBox list => list.Items,
        _ => throw new NotSupportedException("Unsupported list control.")
    };

    public static string? Text(ListControl control, object? item, AgenticControlOptions options) =>
        item is null ? null : options.ItemTextProvider is { } provider ? provider(item) : control.GetItemText(item);

    public static string? Key(ListControl control, object? item, AgenticControlOptions options)
    {
        if (item is null) return null;
        if (options.ItemKeyProvider is { } provider) return provider(item);
        if (string.IsNullOrEmpty(control.ValueMember)) return null;
        // WinForms 的 BindingMemberInfo 将数据源路径与列表项字段分开，遵循原绑定语义。
        var field = new BindingMemberInfo(control.ValueMember).BindingField;
        return AgenticItemCollection.ScalarKey(TypeDescriptor.GetProperties(item).Find(field, true)?.GetValue(item));
    }

    public static IEnumerable<AgenticItemEntry> Capture(ListControl control, AgenticControlOptions options) =>
        Items(control).Cast<object?>().Select(item => new AgenticItemEntry(item,
            Text(control, item, options), Key(control, item, options), control.Enabled, item?.ToString()));
}
