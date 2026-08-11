using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using RedBaronTrainer.Game;

namespace RedBaronTrainer.Memory;

/// <summary>A finding about the emulator's configuration, with the reason it matters to Red Baron.</summary>
public sealed record ConfigFinding(string Severity, string Setting, string Value, string Explanation);

/// <summary>
/// Reads the configuration of the running DOSBox/DOSBox-X, both to locate the game folder and to
/// check the two settings Red Baron's game-port code is sensitive to.
///
/// <para>Red Baron does not ask the BIOS for the joystick; it times the one-shots on port 0x201
/// itself, counting a delay loop until each axis line falls and giving up at 400. The count it gets
/// therefore depends on the emulated CPU speed, which is exactly what <c>[cpu] cycles</c> controls,
/// and on whether the emulator drives those lines at all, which is what <c>[joystick] joysticktype</c>
/// controls. Those two settings are what this class reports on.</para>
/// </summary>
public static partial class DosBoxInspector
{
    /// <summary>Process names the trainer will attach to, most specific first.</summary>
    public static readonly string[] EmulatorProcessNames =
    {
        "dosbox-x", "dosbox-x-sdl2", "dosbox", "dosbox-staging", "dosbox74", "dosbox-notx",
    };

    /// <summary>
    /// Every running emulator process, best candidate first. The caller owns the returned
    /// <see cref="Process"/> objects and must dispose them; everything this method rejects is
    /// disposed here, because <c>GetProcesses</c> hands back one per process on the machine and each
    /// one holds a kernel handle until it is collected.
    /// </summary>
    public static IReadOnlyList<Process> FindEmulators()
    {
        var found = new List<Process>();
        foreach (var name in EmulatorProcessNames)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                if (found.Exists(x => x.Id == p.Id)) p.Dispose();
                else found.Add(p);
            }
        }
        // Anything else whose name merely starts with "dosbox" (custom builds get renamed a lot).
        foreach (var p in Process.GetProcesses())
        {
            bool keep;
            try
            {
                keep = p.ProcessName.StartsWith("dosbox", StringComparison.OrdinalIgnoreCase)
                    && !found.Exists(x => x.Id == p.Id);
            }
            catch (InvalidOperationException)
            {
                keep = false;   // the process exited between the snapshot and this read
            }
            if (keep) found.Add(p);
            else p.Dispose();
        }
        return found;
    }

    /// <summary>The process's full command line, or null when it cannot be read.</summary>
    public static string? GetCommandLine(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
            // The collection and each object are COM-backed and disposable; returning out of the
            // foreach would dispose only the enumerator, and this runs on the attach retry loop.
            using var results = searcher.Get();
            foreach (var o in results)
            {
                using (o)
                    return o["CommandLine"] as string;
            }
        }
        catch (Exception e) when (e is ManagementException or UnauthorizedAccessException
                                    or PlatformNotSupportedException or COMException)
        {
            // WMI is optional here; every caller has a fallback. A broken WMI repository surfaces as
            // a raw COMException rather than a ManagementException, and letting that escape would
            // pop a message box on every retry tick.
        }
        return null;
    }

    /// <summary>
    /// The .conf the emulator is running with: <c>-conf &lt;file&gt;</c> from the command line if
    /// present, otherwise the conventional file beside the executable.
    /// </summary>
    public static string? FindConfigFile(Process emulator)
    {
        ArgumentNullException.ThrowIfNull(emulator);
        string? cmd = GetCommandLine(emulator.Id);
        if (cmd != null)
        {
            var m = ConfArgument().Match(cmd);
            if (m.Success)
            {
                string path = m.Groups["path"].Value.Trim('"');
                if (File.Exists(path)) return path;
            }
        }

        string? dir = TryGetExeDirectory(emulator);
        if (dir == null) return null;
        foreach (var name in new[] { "dosbox-x.conf", "dosbox.conf", "dosbox-staging.conf" })
        {
            string path = Path.Combine(dir, name);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static string? TryGetExeDirectory(Process p)
    {
        try { return Path.GetDirectoryName(p.MainModule?.FileName); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// How many directories <see cref="FindGameFolder"/> will look at before giving up. Somebody's
    /// <c>mount c c:\</c> would otherwise turn "find the game folder" into a walk of the whole
    /// drive — and this runs synchronously while the window is being constructed, so the cost lands
    /// as a trainer that appears hung at launch.
    /// </summary>
    private const int SearchBudget = 3000;

    /// <summary>
    /// Finds Red Baron's folder by reading the <c>mount</c> lines out of the emulator's config and
    /// looking under each mounted host directory. Deliberately shallow and budgeted: the game is
    /// normally one or two levels down (<c>C-DRIVE\GAMES\RED</c>).
    /// </summary>
    public static string? FindGameFolder(string? configFile)
    {
        if (configFile == null || !File.Exists(configFile)) return null;

        string[] lines;
        try { lines = File.ReadAllLines(configFile); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }

        int budget = SearchBudget;
        foreach (var line in lines)
        {
            string? root = ParseMountPath(line);
            if (root == null) continue;

            string? hit = SearchBelow(root, depth: 3, ref budget);
            if (hit != null) return hit;
            if (budget <= 0) break;
        }
        return null;
    }

    /// <summary>
    /// The host path from a DOSBox <c>mount</c> line, or null when the line is not one.
    ///
    /// <para>Options are stripped from the tail rather than whitelisted. Real config files carry
    /// <c>-t dir</c>, <c>-label GAMES</c>, <c>-freesize 1024</c>, <c>-usecd 0</c> and more, and a
    /// pattern that only tolerates <c>-t</c> silently folds the rest into the path — which then
    /// fails <c>Directory.Exists</c> and looks exactly like "no mount line found".</para>
    /// </summary>
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

        // Everything up to the first " -option" is the path. Split on whitespace-then-dash so a
        // path containing a hyphen (C-DRIVE, the common case here) survives intact.
        int cut = rest.Length;
        for (int i = 1; i < rest.Length; i++)
        {
            if (rest[i] == '-' && char.IsWhiteSpace(rest[i - 1])) { cut = i - 1; break; }
        }
        string path = rest[..cut].TrimEnd();
        return path.Length > 0 ? path : null;
    }

    private static string? SearchBelow(string root, int depth, ref int budget)
    {
        if (depth < 0 || budget-- <= 0 || !Directory.Exists(root)) return null;
        if (GameFolder.IsGameFolder(root)) return root;
        string[] subs;
        try { subs = Directory.GetDirectories(root); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        foreach (var sub in subs)
        {
            var hit = SearchBelow(sub, depth - 1, ref budget);
            if (hit != null) return hit;
            if (budget <= 0) return null;
        }
        return null;
    }

    /// <summary>
    /// Reads the emulator config and reports the settings that decide whether Red Baron's game-port
    /// timing loop can see a stick.
    /// </summary>
    public static IReadOnlyList<ConfigFinding> CheckConfig(string? configFile)
    {
        var findings = new List<ConfigFinding>();
        if (configFile == null || !File.Exists(configFile))
        {
            findings.Add(new ConfigFinding("info", "config file", "(not found)",
                "Could not read the emulator's .conf, so its joystick and CPU settings were not checked."));
            return findings;
        }

        var settings = ReadSettings(configFile);

        string joystickType = settings.GetValueOrDefault("joystick.joysticktype", "auto");
        findings.Add(joystickType.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? new ConfigFinding("error", "[joystick] joysticktype", joystickType,
                "Joystick emulation is switched off, so port 0x201 reads back idle and Red Baron's "
                + "axis counter saturates at its 400-tick limit. Set joysticktype=2axis.")
            : joystickType.Equals("auto", StringComparison.OrdinalIgnoreCase)
                ? new ConfigFinding("warn", "[joystick] joysticktype", joystickType,
                    "'auto' emulates a game port only if SDL reported a stick when the emulator started. "
                    + "Set joysticktype=2axis to emulate one unconditionally - Red Baron then always sees "
                    + "a stick, and the mapper can bind a pad to it afterwards.")
                : new ConfigFinding("ok", "[joystick] joysticktype", joystickType,
                    "A game port is emulated unconditionally, which is what Red Baron's detection needs."));

        string timed = settings.GetValueOrDefault("joystick.timed", "true");
        findings.Add(timed.Equals("true", StringComparison.OrdinalIgnoreCase)
            ? new ConfigFinding("warn", "[joystick] timed", timed,
                "Timed axis intervals make the one-shot durations depend on wall-clock time rather than "
                + "emulated cycles, which is what makes Red Baron's calibration drift. Try timed=false.")
            : new ConfigFinding("ok", "[joystick] timed", timed,
                "Axis one-shots track emulated time, which keeps Red Baron's calibration stable."));

        string cycles = settings.GetValueOrDefault("cpu.cycles", "auto");
        bool unbounded = cycles.Contains("max", StringComparison.OrdinalIgnoreCase)
                      || cycles.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase);
        findings.Add(unbounded
            ? new ConfigFinding("error", "[cpu] cycles", cycles,
                "An unbounded cycle count makes the emulated CPU run the game-port delay loop far faster "
                + "than the one-shot decays, so the counter hits its 400 limit and the stick reads as "
                + "absent. Use a fixed count - 'cycles=fixed 12000' matches the 286/386 the game expects.")
            : new ConfigFinding("ok", "[cpu] cycles", cycles,
                "A fixed cycle count keeps the game-port timing loop in the range Red Baron calibrates for."));

        return findings;
    }

    /// <summary>Flattens a DOSBox .conf into <c>section.key -&gt; value</c>.</summary>
    public static Dictionary<string, string> ReadSettings(string configFile)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string section = "";
        string[] lines;
        try { lines = File.ReadAllLines(configFile); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return map; }

        foreach (var raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim().ToLowerInvariant();
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line[..eq].Trim().ToLowerInvariant();
            string value = line[(eq + 1)..].Trim();
            map[$"{section}.{key}"] = value;
        }
        return map;
    }

    [GeneratedRegex(@"-conf\s+(?<path>""[^""]+""|\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex ConfArgument();

    [GeneratedRegex(@"^\s*mount\s+[A-Za-z]\s+(?<rest>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MountCommand();
}
