using System.Windows;

namespace AgenticUI.Workbench.Wpf;

public partial class App : Application
{
    private AgenticUI.Remote.AgenticApplicationHost? _host;
    protected override void OnStartup(StartupEventArgs e)
    {
        _host = AgenticUI.Remote.AgenticApplicationHost.StartFromConfiguration();
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
