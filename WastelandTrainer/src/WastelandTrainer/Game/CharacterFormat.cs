namespace WastelandTrainer.Game;

/// <summary>
/// Byte-level layout of a Wasteland character record as it lives in the emulated DOS memory of a
/// running game (DOSBox / DOSBox-X), plus the constants used to find the roster and the party
/// position header.
///
/// The party roster is an array of <see cref="MaxSlots"/> fixed-size records
/// (<see cref="RecordSize"/> bytes each). Occupied members pack from slot 0; unused slots are
/// zero-filled (their name byte is 0x00). Directly <see cref="RecordSize"/> bytes before roster
/// slot 0 sits a 256-byte party-state header carrying the live map position and map name
/// (see <see cref="PartyHeaderSize"/> and the <c>Header*</c> offsets).
///
/// The layout was reverse-engineered from four live DOSBox-X memory dumps of the default party
/// (Hell Razor, Angela Deth, Thrasher, Snake Vargas) and cross-checked against the game manual,
/// the community wiki, and the open-source <c>kayahr/wastelib</c> file-format project. See
/// <c>.docs\Wasteland-Reverse-Engineering.md</c>.
///
/// Names are plain ASCII (NOT high-bit encoded like Dragon Wars). All multi-byte integers are
/// little-endian.
/// </summary>
public static class CharacterFormat
{
    /// <summary>Size of one character record in bytes.</summary>
    public const int RecordSize = 0x100;   // 256

    /// <summary>Number of roster slots (occupied slots pack from 0; the rest are zero-filled).</summary>
    public const int MaxSlots = 7;

    // --- record field offsets ------------------------------------------------
    public const int OffName = 0x00;
    public const int NameLength = 14;

    /// <summary>The seven attributes, one byte each, at 0x0E..0x14 (STR, IQ, LCK, SPD, AGL, DEX, CHR).</summary>
    public const int OffAttributes = 0x0E;
    public const int AttributeCount = 7;

    public const int OffMoney = 0x15;        // 24-bit LE
    public const int OffGender = 0x18;       // 0 = Male, 1 = Female
    public const int OffNationality = 0x19;  // 0 US, 1 Russian, 2 Mexican, 3 Indian, 4 Chinese
    public const int OffArmorClass = 0x1A;
    public const int OffMaxCon = 0x1B;       // u16 LE (max constitution)
    public const int OffCon = 0x1D;          // u16 LE (current constitution)
    public const int OffWeaponState = 0x1F;  // per-character weapon/equip byte — left untouched
    public const int OffSkillPoints = 0x20;  // SKP (unspent skill points)
    public const int OffExperience = 0x21;   // 24-bit LE
    public const int OffLevel = 0x24;
    public const int OffRank = 0x32;         // ASCII, NUL-terminated
    public const int RankLength = 14;

    // --- packed (id, value) arrays -------------------------------------------
    public const int OffSkills = 0x80;       // 30 slots x (skillId, level), 0x00-terminated
    public const int SkillSlots = 30;
    public const int OffInventory = 0xBD;    // 30 slots x (itemId, ammo/qty)
    public const int ItemSlots = 30;
    public const int SlotSize = 2;

    /// <summary>In an inventory slot's quantity byte, bit 7 flags a jammed weapon; the low 7 bits are
    /// the ammo / charge count. Freeze-ammo restores the count and clears this flag (a frozen weapon
    /// can't stay jammed).</summary>
    public const int InventoryJammedFlag = 0x80;
    public const int InventoryCountMask = 0x7F;
    public const int SkillBlockBytes = SkillSlots * SlotSize;   // 60
    public const int ItemBlockBytes = ItemSlots * SlotSize;     // 60

    // --- party-state header (sits at rosterBase - RecordSize) ----------------
    public const int PartyHeaderSize = 0x100;
    public const int HeaderPartyX = 0x08;    // absolute rosterBase - 0xF8
    public const int HeaderPartyY = 0x09;    // absolute rosterBase - 0xF7
    public const int HeaderMapName = 0xD0;   // absolute rosterBase - 0x30, 12 ASCII bytes
    public const int MapNameLength = 12;

    // --- "max" targets used by the trainer's quick actions -------------------
    public const int MaxAttribute = 99;
    public const int MaxSkillLevel = 10;
    public const int MaxSkillPoints = 99;
    /// <summary>Freeze Ammo tops every ammo-bearing slot up to this count each tick (fits the 7-bit
    /// ammo field, leaving the jammed-weapon flag at bit 7 free, which the freeze then clears).</summary>
    public const int MaxAmmo = 99;
    public const int MaxCon = 5000;
    public const long MaxMoney = 0xFFFFFF;   // 24-bit field
    public const long MaxExperience = 0xFFFFFF;

    /// <summary>
    /// Upper bound a MAXCON may take and still be treated as a real record when validating a
    /// scan candidate (see <see cref="CharacterRecord.IsOccupied"/> and the locator). Kept
    /// comfortably above <see cref="MaxCon"/> so a ranger maxed by the trainer still validates
    /// and never vanishes from the next re-scan.
    /// </summary>
    public const int MaxPlausibleCon = 9999;

    // --- lookup tables -------------------------------------------------------
    public static readonly string[] AttributeNames = { "STR", "IQ", "LCK", "SPD", "AGL", "DEX", "CHR" };

    public static readonly string[] Genders = { "Male", "Female" };
    public static string GenderName(int v) => v >= 0 && v < Genders.Length ? Genders[v] : $"?({v})";

    public static readonly string[] Nationalities = { "U.S.", "Russian", "Mexican", "Indian", "Chinese" };
    public static string NationalityName(int v) => v >= 0 && v < Nationalities.Length ? Nationalities[v] : $"?({v})";
}
