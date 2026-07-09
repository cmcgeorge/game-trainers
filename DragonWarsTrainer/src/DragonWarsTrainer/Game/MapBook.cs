namespace DragonWarsTrainer.Game;

/// <summary>A keyed spot on an area map: its (x, y) grid square and what's there.</summary>
public sealed record MapLocation(string Name, int X, int Y, string Notes = "")
{
    public string Coord => $"({X}, {Y})";
}

/// <summary>One explorable area: its board id, grid size, notes, and notable keyed locations.</summary>
public sealed record MapArea(int Id, string Name, int Width, int Height, string Notes,
    IReadOnlyList<MapLocation> Locations)
{
    public string Size => $"{Width}×{Height}";
    public string Header => $"0x{Id:X2}  {Name}   ({Size})";
}

/// <summary>
/// Dragon Wars map/coordinate reference and the live-position ("Heap") layout, sourced from the
/// <c>fraterrisus/dragonjars</c> engine (<c>Heap.java</c>, <c>Lists.MAP_NAMES</c>) and the
/// hitchhikerprod maps. The party's live position lives in a 256-byte global "Heap" whose address
/// changes each session, so the Maps tab locates it at runtime (see <c>HeapLocator</c>) and uses
/// these coordinates to drive the teleport helper. Coordinates are (X = column, Y = row), origin
/// north-west.
/// </summary>
public static class MapBook
{
    // --- Heap (live-position) byte layout ------------------------------------
    public const int HeapSize = 256;
    public const int OffPartyY = 0x00;
    public const int OffPartyX = 0x01;
    public const int OffBoardId = 0x02;
    public const int OffFacing = 0x03;
    public const int OffBoardMaxX = 0x21;
    public const int OffBoardMaxY = 0x22;

    public const int MaxBoardId = 0x27;

    public static readonly string[] Facings = { "North", "East", "South", "West" };

    public static string FacingName(int f) => f >= 0 && f < Facings.Length ? Facings[f] : $"?({f})";

    /// <summary>Board names indexed by board id (0x00..0x27), from <c>Lists.MAP_NAMES</c>.</summary>
    public static readonly string[] MapNames =
    {
        "Dilmun (Overworld)", "Purgatory", "Slave Camp", "Guard Bridge #1", "Salvation",
        "Tars Ruins", "Phoebus", "Guard Bridge #2", "Mud Toad", "Byzanople",
        "Smugglers Cove", "War Bridge", "Scorpion Bridge", "Bridge of Exiles", "Necropolis",
        "Dwarf Ruins", "Dwarf Clan Hall", "Freeport", "Magan Underworld", "Slave Mines",
        "Lansk", "Sunken Ruins Above", "Sunken Ruins Below", "Mystic Wood", "Snake Pit",
        "Kingshome", "Pilgrim Dock", "Depths of Nisir", "Old Dock", "Siege Camp",
        "Game Preserve", "Magic College", "Dragon Valley", "Phoebus Dungeon", "Lanac'toor's Lab",
        "Byzanople Dungeon", "Kingshome Dungeon", "Slave Estate", "Lansk Undercity", "Tars Dungeon",
    };

    public static string MapName(int id) => id >= 0 && id < MapNames.Length ? MapNames[id] : $"Map 0x{id:X2}";

    public static readonly IReadOnlyList<MapArea> Areas = new MapArea[]
    {
        new(0x01, "Purgatory", 32, 32,
            "The starting prison. Level up in the Arena, recharge Power, then escape via the Morgue " +
            "(friendly Slave Camp flag) or swim out through the Hole in the Wall.",
            new MapLocation[]
            {
                new("Game Start", 20, 13),
                new("Phoebus's Tavern", 25, 27, "Recruit Ulrik."),
                new("Low Magic Spell Shoppe", 3, 22),
                new("Black Market Merchant", 12, 30),
                new("Power Recharge Pool", 23, 2, "Restores 100% Power to all."),
                new("Statue of Irkalla", 6, 13, "Sacrifice items for her blessing (flag 0x80)."),
                new("Apsu Waters", 7, 12, "Exit portal to the Magan Underworld."),
                new("The Arena Master", 19, 26, "Acquire Citizenship Papers."),
                new("Humbaba (quest monster)", 31, 31),
                new("The Morgue", 31, 10, "Escape Purgatory; sets friendly flag 0x40."),
                new("Hole in the Wall", 25, 8, "Escape via the bay (needs Swim)."),
            }),

        new(0x00, "Dilmun (Overworld)", 48, 48,
            "The wrapping overworld connecting every region. Entrances to the towns and dungeons, plus " +
            "the free Forlorn heal/recharge pool.",
            new MapLocation[]
            {
                new("Entrance to Purgatory", 13, 4),
                new("Entrance to Slave Camp", 11, 3),
                new("Entrance to Slave Estate", 17, 7),
                new("Entrance to Tars Ruins", 21, 4),
                new("Free Heal & Power Pool", 14, 1, "Forlorn Peninsula."),
                new("Guard Bridge #1", 12, 7, "Forlorn ↔ Isle of the Sun."),
                new("Entrance to Phoebus", 5, 11),
                new("Entrance to Mystic Wood", 2, 6),
                new("Guard Bridge #2", 14, 12, "Sun ↔ Lansk."),
                new("Entrance to Lansk", 16, 14),
                new("War Bridge", 18, 12, "Lansk ↔ Quag."),
                new("City of the Yellow Mud Toad", 25, 8),
                new("Entrance to Necropolis", 27, 15),
                new("Entrance to Kingshome", 18, 27),
                new("Entrance to Byzanople", 7, 27),
                new("Entrance to Dwarf Ruins", 10, 21),
                new("Entrance to Old Dock", 14, 17),
                new("Entrance to Freeport", 43, 23),
                new("Entrance to Sunken Ruins", 38, 15),
                new("Mount Salvation / Nisir", 19, 19),
            }),

        new(0x11, "Freeport", 16, 16,
            "A pirate town reached by boat. Cheap shields, a recruitable companion, and the Order of " +
            "the Sword — plus a deadly fake Sword of Freedom.",
            new MapLocation[]
            {
                new("Boat Dock", 14, 14),
                new("Ryan's Armor Shop", 5, 14, "Large Shields for $100."),
                new("Brews Brothers Tavern", 14, 7, "Recruit Halifax."),
                new("Order of the Sword", 14, 8, "Stone Hands and the Spell Staff."),
                new("Fake Sword of Freedom", 3, 4, "Trap — picking it up incinerates characters."),
            }),
    };
}
