namespace Wizardry1Trainer.Game;

/// <summary>
/// Byte-level layout of a Wizardry 1 character record (TCHAR) as it lives in the emulated
/// DOS memory of a running game (DOSBox / DOSBox-X via WIZDOS.COM, a UCSD p-system
/// emulator). The record is 207 bytes ($CF) and was recovered from the reverse-engineered
/// Pascal source (Thomas William Ewers, 2014, github.com/snafaru/Wizardry.Code).
///
/// The roster is an array of <see cref="MaxSlots"/> fixed-size records. The UCSD p-system
/// allocates the array on its heap at a session-specific address, so the trainer locates it
/// by structural scan (see <see cref="Wizardry1Trainer.Memory.RosterLocator"/>).
///
/// Names use UCSD Pascal STRING[15] encoding: byte 0 = current length (0..15), bytes 1..15 =
/// ASCII characters (the game stores uppercase letters, spaces, and periods).
///
/// Gold and experience use TWIZLONG -- a base-10000 number stored as three little-endian
/// uint16 words (LOW + MID * 10000 + HIGH * 100000000), not packed BCD.
///
/// Attributes (STR, INT, PIE, VIT, AGI, LUK) are packed into 4 bytes at $2C-$2F as six 5-bit
/// values with a non-standard bit layout that wraps some attributes across byte boundaries.
/// </summary>
public static class CharacterFormat
{
    /// <summary>Size of one character record in bytes ($CF).</summary>
    public const int RecordSize = 0xCF;     // 207

    /// <summary>Number of roster slots (the game allows up to 6 party members).</summary>
    public const int MaxSlots = 6;

    // --- name (UCSD Pascal STRING[15]) --------------------------------------
    public const int OffName = 0x00;        // byte 0 = length, bytes 1-15 = ASCII
    public const int NameFieldLength = 16;  // 1 length byte + 15 data bytes

    // --- password (STRING[15]) ---------------------------------------------
    public const int OffPassword = 0x10;    // same layout as name

    // --- identity / state ----------------------------------------------------
    public const int OffInMaze = 0x20;       // uint16: 0 = at edge/available, 1 = in maze
    public const int OffRace = 0x22;         // uint16: 1=Human, 2=Elf, 3=Dwarf, 4=Gnome, 5=Hobbit
    public const int OffClass = 0x24;        // uint16: 0=Fighter..7=Ninja
    public const int OffAge = 0x26;          // 2 bytes; age = byte[0x26]/52 + byte[0x27]*5
    public const int OffStatus = 0x28;       // uint16: 0=OK, 5=Dead, 7=Lost
    public const int OffAlignment = 0x2A;    // uint16: 1=Good, 2=Neutral, 3=Evil

    // --- attributes (packed 6 x 5 bits into 4 bytes at $2C-$2F) --------------
    public const int OffAttributes = 0x2C;   // 4 bytes
    public const int AttributeCount = 6;
    public const int MaxAttribute = 18;

    /// <summary>
    /// Extracts the six attributes from the 4-byte packed field. The packing is non-standard:
    /// Strength and Vitality occupy the low 5 bits of bytes $2C and $2E respectively; Piety and
    /// Luck occupy the middle 5 bits of bytes $2D and $2F; Intelligence and Agility wrap across
    /// byte boundaries (low bits of $2D/$2F combined with high bits of $2C/$2E).
    /// Confirmed: $52 4A 52 4A = all 18s.
    /// </summary>
    public static (int str, int Int, int pie, int vit, int agi, int luk) ReadAttributes(byte[] b, int o)
    {
        int b0 = b[o + 0]; // $2C
        int b1 = b[o + 1]; // $2D
        int b2 = b[o + 2]; // $2E
        int b3 = b[o + 3]; // $2F
        int str = b0 & 0x1F;
        int Int = ((b1 & 0x03) << 3) | ((b0 >> 5) & 0x07);
        int pie = (b1 >> 2) & 0x1F;
        int vit = b2 & 0x1F;
        int agi = ((b3 & 0x03) << 3) | ((b2 >> 5) & 0x07);
        int luk = (b3 >> 2) & 0x1F;
        return (str, Int, pie, vit, agi, luk);
    }

    /// <summary>
    /// Packs the six attributes back into the 4-byte field. Inverse of <see cref="ReadAttributes"/>.
    /// Each value is clamped to 0..18 before packing.
    /// </summary>
    public static void WriteAttributes(byte[] b, int o, int str, int Int, int pie, int vit, int agi, int luk)
    {
        str = Math.Clamp(str, 0, MaxAttribute);
        Int = Math.Clamp(Int, 0, MaxAttribute);
        pie = Math.Clamp(pie, 0, MaxAttribute);
        vit = Math.Clamp(vit, 0, MaxAttribute);
        agi = Math.Clamp(agi, 0, MaxAttribute);
        luk = Math.Clamp(luk, 0, MaxAttribute);

        int b0 = (str & 0x1F) | ((Int & 0x07) << 5);
        int b1 = ((Int >> 3) & 0x03) | ((pie & 0x1F) << 2);
        int b2 = (vit & 0x1F) | ((agi & 0x07) << 5);
        int b3 = ((agi >> 3) & 0x03) | ((luk & 0x1F) << 2);
        b[o + 0] = (byte)b0;
        b[o + 1] = (byte)b1;
        b[o + 2] = (byte)b2;
        b[o + 3] = (byte)b3;
    }

