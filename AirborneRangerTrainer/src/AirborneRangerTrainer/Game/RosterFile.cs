using System.IO;

namespace AirborneRangerTrainer.Game;

/// <summary>
/// A typed, mutable view of one ranger record inside a loaded <c>ROSTER.DAT</c> image.
///
/// <para>Every setter rewrites both representations the file carries — the human-readable text line
/// <i>and</i> the binary tail — because the game reads the tail for its rank and decoration logic
/// but prints the text. Writing only one of them would produce a roster that displays one thing and
/// behaves as another.</para>
///
/// <para>Bytes 3 and 4 of the tail are <b>not</b> interpreted: across the shipped roster they read
/// <c>00 00</c>, <c>01 A4</c>, <c>00 00</c>, <c>01 0F</c>, <c>02 0E</c>, <c>01 E2</c>, which
/// correlates with neither the score nor any obvious mission count in either byte order. They are
/// round-tripped verbatim.</para>
/// </summary>
public sealed class RangerRecord
{
    private readonly byte[] _file;
    private readonly int _base;

    internal RangerRecord(byte[] file, int slot)
    {
        _file = file;
        Slot = slot;
        _base = RosterFormat.RecordOffset(slot);
    }

    /// <summary>Zero-based slot in the roster file.</summary>
    public int Slot { get; }

    private int Line1 => _base + RosterFormat.OffLine1;
    private int Line2 => _base + RosterFormat.OffLine2;
    private int Tail => _base + RosterFormat.OffTail;

    /// <summary>
    /// True when the slot holds a real ranger. An empty slot is all spaces and zeros; the game's own
    /// blank template still carries the <c>PFC</c> mnemonic and a <c>000000</c> score, so emptiness
    /// is decided by the name, which is the only field a real ranger must fill in.
    /// </summary>
    public bool IsOccupied => Name.Length > 0;

    /// <summary>The ranger's name, trimmed.</summary>
    public string Name
    {
        get => RosterFormat.ReadAscii(_file, Line1 + RosterFormat.LineNameColumn, RosterFormat.NameLength).Trim();
        set => RosterFormat.WriteAscii(_file, Line1 + RosterFormat.LineNameColumn, RosterFormat.NameLength,
                                       RosterFormat.SanitiseName(value));
    }

    /// <summary>Rank index into <see cref="RankBook"/>. Writing it updates the text mnemonic too.</summary>
    public int RankIndex
    {
        get => _file[Tail + RosterFormat.TailRankIndex];
        set
        {
            int idx = Math.Clamp(value, 0, RankBook.Count - 1);
            _file[Tail + RosterFormat.TailRankIndex] = (byte)idx;
            RosterFormat.WriteAscii(_file, Line1 + RosterFormat.LineRankColumn, 3, RankBook.Mnemonic(idx));
        }
    }

    /// <summary>The rank mnemonic the game prints, from the tail index.</summary>
    public string RankMnemonic => RankBook.Mnemonic(RankIndex);

    /// <summary>The rank's full name.</summary>
    public string RankName => RankBook.Name(RankIndex);

    /// <summary>
    /// Career merit points. Stored only as six ASCII digits in the text line, so a non-numeric field
    /// (an empty slot) reads as zero.
    /// </summary>
    public int Score
    {
        get
        {
            string text = RosterFormat.ReadAscii(_file, Line1 + RosterFormat.LineScoreColumn, RosterFormat.ScoreDigits);
            return int.TryParse(text.Trim(), out int v) ? v : 0;
        }
        set
        {
            int v = Math.Clamp(value, 0, RosterFormat.MaxScore);
            RosterFormat.WriteAscii(_file, Line1 + RosterFormat.LineScoreColumn, RosterFormat.ScoreDigits,
                                    v.ToString("D" + RosterFormat.ScoreDigits));
        }
    }

