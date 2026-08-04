using System.ComponentModel;
using System.Windows.Forms;

namespace AgenticUI.WinForms;

public interface IAgenticWinFormsControl
{
    string? AgenticId { get; set; }
    string? AgenticDisplayName { get; set; }
    bool IsSensitive { get; set; }
    int InstructionNumber { get; set; }
    string? Hint { get; set; }
}

public abstract class AgenticMetadataControl
{
    internal static AgenticControlOptions Options(Control control) => AgenticControlBinder.GetOptions(control);
    internal static void Refresh(Control control) => AgenticControlBinder.Refresh(control);
}

public class AgenticButton : Button, IAgenticWinFormsControl
{
    public AgenticButton() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}

public class AgenticTextBox : TextBox, IAgenticWinFormsControl
{
    public AgenticTextBox() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}

public class AgenticCheckBox : CheckBox, IAgenticWinFormsControl
{
    public AgenticCheckBox() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}

public class AgenticRadioButton : RadioButton, IAgenticWinFormsControl
{
    public AgenticRadioButton() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}

public class AgenticComboBox : ComboBox, IAgenticWinFormsControl
{
    public AgenticComboBox() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}

public class AgenticDateTimePicker : DateTimePicker, IAgenticWinFormsControl
{
    public AgenticDateTimePicker() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}
public class AgenticNumericUpDown : NumericUpDown, IAgenticWinFormsControl
{
    public AgenticNumericUpDown() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}
public class AgenticListBox : ListBox, IAgenticWinFormsControl
{
    public AgenticListBox() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}
public class AgenticCheckedListBox : CheckedListBox, IAgenticWinFormsControl
{
    public AgenticCheckedListBox()
    {
        CheckOnClick = true;
        AgenticControlBinder.Attach(this);
    }
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}
public class AgenticTabControl : TabControl, IAgenticWinFormsControl
{
    public AgenticTabControl() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}
public class AgenticTrackBar : TrackBar, IAgenticWinFormsControl
{
    public AgenticTrackBar() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}

public class AgenticDataGridView : DataGridView, IAgenticWinFormsControl
{
    public AgenticDataGridView() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}

public class AgenticTreeView : TreeView, IAgenticWinFormsControl
{
    public AgenticTreeView() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}

public class AgenticListView : ListView, IAgenticWinFormsControl
{
    public AgenticListView() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}

public class AgenticProgressBar : ProgressBar, IAgenticWinFormsControl
{
    public AgenticProgressBar() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}

public class AgenticLabel : Label, IAgenticWinFormsControl
{
    public AgenticLabel() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}

public class AgenticMenuStrip : MenuStrip, IAgenticWinFormsControl
{
    public AgenticMenuStrip() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}
public class AgenticToolStrip : ToolStrip, IAgenticWinFormsControl
{
    public AgenticToolStrip() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}
public class AgenticStatusStrip : StatusStrip, IAgenticWinFormsControl
{
    public AgenticStatusStrip() => AgenticControlBinder.Attach(this);
    [Category("AgenticUI")] public string? AgenticId { get => AgenticMetadataControl.Options(this).Id; set { AgenticMetadataControl.Options(this).Id = value; AgenticMetadataControl.Refresh(this); } }
    [Category("AgenticUI")] public string? AgenticDisplayName { get => AgenticMetadataControl.Options(this).DisplayName; set => AgenticMetadataControl.Options(this).DisplayName = value; }
    [Category("AgenticUI")] public bool IsSensitive { get => AgenticMetadataControl.Options(this).IsSensitive; set => AgenticMetadataControl.Options(this).IsSensitive = value; }
    [Category("AgenticUI")] public int InstructionNumber { get => AgenticMetadataControl.Options(this).InstructionNumber; set => AgenticMetadataControl.Options(this).InstructionNumber = value; }
    [Category("AgenticUI")] public string? Hint { get => AgenticMetadataControl.Options(this).Hint; set => AgenticMetadataControl.Options(this).Hint = value; }
}
