using System.Windows;
using System.Windows.Threading;

namespace DarkDesigns1Trainer;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "An unexpected error occurred:\n\n" + e.Exception.Message,
            "Dark Designs I Trainer", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
