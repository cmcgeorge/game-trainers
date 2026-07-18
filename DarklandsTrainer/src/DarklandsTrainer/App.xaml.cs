using System.Windows;
using System.Windows.Threading;

namespace DarklandsTrainer;

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
            "Darklands Trainer", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
