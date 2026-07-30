namespace SwordOfAragonTrainer.Game;

/// <summary>One row of the unit/character table embedded in <c>SWORD.EXE</c>.</summary>
/// <param name="Code">Type code as stored in a roster record (1–10).</param>
/// <param name="Name">The game's own name for the type.</param>
/// <param name="Abbreviation">Four-character abbreviation used on the unit list.</param>
/// <param name="Buy">Base purchase cost in gold pieces, before equipment.</param>
/// <param name="Train">Base training cost in gold pieces, before equipment.</param>
/// <param name="MaintTenths">Base upkeep in <b>tenths</b> of a gold piece per figure per month.</param>
/// <param name="Capacity">Carrying capacity (stored negative in the executable's weight column).</param>
public sealed record UnitType(
    int Code, string Name, string Abbreviation, int Buy, int Train, int MaintTenths, int Capacity)
{
    /// <summary>True for the five hireable character classes (codes 6–10).</summary>
    public bool IsCharacter => Code >= UnitBook.FirstCharacterCode;

    /// <summary>Upkeep in gold pieces per figure per month.</summary>
    public double MaintGold => MaintTenths / 10.0;

    public override string ToString() => Name;
}

/// <summary>One item in an equipment slot.</summary>
/// <param name="Index">Slot index as stored in a roster record; 0 always means "none".</param>
/// <param name="MinLevel">Level the unit must have reached before the game offers this item.</param>
public sealed record EquipmentItem(
    int Index, string Name, int Buy, int Train, int MaintTenths, int Weight, int MinLevel)
{
    public override string ToString() => Index == 0 ? "(none)" : Name;
}

/// <summary>The eight equipment slots a unit or character carries, in roster-record order.</summary>
public enum EquipmentSlot
{
    Armor = 0, Shield = 1, Weapon = 2, Pole = 3, Missile = 4, Bow = 5, Horse = 6, Barding = 7,
}

/// <summary>
/// The unit, character and equipment tables Sword of Aragon carries as QuickBASIC <c>DATA</c> text in
/// <c>SWORD.EXE</c>, plus the cost arithmetic that reproduces the three derived fields of every
/// roster record.
///
/// The cost model is not inferred — it reproduces <c>make</c>, <c>train</c> and <c>maint</c> exactly
/// for all <b>623</b> occupied roster records across the 15 shipped saves, covering 16 distinct
/// (player class, unit type) combinations. That includes the class purchase discounts: a Warrior
/// halves Infantry, a Knight takes 25 % off both Cavalry <i>and</i> Mounted Infantry (the rule book
/// only mentions cavalry), and a Ranger takes 25 % off Bowmen and Horse Bowmen.
/// </summary>
public static class UnitBook
{
    /// <summary>Lowest type code that denotes a character rather than a troop unit.</summary>
    public const int FirstCharacterCode = 6;

    /// <summary>Number of equipment slots per record.</summary>
    public const int SlotCount = 8;

    public static readonly IReadOnlyList<UnitType> Types = new[]
    {
        new UnitType(1,  "Infantry",       "Inf ",   4,  2,  3, 30),
        new UnitType(2,  "Mtd. Infantry",  "Mtd ",   8,  3,  5, 25),
        new UnitType(3,  "Cavalry",        "Cav ",  16,  4, 10, 20),
        new UnitType(4,  "Bowmen",         "Bow ",  12,  4,  6, 35),
        new UnitType(5,  "Horse Bowmen",   "HBow",  20,  5,  8, 25),
        new UnitType(6,  "Warrior",        "Warr",  40, 12, 10, 35),
        new UnitType(7,  "Knight",         "Kngt",  80, 16, 20, 30),
        new UnitType(8,  "Ranger",         "Rngr", 100, 20, 25, 30),
        new UnitType(9,  "Priest",         "Prst", 120, 25, 30, 20),
        new UnitType(10, "Mage",           "Mage", 160, 30, 40, 10),
    };

