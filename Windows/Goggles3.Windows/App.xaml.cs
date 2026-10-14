using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Goggles3.Windows;
public partial class App : Application
{
    public App()
    {
        // An explicit fallback for virtual display drivers that cannot initialize WPF.
        if (Environment.GetCommandLineArgs().Contains("--software-render"))
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
    }
}
