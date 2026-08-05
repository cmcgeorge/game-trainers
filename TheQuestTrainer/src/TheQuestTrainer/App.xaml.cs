using System.Windows;
using System.Windows.Threading;

namespace TheQuestTrainer;

/// <summary>
/// Application entry point. The only behaviour here is turning an unhandled exception into a
/// message box instead of a silent process exit — a trainer that vanishes mid-session while the
/// user is mid-dungeon is worse than one that says what went wrong.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"{e.Exception.GetType().Name}: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "The Quest Trainer — unexpected error",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
