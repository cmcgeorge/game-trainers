namespace BardsTale1Trainer.Game;

/// <summary>
/// Layout constants for the DOS Bard's Tale 1 (BARD.EXE, Interplay 1987) character
/// record and the game's static data segment, reverse-engineered from a DOSBox-X
/// memory dump cross-checked against two on-disk <c>.TPW</c> character files.
///
/// A live character record is 0x5C (92) bytes. The 7-slot party array lives at a
/// fixed offset inside the game's data segment (DGROUP); slot 0 is the special
/// (summoned-monster/illusion) slot, slots 1–6 are the regular members. Character
/// NAMES are not part of the record — the game keeps them only in a 7-row × 37-byte
/// formatted text table (the on-screen party roster), one row per slot.
///
/// On disk a <c>.TPW</c> file is 109 bytes: a 16-byte NUL-padded name followed by the
/// 92-byte record (plus one trailing byte). Byte 0 of the record is 0x01 on disk and
/// 0x00 in live memory (a disk-only marker).
///
/// All multi-byte fields are little-endian. Offsets below are record-relative; add
/// 0x10 for the equivalent .TPW file offset.
/// </summary>
public static class PartyFormat
{
    /// <summary>Bytes in one live character record (and the slot stride — packed).</summary>
    public const int RecordSize = 0x5C;        // 92

    /// <summary>Party slots: index 0 = special/summon slot, 1..6 = members.</summary>
    public const int PartySlots = 7;

    /// <summary>Size of one .TPW character file (name[16] + record[92] + 1 pad byte).</summary>
    public const int TpwFileSize = 109;

    /// <summary>Offset of the record inside a .TPW file (after the 16-byte name).</summary>
    public const int TpwRecordOffset = 0x10;

    public const int TpwNameLength = 16;

    // --- Field offsets (relative to record start) -------------------------------
    public const int OffDiskMarker = 0x00;     // 0x01 on disk, 0x00 live
    public const int OffStatus = 0x01;         // u16: 0 = OK/occupied, 1 = empty slot (best-effort: dead?)
    public const int OffUnknown03 = 0x03;      // u16, always 0 in samples
    public const int OffClass = 0x05;          // u16: 0=Warrior .. 9=Wizard

    /// <summary>Five attributes, max then current, each u16. Order: St, IQ, Dx, Cn, Lk.</summary>
    public const int OffStatsMax = 0x07;       // 5 × u16 -> 0x07..0x10
    public const int OffStatsCur = 0x11;       // 5 × u16 -> 0x11..0x1A
    public const int StatCount = 5;

    public const int OffArmorClass = 0x1B;     // i16, lower is better (-10 shows as "LO")
    public const int OffHpCur = 0x1D;          // u16
    public const int OffHpMax = 0x1F;          // u16
    public const int OffSpCur = 0x21;          // u16
    public const int OffSpMax = 0x23;          // u16

    /// <summary>8 inventory slots, u16 each: bit15 = equipped, low bits = item id (1-based; 0 = empty).</summary>
    public const int OffItems = 0x25;          // 8 × u16 -> 0x25..0x34
    public const int ItemSlotCount = 8;
    public const ushort ItemEquippedFlag = 0x8000;

    public const int OffExperience = 0x35;     // u32
    public const int OffGold = 0x39;           // u32 (best-effort: 0 in both samples)
    public const int OffLevel = 0x3D;          // u16
    public const int OffLevelMax = 0x3F;       // u16 (best-effort: highest level attained?)

    /// <summary>Four spell-class levels (0–7), one byte each: Magician, Conjurer, Sorcerer, Wizard.</summary>
    public const int OffSpellLevels = 0x41;    // 4 bytes -> 0x41..0x44
    public const int SpellClassCount = 4;

    // 0x45..0x58: undecoded (all zero in both sample characters)
    public const int OffRace = 0x59;           // byte: 0=Human .. 6=Gnome
    // 0x5A..0x5B: undecoded

    // --- Data-segment (DGROUP) anchors -------------------------------------------
    // The game's statics sit at fixed offsets inside one 64 KB data segment. The
    // trainer finds the segment by scanning process memory for the race-name table
    // (unique byte string) and validating two more tables at their expected offsets.
    public static readonly byte[] RaceTableBytes =
        System.Text.Encoding.ASCII.GetBytes("Human\0Elf\0Dwarf\0Hobbit\0Half-Elf\0Half-Orc\0Gnome\0");
    public const int DsRaceTable = 0x14A;

