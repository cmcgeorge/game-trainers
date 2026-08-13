namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>
/// Byte-level layout of a Curse of the Azure Bonds character/monster record, as it lives both in
/// the emulated DOS memory of a running game (DOSBox / DOSBox-X) and verbatim on disk as a
/// <c>CHRDATAn.SAV</c> file.
///
/// The record is a fixed 0x1A6 (422) bytes — exactly the size of a <c>.SAV</c> file, which is what
/// first pinned it. The layout was recovered by decoding a real six-character party plus a saved
/// <c>.GUY</c> character and checking every field against AD&amp;D 1st-edition arithmetic the party
/// must satisfy; the derivation is in <c>docs/reverse-engineering.md</c> §3 and the assertions are
/// re-run headlessly by <c>test/FormatCheck</c>.
///
/// Curse is the same Gold Box engine as <i>Pool of Radiance</i> and the two records are recognisably
/// the same structure, but Curse's is not a shifted copy of Pool's — three things grew and moved
/// everything after them by differing amounts:
///   * ability scores are stored as <b>current/maximum pairs</b> (14 bytes, not 7), so drain and
///     restoration have somewhere to go;
///   * the memorized- and known-spell blocks are much larger (84 and 100 bytes) because Curse
///     casters reach 5th-level spells rather than 3rd;
///   * spells-per-day is three five-byte blocks rather than two of three.
///
/// AC and THAC0 are stored "inverted" exactly as in Pool: the displayed value = 60 - storedByte.
/// Helpers on <see cref="CharacterRecord"/> apply the transform.
///
/// In live memory each record is followed by the character's combat-icon sprite and a linked list of
/// item instances, so records are NOT at a fixed stride — the trainer finds them by signature scan
/// (see <see cref="CharacterSignature"/>), not by stride.
/// </summary>
public static class CoabFormat
{
    /// <summary>Size of one character/monster record in bytes.</summary>
    public const int RecordSize = 0x1A6;   // 422

    /// <summary>Name field is a Pascal string: length byte + 15 bytes of ASCII.</summary>
    public const int OffNameLength = 0x00;
    public const int OffName = 0x01;
    public const int NameMaxLength = 15;

    // --- ability scores -------------------------------------------------------
    // Seven consecutive (current, maximum) byte pairs: STR, INT, WIS, DEX, CON, CHA and the
    // exceptional-strength percentile. The "current" byte is what the game uses and what drain
    // lowers; "maximum" is the rolled value a Restoration returns it to. The trainer edits both
    // together so a restore can't quietly undo an edit.
    public const int OffStats = 0x10;
    public const int StatStride = 2;       // (current, maximum)
    public const int StatCount = 6;        // STR..CHA; the percentile pair follows

    public const int OffStr = OffStats + 0 * StatStride;   // 0x10
    public const int OffInt = OffStats + 1 * StatStride;   // 0x12
    public const int OffWis = OffStats + 2 * StatStride;   // 0x14
    public const int OffDex = OffStats + 3 * StatStride;   // 0x16
    public const int OffCon = OffStats + 4 * StatStride;   // 0x18
    public const int OffCha = OffStats + 5 * StatStride;   // 0x1A

    /// <summary>Exceptional-strength percentile pair (1..100 =&gt; 18/01..18/00; 0 = none).</summary>
    public const int OffStrPercent = OffStats + 6 * StatStride;   // 0x1C

    /// <summary>Offset of the maximum half of a stat pair, relative to its current half.</summary>
    public const int StatMaxDelta = 1;

    public const int OffMemorizedSpells = 0x1E;   // 84 bytes: one slot per memorized spell
    public const int MemorizedSpellsLen = 84;     // 0x1E..0x71

    public const int OffThac0Base = 0x73;          // stored as 60 - THAC0
    public const int OffRace = 0x74;
    public const int OffClass = 0x75;
    public const int OffAge = 0x76;                // UInt16 LE
    public const int OffHpMax = 0x78;              // byte

