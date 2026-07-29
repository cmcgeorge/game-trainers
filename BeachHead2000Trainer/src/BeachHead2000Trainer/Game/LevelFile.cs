using System.Globalization;
using System.IO;
using System.Text;

namespace BeachHead2000Trainer.Game;

/// <summary>
/// Parsed representation of a BeachHead 2000 level file (<c>Level_00</c> … <c>Level_60</c>).
/// The format is a simple text script: an <c>Ammo</c> line, a <c>Time</c> line, an
/// <c>Aggression</c> line, an <c>Artillery</c> flag, then a series of <c>Object</c> /
/// <c>ObjectInc</c> blocks with <c>Visible</c>, <c>Delay</c>, and <c>Revive</c> properties,
/// terminated by <c>End</c>. Comments start with <c>//</c> or <c>/*** ... ***/</c>.
/// The parser preserves all lines so the editor can round-trip a file without losing
/// comments, blank lines, or unknown properties.
/// </summary>
public sealed class LevelFile
{
    /// <summary>Raw lines from the file, in order. The editor mutates in place and re-serializes.</summary>
    public List<string> Lines { get; } = new();

    /// <summary>Parsed ammo values: [bullets, projectiles, missiles].</summary>
    public int Bullets { get; set; }
    public int Projectiles { get; set; }
    public int Missiles { get; set; }

    /// <summary>Time limit in seconds.</summary>
    public int Time { get; set; }

    /// <summary>Aggression values: [tank, jet, heliGun, heliRocket], each 1-9.</summary>
    public int AggressionTank { get; set; }
    public int AggressionJet { get; set; }
    public int AggressionHeliGun { get; set; }
    public int AggressionHeliRocket { get; set; }

    /// <summary>Artillery strikes flag (0 = off, 1 = on).</summary>
    public int Artillery { get; set; }

    /// <summary>Path the file was loaded from (null if created from scratch).</summary>
    public string? SourcePath { get; set; }

    // --- parsing ---------------------------------------------------------------

    /// <summary>Parses a level file from disk. Throws on I/O errors.</summary>
    public static LevelFile Load(string path)
    {
        var text = File.ReadAllText(path, Encoding.ASCII);
        var lf = Parse(text);
        lf.SourcePath = path;
        return lf;
    }

    /// <summary>Parses level-file text into a <see cref="LevelFile"/>.</summary>
    public static LevelFile Parse(string text)
    {
        var lf = new LevelFile();
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        // Drop the trailing empty string produced by a final newline so round-trip
        // doesn't grow the file by one blank line each save.
        while (lines.Length > 0 && lines[^1].Length == 0)
            lines = lines[..^1];
        lf.Lines.AddRange(lines);

        bool inBlockComment = false;
        foreach (var line in lf.Lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//")) continue;
            if (trimmed.StartsWith("/***")) inBlockComment = !trimmed.EndsWith("***/");
            if (inBlockComment) continue;
            if (trimmed.StartsWith("***/")) { inBlockComment = false; continue; }

            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0])
            {
                case "Ammo" when parts.Length >= 4:
                    if (TryParseInt(parts[1], out int b)) lf.Bullets = b;
                    if (TryParseInt(parts[2], out int p)) lf.Projectiles = p;
                    if (TryParseInt(parts[3], out int m)) lf.Missiles = m;
                    break;
                case "Time" when parts.Length >= 2:
                    if (TryParseInt(parts[1], out int t)) lf.Time = t;
                    break;
                case "Aggression" when parts.Length >= 5:
                    if (TryParseInt(parts[1], out int at)) lf.AggressionTank = at;
                    if (TryParseInt(parts[2], out int aj)) lf.AggressionJet = aj;
                    if (TryParseInt(parts[3], out int ahg)) lf.AggressionHeliGun = ahg;
                    if (TryParseInt(parts[4], out int ahr)) lf.AggressionHeliRocket = ahr;
                    break;
                case "Artillery" when parts.Length >= 2:
                    if (TryParseInt(parts[1], out int ar)) lf.Artillery = ar;
                    break;
            }
        }

        return lf;
    }

    /// <summary>Serializes the level file back to text, applying edited values to the Ammo,
    /// Time, Aggression, and Artillery lines. All other lines (comments, objects, blanks)
    /// are preserved as-is.</summary>
    public string ToText()
    {
        var sb = new StringBuilder();
        bool ammoWritten = false, timeWritten = false, aggrWritten = false, artWritten = false;

        bool inBlockComment = false;
        foreach (var line in Lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//"))
            {
                sb.AppendLine(line);
                continue;
            }
            if (trimmed.StartsWith("/***"))
            {
                inBlockComment = !trimmed.EndsWith("***/");
                sb.AppendLine(line);
                continue;
            }
            if (inBlockComment)
            {
                if (trimmed.StartsWith("***/")) inBlockComment = false;
                sb.AppendLine(line);
                continue;
            }

            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) { sb.AppendLine(line); continue; }

            switch (parts[0])
            {
                case "Ammo":
                    sb.AppendLine($"Ammo {Bullets} {Projectiles} {Missiles}");
                    ammoWritten = true;
                    break;
                case "Time":
                    sb.AppendLine($"Time {Time}");
                    timeWritten = true;
                    break;
                case "Aggression":
                    sb.AppendLine($"Aggression {AggressionTank} {AggressionJet} {AggressionHeliGun} {AggressionHeliRocket}");
                    aggrWritten = true;
                    break;
                case "Artillery":
                    sb.AppendLine($"Artillery {Artillery}");
                    artWritten = true;
                    break;
                default:
                    sb.AppendLine(line);
                    break;
            }
        }

        // Append any header fields that were missing from the original file,
        // in the standard order (Ammo, Time, Aggression, Artillery).
        var missing = new StringBuilder();
        if (!ammoWritten) missing.AppendLine($"Ammo {Bullets} {Projectiles} {Missiles}");
        if (!timeWritten) missing.AppendLine($"Time {Time}");
        if (!aggrWritten) missing.AppendLine($"Aggression {AggressionTank} {AggressionJet} {AggressionHeliGun} {AggressionHeliRocket}");
        if (!artWritten) missing.AppendLine($"Artillery {Artillery}");
        if (missing.Length > 0) sb.Insert(0, missing.ToString());

        return sb.ToString();
    }

    /// <summary>Saves the level file to disk (overwrites the source path or a given path).</summary>
    public void Save(string? path = null)
    {
        var target = path ?? SourcePath
            ?? throw new InvalidOperationException("No file path to save to.");
        File.WriteAllText(target, ToText(), Encoding.ASCII);
        SourcePath = target;
    }

    private static bool TryParseInt(string s, out int value) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
