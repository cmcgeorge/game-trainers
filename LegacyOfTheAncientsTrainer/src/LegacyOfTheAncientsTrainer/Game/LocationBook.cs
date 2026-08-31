namespace LegacyOfTheAncientsTrainer.Game;

/// <summary>Information about a single Legacy of the Ancients location.</summary>
public sealed record LocationInfo(int Id, string Name, string Type, string? Note = null);

/// <summary>
/// The towns, dungeons, castles, and other locations of Legacy of the Ancients,
/// from the game manual and walkthrough. There are 12 towns on the main continent
/// of Tarmalon, plus the Galactic Museum, Kelfor Castle, and three dungeons.
/// </summary>
public static class LocationBook
{
    public static readonly LocationInfo[] Locations =
    {
        // Towns
        new(0,  "Eagle Hollow",       "Town", "West of the museum; near pirates' isle"),
        new(1,  "Thornberry",         "Town", "Reachable via museum jade coin exhibit"),
        new(2,  "Holy Point",         "Town", "North of the museum; good starting base"),
        new(3,  "Big Rapids",         "Town", "Southwest corner of the main continent"),
        new(4,  "Laingsburg",         "Town", "Combat training school"),
        new(5,  "Grand Ledge",        "Town", "Combat training school"),
        new(6,  "Merchant Square",    "Town", "Close to the museum; convenient layout"),
        new(7,  "Alanville",          "Town", "Near Kelfor's Castle; buy a boat here"),
        new(8,  "Thompson Crossing",  "Town", "Northern reaches; combat training"),
        new(9,  "Cobbleton",          "Town", "Tucked behind hills"),
        new(10, "Mazelton",           "Town", "Armor training school"),
        new(11, "Isle City",          "Town", "On an island; confusing layout"),

        // Special locations
        new(12, "Galactic Museum",    "Special", "Center of the quest; exhibits and gateways"),
        new(13, "Kelfor Castle",      "Castle", "On an island in the inland lake"),
        new(14, "Pirates' Cave",      "Dungeon", "West of Eagle Hollow; 8 levels; sapphire coin at bottom"),
        new(15, "Armaz",              "Dungeon", "The Test; +10 strength; starts from bottom"),
        new(16, "Four Jewels Dungeon", "Dungeon", "Toughest dungeon; four guard jewels at bottom"),
    };

    public static int Count => Locations.Length;
    public static int TownCount => 12;
}
