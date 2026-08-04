namespace AgenticUI.Workbench.WinForms;

public partial class ConfirmDialogForm : Form
{
    public ConfirmDialogForm()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelButton_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