    public const int OffKnownSpells = 0x79;        // 0x79..0xDC known-spell flags (one byte per spell)
    public const int KnownSpellsLen = 100;

    public const int OffAttackLevel = 0xDD;
    public const int OffIconDimensions = 0xDE;

    public const int OffSaves = 0xDF;              // 5 saving throws (0xDF..0xE3)
    public const int SavesLen = 5;

    public const int OffMovementBase = 0xE4;
    public const int OffLevelHighest = 0xE5;
    public const int OffDrainedLevels = 0xE6;
    public const int OffDrainedHp = 0xE7;
    public const int OffUndeadLevel = 0xE8;

    public const int OffThiefSkills = 0xEA;        // 8 thief skill percentages (0xEA..0xF1)
    public const int ThiefSkillsLen = 8;

    public const int OffEffectsPtr = 0xF2;         // 4-byte far pointer into guest RAM (head of the .FX list)
    public const int OffNpcFlag = 0xF7;
    public const int OffModifiedFlag = 0xF8;

    // Money — seven UInt16 counters (little-endian).
    public const int OffCopper = 0xFB;
    public const int OffSilver = 0xFD;
    public const int OffElectrum = 0xFF;
    public const int OffGold = 0x101;
    public const int OffPlatinum = 0x103;
    public const int OffGems = 0x105;
    public const int OffJewelry = 0x107;

    // Per-class levels (cleric, druid, fighter, paladin, ranger, mage, thief, monk).
    public const int OffClassLevels = 0x109;
    public const int ClassLevelCount = 8;

    public const int OffGender = 0x111;
    public const int OffAlignment = 0x113;

    public const int OffAcBase = 0x124;            // stored as 60 - AC; the unarmored 10 baseline
    public const int OffExperience = 0x127;        // UInt32 LE — per class share for multiclass
    public const int OffHpRolled = 0x12C;          // raw die roll before the CON bonus

    // Spells-per-day. Three five-byte blocks (spell levels 1-5). The middle block is unidentified —
    // it is zero for every caster in the sample party, priest and mage alike, and the mage block
    // measurably starts at 0x137 — so only the two that decode are surfaced.
    public const int OffClericSlots = 0x12D;
    public const int OffUnknownSlots = 0x132;
    public const int OffMageSlots = 0x137;
    public const int SpellSlotLevels = 5;

    public const int OffXpAward = 0x13C;           // UInt16: XP granted for killing this creature (monsters)

    public const int OffOrderNumber = 0x143;       // marching-order slot
    public const int OffIconSize = 0x144;          // combat-icon size (0 n/a, 1 small, 2 large)
    public const int OffIconColor = 0x145;         // 6 combat-icon color bytes (body, arm, leg, hair/face, shield, weapon)
    public const int IconColorLen = 6;             // each byte packs two 4-bit palette indices (low nibble, high nibble)
    public const int OffNumberOfItems = 0x14B;
    public const int OffItemsPtr = 0x14C;          // linked list of carried items
    public const int OffEquipWeapon = 0x150;       // 13 equipped-item far pointers (0x150..0x183)

    public const int OffEncumbrance = 0x187;       // UInt16 — carried weight, coins included
    public const int OffNextCharPtr = 0x189;       // party linked-list pointer
    public const int OffCombatPtr = 0x18D;         // valid during combat

    public const int OffStatus = 0x195;            // 0 = okay
    public const int OffThac0Cur = 0x199;          // stored 60 - THAC0
    public const int OffAcCur = 0x19A;             // stored 60 - AC
    public const int OffHpCur = 0x1A4;             // byte (LIVE current HP)
    public const int OffMovementCur = 0x1A5;

    /// <summary>The 60-x transform used to store AC and THAC0 (displayed = 60 - stored).</summary>
    public const int InvertBase = 60;

