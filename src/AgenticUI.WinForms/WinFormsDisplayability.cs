using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AgenticUI.WinForms;

internal static class WinFormsDisplayability
{
    public static bool IsDisplayable(Control control)
    {
        if (control is null ||
            control.IsDisposed ||
            !control.IsHandleCreated ||
            !control.Visible ||
            control.Width <= 0 ||
            control.Height <= 0)
        {
            return false;
        }

        var form = control.FindForm();
        if (form is null ||
            form.IsDisposed ||
            !form.Visible ||
            form.WindowState == FormWindowState.Minimized)
        {
            return false;
        }

        // Modal dialog owns exclusive remote interaction (same as UI modality rules).
        if (!IsInActiveInteractionScope(form))
        {
            return false;
        }

        if (IsOnInactiveTabPage(control))
        {
            return false;
        }

        var bounds = GetClippedScreenBounds(control);
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return false;
        }

        var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        var hwnd = WindowFromPoint(new POINT { X = center.X, Y = center.Y });
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        // Ignore foreign-process windows (IDE overlays, etc.); in-app modality/occlusion matter.
        GetWindowThreadProcessId(hwnd, out var processId);
        if (processId != (uint)Process.GetCurrentProcess().Id)
        {
            return true;
        }

        var hit = Control.FromChildHandle(hwnd) ?? Control.FromHandle(hwnd);
        if (hit is not null)
        {
            if (IsAssociated(control, hit))
            {
                return true;
            }

            // Forms on other UI threads (e.g. parallel tests) must not count as occluders.
            var hitForm = hit.FindForm();
            if (hitForm is not null && hitForm.InvokeRequired)
            {
                return true;
            }

            return false;
        }

        return IsHandleOwnedByForm(form.Handle, hwnd);
    }

    /// <summary>
    /// When a modal form is open, only controls on that topmost modal form are interactable.
    /// </summary>
    public static bool IsInActiveInteractionScope(Form form)
    {
        var modal = GetTopModalForm();
        if (modal is null)
        {
            return true;
        }

        return ReferenceEquals(form, modal);
    }

    private static Form? GetTopModalForm()
    {
        Form? topModal = null;
        foreach (Form open in Application.OpenForms)
        {
            // Limit to the current UI thread so parallel STA tests / multi-pump apps don't leak modality.
            if (open is { IsDisposed: false, Visible: true, Modal: true } &&
                !open.InvokeRequired)
            {
                topModal = open;
            }
        }

        return topModal;
    }

    private static bool IsOnInactiveTabPage(Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is TabPage page && page.Parent is TabControl tabs)
            {
                if (!ReferenceEquals(tabs.SelectedTab, page))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Rectangle GetClippedScreenBounds(Control control)
    {
        var bounds = control.RectangleToScreen(control.ClientRectangle);
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            bounds = Rectangle.Intersect(bounds, parent.RectangleToScreen(parent.ClientRectangle));
            if (bounds.Width < 2 || bounds.Height < 2)
            {
                return Rectangle.Empty;
            }
        }

        return bounds;
    }

    private static bool IsAssociated(Control target, Control hit)
    {
        for (Control? current = hit; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }
        }

        // Composite hosts (e.g. DataGridView editing control) may report an inner child.
        for (Control? current = target; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, hit))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHandleOwnedByForm(IntPtr formHandle, IntPtr hwnd)
    {
        for (var current = hwnd; current != IntPtr.Zero; current = GetParent(current))
        {
            if (current == formHandle)
            {
                return true;
            }
        }

        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
