namespace EyeOfTheBeholder1Trainer.Game;

/// <summary>
/// Byte-level layout of an Eye of the Beholder I character record as it lives in the save file
/// (<c>EOBDATA.SAV</c>) and in the running game's emulated DOS memory (DOSBox / DOSBox-X).
///
/// The party is an array of <see cref="MaxSlots"/> fixed-size records (<see cref="RecordSize"/>
/// bytes each) with no file header — slot 0 begins at the first byte of the save file. The same
/// layout is used in live memory: the game loads character data verbatim from the save file into
/// its data segment, and writes it back on save.
///
/// The record layout was derived from the ModdingWiki format specification, the Synalysis grammar
/// file, the EOB2 hex list by Marc Rene Delhalle (EOB1 and EOB2 share the same character structure),
/// and verified against the shipped <c>EOBDATA.SAV</c> (see <see cref="SaveFile"/>).
///
/// All multi-byte values are <b>little-endian</b>. Names are plain ASCII, null-terminated, max 10
/// characters. Ability scores are stored as (modified, base) byte pairs. Hit points are single
/// bytes (max 255). Armor class is a signed byte (lower is better; -10 is the AD&D best).
/// </summary>
public static class CharacterFormat
{
    /// <summary>Size of one character record in bytes.</summary>
    public const int RecordSize = 243;

    /// <summary>Number of character slots.</summary>
    public const int MaxSlots = 6;

    /// <summary>Total party bytes = <see cref="MaxSlots"/> × <see cref="RecordSize"/>.</summary>
    public const int PartySize = MaxSlots * RecordSize;  // 1458

    // --- record field offsets ------------------------------------------------
    public const int OffCharId = 0x00;       // UINT8 — character slot index (0..5)
    public const int OffActive = 0x01;       // UINT8 — 1 = active party member, 0 = empty slot
    public const int OffName = 0x02;         // CHAR[10] — null-terminated name, max 10 chars
    public const int NameLength = 10;

    // Six abilities, each a (modified, base) byte pair.
    public const int OffStrMod = 0x0D;       // Strength (modified)
    public const int OffStrBase = 0x0E;      // Strength (base)
    public const int OffStrExcMod = 0x0F;    // Exceptional Strength % (modified, fighters only)
    public const int OffStrExcBase = 0x10;   // Exceptional Strength % (base)
    public const int OffIntMod = 0x11;       // Intelligence (modified)
    public const int OffIntBase = 0x12;      // Intelligence (base)
    public const int OffWisMod = 0x13;       // Wisdom (modified)
    public const int OffWisBase = 0x14;      // Wisdom (base)
    public const int OffDexMod = 0x15;       // Dexterity (modified)
    public const int OffDexBase = 0x16;      // Dexterity (base)
    public const int OffConMod = 0x17;       // Constitution (modified)
    public const int OffConBase = 0x18;      // Constitution (base)
    public const int OffChaMod = 0x19;       // Charisma (modified)
    public const int OffChaBase = 0x1A;      // Charisma (base)
    public const int AbilityCount = 6;

    /// <summary>Modified-value offsets for the six abilities (each base is +1).</summary>
    public static readonly int[] AbilityModOffsets =
        { OffStrMod, OffIntMod, OffWisMod, OffDexMod, OffConMod, OffChaMod };

    public const int OffHpCur = 0x1B;        // UINT8 — current hit points
    public const int OffHpMax = 0x1C;        // UINT8 — maximum hit points
    public const int OffAC = 0x1D;           // INT8 — armor class (signed; lower is better)
    public const int OffUnknown1 = 0x1E;     // BYTE — unknown / unused

    public const int OffRace = 0x1F;         // UINT8 — race+sex (0..11)
    public const int OffClass = 0x20;        // UINT8 — character class (0..14)
    public const int OffAlignment = 0x21;    // UINT8 — alignment (0..8)
    public const int OffPortrait = 0x22;     // UINT8 — portrait index
    public const int OffFood = 0x23;         // UINT8 — food % (0..100)

