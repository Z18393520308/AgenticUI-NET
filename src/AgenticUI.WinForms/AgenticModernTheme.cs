using System.Drawing;
using System.Windows.Forms;

namespace AgenticUI.WinForms;

/// <summary>
/// Workbench / 宿主应用可选用的外观主题。
/// </summary>
public enum AgenticUiTheme
{
    /// <summary>系统原生外观（默认 WinForms 视觉样式）。</summary>
    Native = 0,

    /// <summary>AgenticUI 现代主题（蓝色强调按钮与浅色画布）。</summary>
    Modern = 1
}

public static class AgenticModernTheme
{
    private static readonly Color Accent = Color.FromArgb(45, 125, 255);
    private static readonly Color Surface = Color.White;
    private static readonly Color Canvas = Color.FromArgb(246, 248, 252);

    /// <summary>应用现代主题。</summary>
    public static void Apply(Control root) => ApplyModern(root);

    /// <summary>按主题枚举应用外观。</summary>
    public static void Apply(Control root, AgenticUiTheme theme)
    {
        if (theme == AgenticUiTheme.Native)
        {
            ApplyNative(root);
        }
        else
        {
            ApplyModern(root);
        }
    }

    /// <summary>应用现代主题（蓝色强调按钮与浅色画布）。</summary>
    public static void ApplyModern(Control root)
    {
        if (root is null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        root.Font = new Font("Segoe UI", 9F);
        ApplyModernChildren(root);
    }

    /// <summary>还原系统原生外观。</summary>
    public static void ApplyNative(Control root)
    {
        if (root is null)
        {
            throw new ArgumentNullException(nameof(root));
        }

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