    private static readonly EquipmentItem[] ArmorItems =
    {
        new(0, "(none)",  0, 0,  0, 0, 0),
        new(1, "Robe",    2, 0,  0, 1, 0),
        new(2, "Leather", 8, 0,  2, 2, 0),
        new(3, "Chain",  20, 1,  5, 3, 0),
        new(4, "Mail",   40, 2, 10, 4, 0),
        new(5, "Plate",  80, 3, 15, 6, 3),
    };

    private static readonly EquipmentItem[] ShieldItems =
    {
        new(0, "(none)", 0, 0, 0, 0, 0),
        new(1, "Small",  2, 0, 0, 1, 0),
        new(2, "Large",  6, 1, 1, 3, 0),
        new(3, "Kite",   8, 1, 2, 4, 0),
    };

    private static readonly EquipmentItem[] WeaponItems =
    {
        new(0, "(none)",  0, 0, 0, 0, 0),
        new(1, "Dagger",  0, 0, 0, 0, 0),
        new(2, "Mace",    2, 0, 1, 0, 0),
        new(3, "Sword",   4, 1, 2, 1, 0),
        new(4, "Halberd", 6, 2, 3, 2, 1),
        new(5, "2-Hand",  8, 2, 2, 2, 3),
    };

    private static readonly EquipmentItem[] PoleItems =
    {
        new(0, "(none)",  0, 0, 0, 0, 0),
        new(1, "Spear",   2, 1, 3, 1, 0),
        new(2, "Pike",    4, 2, 4, 4, 4),
        new(3, "Lance",  10, 2, 6, 2, 0),
    };

    private static readonly EquipmentItem[] MissileItems =
    {
        new(0, "(none)",       0, 0, 0, 0, 0),
        new(1, "Thrown Spear", 3, 1, 3, 1, 0),
        new(2, "Javelin",      5, 2, 4, 2, 0),
        new(3, "Sling",        1, 2, 1, 0, 0),
    };

    private static readonly EquipmentItem[] BowItems =
    {
        new(0, "(none)",    0, 0,  0, 0, 0),
        new(1, "Crossbow",  8, 1,  4, 2, 0),
        new(2, "Short",     5, 3,  6, 1, 0),
        new(3, "Long",     15, 5,  8, 2, 3),
        new(4, "Compound", 25, 8, 10, 3, 5),
    };

    private static readonly EquipmentItem[] HorseItems =
    {
        new(0, "(none)",   0, 0,  0,   0, 0),
        new(1, "Light",   50, 2, 15, -10, 0),
        new(2, "Medium",  75, 3, 20, -20, 0),
        new(3, "Heavy",  100, 4, 25, -25, 2),
    };

    private static readonly EquipmentItem[] BardingItems =
    {
        new(0, "(none)",   0, 0,  0,  0, 0),
        new(1, "Leather", 10, 0,  6,  5, 0),
        new(2, "Chain",   20, 1,  8,  8, 0),
        new(3, "Mail",    40, 2, 10, 12, 2),
    };

    /// <summary>Stacking size points by horse slot: foot 2, light 4, medium 5, heavy 6.</summary>
    private static readonly int[] SizeByHorse = { 2, 4, 5, 6 };

    /// <summary>The items available in a slot, index 0 being "none".</summary>
    public static IReadOnlyList<EquipmentItem> Items(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Armor   => ArmorItems,
        EquipmentSlot.Shield  => ShieldItems,
        EquipmentSlot.Weapon  => WeaponItems,
        EquipmentSlot.Pole    => PoleItems,
        EquipmentSlot.Missile => MissileItems,
        EquipmentSlot.Bow     => BowItems,
        EquipmentSlot.Horse   => HorseItems,
        _                     => BardingItems,
    };

    /// <summary>Human-readable slot name.</summary>
    public static string SlotName(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Missile => "Missile",
        EquipmentSlot.Barding => "Barding",
        _ => slot.ToString(),
    };

    /// <summary>All eight slots in record order.</summary>
    public static readonly EquipmentSlot[] Slots =
    {
        EquipmentSlot.Armor, EquipmentSlot.Shield, EquipmentSlot.Weapon, EquipmentSlot.Pole,
        EquipmentSlot.Missile, EquipmentSlot.Bow, EquipmentSlot.Horse, EquipmentSlot.Barding,
    };

