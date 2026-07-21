namespace ColonizationTrainer.Game;

/// <summary>A unit type: its type index and name.</summary>
public sealed record UnitType(int Id, string Name, string Role);

/// <summary>
/// The 24 unit types in index order (the <c>type</c> byte of a unit record indexes this list).
/// Verified against the viceroy <c>unit_type_list</c> and <c>.games/PEDIA.TXT</c> (<c>@UNIT0…</c>).
/// </summary>
public static class UnitBook
{
    public static readonly IReadOnlyList<UnitType> Units = new[]
    {
        new UnitType(0,  "Colonist",            "Land — any job"),
        new UnitType(1,  "Soldier",             "Land — armed (muskets)"),
        new UnitType(2,  "Pioneer",             "Land — clears/plows/roads (tools)"),
        new UnitType(3,  "Missionary",          "Land — native missions"),
        new UnitType(4,  "Dragoon",             "Land — mounted soldier (best attacker)"),
        new UnitType(5,  "Scout",               "Land — mounted explorer"),
        new UnitType(6,  "Regular",             "Land — King's line infantry (REF)"),
        new UnitType(7,  "Continental Cavalry", "Land — your revolutionary cavalry"),
        new UnitType(8,  "Cavalry",             "Land — King's cavalry (REF)"),
        new UnitType(9,  "Continental Army",    "Land — your revolutionary infantry"),
        new UnitType(10, "Treasure Train",      "Land — carries plundered gold"),
        new UnitType(11, "Artillery",           "Land — siege (7/5; degrades on loss)"),
        new UnitType(12, "Wagon Train",         "Land — overland cargo"),
        new UnitType(13, "Caravel",             "Sea — 2 holds"),
        new UnitType(14, "Merchantman",         "Sea — 4 holds"),
        new UnitType(15, "Galleon",             "Sea — 6 holds"),
        new UnitType(16, "Privateer",           "Sea — raider (no flag)"),
        new UnitType(17, "Frigate",             "Sea — warship"),
        new UnitType(18, "Man-O-War",           "Sea — King's warship (REF)"),
        new UnitType(19, "Brave",               "Native — foot warrior"),
        new UnitType(20, "Armed Brave",         "Native — musket-armed"),
        new UnitType(21, "Mounted Brave",       "Native — mounted"),
        new UnitType(22, "Mounted Warrior",     "Native — musket + horse"),
        new UnitType(23, "Indian Convert",      "Land — converted native colonist"),
    };
}
