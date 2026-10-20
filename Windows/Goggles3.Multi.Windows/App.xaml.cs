using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Goggles3.Multi.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--software-render")) RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        new MainWindow().Show();
    }
}
