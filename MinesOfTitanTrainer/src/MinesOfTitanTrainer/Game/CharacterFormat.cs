namespace MinesOfTitanTrainer.Game;

/// <summary>
/// Byte-level layout of a Mines of Titan character record as it lives both in
/// <c>SAVEGAME.DAT</c> and in the emulated DOS memory of a running game (DOSBox / DOSBox-X),
/// plus the constants used to find the party.
///
/// Reverse-engineered from a byte-level analysis of <c>SAVEGAME.DAT</c> cross-checked against a
/// Ghidra disassembly of <c>TITAN.EXE</c> (see <c>.docs/ReverseEngineering.md</c>). Each save slot
/// begins with the ASCII magic <see cref="SlotMagic"/> (<c>IJKM</c>); the character array starts a
/// fixed delta (<see cref="SlotToFirstRecord"/>) past that anchor and packs as an array of
/// <see cref="RecordSize"/>-byte records.
///
/// Names are plain, <c>0x00</c>-padded ASCII (not high-bit encoded).
/// </summary>
public static class CharacterFormat
{
    /// <summary>Size of one character record in bytes.</summary>
    public const int RecordSize = 0x56;     // 86

    /// <summary>Save-slot magic; the character array begins <see cref="SlotToFirstRecord"/> past it.</summary>
    public static readonly byte[] SlotMagic = { 0x49, 0x4A, 0x4B, 0x4D };   // "IJKM"

    /// <summary>Delta from a slot's magic anchor to its first character record.</summary>
    public const int SlotToFirstRecord = 0x1A;

    /// <summary>Maximum party members read from a located party.</summary>
    public const int MaxSlots = 6;

    // --- record field offsets ------------------------------------------------
    public const int OffName = 0x00;
    public const int NameLength = 16;
    public const int OffSex = 0x10;         // ASCII 'M' / 'F'
    public const int OffAge = 0x11;         // byte
    public const int OffAttributes = 0x12;  // 6 bytes
    public const int AttributeCount = 6;
    public const int OffSkills = 0x18;      // 27 bytes, one rank each
    public const int SkillCount = 27;
    public const int OffCredits = 0x48;     // uint32 little-endian

    // --- value bounds --------------------------------------------------------
    // Shared by the occupancy check (a scanned window is rejected outside these) and by the
    // editor setters (which clamp to them), so an edit can never write bytes that would make the
    // record fail its own validation and vanish from the next re-scan.
    public const int MinAge = 1;
    public const int MaxAge = 120;
    public const int MaxAttribute = 15;     // primary attributes display 0..15
    public const int MaxSkill = 15;         // skill ranks observed maxing at 15 in a cheated save
    public const long MaxCredits = 999_999;

    // --- lookup tables -------------------------------------------------------
    public static readonly string[] AttributeNames =
    {
        "Might", "Agility", "Stamina", "Wisdom", "Education", "Charisma"
    };

    private static readonly string[] NamedSkills =
    {
        "Administration", "Arc Gun", "Automatic Weapons", "Battle Armor",
        "Blade", "Cudgel", "Gambling", "Golum",
        "Handgun", "Medical", "Melee", "Mining",
        "Programming", "Rifle", "Street", "Throwing"
    };

    /// <summary>
    /// Skill ranks, one byte each (0x18..0x32). The first 16 are the skills named in the
    /// Player's Guide; the remaining slots are reserved/unnamed and shown as "Skill N".
    /// </summary>
    public static readonly string[] SkillNames = BuildSkillNames();

    private static string[] BuildSkillNames()
    {
        var names = new string[SkillCount];
        for (int i = 0; i < SkillCount; i++)
            names[i] = i < NamedSkills.Length ? NamedSkills[i] : $"Skill {i + 1}";
        return names;
    }

    public static string SexName(char sex) => sex switch
    {
        'M' or 'm' => "Male",
        'F' or 'f' => "Female",
        _ => "?"
    };
}