    /// <summary>The type row for a code, or null if the code is not one of the ten known types.</summary>
    public static UnitType? Type(int code) => Types.FirstOrDefault(t => t.Code == code);

    /// <summary>The game's name for a type code, or a bare number if it is unrecognised.</summary>
    public static string TypeName(int code) => Type(code)?.Name ?? $"type {code}";

    /// <summary>Highest valid index in a slot (so callers can clamp before writing).</summary>
    public static int MaxIndex(EquipmentSlot slot) => Items(slot).Count - 1;

    /// <summary>The item in a slot, or the "none" entry if the index is out of range.</summary>
    public static EquipmentItem Item(EquipmentSlot slot, int index)
    {
        var items = Items(slot);
        return index > 0 && index < items.Count ? items[index] : items[0];
    }

    /// <summary>Stacking size points implied by a record's horse slot.</summary>
    public static int SizePoints(int horseIndex) =>
        horseIndex > 0 && horseIndex < SizeByHorse.Length ? SizeByHorse[horseIndex] : SizeByHorse[0];

    /// <summary>
    /// Purchase/training/upkeep multiplier the player's own class grants for a unit type. A class with
    /// no matching discount, and every character record regardless of class, pays full price.
    ///
    /// The Warrior and Knight rows are Confirmed against the 623-record corpus (and the Knight's 25 %
    /// reaching Mounted Infantry as well as Cavalry is a finding the rule book does not mention). The
    /// **Ranger** row is the one exception: no Ranger-player save is shipped, so it rests on the rule
    /// book's statement alone and is labelled Unconfirmed in <c>docs/RE.md</c> §6.4a.
    /// </summary>
    public static double Discount(int playerClassCode, int unitTypeCode)
    {
        if (unitTypeCode >= FirstCharacterCode) return 1.0;      // characters never discounted
        return playerClassCode switch
        {
            6 when unitTypeCode == 1              => 0.50,       // Warrior  -> Infantry (Confirmed)
            7 when unitTypeCode is 2 or 3         => 0.75,       // Knight   -> Mtd. Inf, Cavalry (Confirmed)
            8 when unitTypeCode is 4 or 5         => 0.75,       // Ranger   -> Bowmen, H.Bow (rule book only)
            _                                     => 1.00,
        };
    }

    /// <summary>The three derived cost fields of a roster record.</summary>
    /// <param name="Make">Cost to raise a new unit of this exact configuration (GP).</param>
    /// <param name="Train">Cost to train it one step (GP).</param>
    /// <param name="MaintTenths">Upkeep in tenths of a GP per figure per month.</param>
    public readonly record struct Costs(int Make, int Train, int MaintTenths);

    /// <summary>
    /// Recomputes <see cref="Costs"/> from a type code, the eight equipment indices and the player's
    /// own class. <paramref name="equipment"/> must be in record order (armor…barding); out-of-range
    /// indices are treated as "none". Fractional discounted totals round half away from zero, which
    /// matches every fractional case in the shipped saves.
    /// </summary>
    public static Costs ComputeCosts(int typeCode, ReadOnlySpan<int> equipment, int playerClassCode)
    {
        var type = Type(typeCode);
        if (type == null) return default;

        int buy = type.Buy, train = type.Train, maint = type.MaintTenths;
        for (int i = 0; i < SlotCount && i < equipment.Length; i++)
        {
            var item = Item(Slots[i], equipment[i]);
            buy += item.Buy;
            train += item.Train;
            maint += item.MaintTenths;
        }

        double multiplier = Discount(playerClassCode, typeCode);
        if (multiplier < 1.0)
        {
            buy = RoundHalfUp(buy * multiplier);
            train = RoundHalfUp(train * multiplier);
            maint = RoundHalfUp(maint * multiplier);
        }
        return new Costs(buy, train, maint);
    }

    private static int RoundHalfUp(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