    public const int OffLevel1 = 0x24;       // UINT8 — level of primary class
    public const int OffLevel2 = 0x25;       // UINT8 — level of secondary class (multi-class)
    public const int OffLevel3 = 0x26;       // UINT8 — level of tertiary class (multi-class)

    public const int OffXp1 = 0x27;          // UINT32LE — experience for primary class
    public const int OffXp2 = 0x2B;          // UINT32LE — experience for secondary class
    public const int OffXp3 = 0x2F;          // UINT32LE — experience for tertiary class

    // Offsets 0x33..0x76 (51..118) contain spell data (learned/memorized spells).
    // The exact bit layout of this region is partially documented; it is round-tripped
    // untouched by the trainer.
    public const int OffSpellData = 0x33;    // 68 bytes of spell/other data
    public const int SpellDataLength = 68;

    // Equipment slots — each is a 2-byte item ID (0x0000 = empty).
    public const int OffHand1 = 0x77;        // Left hand weapon/shield (2 bytes)
    public const int OffHand2 = 0x79;        // Right hand weapon/shield (2 bytes)
    public const int OffBackpack = 0x7B;     // 14 backpack slots × 2 bytes = 28 bytes
    public const int BackpackSlots = 14;
    public const int BackpackSlotSize = 2;

    // Offsets 0x97..0xF2 (151..242) contain additional equipment slots (armor, bracers,
    // helm, medallion, boots, belt, rings) and other character data. Round-tripped untouched.

    // --- "max" targets used by the trainer's quick actions -------------------
    public const int MaxAttribute = 25;      // AD&D 2nd edition natural maximum
    public const int MaxStrExc = 100;        // Exceptional strength percentage
    public const int MaxHp = 255;            // UINT8 maximum
    public const int MaxLevel = 40;          // practical level cap
    public const long MaxXp = 9_999_999;     // practical XP cap
    public const int MaxFood = 100;          // food percentage maximum
    public const int MinAC = -10;            // best armor class in AD&D

    // --- lookup tables -------------------------------------------------------
    public static readonly string[] AbilityNames =
        { "Strength", "Intelligence", "Wisdom", "Dexterity", "Constitution", "Charisma" };

    public static readonly string[] AbilityShort =
        { "STR", "INT", "WIS", "DEX", "CON", "CHA" };

    public static readonly string[] RaceNames =
    {
        "Human Male", "Human Female", "Elf Male", "Elf Female",
        "Half-Elf Male", "Half-Elf Female", "Dwarf Male", "Dwarf Female",
        "Gnome Male", "Gnome Female", "Halfling Male", "Halfling Female"
    };

    public static readonly string[] ClassNames =
    {
        "Fighter", "Ranger", "Paladin", "Mage", "Cleric", "Thief",
        "Fighter/Cleric", "Fighter/Thief", "Fighter/Mage", "Fighter/Mage/Thief",
        "Thief/Mage", "Cleric/Thief", "Fighter/Cleric/Mage", "Ranger/Cleric", "Cleric/Mage"
    };

    public static readonly string[] AlignmentNames =
    {
        "Lawful Good", "Neutral Good", "Chaotic Good",
        "Lawful Neutral", "True Neutral", "Chaotic Neutral",
        "Lawful Evil", "Neutral Evil", "Chaotic Evil"
    };

    public static string RaceName(int v) => v >= 0 && v < RaceNames.Length ? RaceNames[v] : $"?({v})";
    public static string ClassName(int v) => v >= 0 && v < ClassNames.Length ? ClassNames[v] : $"?({v})";
    public static string AlignmentName(int v) => v >= 0 && v < AlignmentNames.Length ? AlignmentNames[v] : $"?({v})";

    /// <summary>Human-readable status summary for the character list.</summary>
    public static string StatusSummary(int hpCur, int hpMax, int ac, int food)
        => $"HP {hpCur}/{hpMax}  AC {ac}  Food {food}%";
}
