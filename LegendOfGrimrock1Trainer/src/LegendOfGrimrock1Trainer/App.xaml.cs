using System.Windows;
using System.Windows.Threading;

namespace LegendOfGrimrock1Trainer;

/// <summary>Application shell. Surfaces unhandled dispatcher errors instead of dying silently.</summary>
public partial class App : Application
{
    /// <summary>Hooks the dispatcher's unhandled-exception event.</summary>
    public App() => DispatcherUnhandledException += OnDispatcherUnhandledException;

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "An unexpected error occurred:\n\n" + e.Exception.Message,
            "Legend of Grimrock Trainer", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
