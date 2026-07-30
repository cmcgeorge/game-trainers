using System.IO;
using System.Text;

namespace SwordOfAragonTrainer.Game;

/// <summary>
/// A loaded <c>ARAGON.HS&lt;letter&gt;</c> save — the kingdom state. The file is plain ASCII written
/// by QuickBASIC's <c>WRITE #</c>: CRLF-terminated comma-separated lines, three header lines, then 20
/// city blocks of 14 lines, then a two-line trailer, then a <c>0x1A</c> end-of-file byte.
///
/// The trainer keeps the file as a list of lines and rewrites individual CSV fields, so a save that
/// goes through it differs from the original only in the numbers that were deliberately changed —
/// including the header line and trailer whose meaning is still unproven.
/// </summary>
public sealed class KingdomFile
{
    /// <summary>Header lines before the first city block.</summary>
    public const int HeaderLines = 3;

    /// <summary>Cities and wilderness regions in the save, always 20.</summary>
    public const int CityCount = 20;

    /// <summary>Lines the format needs at minimum: header + 20 blocks.</summary>
    public const int MinLineCount = HeaderLines + CityCount * CityRecord.BlockLines;   // 283

    /// <summary>DOS end-of-file byte QuickBASIC appends when it closes a text file.</summary>
    public const byte EofMarker = 0x1A;

    // header line 0: yearOffset, month, ?, ?, cursorX, cursorY
    private const int LineDate = 0;
    private const int FieldYearOffset = 0;
    private const int FieldMonth = 1;
    private const int FieldCursorX = 4;
    private const int FieldCursorY = 5;

    // header line 2: wealth, score, income, maintenance
    private const int LineGlobals = 2;
    private const int FieldWealth = 0;
    private const int FieldScore = 1;
    private const int FieldIncome = 2;
    private const int FieldMaintenance = 3;

    private readonly List<string> _lines;
    private readonly bool _hadEofMarker;

    /// <summary>Path the save was read from; <see cref="Save"/> writes back here by default.</summary>
    public string SourcePath { get; }

    /// <summary>The 20 city/region blocks in save order.</summary>
    public IReadOnlyList<CityRecord> Cities { get; }

    private KingdomFile(List<string> lines, bool hadEofMarker, string path)
    {
        _lines = lines;
        _hadEofMarker = hadEofMarker;
        SourcePath = path;
        Cities = Enumerable.Range(0, CityCount)
            .Select(i => new CityRecord(lines, i, HeaderLines + i * CityRecord.BlockLines))
            .ToArray();
    }

    // --- global figures ---------------------------------------------------------
    /// <summary>
    /// Gold in the treasury. A non-finite input becomes 0 rather than being clamped, because
    /// <c>Math.Clamp</c> lets NaN through.
    /// </summary>
    public double Wealth
    {
        get => CsvRow.GetDouble(_lines[LineGlobals], FieldWealth);
        set => _lines[LineGlobals] = CsvRow.SetDouble(_lines[LineGlobals], FieldWealth,
            double.IsFinite(value) ? Math.Clamp(value, 0, GameFacts.MaxWealth) : 0);
    }

    /// <summary>Current score, out of <see cref="GameFacts.MaxScore"/>.</summary>
    public int Score
    {
        get => CsvRow.GetInt(_lines[LineGlobals], FieldScore);
        set => _lines[LineGlobals] = CsvRow.SetInt(_lines[LineGlobals], FieldScore,
                                                   Math.Clamp(value, 0, GameFacts.MaxScore));
    }

    /// <summary>Total income from all cities last month. Recomputed monthly, so read-only.</summary>
    public double Income => CsvRow.GetDouble(_lines[LineGlobals], FieldIncome);

    /// <summary>Total army upkeep last month. Recomputed monthly, so read-only.</summary>
    public double Maintenance => CsvRow.GetDouble(_lines[LineGlobals], FieldMaintenance);

    /// <summary>Years elapsed since 871 QJ.</summary>
    public int YearOffset => CsvRow.GetInt(_lines[LineDate], FieldYearOffset);

    /// <summary>Month index, 0 = January.</summary>
    public int Month => CsvRow.GetInt(_lines[LineDate], FieldMonth);

    /// <summary>The in-game date, formatted the way the Chronicle of Deeds writes it.</summary>
    public string Date => GameFacts.FormatDate(YearOffset, Month);

    /// <summary>World-map cursor column.</summary>
    public int CursorX => CsvRow.GetInt(_lines[LineDate], FieldCursorX);

    /// <summary>World-map cursor row.</summary>
    public int CursorY => CsvRow.GetInt(_lines[LineDate], FieldCursorY);

    /// <summary>Cities whose "changed this month" lines are populated — i.e. the ones you own.</summary>
    public IEnumerable<CityRecord> PlayerCities => Cities.Where(c => c.LooksPlayerOwned);

    // --- load / save ------------------------------------------------------------
    /// <summary>
    /// Reads a kingdom save. Throws <see cref="InvalidDataException"/> if the file does not have the
    /// header + 20-block shape, or if any block is malformed — the read-validate-write guard that stops
    /// the trainer editing a file that is not an <c>ARAGON.HS?</c> save.
    /// </summary>
    public static KingdomFile Load(string path) => Parse(File.ReadAllBytes(path), path);

    /// <summary>Parses raw save bytes (used by <see cref="Load"/> and the verification harness).</summary>
    public static KingdomFile Parse(byte[] bytes, string path = "")
    {
        string text = Encoding.Latin1.GetString(bytes);
        bool hadEof = false;
        int eof = text.IndexOf((char)EofMarker);
        if (eof >= 0)
        {
            hadEof = true;
            text = text[..eof];
        }

        var lines = text.Split("\r\n").ToList();
        string name = string.IsNullOrEmpty(path) ? "save" : Path.GetFileName(path);
        if (lines.Count < MinLineCount)
            throw new InvalidDataException(
                $"'{name}' has {lines.Count} lines; a Sword of Aragon kingdom save has at least " +
                $"{MinLineCount}.");

        var save = new KingdomFile(lines, hadEof, path);
        if (CsvRow.Count(lines[LineGlobals]) < 4)
            throw new InvalidDataException($"'{name}' header line 3 does not hold four player figures.");

        foreach (var city in save.Cities)
        {
            string? problem = city.Validate();
            if (problem != null) throw new InvalidDataException($"'{name}': {problem}.");
        }
        return save;
    }

    /// <summary>
    /// Serialises back to the exact on-disk shape: CRLF joins and, if the original had one, the
    /// trailing <c>0x1A</c>.
    /// </summary>
    public byte[] ToBytes()
    {
        string text = string.Join("\r\n", _lines);
        if (_hadEofMarker) text += (char)EofMarker;
        return Encoding.Latin1.GetBytes(text);
    }

    /// <summary>
    /// Writes the save back. A one-off <c>.bak</c> copy is taken first if none exists yet; the path of
    /// a backup actually created is returned, or null when one was already present.
    /// </summary>
    public string? Save(string? path = null)
    {
        string target = path ?? SourcePath;
        string? backup = SaveBackup.EnsureFor(target);
        File.WriteAllBytes(target, ToBytes());
        return backup;
    }
}
