namespace Roadwar2000Trainer.Game;

/// <summary>
/// The engine's terrain table: 23 entries at <c>DS:0x3BFE</c>, each a pointer to the name the
/// status line prints. Several codes deliberately point at a blank string -- the three city
/// codes, because the engine prints the city's name instead, and the impassable codes, because
/// you can never be standing on one.
/// </summary>
public static class TerrainBook
{
    /// <summary>Codes 0..22, in engine order. Index is the byte stored in the map.</summary>
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Plains",       // 0
        "Farmland",     // 1
        "Desert",       // 2
        "Forest",       // 3
        "Water",        // 4  (blank in the engine's table)
        "Ruins",        // 5
        "Wilderness",   // 6  (blank in the engine's table)
        "Road", "Road", "Road", "Road", "Road", "Road",          // 7..12
        "Road", "Road", "Road", "Road", "Road", "Road",          // 13..18
        "Small metropolis",  // 19 (blank; the city name is printed)
        "Large metropolis",  // 20
        "Metroplex",         // 21
        "Oilfield",          // 22
    };

    public const int Plains = 0;
    public const int Farmland = 1;
    public const int Desert = 2;
    public const int Forest = 3;
    public const int Water = 4;
    public const int Ruins = 5;
    public const int Wilderness = 6;
    public const int RoadFirst = 7;
    public const int RoadLast = 18;
    public const int CitySmall = 19;
    public const int CityLarge = 20;
    public const int CityMetroplex = 21;
    public const int Oilfield = 22;

    /// <summary>Highest code the engine's own name table covers.</summary>
    public const int MaxNamedCode = 22;

    public static bool IsRoad(int code) => code >= RoadFirst && code <= RoadLast;

    public static bool IsCity(int code) => code is CitySmall or CityLarge or CityMetroplex;

    /// <summary>
    /// True for squares a gang can occupy. Codes above 22 are scenery tiles -- coastline,
    /// mountain and open-water art -- that the engine's name table does not cover; teleporting
    /// onto one leaves the status line reading garbage, which is how they were identified.
    /// </summary>
    public static bool IsPassable(int code) =>
        code <= MaxNamedCode && code != Water && code != Wilderness;

    public static string NameOf(int code) =>
        code >= 0 && code < Names.Count ? Names[code] : $"Impassable scenery ({code})";
}

/// <summary>The five crew grades, best first. Rank order drives every casualty and promotion roll.</summary>
public static class RankBook
{
    /// <summary>Table at <c>DS:0x3BC2</c>. The order is load-bearing -- it is the array order in the save.</summary>
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Armsmaster", "Bodyguard", "Commando", "Dragoon", "Escort",
    };

    public static string NameOf(int index) =>
        index >= 0 && index < Names.Count ? Names[index] : $"Rank {index}";
}

/// <summary>
/// Who holds a city. Table at <c>DS:0x3ACE</c>; index 0 is the engine's own "NO ONE." entry,
/// which is what a town reads as when nobody has taken it.
/// </summary>
public static class ResidentBook
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "No one",                       // 0
        "Lawful National Guard",        // 1
        "Renegade National Guard",      // 2
        "A local gang",                 // 3
        "Bureaucrats",                  // 4
        "Survivalists",                 // 5
        "Reborners",                    // 6
        "Satanists",                    // 7
        "Invaders",                     // 8
        "The Mob",                      // 9
    };

    /// <summary>The value that clears a town of its residents.</summary>
    public const int NoOne = 0;

    /// <summary>
    /// Residents beyond the ten named entries were observed in the shipped save (10, 12, 14 and
    /// 17 all occur). They are reported by number rather than guessed at; see
    /// docs/reverse-engineering.md for what was tried.
    /// </summary>
    public static string NameOf(int index) =>
        index >= 0 && index < Names.Count ? Names[index] : $"Faction {index}";
}

/// <summary>Foot-gangs met by the P)eople search. Table at <c>DS:0x3B84</c>.</summary>
public static class FootgangBook
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Street gangsters", "Armed rabble", "Mercenaries", "The needy",
        "Cannibals", "Satanists", "Mutants",
    };
}

/// <summary>The named road gangs that drive modified vehicles. Table at <c>DS:0x3DC2</c>.</summary>
public static class RoadGangBook
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Furies", "Muthuh Truckers", "Motorheads", "Hot Rod Lincolns", "Hard Hats",
        "Greyhounds", "Redneck Yahoos", "Dune Buggers", "Skulls", "Roughriders",
        "Invader Death Squad",
    };
}

/// <summary>
/// The eight G.U.B. scientists. Table at <c>DS:0x3CA2</c>. Bringing them home is how the game
/// is won; the Radio Direction Finder is how the last one or two are found.
/// </summary>
public static class ScientistBook
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Myron Smidlapp", "Alec Trotier", "Pedro Pintero", "Gloria Mills",
        "Gabriel Washington", "Donny Dade", "Dorothy Macalister", "Cheng Lu Sinh",
    };
}

/// <summary>Vehicle upgrade shops found while looting. Table at <c>DS:0x3CE6</c>.</summary>
public static class ImprovementBook
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Speed shop", "Performance shop", "Foundry", "Brake shop", "Welding shop", "Underbody shop",
    };
}

/// <summary>Assorted facts that were measured rather than read out of a table.</summary>
public static class GameFacts
{
    /// <summary>
    /// Carrying capacity in spaces for a vehicle of the given mass. Exact for all 19 shipped
    /// types (motorcycle 1 -&gt; 5 spaces, trailer truck 20 -&gt; 2,000).
    /// </summary>
    public static int CarryingCapacity(int mass) => 5 * mass * mass;

    /// <summary>Clock hour for a stored time-of-day index; the day runs from 6:00 AM.</summary>
    public static int HourOf(int timeIndex) => 6 + timeIndex;

    /// <summary>
    /// The stored index for a clock hour. The day starts at 6 AM, so hours 0-5 are indices 18-23 --
    /// this has to wrap, not clamp, or the last six hours of the game day all collapse onto 6 AM.
    /// </summary>
    public static int TimeIndexOf(int hour) => ((hour - 6) % 24 + 24) % 24;

    /// <summary>A 12-hour clock string for a stored time-of-day index.</summary>
    public static string ClockOf(int timeIndex)
    {
        int h = HourOf(timeIndex) % 24;
        string suffix = h < 12 ? "AM" : "PM";
        int display = h % 12 == 0 ? 12 : h % 12;
        return $"{display}:00 {suffix}";
    }

    /// <summary>The year is fixed at 1999/2000 in-fiction; the engine prints 1999 and rolls over with the day.</summary>
    public const int StartYear = 1999;
}
