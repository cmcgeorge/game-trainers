using System.Windows;
using System.Windows.Threading;

namespace SyndicateTrainer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show("Unexpected error:\n\n" + e.Exception.Message,
            "Syndicate Plus Trainer", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
