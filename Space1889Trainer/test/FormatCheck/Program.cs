using System.IO;
using Space1889Trainer.Files;

namespace Space1889Trainer.FormatCheck;

/// <summary>
/// Headless verification for the Space 1889 trainer.
/// <para>
/// Everything in the first group runs with no game and no emulator: format constants, the reference books, a
/// synthetic state block driven through every record view (including a target that refuses writes), the locator
/// over a synthetic DOSBox guest, the file decoders over synthetic files, the view-models over a fake host, and a
/// XAML smoke test that builds the real window. When a game folder is found (first non-option argument, the
/// <c>S1889_GAME_DIR</c> environment variable, or the usual guesses) the shipped <c>DEF.S</c>, <c>ITEMS.DAT</c>,
/// career table and all 144 maps are checked too.
/// </para>
/// <para>
/// <c>--live</c> attaches (read-only) to a running DOSBox and verifies the locator end to end; it is skipped rather
/// than failed when there is none. <c>--export-maps &lt;dir&gt;</c> writes the schematic PNGs the strategy guide uses.
/// </para>
/// </summary>
internal static partial class Program
{
    private static int _checks;
    private static int _failures;

    private static void Check(bool condition, string what)
    {
        _checks++;
        if (condition) return;
        _failures++;
        Console.Error.WriteLine($"  FAIL  {what}");
    }

    private static void CheckEqual<T>(T expected, T actual, string what)
    {
        _checks++;
        if (EqualityComparer<T>.Default.Equals(expected, actual)) return;
        _failures++;
        Console.Error.WriteLine($"  FAIL  {what}: expected {expected}, got {actual}");
    }

    [STAThread]
    private static int Main(string[] args)
    {
        bool live = args.Contains("--live");
        int exportAt = Array.IndexOf(args, "--export-maps");
        string? exportDir = exportAt >= 0 && exportAt + 1 < args.Length ? args[exportAt + 1] : null;
        string? folder = args.Where((a, i) => !a.StartsWith("--") && (exportAt < 0 || i != exportAt + 1)).FirstOrDefault();
        folder ??= GameFolder.Guess();

        Console.WriteLine("Space 1889 trainer - format checks");
        Console.WriteLine(new string('-', 60));

        CheckFormatConstants();
        CheckBooks();
        CheckSyntheticState();
        CheckCharacterRecord();
        CheckWorldRecord();
        CheckSaveGame();
        CheckLocator();
        CheckSyntheticFiles();
        CheckViewModels();
        CheckSaveEditor();
        CheckCluebookWithoutGame();
        CheckWindow();

        if (folder is not null && GameFolder.IsGameFolder(folder))
        {
            Console.WriteLine($"Game folder: {folder}");
            CheckShippedState(folder);
            CheckShippedItems(folder);
            CheckShippedCareers(folder);
            CheckShippedMaps(folder);
            if (exportDir is not null) ExportMaps(folder, exportDir);
        }
        else
        {
            Console.WriteLine("Game folder not found - skipping the checks against the shipped data files.");
            Console.WriteLine($"Pass the Space 1889 folder as the first argument, or set {GameFolder.EnvironmentVariable}, to run them.");
        }

        if (live) CheckLive();

        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{_checks - _failures}/{_checks} checks passed.");
        if (_failures > 0) Console.Error.WriteLine($"{_failures} FAILED.");
        return _failures == 0 ? 0 : 1;
    }

    private static void Section(string name) => Console.WriteLine("  " + name);

    private static string TempDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(), "S1889FormatCheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
