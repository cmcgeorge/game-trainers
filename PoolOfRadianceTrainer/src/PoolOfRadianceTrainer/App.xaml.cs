using System.Windows;
using System.Windows.Threading;
#if DEBUG
using System.IO;
#endif

namespace PoolOfRadianceTrainer;

public partial class App : Application
{
#if DEBUG
    // DEBUG-only: when PORTRAINER_SMOKETEST is set, load the main window, write a marker,
    // and exit — a headless smoke test that the XAML (resources, converters, bindings)
    // actually parses at runtime. Compiled out of Release builds entirely.
    private static readonly string? SmokeTestMarker = Environment.GetEnvironmentVariable("PORTRAINER_SMOKETEST");
#endif

    public App()
    {
        // A single unhandled-exception guard keeps a stray memory-access hiccup from
        // tearing down the whole trainer; the message surfaces instead.
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
                        // Realize EVERY tab so all DataTemplates and bindings actually activate — a
                        // binding fault like a TwoWay bind to a read-only property throws when its tab
                        // is first shown, not merely when the window loads the default tab.
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
    /// <summary>Depth-first search of the visual tree for the tabs, so the smoke test can walk them.</summary>
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

        MessageBox.Show(
            "An unexpected error occurred:\n\n" + e.Exception.Message,
            "Pool of Radiance Trainer", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
