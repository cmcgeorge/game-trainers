namespace Questron2Trainer.Game;

/// <summary>Information about a single Questron II location.</summary>
public sealed record LocationInfo(int Id, string Name, string Type);

/// <summary>
/// The towns, cathedrals, castles, tombs, dungeons, and other locations of Questron II,
/// extracted from START.EXE strings. ICN file names confirm the building types.
/// </summary>
public static class LocationBook
{
    public static readonly LocationInfo[] Locations =
    {
        // Towns
        new(0,  "Hidden Rock",           "Town"),
        new(1,  "Bay View",              "Town"),
        new(2,  "Folman",                "Town"),
        new(3,  "Ontaga",                "Town"),
        new(4,  "Crooked Pine",          "Town"),
        new(5,  "Santor",                "Town"),
        new(6,  "Long View",             "Town"),
        new(7,  "Seacrest",              "Town"),
        new(8,  "Octapoint",             "Town"),
        new(9,  "Cramford",              "Town"),
        // Cathedrals
        new(10, "Sanctuary Cathedral",   "Cathedral"),
        new(11, "Rivercrest Cathedral",  "Cathedral"),
        new(12, "Great Plains Cathedral","Cathedral"),
        new(13, "Twilight Cathedral",    "Cathedral"),
        // Castles
        new(14, "Redstone Castle",       "Castle"),
        // Other surface locations
        new(15, "Slippery Rock",         "Landmark"),
        new(16, "Lookout Point",         "Landmark"),
        new(17, "Big Oak",               "Landmark"),
        new(18, "Grissold",              "Landmark"),
        new(19, "Orchard Lake",          "Landmark"),
        new(20, "Brantown",              "Landmark"),
        new(21, "Burnside",              "Landmark"),
        // Tombs
        new(22, "Rivercrest Tomb",       "Tomb"),
        new(23, "Twilight Tomb",         "Tomb"),
        // Dungeons
        new(24, "The Dungeon of Despair","Dungeon"),
        // Conclave
        new(25, "The Conclave of Sorcerers","Special"),
    };

    public static int Count => Locations.Length;
}