    public static readonly string[] Stats =
        { "Strength", "Intelligence", "Wisdom", "Dexterity", "Constitution", "Charisma" };

    public static readonly string[] StatsShort =
        { "STR", "INT", "WIS", "DEX", "CON", "CHA" };

    public static readonly string[] Races =
        { "Monster", "Dwarf", "Elf", "Gnome", "Half-Elf", "Halfling", "Half-Orc", "Human" };

    public static readonly string[] Classes =
    {
        "Cleric", "Druid", "Fighter", "Paladin", "Ranger", "Mage", "Thief", "Monk",
        "Cleric/Fighter", "Cleric/Fighter/Mage", "Cleric/Ranger", "Cleric/Mage",
        "Cleric/Thief", "Fighter/Mage", "Fighter/Thief", "Fighter/Mage/Thief",
        "Mage/Thief", "Monster"
    };

    public static readonly string[] Alignments =
    {
        "Lawful Good", "Lawful Neutral", "Lawful Evil",
        "Neutral Good", "True Neutral", "Neutral Evil",
        "Chaotic Good", "Chaotic Neutral", "Chaotic Evil"
    };

    public static readonly string[] Genders = { "Male", "Female" };

    public static readonly string[] Statuses =
        { "Okay", "Animated", "Temp Gone", "Running", "Unconscious", "Dying", "Dead", "Stoned", "Gone" };

    /// <summary>Class-level byte labels, matching <see cref="OffClassLevels"/> order.</summary>
    public static readonly string[] ClassLevelNames =
        { "Cleric", "Druid", "Fighter", "Paladin", "Ranger", "Mage", "Thief", "Monk" };

    /// <summary>Money counter labels, matching the money offsets order.</summary>
    public static readonly string[] MoneyNames =
        { "Copper", "Silver", "Electrum", "Gold", "Platinum", "Gems", "Jewelry" };

    public static readonly int[] MoneyOffsets =
        { OffCopper, OffSilver, OffElectrum, OffGold, OffPlatinum, OffGems, OffJewelry };

    /// <summary>Saving-throw labels, matching <see cref="OffSaves"/> order.</summary>
    public static readonly string[] SaveNames =
    {
        "Paralyze/Poison/Death", "Petrify/Polymorph", "Rod/Staff/Wand", "Breath Weapon", "Spell"
    };

    /// <summary>Thief-skill labels, matching <see cref="OffThiefSkills"/> order.</summary>
    public static readonly string[] ThiefSkillNames =
    {
        "Pick Pockets", "Open Locks", "Find/Remove Traps", "Move Silently",
        "Hide in Shadows", "Hear Noise", "Climb Walls", "Read Languages"
    };

    /// <summary>The 16 EGA palette entries a combat-icon color nibble can hold (index 0..15).</summary>
    public static readonly string[] IconColors =
    {
        "Black", "Blue", "Green", "Cyan", "Red", "Magenta", "Brown", "Light Gray",
        "Dark Gray", "Bright Blue", "Bright Green", "Bright Cyan", "Bright Red", "Bright Magenta",
        "Bright Yellow", "Bright White"
    };

    /// <summary>The six combat-icon parts, one per <see cref="OffIconColor"/> byte.</summary>
    public static readonly string[] IconColorParts =
        { "Body", "Arm", "Leg", "Hair/Face", "Shield", "Weapon" };

    public static string RaceName(int v) => v >= 0 && v < Races.Length ? Races[v] : $"?({v})";
    public static string ClassName(int v) => v >= 0 && v < Classes.Length ? Classes[v] : $"?({v})";
    public static string AlignmentName(int v) => v >= 0 && v < Alignments.Length ? Alignments[v] : $"?({v})";
    public static string GenderName(int v) => v >= 0 && v < Genders.Length ? Genders[v] : $"?({v})";
    public static string StatusName(int v) => v >= 0 && v < Statuses.Length ? Statuses[v] : $"Afflicted(0x{v:X2})";
}
