using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Space1889Trainer.Files;

/// <summary>
/// Finds and reads the Space 1889 game folder. The trainer never copies game data into itself: maps,
/// tiles and NPC positions are read from the player's own installation when it can be found.
/// </summary>
public static partial class GameFolder
{
    /// <summary>Environment variable that overrides the search.</summary>
    public const string EnvironmentVariable = "S1889_GAME_DIR";

    /// <summary>Files every copy of the game has.</summary>
    private static readonly string[] Markers = { "M.EXE", "S.EXE", "A.SYS", "B.SYS", "ITEMS.DAT" };

    /// <summary>Does <paramref name="folder"/> hold the game?</summary>
    public static bool IsGameFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;
        foreach (var m in Markers)
            if (Find(folder, m) is null) return false;
        return true;
    }

    /// <summary>Case-insensitive lookup of a file in the folder (DOS names are stored in any case).</summary>
    public static string? Find(string folder, string fileName)
    {
        string direct = Path.Combine(folder, fileName);
        if (File.Exists(direct)) return direct;
        try
        {
            return Directory.EnumerateFiles(folder)
                .FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Reads a game file, or returns null.</summary>
    public static byte[]? ReadFile(string folder, string fileName)
    {
        var path = Find(folder, fileName);
        if (path is null) return null;
        try { return File.ReadAllBytes(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>
    /// Best guess at the game folder: the environment variable, then the <c>mount</c> lines of the attached
    /// emulator's configuration, then a few conventional locations on every fixed drive.
    /// </summary>
    public static string? Guess(int? emulatorProcessId = null)
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } env && IsGameFolder(env))
            return env;

        if (emulatorProcessId is int pid && FromEmulator(pid) is { } mounted)
            return mounted;

        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                bool dosbox;
                try { dosbox = p.ProcessName.StartsWith("dosbox", StringComparison.OrdinalIgnoreCase); }
                catch (InvalidOperationException) { continue; }
                if (dosbox && FromEmulator(p.Id) is { } hit) return hit;
            }
        }

        var stems = new[] { @"\GAMES\S1889", @"\S1889", @"\SPACE1889", @"\GAMES\SPACE1889", @"\DOS\S1889", @"\DOS\GAMES\S1889" };
        string[] drives;
        try
        {
            drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => d.Name.TrimEnd('\\')).ToArray();
        }
        catch (IOException) { drives = new[] { "C:" }; }
        foreach (var drive in drives)
            foreach (var stem in stems)
                if (IsGameFolder(drive + stem)) return drive + stem;
        return null;
    }

    // --- DOSBox configuration -------------------------------------------------------

    /// <summary>How many directories one search may look at, so a <c>mount c c:\</c> cannot walk a whole drive.</summary>
    private const int SearchBudget = 3000;

    private static string? FromEmulator(int pid)
    {
        string? conf = FindConfigFile(pid);
        if (conf is null) return null;
        string[] lines;
        try { lines = File.ReadAllLines(conf); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }

        int budget = SearchBudget;
        foreach (var line in lines)
        {
            if (ParseMountPath(line) is not { } root) continue;
            if (SearchBelow(root, 4, ref budget) is { } hit) return hit;
            if (budget <= 0) break;
        }
        return null;
    }

    private static string? FindConfigFile(int pid)
    {
        string? cmd = CommandLine(pid);
        if (cmd is not null && ConfArgument().Match(cmd) is { Success: true } m)
        {
            string path = m.Groups["path"].Value.Trim('"');
            if (File.Exists(path)) return path;
        }
        try
        {
            using var p = Process.GetProcessById(pid);
            string? dir = Path.GetDirectoryName(p.MainModule?.FileName);
            if (dir is null) return null;
            foreach (var name in new[] { "dosbox-x.conf", "dosbox.conf", "dosbox-staging.conf" })
                if (File.Exists(Path.Combine(dir, name))) return Path.Combine(dir, name);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        return null;
    }

    private static string? CommandLine(int pid)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            using var results = searcher.Get();
            foreach (var o in results)
                using (o) return o["CommandLine"] as string;
        }
        catch (Exception e) when (e is ManagementException or UnauthorizedAccessException or PlatformNotSupportedException or COMException) { }
        return null;
    }

    /// <summary>The host path of a DOSBox <c>mount</c> line (options stripped), or null.</summary>
    public static string? ParseMountPath(string line)
    {
        var m = MountCommand().Match(line ?? "");
        if (!m.Success) return null;
        string rest = m.Groups["rest"].Value.Trim();
        if (rest.Length == 0) return null;
        if (rest.StartsWith('"'))
        {
            int close = rest.IndexOf('"', 1);
            return close > 1 ? rest[1..close] : null;
        }
        int cut = rest.Length;
        for (int i = 1; i < rest.Length; i++)
            if (rest[i] == '-' && char.IsWhiteSpace(rest[i - 1])) { cut = i - 1; break; }
        string path = rest[..cut].TrimEnd();
        return path.Length > 0 ? path : null;
    }

    private static string? SearchBelow(string root, int depth, ref int budget)
    {
        if (depth < 0 || budget-- <= 0 || !Directory.Exists(root)) return null;
        if (IsGameFolder(root)) return root;
        string[] subs;
        try { subs = Directory.GetDirectories(root); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        foreach (var sub in subs)
        {
            if (SearchBelow(sub, depth - 1, ref budget) is { } hit) return hit;
            if (budget <= 0) return null;
        }
        return null;
    }

    [GeneratedRegex(@"^\s*mount\s+[a-zA-Z]\s+(?<rest>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MountCommand();

    [GeneratedRegex(@"-conf\s+(?<path>""[^""]+""|\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex ConfArgument();
}
