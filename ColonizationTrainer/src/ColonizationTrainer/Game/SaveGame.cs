using System.IO;
using System.Text;

namespace ColonizationTrainer.Game;

/// <summary>
/// A loaded <c>COLONYxx.SAV</c> file: the raw bytes plus typed views over the header, the four nation
/// records (which hold gold/tax/Fathers), and the colony records. The file has no checksum, so edits
/// are pure in-place byte writes; <see cref="Save"/> writes the buffer back and keeps a one-time
/// <c>.bak</c>. Section offsets are computed from the header counts (see
/// <c>docs/Colonization-Reverse-Engineering.md §1</c>), never hard-coded past the header.
/// </summary>
public sealed class SaveGame
{
    private readonly byte[] _data;

    private SaveGame(byte[] data, string path)
    {
        _data = data;
        Path = path;

        MapWidth = Bytes.U16(_data, SaveFormat.Off_MapSizeX);
        MapHeight = Bytes.U16(_data, SaveFormat.Off_MapSizeY);
        Year = Bytes.U16(_data, SaveFormat.Off_Year);
        IsAutumn = Bytes.U16(_data, SaveFormat.Off_Season) != 0;
        Turn = Bytes.U16(_data, SaveFormat.Off_Turn);
        Difficulty = Bytes.U8(_data, SaveFormat.Off_Difficulty);
        HumanPlayer = Bytes.U16(_data, SaveFormat.Off_HumanPlayer);
        ColonyCount = Bytes.U16(_data, SaveFormat.Off_ColonyCount);
        UnitCount = Bytes.U16(_data, SaveFormat.Off_UnitCount);
        TribeCount = Bytes.U16(_data, SaveFormat.Off_TribeCount);
        IsManualSave = Bytes.U8(_data, SaveFormat.Off_ManualSaveFlag) != 0;

        var leaders = new string[SaveFormat.NationCount];
        var countries = new string[SaveFormat.NationCount];
        for (int i = 0; i < SaveFormat.NationCount; i++)
        {
            int b = SaveFormat.PlayerBlockStart + i * SaveFormat.PlayerRecordSize;
            leaders[i] = ColonyText.ReadName(_data, b + SaveFormat.Player_Name, SaveFormat.PlayerNameMax);
            countries[i] = ColonyText.ReadName(_data, b + SaveFormat.Player_Country, SaveFormat.PlayerNameMax);
        }
        LeaderNames = leaders;
        CountryNames = countries;

        var nations = new NationRecord[SaveFormat.NationCount];
        for (int i = 0; i < SaveFormat.NationCount; i++)
            nations[i] = new NationRecord(_data, SaveFormat.NationBase(ColonyCount, UnitCount, i), i);
        Nations = nations;

        var colonies = new List<ColonyRecord>(ColonyCount);
        for (int i = 0; i < ColonyCount; i++)
            colonies.Add(new ColonyRecord(_data, SaveFormat.ColonyBase(i), i));
        Colonies = colonies;
    }

    /// <summary>Path the save was loaded from (empty for a buffer parsed in a test).</summary>
    public string Path { get; }

    public int MapWidth { get; }
    public int MapHeight { get; }
    public int Year { get; }
    public bool IsAutumn { get; }
    public int Turn { get; }
    public int Difficulty { get; }
    public int HumanPlayer { get; }
    public int ColonyCount { get; }
    public int UnitCount { get; }
    public int TribeCount { get; }
    public bool IsManualSave { get; }

    /// <summary>The four leader names (index 0..3).</summary>
    public IReadOnlyList<string> LeaderNames { get; }

    /// <summary>The four country names (index 0..3).</summary>
    public IReadOnlyList<string> CountryNames { get; }

    /// <summary>The four nation records, in England/France/Spain/Netherlands order.</summary>
    public IReadOnlyList<NationRecord> Nations { get; }

    /// <summary>The colony records (may be empty in a fresh game).</summary>
    public IReadOnlyList<ColonyRecord> Colonies { get; }

    /// <summary>The nation record for the human player (falls back to England if the index is out of range).</summary>
    public NationRecord HumanNation =>
        HumanPlayer >= 0 && HumanPlayer < Nations.Count ? Nations[HumanPlayer] : Nations[0];

    public string DifficultyName => NationBook.DifficultyName(Difficulty);
    public string HumanNationName => NationBook.NameOf(HumanPlayer);

    // --- loading ------------------------------------------------------------------
    /// <summary>Loads and validates a save file from disk.</summary>
    public static SaveGame Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path is required.", nameof(path));
        var data = File.ReadAllBytes(path);
        return Parse(data, path);
    }

    /// <summary>
    /// Validates a save buffer and builds the typed views. Throws <see cref="InvalidDataException"/>
    /// with a clear message if the signature is wrong or the header counts point past the buffer.
    /// </summary>
    public static SaveGame Parse(byte[] data, string path = "")
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length < SaveFormat.MinFileSize)
            throw new InvalidDataException(
                $"File is only {data.Length} bytes — too small to be a Colonization save.");

        string sig = Encoding.ASCII.GetString(data, 0, SaveFormat.Signature.Length);
        if (sig != SaveFormat.Signature)
            throw new InvalidDataException(
                $"Not a Colonization save: expected a \"{SaveFormat.Signature}\" signature, found \"{Sanitize(sig)}\".");

        int colonyCount = Bytes.U16(data, SaveFormat.Off_ColonyCount);
        int unitCount = Bytes.U16(data, SaveFormat.Off_UnitCount);
        int needed = SaveFormat.NationSectionEnd(colonyCount, unitCount);
        if (needed > data.Length)
            throw new InvalidDataException(
                $"Save header is inconsistent: {colonyCount} colonies + {unitCount} units place the " +
                $"nation records at byte {needed}, past the {data.Length}-byte file. The file may be corrupt.");

        return new SaveGame(data, path);
    }

    // --- saving -------------------------------------------------------------------
    /// <summary>
    /// Writes the (possibly edited) buffer back to disk. Saving to the original path makes a one-time
    /// <c>.bak</c> of the untouched original first. An unedited save round-trips byte-for-byte.
    /// </summary>
    public void Save(string? path = null)
    {
        string target = string.IsNullOrWhiteSpace(path) ? Path : path!;
        if (string.IsNullOrWhiteSpace(target))
            throw new InvalidOperationException("No path to save to — this save was parsed from a buffer.");

        if (File.Exists(target))
        {
            string bak = target + ".bak";
            if (!File.Exists(bak)) File.Copy(target, bak);
        }
        File.WriteAllBytes(target, _data);
    }

    /// <summary>A defensive copy of the current bytes (for round-trip tests).</summary>
    public byte[] ToArray() => (byte[])_data.Clone();

    private static string Sanitize(string s) =>
        new string(s.Select(c => c is >= ' ' and < (char)127 ? c : '?').ToArray());
}
