namespace SwordOfAragonTrainer.Game;

/// <summary>
/// Layout of an <c>ARAGON.HR&lt;letter&gt;</c> roster file: exactly 80 fixed-size records of 100 bytes,
/// split into 20 character slots followed by 60 unit slots — a split the game itself enforces
/// ("A maximum of 20 individual characters are allowed", "You may only have 60 different units.").
/// Occupied slots pack from the start of each range.
///
/// Only the offsets marked Confirmed in <c>docs/RE.md</c> §6.3 are named here; the trainer never
/// writes an offset whose meaning is unproven.
/// </summary>
public static class RosterFormat
{
    /// <summary>Total file size. A file of any other length is not a roster.</summary>
    public const int FileSize = 8_000;

    /// <summary>Bytes per record.</summary>
    public const int RecordSize = 100;

    /// <summary>Records in the file.</summary>
    public const int SlotCount = FileSize / RecordSize;      // 80

    /// <summary>Slots 0..19 hold hired/created characters.</summary>
    public const int CharacterSlots = 20;

    /// <summary>Slots 20..79 hold troop units.</summary>
    public const int UnitSlots = SlotCount - CharacterSlots; // 60

    /// <summary>Index of the first unit slot.</summary>
    public const int FirstUnitSlot = CharacterSlots;

    /// <summary>The player's own character always occupies slot 0.</summary>
    public const int PlayerSlot = 0;

    // --- field offsets within a record -----------------------------------------
    public const int OffName = 0x00;
    public const int NameLength = 16;
    public const int OffExperience = 0x10;   // MBF single
    public const int OffType = 0x14;
    public const int OffArmor = 0x16;
    public const int OffShield = 0x18;
    public const int OffWeapon = 0x1A;
    public const int OffPole = 0x1C;
    public const int OffMissile = 0x1E;
    public const int OffBow = 0x20;
    public const int OffHorse = 0x22;
    public const int OffBarding = 0x24;
    public const int OffMakeCost = 0x28;
    public const int OffTrainCost = 0x2A;
    public const int OffMaintTenths = 0x2C;
    public const int OffLevel = 0x32;
    public const int OffMoveMax = 0x34;
    public const int OffX = 0x38;
    public const int OffY = 0x3A;
    public const int OffMen = 0x3C;
    public const int OffHits = 0x3E;
    public const int OffArmorClassHand = 0x40;
    public const int OffArmorClassMissile = 0x42;
    public const int OffMoveLeft = 0x46;
    public const int OffSize = 0x48;
    public const int OffHandDamage = 0x4C;
    public const int OffHandBonus = 0x50;

    /// <summary>Byte-wide mirror of <see cref="OffLevel"/> the game keeps in step.</summary>
    public const int OffPackedLevel = 0x60;

    /// <summary>Byte-wide mirror of <see cref="OffType"/> the game keeps in step.</summary>
    public const int OffPackedType = 0x61;

    /// <summary>Equipment slot offsets in <see cref="EquipmentSlot"/> order.</summary>
    public static readonly int[] EquipmentOffsets =
    {
        OffArmor, OffShield, OffWeapon, OffPole, OffMissile, OffBow, OffHorse, OffBarding,
    };

    /// <summary>Highest level the trainer will write. The game's own ladder never approaches this.</summary>
    public const int MaxLevel = 99;

    /// <summary>Highest figure count the trainer will write into a unit.</summary>
    public const int MaxMen = 999;

    /// <summary>Byte offset of a slot's record.</summary>
    public static int RecordOffset(int slot) => slot * RecordSize;

    /// <summary>True if <paramref name="slot"/> is one of the 20 character slots.</summary>
    public static bool IsCharacterSlot(int slot) => slot >= 0 && slot < CharacterSlots;
}
