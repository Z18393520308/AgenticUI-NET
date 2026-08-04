using System.Drawing;
using System.Windows.Forms;

namespace AgenticUI.Workbench.WinForms;

/// <summary>
/// Workbench 本地主题切换，避免依赖 AgenticUI.WinForms 程序集中可能未同步的主题 API。
/// </summary>
internal static class WorkbenchTheme
{
    private static readonly Color Accent = Color.FromArgb(45, 125, 255);
    private static readonly Color Surface = Color.White;
    private static readonly Color Canvas = Color.FromArgb(246, 248, 252);

    public static void ApplyModern(Control root)
    {
        root.Font = new Font("Segoe UI", 9F);
        ApplyModernChildren(root);
    }

    public static void ApplyNative(Control root)
    {
        root.Font = SystemFonts.MessageBoxFont ?? Control.DefaultFont;
        if (root is Form form)
        {
            form.BackColor = SystemColors.Control;
            form.ForeColor = SystemColors.ControlText;
        }

        ApplyNativeChildren(root);
    }

    private static void ApplyModernChildren(Control root)
    {
        if (root is Form or UserControl or Panel)
        {
            root.BackColor = Canvas;
        }

        foreach (Control child in root.Controls)
        {
            switch (child)
            {
                case Button button:
                    button.UseVisualStyleBackColor = false;
                    button.BackColor = Accent;
                    button.ForeColor = Color.White;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 0;
                    button.Padding = new Padding(10, 4, 10, 4);
                    break;
                case TextBoxBase text:
                    text.BackColor = Surface;
                    text.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox combo:
                    combo.BackColor = Surface;
                    combo.FlatStyle = FlatStyle.Flat;
                    break;
            }

            ApplyModernChildren(child);
        }
    }

    private static void ApplyNativeChildren(Control root)
    {
        if (root is Panel or UserControl)
        {
            root.BackColor = SystemColors.Control;
            root.ForeColor = SystemColors.ControlText;
        }

        foreach (Control child in root.Controls)
        {
            switch (child)
            {
                case Button button:
                    button.FlatStyle = FlatStyle.Standard;
                    button.Padding = Padding.Empty;
                    button.ForeColor = SystemColors.ControlText;
                    button.BackColor = SystemColors.Control;
                    button.UseVisualStyleBackColor = true;
                    break;
                case TextBoxBase text:
                    text.BorderStyle = BorderStyle.Fixed3D;
                    text.BackColor = SystemColors.Window;
                    text.ForeColor = SystemColors.WindowText;
                    break;
                case ComboBox combo:
                    combo.FlatStyle = FlatStyle.Standard;
                    combo.BackColor = SystemColors.Window;
                    combo.ForeColor = SystemColors.WindowText;
                    break;
                case Label label:
                    label.ForeColor = SystemColors.ControlText;
                    label.BackColor = Color.Transparent;
                    break;
                case ListBox list:
                    list.BackColor = SystemColors.Window;
                    list.ForeColor = SystemColors.WindowText;
                    break;
                case TabPage page:
                    page.BackColor = SystemColors.Control;
                    page.UseVisualStyleBackColor = true;
                    break;
            }

            ApplyNativeChildren(child);
        }
    }
}
