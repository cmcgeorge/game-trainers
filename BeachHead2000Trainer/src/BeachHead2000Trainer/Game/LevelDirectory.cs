using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BeachHead2000Trainer.Game;

/// <summary>
/// Locates the BeachHead 2000 level-file directory at runtime so the level editor's
/// open dialog can default to the right place. The <c>Level_00</c>…<c>Level_60</c> files
/// live in the <c>beachhead\</c> subdirectory of the game install; the Steam Gold Edition
/// installs the game to the <c>509610</c> subfolder of a Steam library. The directory is
/// resolved in priority order: a caller-supplied last-used directory, the attached game
/// process's executable folder, then the Steam libraries discovered from the registry and
/// <c>libraryfolders.vdf</c>. Returns null when nothing is found so the caller can fall
/// back to the system default.
/// </summary>
public static class LevelDirectory
{
    /// <summary>
    /// Returns the best-guess level-file directory, or null if none was found.
    /// <paramref name="processId"/> is the attached game process (0 when not attached).
    /// <paramref name="lastDir"/> is the directory used last in this session, if any.
    /// </summary>
    public static string? Find(int processId, string? lastDir)
    {
        if (!string.IsNullOrWhiteSpace(lastDir) && Directory.Exists(lastDir))
            return lastDir;

        if (processId != 0)
        {
            string? fromProcess = FromProcess(processId);
            if (fromProcess != null) return fromProcess;
        }

        return FromSteam();
    }

    private static string? FromProcess(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            string? exe = p.MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return null;
            string? root = Path.GetDirectoryName(exe);
            if (string.IsNullOrEmpty(root)) return null;
            string dir = Path.Combine(root, GameFacts.LevelSubdirectory);
            return Directory.Exists(dir) ? dir : null;
        }
        catch
        {
            // Process exited, access denied, or cross-bitness module enumeration failure.
            return null;
        }
    }

    private static string? FromSteam()
    {
        foreach (string library in SteamLibraries())
        {
            string common = Path.Combine(library, "steamapps", "common");
            // Confirmed Gold Edition layout: steamapps\common\BeachHead Gold Edition\509610\beachhead
            string nested = Path.Combine(common, GameFacts.SteamInstallFolder,
                GameFacts.SteamAppFolder, GameFacts.LevelSubdirectory);
            if (Directory.Exists(nested)) return nested;

            // Fallback: a flat steamapps\common\509610\beachhead layout.
            string flat = Path.Combine(common, GameFacts.SteamAppFolder, GameFacts.LevelSubdirectory);
            if (Directory.Exists(flat)) return flat;
        }
        return null;
    }

    private static IEnumerable<string> SteamLibraries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string full = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                             .Replace('/', Path.DirectorySeparatorChar);
            if (Directory.Exists(full)) seen.Add(full);
        }

        string? steamRoot = GetSteamRoot();
        TryAdd(steamRoot);
        if (steamRoot != null)
        {
            string vdf = Path.Combine(steamRoot, "config", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                try
                {
                    foreach (string library in ParseLibraryFolders(File.ReadAllText(vdf)))
                        TryAdd(library);
                }
                catch
                {
                }
            }
        }

        foreach (string common in CommonSteamRoots())
            TryAdd(common);

        return seen;
    }

    private static string? GetSteamRoot()
    {
        try
        {
            using RegistryKey? k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (k?.GetValue("SteamPath") is string s && !string.IsNullOrWhiteSpace(s))
                return s;
        }
        catch
        {
        }
        try
        {
            using RegistryKey? k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            if (k?.GetValue("InstallPath") is string s && !string.IsNullOrWhiteSpace(s))
                return s;
        }
        catch
        {
        }
        return null;
    }

    /// <summary>Extracts the <c>"path"</c> library folders from a <c>libraryfolders.vdf</c> body.</summary>
    private static IEnumerable<string> ParseLibraryFolders(string vdf)
    {
        foreach (Match m in Regex.Matches(vdf, @"""path""\s+""([^""]+)"""))
        {
            string path = m.Groups[1].Value;
            if (path.Length > 0)
                yield return path.Replace("\\\\", @"\");
        }
    }

    private static IEnumerable<string> CommonSteamRoots()
    {
        yield return @"C:\Program Files (x86)\Steam";
        yield return @"C:\Program Files\Steam";
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            string root = drive.RootDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar);
            yield return Path.Combine(root, "Steam");
            yield return Path.Combine(root, "SteamLibrary");
            yield return Path.Combine(root, "Games", "Steam");
        }
    }
}
