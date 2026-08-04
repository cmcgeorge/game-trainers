namespace DarkDesigns1Trainer.Game;

/// <summary>
/// Byte-level layout of a Dark Designs I character record as it lives in the emulated DOS
/// memory of a running game (DOSBox / DOSBox-X) and in the offline <c>DDCHARS.DAT</c> file.
///
/// The roster is an array of <see cref="MaxSlots"/> fixed-size records
/// (<see cref="RecordSize"/> bytes each), preceded by a <see cref="HeaderSize"/>-byte header
/// that holds party state. Records pack from slot 0: occupied slots come first, followed by
/// empty slots.
///
/// The layout was recovered by disassembling the unpacked <c>DARKDES.EXE</c>: the game
/// multiplies a character index by <c>0x48</c> (72) in every one of the ~300 places it touches a
/// record, and its character-sheet printer, its rest/heal code and its own built-in "max
/// character" debug routine between them pin every field below. See
/// <c>docs/ReverseEngineering.md</c> §4.
/// </summary>
public static class CharacterFormat
{
    /// <summary>Size of one character record in bytes.</summary>
    public const int RecordSize = 0x48;    // 72

    /// <summary>Number of roster slots stored in <c>DDCHARS.DAT</c>.</summary>
    public const int MaxSlots = 15;

    /// <summary>Size of the DDCHARS.DAT header that precedes the roster.</summary>
    public const int HeaderSize = 0x90;    // 144 = 8 + 2 + 2 + 2 + 2 + 128, read field-by-field

    /// <summary>Total file size: header + max slots × record size.</summary>
    public const int FileSize = HeaderSize + MaxSlots * RecordSize;   // 1224

    /// <summary>Party positions the game keeps live working copies for (roster slot 0 = none).</summary>
    public const int PartySize = 4;

    // --- DDCHARS.DAT header fields -------------------------------------------
    // The loader reads the header as six separate reads — 8, 2, 2, 2, 2 and 128 bytes — straight
    // into the data-segment globals it uses at run time, which is what names these fields: the
    // 8-byte read lands on the party-slot array at DGROUP:0x1318 and the four 2-byte reads on the
    // level / X / Y / facing globals at 0x1320–0x1327. See docs/ReverseEngineering.md §4.1.

    /// <summary>Four <c>uint16</c>: which roster slot each party position holds (0 = empty).</summary>
    public const int HdrOffPartySlots = 0x00;

    /// <summary>The party position: level, X, Y and facing as four consecutive <c>uint16</c>.</summary>
    public const int HdrOffPosition = 0x08;

    /// <summary>128 bytes the game keeps but the teardown has not identified; round-tripped as-is.</summary>
    public const int HdrOffUnknown = 0x10;
    public const int HdrUnknownLength = 0x80;

    // --- record field offsets ------------------------------------------------
    public const int OffExists = 0x00;       // byte: 1 = present, 0 = empty
    public const int OffNameLen = 0x01;      // byte: name length
    public const int OffName = 0x02;         // 12 bytes: ASCII name, null-padded
    public const int NameLength = 12;

    public const int OffUnknown0E = 0x0E;    // byte: never read by the game
    public const int OffStatus = 0x0F;       // byte: 1=fine, 2=KO, 3=STUNED, 4=STONE, 5=DEAD
    public const int OffClass = 0x10;        // byte: 1=Fighter, 2=Priest, 3=Wizard

    // Five attributes, each uint16 LE
    public const int OffStr = 0x11;          // Strength
    public const int OffDex = 0x13;          // Dexterity
    public const int OffCon = 0x15;          // Constitution
    public const int OffInt = 0x17;          // Intelligence
    public const int OffPie = 0x19;          // Piety
    public const int AttributeCount = 5;
    public const int AttributeSize = 2;      // uint16 LE

    public const int OffLevel = 0x1B;        // uint16 LE: level
    public const int OffExperience = 0x1D;   // uint32 LE: experience points
    public const int OffNextLevel = 0x21;    // uint32 LE: experience needed for the next level

    public const int OffMagicCur = 0x25;     // uint16 LE: current magic/spell points
    public const int OffMagicMax = 0x27;     // uint16 LE: max magic/spell points
    public const int OffBodyCur = 0x29;      // uint16 LE: current body/HP
    public const int OffBodyMax = 0x2B;      // uint16 LE: max body/HP
    public const int OffGold = 0x2D;         // uint16 LE: gold pieces
    public const int OffUnknown2F = 0x2F;    // byte: read by the game, meaning not identified

    // --- readied equipment (each a byte item id into ItemBook) ----------------
    public const int OffReadyRightHand = 0x30;
    public const int OffReadyLeftHand = 0x31;
    public const int OffUnknown32 = 0x32;    // byte: never read by the game
    public const int OffReadyArmor = 0x33;
    public const int OffReadyRing = 0x34;

    public const int OffUnknown35 = 0x35;    // 9 bytes: never read by the game

    // --- carried inventory ----------------------------------------------------
    /// <summary>
    /// First carried-item byte. The game indexes this pack as <c>base + 0x3D + slot</c> with
    /// <c>slot</c> running 1–10 (keys <c>A</c>–<c>J</c> on the item screen), so the ten carried
    /// items are the last ten bytes of the record.
    /// </summary>
    public const int OffItems = 0x3E;

    /// <summary>Number of carried-item slots (item screen keys A–J).</summary>
    public const int ItemSlotCount = 10;

    /// <summary>Highest item id the game will accept in a pack slot; 0 means "empty".</summary>
    public const int MaxItemId = 63;

    // --- "max" targets used by the trainer's quick actions --------------------
    // Taken from the game's own built-in "max character" routine, which writes 99 to each
    // attribute, 30 to level, 99 to both vital maxima, 999,999 to the *next-level* threshold at
    // 0x21, and 10,000 gold. Note it does not touch experience at 0x1D at all.
    public const int MaxAttribute = 99;
    public const int MaxVital = 99;            // body / magic max
    public const int MaxLevel = 30;

    /// <summary>
    /// Parked well above <see cref="MaxLevel"/>'s reach so a maxed character does not immediately
    /// level past the game's own cap on the next experience award.
    /// </summary>
    public const long MaxNextLevel = 999999;

    /// <summary>The uint16 ceiling — more than the 10,000 the game's own routine writes.</summary>
    public const int MaxGold = 65535;

    // --- class constants -----------------------------------------------------
    public const int ClassFighter = 1;
    public const int ClassPriest = 2;
    public const int ClassWizard = 3;

    // --- status constants ----------------------------------------------------
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

    /// <summary>Byte offset of carried-item slot <paramref name="slot"/> (0-based, A–J).</summary>
    public static int ItemOffset(int slot) => OffItems + slot;

    /// <summary>
    /// Validates a 72-byte window as a plausible Dark Designs I character record:
    /// exists flag = 1, name length 1–12, ASCII name starting with a letter, status 1–5,
    /// class 1–3, five attributes in 1..999, level 1–99, body max &gt; 0.
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

        int status = b[o + OffStatus];
        if (status < StatusFine || status > StatusDead) return false;

        int cls = b[o + OffClass];
        if (cls < ClassFighter || cls > ClassWizard) return false;

        for (int i = 0; i < AttributeCount; i++)
        {
            int attr = b[o + AttributeOffsets[i]] | (b[o + AttributeOffsets[i] + 1] << 8);
            if (attr < 1 || attr > 999) return false;
        }

        int level = b[o + OffLevel] | (b[o + OffLevel + 1] << 8);
        if (level < 1 || level > 99) return false;

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
