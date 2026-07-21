namespace ColonizationTrainer.Game;

/// <summary>A colony building: its pedia index, name, and what it does.</summary>
public sealed record Building(int Id, string Name, string Effect);

/// <summary>
/// The 42 building entries in pedia index order (<c>.games/PEDIA.TXT @BUILDING0…41</c>). Many are
/// tiers of the same slot (Stockade→Fort→Fortress, etc.); the colony record stores each slot's level
/// in a packed bitfield (see <c>docs/Colonization-Reverse-Engineering.md §6</c>). This is a
/// read-only reference — the trainer does not rewrite the packed building bits.
/// </summary>
public static class BuildingBook
{
    public static readonly IReadOnlyList<Building> Buildings = new[]
    {
        new Building(0,  "Stockade",           "+100% colony defense (pop 3+)"),
        new Building(1,  "Fort",               "+150% colony defense; bombards ships (pop 4+)"),
        new Building(2,  "Fortress",           "+200% colony defense; bombards ships (pop 8+)"),
        new Building(3,  "Armory",             "Makes Muskets from Tools"),
        new Building(4,  "Magazine",           "Faster musket production"),
        new Building(5,  "Arsenal",            "Fastest musket production (factory)"),
        new Building(6,  "Docks",              "Work ocean tiles for fish"),
        new Building(7,  "Drydock",            "Repair ships; build small ships"),
        new Building(8,  "Shipyard",           "Build large ships"),
        new Building(9,  "Town Hall",          "Produces Liberty Bells"),
        new Building(10, "Town Hall (upg.)",   "Improved Liberty Bell output"),
        new Building(11, "Colonial Assembly",  "Top-tier Liberty Bells"),
        new Building(12, "Schoolhouse",        "Teach basic experts (4 turns)"),
        new Building(13, "College",            "Teach mid experts (6 turns)"),
        new Building(14, "University",          "Teach elite experts (8 turns)"),
        new Building(15, "Warehouse",          "Raises storage cap to 200"),
        new Building(16, "Warehouse Expansion","Raises storage cap to 300"),
        new Building(17, "Stables",            "Breed horses twice as fast"),
        new Building(18, "Custom House",       "Auto-sell goods to Europe (even in war)"),
        new Building(19, "Printing Press",     "+Liberty Bell production"),
        new Building(20, "Newspaper",          "++Liberty Bell production"),
        new Building(21, "Weaver's House",     "Cotton → Cloth"),
        new Building(22, "Weaver's Shop",      "Faster Cotton → Cloth"),
        new Building(23, "Textile Mill",       "Cotton → Cloth (factory)"),
        new Building(24, "Tobacconist's House","Tobacco → Cigars"),
        new Building(25, "Tobacconist's Shop", "Faster Tobacco → Cigars"),
        new Building(26, "Cigar Factory",      "Tobacco → Cigars (factory)"),
        new Building(27, "Rum Distiller's House","Sugar → Rum"),
        new Building(28, "Rum Distillery",     "Faster Sugar → Rum"),
        new Building(29, "Rum Factory",        "Sugar → Rum (factory)"),
        new Building(30, "Capitol",            "Government building"),
        new Building(31, "Capitol Expansion",  "Government building"),
        new Building(32, "Fur Trader's House", "Furs → Coats"),
        new Building(33, "Fur Trader's Shop",  "Faster Furs → Coats"),
        new Building(34, "Coat Factory",       "Furs → Coats (factory)"),
        new Building(35, "Carpenter's Shop",   "Lumber → Hammers (construction)"),
        new Building(36, "Lumber Mill",        "Faster Lumber → Hammers"),
        new Building(37, "Church",             "Produces Crosses (immigration)"),
        new Building(38, "Cathedral",          "More Crosses"),
        new Building(39, "Blacksmith's House", "Ore → Tools"),
        new Building(40, "Blacksmith's Shop",  "Faster Ore → Tools"),
        new Building(41, "Iron Works",         "Ore → Tools (factory)"),
    };
}
