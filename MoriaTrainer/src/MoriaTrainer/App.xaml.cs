using System.Windows;
using System.Windows.Threading;

namespace MoriaTrainer;

public partial class App : Application
{
    public App()
    {
        // A single unhandled-exception guard keeps a stray memory-access hiccup from tearing
        // down the whole trainer; the message surfaces instead.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "An unexpected error occurred:\n\n" + e.Exception.Message,
            "UMoria Trainer", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
