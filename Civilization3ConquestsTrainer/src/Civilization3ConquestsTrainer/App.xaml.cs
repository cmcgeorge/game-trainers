using System.Windows;
using System.Windows.Threading;

namespace Civilization3ConquestsTrainer;

/// <summary>Application shell. Surfaces unhandled dispatcher errors instead of dying silently.</summary>
public partial class App : Application
{
    public App() => DispatcherUnhandledException += OnDispatcherUnhandledException;

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "An unexpected error occurred:\n\n" + e.Exception.Message,
            "Civilization III: Conquests Trainer", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
