namespace FountainOfDreamsTrainer.Game;

/// <summary>
/// Byte-level layout of a Fountain of Dreams character record as it lives in the emulated DOS
/// memory of a running game (DOSBox / DOSBox-X), plus the constants used to find the party.
///
/// The party roster is an array of <see cref="MaxSlots"/> fixed-size records
/// (<see cref="RecordSize"/> bytes each). Occupied members pack from slot 0; unused slots are
/// zero-filled (their name byte is 0x00).
///
/// The layout was reverse-engineered from static analysis of the shipped DISK1 save file
/// (three starting characters: Ojnab Bob, Junior, Ignatz Krebs) and cross-checked against the
/// ARCHTYPE profession template file, the FOD.EXE character-creation display strings, and the
/// game manual. No live memory dump was available (DOSBox was not installed), so every offset
/// carries a [Static] confidence marker — confirmed against save-file data but not yet
/// verified against a running game's RAM. See
/// <c>docs\FountainOfDreams-Reverse-Engineering.md</c>.
///
/// Names are plain ASCII. All multi-byte integers are little-endian. The game uses Microsoft C
/// 1988 and EXEPACK compression; the main engine is KEH.EXE.
/// </summary>
public static class CharacterFormat
{
    /// <summary>Size of one character record in bytes (confirmed by inter-character spacing in DISK1).</summary>
    public const int RecordSize = 0x14C;   // 332

    /// <summary>Number of roster slots (the party is up to 3 characters).</summary>
    public const int MaxSlots = 3;

    // --- record field offsets ------------------------------------------------
    /// <summary>Name field: null-terminated ASCII, followed by variable-length quote text up to +0x13.</summary>
    public const int OffName = 0x00;
    public const int NameFieldLength = 20;      // +0x00..+0x13 (name + quote + padding)

    /// <summary>Cash in dollars, uint32 LE (confirmed: 0, 25, 50 for three test characters).</summary>
    public const int OffCash = 0x14;

    /// <summary>Seven attributes, one byte each, at +0x18..+0x1E (ST, IQ, DX, WP, AP, CH, LK).</summary>
    public const int OffAttributes = 0x18;
    public const int AttributeCount = 7;

    /// <summary>Profession/sex/flags byte (0 for all three test characters).</summary>
    public const int OffProfession = 0x1F;

    /// <summary>Constitution (current CON), uint8 (confirmed: 21, 21, 22 — within profession CON ranges).</summary>
    public const int OffCon = 0x23;

    /// <summary>MaxCON, uint16 LE (confirmed: 25, 15, 17 — matches profession CON max ranges).</summary>
    public const int OffMaxCon = 0x46;

    /// <summary>Armor Class, uint8 (at +0x44; +0x45 is a flag byte — 0xFF for the default character).</summary>
    public const int OffArmorClass = 0x44;

    /// <summary>Equipped-weapon/armor flag byte (0xFF = unequipped for default character).</summary>
    public const int OffEquipFlag = 0x45;

    /// <summary>Level, uint8 (confirmed: all 1 for starting characters).</summary>
    public const int OffLevel = 0x50;

    /// <summary>Rank, uint16 LE (confirmed: 6, 7, 8 for the three test characters).</summary>
    public const int OffRank = 0x52;

    /// <summary>Experience, uint32 LE (confirmed: all 0 for starting characters).</summary>
    public const int OffExperience = 0x54;

    /// <summary>Next-level experience threshold, uint16 LE (confirmed: 1500, 1000, 1500).</summary>
    public const int OffNextLevelXp = 0x5E;

    // --- inventory -----------------------------------------------------------
    /// <summary>Inventory: 27 slots × 6 bytes each. First byte = item ID (0xFF = empty); remaining 5 bytes are item-specific.</summary>
    public const int OffInventory = 0x80;
    public const int InventorySlots = 27;
    public const int InventorySlotSize = 6;
    public const int InventoryEmpty = 0xFF;
    public static readonly int InventoryBytes = InventorySlots * InventorySlotSize;  // 162

    // --- "max" targets used by the trainer's quick actions -------------------
    public const int MaxAttribute = 20;
    public const int MaxSkillLevel = 10;
    public const int MaxCon = 99;
    public const long MaxCash = 9_999_999;
    public const long MaxExperience = 9_999_999;
    public const int MaxLevel = 99;

    /// <summary>
    /// Upper bound a MaxCON may take and still be treated as a real record when validating a
    /// scan candidate. Kept comfortably above <see cref="MaxCon"/> so an edited character
    /// still validates and never vanishes from the next re-scan.
    /// </summary>
    public const int MaxPlausibleCon = 999;

    // --- lookup tables -------------------------------------------------------
    /// <summary>Attribute abbreviations in record order (ST, IQ, DX, WP, AP, CH, LK).</summary>
    public static readonly string[] AttributeNames = { "ST", "IQ", "DX", "WP", "AP", "CH", "LK" };

    /// <summary>Full attribute names in record order.</summary>
    public static readonly string[] AttributeFullNames =
    {
        "Strength", "Intelligence", "Dexterity", "Willpower",
        "Appeal", "Charisma", "Luck"
    };

    public static readonly string[] Genders = { "Male", "Female" };
    public static string GenderName(int v) => v >= 0 && v < Genders.Length ? Genders[v] : $"?({v})";

    /// <summary>Condition states from the game manual.</summary>
    public static readonly string[] Conditions =
    {
        "Unafflicted", "Afflicted", "UNC", "SER", "CRT", "COM", "DED"
    };

    /// <summary>Profession names (playable: 0-4; NPC: 5-6).</summary>
    public static readonly string[] Professions =
    {
        "Survivalist", "Vigilante", "Medic", "Hood", "Mechanic", "Yuppie", "Clown"
    };

    public static string ProfessionName(int v) =>
        v >= 0 && v < Professions.Length ? Professions[v] : $"?({v})";
}
