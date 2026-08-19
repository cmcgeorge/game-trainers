using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Roadwar2000Trainer;

public partial class App : Application
{
#if DEBUG
    // DEBUG-only: with RW2K_SMOKETEST set, load the window, walk every tab so all the
    // DataTemplates and bindings actually activate, write a marker and exit. That catches a
    // XAML fault which would otherwise only show up when a human clicks the fourth tab.
    // Compiled out of Release builds entirely.
    private static readonly string? SmokeTestMarker = Environment.GetEnvironmentVariable("RW2K_SMOKETEST");
#endif

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

#if DEBUG
        if (SmokeTestMarker != null)
        {
            Startup += (_, _) =>
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    try
                    {
                        if (MainWindow is { } w && FindTabControl(w) is { } tabs)
                        {
                            for (int i = 0; i < tabs.Items.Count; i++) { tabs.SelectedIndex = i; w.UpdateLayout(); }
                            tabs.SelectedIndex = 0;
                        }
                        File.WriteAllText(SmokeTestMarker, "OK");
                        Shutdown(0);
                    }
                    catch (Exception ex) { File.WriteAllText(SmokeTestMarker, "ERROR: " + ex); Shutdown(1); }
                };
                timer.Start();
            };
        }
#endif
    }

#if DEBUG
    private static System.Windows.Controls.TabControl? FindTabControl(DependencyObject root)
    {
        if (root is System.Windows.Controls.TabControl tc) return tc;
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
            if (FindTabControl(System.Windows.Media.VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }
#endif

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
#if DEBUG
        if (SmokeTestMarker != null)
        {
            File.WriteAllText(SmokeTestMarker, "ERROR: " + e.Exception);
            e.Handled = true;
            Shutdown(1);
            return;
        }
#endif

        // Anything that reaches the dispatcher escaped the operation that raised it, so whatever
        // it was doing -- a partial vehicle record, a half-applied freeze -- did not finish. The
        // full diagnostics go to a log beside the executable and the user chooses whether to
        // carry on, because continuing with a view-model that is out of step with the game is
        // how a trainer ends up writing nonsense into a running save.
        string log = WriteCrashLog(e.Exception);
        var choice = MessageBox.Show(
            "An unexpected error occurred:\n\n" + e.Exception.Message +
            "\n\nThe operation did not finish, so the trainer may be out of step with the game. " +
            "Closing now is the safe option; if you continue, Detach and Attach again before " +
            "editing anything.\n\nDetails written to:\n" + log +
            "\n\nClose the trainer?",
            "Roadwar 2000 Trainer", MessageBoxButton.YesNo, MessageBoxImage.Error);
        e.Handled = true;
        if (choice == MessageBoxResult.Yes) Shutdown(1);
    }

    private static string WriteCrashLog(Exception ex)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory,
                $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path, $"{DateTime.Now:u}{Environment.NewLine}{ex}");
            return path;
        }
        catch (Exception logFailure) { return "(could not write a log: " + logFailure.Message + ")"; }
    }
}
