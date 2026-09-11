namespace AgenticUI.Workbench.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var host = AgenticUI.Remote.AgenticApplicationHost.StartFromConfiguration();
        Application.Run(new MainForm());
    }
}
