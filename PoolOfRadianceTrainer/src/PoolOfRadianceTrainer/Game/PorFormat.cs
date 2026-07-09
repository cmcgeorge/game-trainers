namespace PoolOfRadianceTrainer.Game;

/// <summary>
/// Byte-level layout of a Pool of Radiance character/monster record as it lives in the
/// emulated DOS memory of a running game (DOSBox / DOSBox-X).
///
/// The record is a fixed 0x11D (285) bytes. This layout was reverse-engineered two ways
/// that agree byte-for-byte:
///   1. Differential analysis of two DOSBox-X memory dumps of a live party (see
///      <c>.docs/reverse-engineering.md</c>): every field below decodes the bundled
///      sample party to internally-consistent values, and Rhiannon's live combat state
///      (HP 7→0, status okay→unconscious) confirms the current-HP / status offsets.
///   2. The Gold Box Companion <c>formats.zip</c> character-format document and the
///      open-source <c>coab</c> reimplementation's <c>PoolRadPlayer.cs</c> (StructSize
///      = 0x11D), which list the same offsets.
///
/// In live memory each 285-byte record is followed by the character's combat-icon sprite
/// and a linked list of item instances, so records are NOT at a fixed stride — the trainer
/// finds them by signature scan (see <see cref="CharacterSignature"/>), not by stride.
///
/// AC and THAC0 are stored "inverted": the displayed value = 60 - storedByte. Helpers on
/// <see cref="CharacterRecord"/> apply the transform.
/// </summary>
public static class PorFormat
{
    /// <summary>Size of one character/monster record in bytes.</summary>
    public const int RecordSize = 0x11D;   // 285

    /// <summary>Name field is a Pascal string: length byte + 15 bytes of ASCII.</summary>
    public const int OffNameLength = 0x00;
    public const int OffName = 0x01;
    public const int NameMaxLength = 15;

    // Six primary ability scores, one byte each (STR, INT, WIS, DEX, CON, CHA).
    public const int OffStr = 0x10;
    public const int OffInt = 0x11;
    public const int OffWis = 0x12;
    public const int OffDex = 0x13;
    public const int OffCon = 0x14;
    public const int OffCha = 0x15;
    public const int StatCount = 6;

    /// <summary>Exceptional-strength percentile (1..100 => 18/01..18/00; 0 = none). Fighters only.</summary>
    public const int OffStrPercent = 0x16;

    public const int OffMemorizedSpells = 0x17;   // 21 bytes: spell slots memorized
    public const int MemorizedSpellsLen = 21;

    public const int OffThac0Base = 0x2D;          // stored as 60 - THAC0
    public const int OffRace = 0x2E;
    public const int OffClass = 0x2F;
    public const int OffAge = 0x30;                // UInt16 LE
    public const int OffHpMax = 0x32;              // byte

    public const int OffKnownSpells = 0x33;        // 0x33..0x69 known-spell flags (one byte per spell)
    public const int KnownSpellsLen = 0x37;        // through 0x69

    public const int OffAttackLevel = 0x6B;
    public const int OffIconDimensions = 0x6C;

    public const int OffSaves = 0x6D;              // 5 saving throws (0x6D..0x71)
    public const int SavesLen = 5;

    public const int OffMovementBase = 0x72;
    public const int OffLevelHighest = 0x73;
    public const int OffDrainedLevels = 0x74;
    public const int OffDrainedHp = 0x75;
    public const int OffUndeadLevel = 0x76;

    public const int OffThiefSkills = 0x77;        // 8 thief skill percentages (0x77..0x7E)
    public const int ThiefSkillsLen = 8;

    public const int OffEffectsPtr = 0x7F;         // 4-byte far pointer into guest RAM
    public const int OffNpcFlag = 0x84;
    public const int OffModifiedFlag = 0x85;

    // Money — seven UInt16 counters (little-endian).
    public const int OffCopper = 0x88;
    public const int OffSilver = 0x8A;
    public const int OffElectrum = 0x8C;
    public const int OffGold = 0x8E;
    public const int OffPlatinum = 0x90;
    public const int OffGems = 0x92;
    public const int OffJewelry = 0x94;

    // Per-class levels (cleric, druid, fighter, paladin, ranger, mage, thief, monk).
    public const int OffClassLevels = 0x96;
    public const int ClassLevelCount = 8;

    public const int OffGender = 0x9E;
    public const int OffAlignment = 0xA0;

    public const int OffAcBase = 0xA9;             // stored as 60 - AC
    public const int OffExperience = 0xAC;         // UInt32 LE (single total, not per class)
    public const int OffHpRolled = 0xB1;

    // Spells-per-day (cleric L1-3, mage L1-3).
    public const int OffClericSlots = 0xB2;
    public const int OffMageSlots = 0xB5;

    public const int OffXpAward = 0xB8;            // UInt16: XP granted for killing this creature (monsters)

    public const int OffOrderNumber = 0xBF;        // marching-order slot
    public const int OffIconSize = 0xC0;           // combat-icon size (0 n/a, 1 small, 2 large)
    public const int OffIconColor = 0xC1;          // 6 combat-icon color bytes (body, arm, leg, hair/face, shield, weapon)
    public const int IconColorLen = 6;             // each byte packs two 4-bit palette indices (low nibble, high nibble)
    public const int OffNumberOfItems = 0xC7;
    public const int OffItemsPtr = 0xC8;           // linked list of carried items
    public const int OffEquipWeapon = 0xCC;        // 13 equipped-item far pointers (0xCC..0xFF)

    public const int OffEncumbrance = 0x102;       // UInt16
    public const int OffNextCharPtr = 0x104;       // party linked-list pointer
    public const int OffCombatPtr = 0x108;         // valid during combat

    public const int OffStatus = 0x10C;            // 0 = okay
    public const int OffThac0Cur = 0x110;          // stored 60 - THAC0
    public const int OffAcCur = 0x111;             // stored 60 - AC
    public const int OffHpCur = 0x11B;             // byte (LIVE current HP)
    public const int OffMovementCur = 0x11C;

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
