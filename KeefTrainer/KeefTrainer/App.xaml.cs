using System.Windows;

namespace KeefTrainer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Diagnostic screenshot mode renders headlessly; force the software rasterizer
        // so RenderTargetBitmap captures real pixels even without GPU/DWM access.
        if (Array.IndexOf(e.Args, "--screenshot") >= 0)
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        base.OnStartup(e);
    }
}
