using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace MightAndMagic1Trainer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The interesting error is almost never the outermost one: a throw during window/XAML
        // construction reaches us wrapped in a TargetInvocationException ("Exception has been
        // thrown by the target of an invocation."), whose Message tells you nothing. Report the
        // whole chain — innermost first — and drop a copy on disk for after the dialog is gone.
        DispatcherUnhandledException += (_, args) =>
        {
            Report(args.Exception);
            args.Handled = true;
        };

        // A background-thread throw (a pool thread, a finalizer) never reaches the dispatcher
        // handler; catch it here so it's logged instead of silently killing the process.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) Report(ex);
        };
    }

    private static void Report(Exception ex)
    {
        string details = Describe(ex);
        string logPath = TryWriteLog(details);

        // Lead with the real (innermost) cause; the full chain + stack are in the log.
        var root = Innermost(ex);
        var sb = new StringBuilder();
        sb.Append(root.GetType().Name).Append(": ").AppendLine(root.Message);
        if (!ReferenceEquals(root, ex))
            sb.AppendLine().Append("(surfaced as ").Append(ex.GetType().Name).Append(')');
        if (logPath.Length > 0)
            sb.AppendLine().AppendLine().Append("Full details written to:").AppendLine().Append(logPath);

        MessageBox.Show(sb.ToString(), "MM1 Trainer — unexpected error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static Exception Innermost(Exception ex)
    {
        while (ex.InnerException is { } inner) ex = inner;
        return ex;
    }

    private static string Describe(Exception ex)
    {
        var sb = new StringBuilder();
        for (var cur = ex; cur != null; cur = cur.InnerException)
        {
            if (sb.Length > 0) sb.AppendLine().AppendLine("── caused by ──");
            sb.Append(cur.GetType().FullName).Append(": ").AppendLine(cur.Message);
            if (cur.StackTrace is { Length: > 0 } stack) sb.AppendLine(stack);
        }
        return sb.ToString();
    }

    private static string TryWriteLog(string details)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MM1Trainer");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "crash.log");
            var header = $"MM1 Trainer crash — {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                         $"Version {Assembly.GetExecutingAssembly().GetName().Version}{Environment.NewLine}{Environment.NewLine}";
            File.WriteAllText(path, header + details);
            return path;
        }
        catch
        {
            return "";   // logging is best-effort; the dialog still shows the cause
        }
    }
}
