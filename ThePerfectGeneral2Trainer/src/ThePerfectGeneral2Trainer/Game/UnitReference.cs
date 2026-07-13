namespace ThePerfectGeneral2Trainer.Game;

/// <summary>
/// One row of the shipped <c>UNITINFO.DOC</c> unit table. All values are <b>Confirmed</b> game rules
/// (see <c>.docs/ReverseEngineering.md</c> §5). Numeric fields that the manual leaves blank for a unit
/// (a mine or fortification has no move / hit points / damage) are modelled as <see langword="null"/>;
/// fields the manual expresses as a range or note (a plane's move and damage) are kept as text.
/// </summary>
public readonly record struct UnitInfo(
    string Code,
    string Name,
    int Cost,
    string Move,
    string Bombard,
    int? HitPoints,
    string Damage,
    bool Repairable,
    bool AntiAir,
    string AttackStyle,
    string DefenseStyle);

/// <summary>
/// The read-only unit-rules reference the trainer surfaces so a player can weigh match-ups without
/// alt-tabbing to the manual. Ordered exactly as the <c>UNITINFO.DOC</c> stat table (which differs from
/// the purchase-screen order used by <see cref="PurchaseFormat"/> — only where MINE sits).
/// </summary>
public static class UnitReference
{
    /// <summary>Number of distinct unit types the game defines.</summary>
    public const int TypeCount = 16;

    /// <summary>The 16 unit definitions, in <c>UNITINFO.DOC</c> stat-table order.</summary>
    public static readonly IReadOnlyList<UnitInfo> Units = Array.AsReadOnly(new UnitInfo[]
    {
        new("INF",    "Infantry",          1, "1",     "",      3,   "2",           false, false, "Inf",   "Inf"),
        new("MGUN",   "Machine Gun",       3, "1",     "",      3,   "4",           false, true,  "MG",    "Inf"),
        new("ENG",    "Engineer",          5, "2",     "",      4,   "6",           false, false, "Eng",   "Inf"),
        new("BAZ",    "Bazooka",           3, "1",     "",      3,   "4",           false, false, "Armor", "Inf"),
        new("AC w/MG","Armored Car w/MG",  6, "11",    "",      3,   "4",           true,  true,  "MG",    "AC"),
        new("AC",     "Armored Car",       5, "11",    "",      3,   "2",           true,  false, "Armor", "AC"),
        new("LTANK",  "Light Tank",        6, "7",     "",      6,   "3",           true,  false, "Armor", "Armor"),
        new("MTANK",  "Medium Tank",       8, "6",     "",      8,   "4",           true,  false, "Armor", "Armor"),
        new("HTANK",  "Heavy Tank",       12, "5",     "",     15,   "6",           true,  true,  "Armor", "Armor"),
        new("MobART", "Mobile Artillery", 14, "5",     "11",    6,   "6",           true,  false, "Armor", "Armor"),
        new("LART",   "Light Artillery",   9, "0",     "13",    1,   "6",           false, false, "Armor", "Inf"),
        new("HART",   "Heavy Artillery",  20, "0",     "26",    1,   "6",           false, false, "Armor", "Inf"),
        new("PLANE",  "Plane",            15, "40-60", "20-30", 1,   "66% kill (ET 50%)", false, false, "Plane", "Plane"),
        new("MINE",   "Mine",              3, "",      "",      null, "",           false, false, "",      ""),
        new("FORT",   "Fortification",     2, "",      "",      null, "",           false, false, "",      ""),
        new("ETANK",  "Elephant Tank",    15, "3",     "",     21,   "9",           true,  true,  "Armor", "Armor"),
    });
}