    public static readonly byte[] ItemTableBytes =
        System.Text.Encoding.ASCII.GetBytes("Torch\0Lamp\0Broadsword\0");
    public const int DsItemTable = 0x808;

    public static readonly byte[] ClassTableBytes =
        System.Text.Encoding.ASCII.GetBytes("Misc. Item\0Warrior\0Paladin\0");
    public const int DsClassTable = 0xD91;

    /// <summary>The 7-slot party record array (slot 0 first), records packed at <see cref="RecordSize"/>.</summary>
    public const int DsPartySlots = 0xD0BF;

    /// <summary>The 7-row party display table; row k belongs to slot k. Name = bytes 0..15 of a row.</summary>
    public const int DsPartyRows = 0x455;
    public const int PartyRowStride = 37;       // 0x25 — one formatted roster line
    public const int PartyRowNameLength = 16;

    // --- Monster name table (the bestiary's source) --------------------------------
    // NUL-separated names carrying inflection markup ("Kobold^^s^", "Dwar^f^ves^", …)
    // followed by a table of u16 DS-relative pointers, one per monster id in order.
    // Per-monster combat stats sit in four parallel byte arrays at DS:0x19C3 / 0x1A43 /
    // 0x1AC3 / 0x1B43 (one byte per monster each) — located but not yet decoded.
    public const int DsMonsterNames = 0x2874;
    public const int DsMonsterNamePtrs = 0x2F3E;
    public const int MonsterCount = 127;

    // --- enums --------------------------------------------------------------------
    public static readonly string[] Stats = { "Strength", "IQ", "Dexterity", "Constitution", "Luck" };

    public static readonly string[] Classes =
        { "Warrior", "Paladin", "Rogue", "Bard", "Hunter", "Monk",
          "Conjurer", "Magician", "Sorcerer", "Wizard" };

    public static readonly string[] Races =
        { "Human", "Elf", "Dwarf", "Hobbit", "Half-Elf", "Half-Orc", "Gnome" };

    /// <summary>Record byte order of the four per-class spell levels (matches the game's spell tables).</summary>
    public static readonly string[] SpellClasses = { "Magician", "Conjurer", "Sorcerer", "Wizard" };

    public static string ClassName(int c) => c >= 0 && c < Classes.Length ? Classes[c] : $"?({c})";
    public static string RaceName(int r) => r >= 0 && r < Races.Length ? Races[r] : $"?({r})";

    /// <summary>
    /// Index into the four spell-level bytes for a caster class, or -1 for non-casters.
    /// Class ids: 6=Conjurer, 7=Magician, 8=Sorcerer, 9=Wizard; byte order is
    /// Magician, Conjurer, Sorcerer, Wizard.
    /// </summary>
    public static int SpellLevelIndexForClass(int classId) => classId switch
    {
        6 => 1,   // Conjurer
        7 => 0,   // Magician
        8 => 2,   // Sorcerer
        9 => 3,   // Wizard
        _ => -1,
    };

    /// <summary>
    /// Plausibility check used as a scan-time sanity gate on a 92-byte slot: an empty
    /// slot (status 1, rest ~zero) or a live character (status small, class 0..9,
    /// stats within mortal bounds). Deliberately loose — the locator anchors on the
    /// data-segment string tables, this only guards against a corrupted candidate.
    /// </summary>
    public static bool LooksLikeSlot(byte[] buf, int i)
    {
        if (i < 0 || i + RecordSize > buf.Length) return false;
        int status = buf[i + OffStatus] | (buf[i + OffStatus + 1] << 8);
        if (status > 16) return false;
        int cls = buf[i + OffClass] | (buf[i + OffClass + 1] << 8);
        if (cls > 9) return false;
        for (int s = 0; s < StatCount; s++)
        {
            int cur = buf[i + OffStatsCur + s * 2] | (buf[i + OffStatsCur + s * 2 + 1] << 8);
            if (cur > 99) return false;
        }
        return true;
    }
}
