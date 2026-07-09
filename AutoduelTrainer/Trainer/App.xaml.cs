using System.Windows;
using System.Windows.Threading;

namespace AutoduelTrainer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Autoduel Trainer — Error",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
