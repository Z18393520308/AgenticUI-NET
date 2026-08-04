using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace AgenticUI.WinForms;

public static class AgenticControlBinder
{
    private static readonly ConditionalWeakTable<Control, WinFormsControlAdapter> Adapters = new();

    public static void Attach(Control control, AgenticControlOptions? options = null)
    {
        if (control is null)
        {
            throw new ArgumentNullException(nameof(control));
        }

        if (Adapters.TryGetValue(control, out var existing))
        {
            existing.UpdateOptions(options ?? existing.Options);
            return;
        }

        var adapter = new WinFormsControlAdapter(control, options ?? new AgenticControlOptions());
        Adapters.Add(control, adapter);
        adapter.Attach();
    }

    public static void Detach(Control control)
    {
        if (Adapters.TryGetValue(control, out var adapter))
        {
            adapter.Detach();
            Adapters.Remove(control);
        }
    }

    public static AgenticControlOptions GetOptions(Control control)
    {
        Attach(control);
        return Adapters.GetValue(control, _ => throw new InvalidOperationException()).Options;
    }

    public static void Refresh(Control control)
    {
        if (Adapters.TryGetValue(control, out var adapter))
        {
            adapter.RefreshRegistration();
        }
    }
}
