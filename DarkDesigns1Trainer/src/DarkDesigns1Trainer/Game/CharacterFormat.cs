namespace DarkDesigns1Trainer.Game;

/// <summary>
/// Byte-level layout of a Dark Designs I character record as it lives in the emulated DOS
/// memory of a running game (DOSBox / DOSBox-X) and in the offline <c>DDCHARS.DAT</c> file.
///
/// The roster is an array of <see cref="MaxSlots"/> fixed-size records
/// (<see cref="RecordSize"/> bytes each), preceded by a <see cref="HeaderSize"/>-byte header
/// that holds party state. Records pack from slot 0: occupied slots come first, followed by
/// empty (all-zero) slots.
///
/// The record layout was reverse-engineered from a sample <c>DDCHARS.DAT</c> (one
/// character, "CHRISTOPHER", Fighter L1) and cross-checked against the game's display
/// strings extracted from the LZEXE-compressed <c>DARKDES.EXE</c>.
/// </summary>
public static class CharacterFormat
{
    /// <summary>Size of one character record in bytes.</summary>
    public const int RecordSize = 0x36;    // 54

    /// <summary>Maximum number of roster slots (the game allows up to 20 created characters).</summary>
    public const int MaxSlots = 20;

    /// <summary>Size of the DDCHARS.DAT header that precedes the roster.</summary>
    public const int HeaderSize = 0x90;    // 144

    /// <summary>Total file size: header + max slots × record size.</summary>
    public const int FileSize = HeaderSize + MaxSlots * RecordSize;   // 1224

    // --- record field offsets ------------------------------------------------
    public const int OffExists = 0x00;       // byte: 1 = present, 0 = empty
    public const int OffNameLen = 0x01;      // byte: name length
    public const int OffName = 0x02;         // 12 bytes: ASCII name, null-padded
    public const int NameLength = 12;

    public const int OffUnknown0E = 0x0E;    // byte: unknown (0 in sample)
    public const int OffClass = 0x0F;        // byte: 1=Fighter, 2=Priest, 3=Wizard
    public const int OffLevel = 0x10;        // byte: level

    // Five attributes, each uint16 LE
    public const int OffStr = 0x11;          // Strength
    public const int OffDex = 0x13;          // Dexterity
    public const int OffCon = 0x15;          // Constitution
    public const int OffInt = 0x17;          // Intelligence
    public const int OffPie = 0x19;          // Piety
    public const int AttributeCount = 5;
    public const int AttributeSize = 2;       // uint16 LE

    public const int OffStatus = 0x1B;       // uint16 LE: status (1=fine, others inferred)
    public const int OffUnknown1D = 0x1D;    // 4 bytes: unknown (zeros in sample)

    public const int OffGold = 0x21;         // uint16 LE: gold pieces
    public const int OffUnknown23 = 0x23;    // 6 bytes: unknown (zeros in sample)

    public const int OffBodyCur = 0x29;      // uint16 LE: current body/HP
    public const int OffBodyMax = 0x2B;      // uint16 LE: max body/HP
    public const int OffExperience = 0x2D;   // uint16 LE: experience points
    public const int OffMagicCur = 0x2F;     // uint16 LE: current magic/MP
    public const int OffUnknown31 = 0x31;    // 5 bytes: unknown (magic max? spells? items?)

    // --- "max" targets used by the trainer's quick actions --------------------
    public const int MaxAttribute = 30;       // well above the 3–18 roll range; safe for uint16
    public const int MaxVital = 999;          // body / magic cap
    public const int MaxLevel = 50;
    public const int MaxGold = 65535;         // uint16 max
    public const int MaxExperience = 65535;   // uint16 max

    // --- class constants -----------------------------------------------------
    public const int ClassFighter = 1;
    public const int ClassPriest = 2;
    public const int ClassWizard = 3;

    // --- status constants (inferred from game strings) -----------------------
    public const int StatusFine = 1;
    public const int StatusKO = 2;
    public const int StatusStuned = 3;
    public const int StatusStone = 4;
    public const int StatusDead = 5;

    // --- lookup tables -------------------------------------------------------
    public static readonly string[] AttributeNames =
        { "Strength", "Dexterity", "Constitution", "Intelligence", "Piety" };

    public static readonly string[] AttributeShort =
        { "STR", "DEX", "CON", "INT", "PIE" };

    /// <summary>uint16 LE offsets for the five attributes.</summary>
    public static readonly int[] AttributeOffsets =
        { OffStr, OffDex, OffCon, OffInt, OffPie };

    public static readonly string[] ClassNames =
        { "(none)", "Fighter", "Priest", "Wizard" };

    public static string ClassName(int c) =>
        c >= 0 && c < ClassNames.Length ? ClassNames[c] : $"?({c})";

    public static readonly string[] StatusNames =
        { "(unknown)", "fine", "KO", "STUNED", "STONE", "DEAD" };

    public static string StatusName(int s)
    {
        if (s >= 0 && s < StatusNames.Length) return StatusNames[s];
        return $"?({s})";
    }

    /// <summary>
    /// Validates a 54-byte window as a plausible Dark Designs I character record:
    /// exists flag = 1, name length 1–12, ASCII name starting with a letter,
    /// class 1–3, level ≥ 1, five attributes in 3..30, body max > 0.
    /// </summary>
    public static bool LooksLikeRecord(byte[] b, int o)
    {
        if (b == null || o < 0 || o + RecordSize > b.Length) return false;
        if (b[o + OffExists] != 1) return false;

        int nameLen = b[o + OffNameLen];
        if (nameLen < 1 || nameLen > NameLength) return false;

        // Name must start with a letter
        char first = (char)b[o + OffName];
        if (!char.IsLetter(first)) return false;

        // Name characters must be printable ASCII letters or spaces
        for (int i = 0; i < nameLen; i++)
        {
            char c = (char)b[o + OffName + i];
            if (!char.IsLetterOrDigit(c) && c != ' ' && c != '-') return false;
        }

        int cls = b[o + OffClass];
        if (cls < ClassFighter || cls > ClassWizard) return false;

        int level = b[o + OffLevel];
        if (level < 1 || level > 99) return false;

        for (int i = 0; i < AttributeCount; i++)
        {
            int attr = b[o + AttributeOffsets[i]] | (b[o + AttributeOffsets[i] + 1] << 8);
            if (attr < 1 || attr > 999) return false;
        }

        int bodyMax = b[o + OffBodyMax] | (b[o + OffBodyMax + 1] << 8);
        if (bodyMax < 1 || bodyMax > 9999) return false;

        return true;
    }

    /// <summary>An empty slot has exists flag = 0 (the game may leave stale data in other fields).</summary>
    public static bool IsEmptySlot(byte[] b, int o)
    {
        if (b == null || o < 0 || o + RecordSize > b.Length) return false;
        return b[o + OffExists] == 0;
    }
}