    /// <summary>Attribute names in the game's canonical order (STR, INT, PIE, VIT, AGI, LUK).</summary>
    public static readonly string[] AttributeNames =
        { "Strength", "Intelligence", "Piety", "Vitality", "Agility", "Luck" };

    public static readonly string[] AttributeShort =
        { "STR", "INT", "PIE", "VIT", "AGI", "LUK" };

    // --- luck/skill bits (packed 4 bytes at $30-$33) -------------------------
    public const int OffLuckSkill = 0x30;   // [Inferred] packed array, 4 bytes

    // --- gold (TWIZLONG = 3 x uint16 LE, 6 bytes at $34-$39) ----------------
    public const int OffGold = 0x34;
    public const int WizLongSize = 6;

    /// <summary>Reads a TWIZLONG (base-10000, 3 x uint16 LE) as a 64-bit value.</summary>
    public static long ReadWizLong(byte[] b, int o)
    {
        uint low = (uint)(b[o] | (b[o + 1] << 8));
        uint mid = (uint)(b[o + 2] | (b[o + 3] << 8));
        uint high = (uint)(b[o + 4] | (b[o + 5] << 8));
        return low + mid * 10000L + high * 100000000L;
    }

    /// <summary>Writes a 64-bit value into a TWIZLONG (base-10000, 3 x uint16 LE).</summary>
    public static void WriteWizLong(byte[] b, int o, long value)
    {
        value = Math.Clamp(value, 0, 999999999999L);
        uint low = (uint)(value % 10000);
        uint mid = (uint)((value / 10000) % 10000);
        uint high = (uint)((value / 100000000) % 10000);
        b[o] = (byte)(low & 0xFF);
        b[o + 1] = (byte)((low >> 8) & 0xFF);
        b[o + 2] = (byte)(mid & 0xFF);
        b[o + 3] = (byte)((mid >> 8) & 0xFF);
        b[o + 4] = (byte)(high & 0xFF);
        b[o + 5] = (byte)((high >> 8) & 0xFF);
    }

    /// <summary>Maximum gold the trainer will write (under 10 billion, fits in TWIZLONG).</summary>
    public const long MaxGold = 9_999_999_999;

    // --- equipment (8 items, 8 bytes each at $3C-$7B) -----------------------
    public const int OffEquipmentCount = 0x3A;   // uint16
    public const int OffEquipment = 0x3C;         // 8 x 8 bytes = 64 bytes
    public const int EquipmentSlotCount = 8;
    public const int EquipmentSlotSize = 8;

    // --- experience (TWIZLONG, 6 bytes at $7C-$81) -------------------------
    public const int OffExperience = 0x7C;
    public const long MaxExperience = 9_999_999_999;

    // --- progression --------------------------------------------------------
    public const int OffLastLevel = 0x82;     // uint16
    public const int OffLevel = 0x84;         // uint16
    public const int OffHpCurrent = 0x86;      // uint16
    public const int OffHpMax = 0x88;         // uint16
    public const int MaxLevel = 99;
    public const int MaxHp = 999;

    // --- spells --------------------------------------------------------------
    public const int OffSpellKnowledge = 0x8A; // 8 bytes (50 bits used, padded to 56)
    public const int SpellKnowledgeBytes = 8;
    public const int SpellCount = 50;

    public const int OffMageSpells = 0x92;     // 7 x uint16 = 14 bytes (charges per level)
    public const int OffPriestSpells = 0xA0;   // 7 x uint16 = 14 bytes (charges per level)
    public const int SpellLevels = 7;
    public const int MaxSpellCharges = 9;

    // --- combat stats --------------------------------------------------------
    public const int OffArmorClassLast = 0xAE;  // uint16
    public const int OffArmorClass = 0xB0;      // uint16
    public const int OffHealPoints = 0xB2;       // uint16
    public const int OffSwingCount = 0xB6;       // uint16

    // --- position (lost location: level, x, y, facing) ----------------------
    public const int OffPosition = 0xC6;         // 4 x uint16 = 8 bytes

    // --- honors --------------------------------------------------------------
    public const int OffHonors = 0xCE;           // 1 byte

    // --- status constants ----------------------------------------------------
    public const int StatusOK = 0;
    public const int StatusDead = 5;
    public const int StatusLost = 7;
    public const int StatusAshes = 6;
    public const int StatusStoned = 4;

    public static string StatusName(int status) => status switch
    {
        StatusOK => "OK",
        StatusDead => "Dead",
        StatusAshes => "Ashes",
        StatusStoned => "Stoned",
        StatusLost => "Lost",
        _ => $"?({status})"
    };

    // --- race constants ------------------------------------------------------
    public static readonly string[] RaceNames =
        { "Human", "Elf", "Dwarf", "Gnome", "Hobbit" };

    public static string RaceName(int race) =>
        race >= 1 && race <= RaceNames.Length ? RaceNames[race - 1] : $"?({race})";

    // --- class constants -----------------------------------------------------
    public static readonly string[] ClassNames =
        { "Fighter", "Mage", "Priest", "Thief", "Bishop", "Samurai", "Lord", "Ninja" };

    public static string ClassName(int cls) =>
        cls >= 0 && cls < ClassNames.Length ? ClassNames[cls] : $"?({cls})";

    // --- alignment constants -------------------------------------------------
    public static readonly string[] AlignmentNames =
        { "Good", "Neutral", "Evil" };

    public static string AlignmentName(int align) =>
        align >= 1 && align <= AlignmentNames.Length ? AlignmentNames[align - 1] : $"?({align})";
}
