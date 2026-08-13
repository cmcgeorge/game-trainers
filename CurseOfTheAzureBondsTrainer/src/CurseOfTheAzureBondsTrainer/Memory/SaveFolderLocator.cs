using System.Diagnostics;
using System.IO;
using CurseOfTheAzureBondsTrainer.Game;

namespace CurseOfTheAzureBondsTrainer.Memory;

/// <summary>
/// Finds the folder the game is actually saving into, so the offline editors open on a real party
/// instead of an empty list. There is no fixed install path: every re-release mounts the game
/// somewhere different, and the GOG build in particular mounts an <c>overlay</c> drive, which sends
/// every save to a <c>cloud_saves</c> folder beside the game rather than into the game folder itself.
///
/// So rather than guess, this walks outwards from the running emulator's own executable — whatever
/// DOSBox is playing the game must live next to that game's files — and falls back to the usual
/// store install roots when the game isn't running. Candidates are ranked by how recently they were
/// written, which picks the live overlay folder over the pristine install copy.
/// </summary>
public static class SaveFolderLocator
{
    private static readonly string[] EmulatorNames =
        { "dosbox", "dosbox-x", "dosbox-staging", "boxer" };

    /// <summary>
    /// Install roots worth checking when the game isn't running. These are only a fallback — with
    /// the game running the search starts from the emulator's own folder and never needs them.
    ///
    /// <para>Settable, and read from <c>CURSE_SAVE_ROOTS</c> (a <c>;</c>-separated list) at
    /// startup, so a copy installed somewhere unusual can be found without falling back to typing
    /// the path in by hand.</para>
    /// </summary>
    public static IReadOnlyList<string> FallbackRoots { get; set; } = ReadFallbackRoots();

    private static string[] ReadFallbackRoots()
    {
        string[] defaults =
        {
            @"C:\Temp\Games",
            @"C:\Temp\Scratch",
            @"C:\Games",
            @"C:\GOG Games",
            @"C:\Program Files (x86)\GOG Galaxy\Games",
            @"C:\Program Files (x86)\Steam\steamapps\common",
            @"C:\Program Files\GOG Galaxy\Games",
        };
        string? extra = Environment.GetEnvironmentVariable("CURSE_SAVE_ROOTS");
        if (string.IsNullOrWhiteSpace(extra)) return defaults;
        // Listed first: someone who names a root explicitly means that one.
        return extra.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Concat(defaults).ToArray();
    }

    /// <summary>Is an emulator running right now? The offline save editor asks, because the game
    /// rewrites its save files on its own schedule and an edit applied underneath it is simply
    /// overwritten when it next saves.</summary>
    public static bool EmulatorRunning()
    {
        foreach (var p in Process.GetProcesses())
        {
            bool match = false;
            try { match = EmulatorNames.Any(n => p.ProcessName.Contains(n, StringComparison.OrdinalIgnoreCase)); }
            catch { /* exited between enumeration and query */ }
            finally { p.Dispose(); }
            if (match) return true;
        }
        return false;
    }

    /// <summary>
    /// How far below a candidate root to look for the save folder. The GOG layout needs two
    /// (<c>Curse of the Azure Bonds\cloud_saves\CURSE</c>), but Curse writes into a <c>SAVE</c>
    /// sub-folder of the game rather than into the game folder itself (its <c>CURSE.CFG</c> records
    /// <c>C:\GAMES\CURSE\SAVE\</c>), and an emulated-drive layout buries that a few levels further
    /// down — <c>…\Win31DOSBox\C-DRIVE\GAMES\CURSE\SAVE</c>. Five covers those; deeper than that,
    /// name the folder with <c>CURSE_SAVE_ROOTS</c> or type it into the Save folder box.
    /// </summary>
    private const int MaxDepth = 5;

    /// <summary>The best save folder found, or null. "Best" is the most recently written, so an
    /// actively-played overlay folder wins over a stale copy of the same save.</summary>
    public static string? Find()
    {
        var best = (Path: (string?)null, Stamp: DateTime.MinValue);
        foreach (string root in Roots())
        {
            foreach (string dir in Descend(root))
            {
                if (!SaveGame.LooksLikeSaveFolder(dir)) continue;
                var stamp = Newest(dir);
                if (stamp > best.Stamp) best = (dir, stamp);
            }
        }
        return best.Path;
    }

    // --- candidate roots -----------------------------------------------------
    private static IEnumerable<string> Roots()
    {
        foreach (string dir in EmulatorFolders()) yield return dir;
        foreach (string root in FallbackRoots) if (Directory.Exists(root)) yield return root;
    }

    /// <summary>The game folders around every running emulator: its own directory and the two above
    /// it, since emulators are habitually launched from a <c>DOSBOX\</c> sub-folder of the game.</summary>
    private static IEnumerable<string> EmulatorFolders()
    {
        foreach (var p in Process.GetProcesses())
        {
            string? exe = null;
            try
            {
                if (EmulatorNames.Any(n => p.ProcessName.Contains(n, StringComparison.OrdinalIgnoreCase)))
                    exe = p.MainModule?.FileName;
            }
            catch { /* exited, or a process we may not query */ }
            finally { p.Dispose(); }

            var dir = exe == null ? null : Directory.GetParent(exe);
            for (int up = 0; up < 3 && dir != null; up++, dir = dir.Parent)
                yield return dir.FullName;
        }
    }

    /// <summary>A directory and its sub-directories down to <see cref="MaxDepth"/>.</summary>
    private static IEnumerable<string> Descend(string root, int depth = 0)
    {
        yield return root;
        if (depth >= MaxDepth) yield break;

        string[] children;
        try { children = Directory.GetDirectories(root); }
        catch { yield break; }   // unreadable or vanished — just skip it

        foreach (string child in children)
            foreach (string found in Descend(child, depth + 1))
                yield return found;
    }

    /// <summary>
    /// The folder holding the game's own resource files, given the folder it saves into. Curse
    /// writes to a <c>SAVE</c> sub-folder, so the archives are normally one level up — but some
    /// installs save into the game folder itself, so this checks there first and then walks up.
    /// Returns an empty string if nothing nearby looks like the game.
    /// </summary>
    public static string GameFolderFor(string? saveFolder)
    {
        if (string.IsNullOrWhiteSpace(saveFolder)) return "";
        var dir = new DirectoryInfo(saveFolder);
        for (int up = 0; up < 3 && dir != null; up++, dir = dir.Parent)
        {
            try
            {
                if (dir.Exists && dir.GetFiles("GEO?.DAX").Length > 0) return dir.FullName;
            }
            catch { /* unreadable — keep walking up */ }
        }
        return "";
    }

    private static DateTime Newest(string folder)
    {
        var newest = DateTime.MinValue;
        try
        {
            foreach (string f in Directory.EnumerateFiles(folder, "CHRDAT*.SAV"))
            {
                var stamp = File.GetLastWriteTimeUtc(f);
                if (stamp > newest) newest = stamp;
            }
        }
        catch { /* vanished mid-scan */ }
        return newest;
    }
}
