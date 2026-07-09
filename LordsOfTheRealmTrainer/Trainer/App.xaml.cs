using System.Windows;
using System.Windows.Threading;

namespace LordsTrainer;

public partial class App : Application
{
    public App()
    {
        // Never let an unexpected error take the whole trainer down silently.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Fail safe rather than fail open: since our steady state is writing another
        // process's memory off a cached base pointer, an unexpected fault may mean the
        // state that made writes safe has broken. Detach first (stops the freeze loop
        // and all writes), then show a generic message — the raw exception text can leak
        // internal paths, so we surface only the exception type for support.
        (MainWindow as MainWindow)?.HandleFatalError();

        MessageBox.Show(
            "An unexpected error occurred, so the trainer detached from the game to avoid " +
            "writing bad data. You can re-attach when you're ready.\n\n(" + e.Exception.GetType().Name + ")",
            "Lords Trainer — unexpected error",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