    /// <summary>
    /// Decoration bitmask; see <see cref="DecorationBook"/>. Writing it re-renders the ribbon line.
    /// </summary>
    public int Decorations
    {
        get => _file[Tail + RosterFormat.TailDecorations];
        set
        {
            int mask = value & DecorationBook.AllMask;
            _file[Tail + RosterFormat.TailDecorations] = (byte)mask;
            RosterFormat.WriteAscii(_file, Line2, RosterFormat.DecorationLineLength,
                                    DecorationBook.RenderLine(mask, HasCampaignRibbon));
        }
    }

    /// <summary>
    /// True when the ribbon line carries the <c>(CMPN)</c> campaign marker. It has no bit in the
    /// tail mask, so it lives only in the text and is preserved across decoration edits.
    /// </summary>
    public bool HasCampaignRibbon
    {
        get => RosterFormat.ReadAscii(_file, Line2, RosterFormat.DecorationLineLength)
                           .Contains(DecorationBook.CampaignMarker, StringComparison.Ordinal);
        set => RosterFormat.WriteAscii(_file, Line2, RosterFormat.DecorationLineLength,
                                       DecorationBook.RenderLine(Decorations, value));
    }

    /// <summary>True when <paramref name="bit"/> of the decoration mask is set.</summary>
    public bool HasDecoration(int bit) => (Decorations & bit) != 0;

    /// <summary>Sets or clears one decoration bit.</summary>
    public void SetDecoration(int bit, bool on) =>
        Decorations = on ? Decorations | bit : Decorations & ~bit;

    /// <summary>The ribbon line exactly as stored.</summary>
    public string DecorationLine =>
        RosterFormat.ReadAscii(_file, Line2, RosterFormat.DecorationLineLength).TrimEnd();

    /// <summary>The rank/name/score line exactly as stored.</summary>
    public string TextLine =>
        RosterFormat.ReadAscii(_file, Line1, RosterFormat.LineLength).TrimEnd();
}

/// <summary>
/// A loaded <c>ROSTER.DAT</c>. Parsing is strict — a file that does not match
/// <see cref="RosterFormat.LooksLikeRoster"/> is rejected rather than edited — and saving takes a
/// one-shot <c>.bak</c> of the original before the first write.
/// </summary>
public sealed class RosterFile
{
    private readonly byte[] _bytes;

    private RosterFile(byte[] bytes, string? path)
    {
        _bytes = bytes;
        Path = path;
        var records = new RangerRecord[RosterFormat.RecordCount];
        for (int i = 0; i < records.Length; i++) records[i] = new RangerRecord(_bytes, i);
        Records = records;
    }

    /// <summary>Where the file was loaded from, if it came from disk.</summary>
    public string? Path { get; }

    /// <summary>The six ranger slots, in file order.</summary>
    public IReadOnlyList<RangerRecord> Records { get; }

    /// <summary>The raw file image, as it would be written back.</summary>
    public byte[] Bytes => _bytes;

    /// <summary>Parses a roster image, returning null when it is not one.</summary>
    public static RosterFile? TryParse(byte[]? bytes, string? path = null)
    {
        if (!RosterFormat.LooksLikeRoster(bytes)) return null;
        return new RosterFile((byte[])bytes!.Clone(), path);
    }

    /// <summary>Loads and parses a roster file, returning null when it is missing or malformed.</summary>
    public static RosterFile? Load(string path)
    {
        try
        {
            return TryParse(File.ReadAllBytes(path), path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the roster back, taking a <c>.bak</c> of the untouched original first if one does not
    /// exist yet. The backup is deliberately one-shot: repeated saves must not overwrite the copy of
    /// the file as it was before the trainer ever touched it.
    /// </summary>
    /// <returns>The path of the backup that was created, or null if none was needed.</returns>
    public string? Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        string backup = path + ".bak";
        string? created = null;
        if (File.Exists(path) && !File.Exists(backup))
        {
            File.Copy(path, backup);
            created = backup;
        }
        File.WriteAllBytes(path, _bytes);
        return created;
    }
}
