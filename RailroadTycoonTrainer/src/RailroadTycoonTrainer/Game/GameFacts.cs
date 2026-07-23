namespace RailroadTycoonTrainer.Game;

/// <summary>A station type: build cost, catchment radius/coverage and yearly maintenance.</summary>
public sealed record StationKind(string Name, int Cost, int Radius, string Coverage, int Maintenance);

/// <summary>One of the four playable maps and the year its game begins.</summary>
public sealed record Scenario(string Name, int StartYear, string Currency, string Notes);

/// <summary>A difficulty level and the length of a game played to the end at it.</summary>
public sealed record Difficulty(string Name, int GameYears, string DifficultyFactor);

/// <summary>A station improvement and what it does.</summary>
public sealed record Improvement(string Name, int Cost, string Effect);

/// <summary>
/// Static game-knowledge tables for the Reference tab and the docs: the internal representation facts
/// the trainer relies on, plus the manual's stations / scenarios / difficulty / improvements data.
/// Nothing here touches the live process — it explains what the scanner and locator are aiming at.
/// </summary>
public static class GameFacts
{
    // --- internal representation (what the trainer reads/writes) --------------------------------
    /// <summary>Cash is stored in units of $1,000 (a signed int16). $ display = stored × 1000.</summary>
    public const int CashUnitDollars = 1000;

    // --- engine limits (manual / README.DOC) ---------------------------------------------------
    public const int MaxTrains = 32;
    public const int MaxStations = 32;
    public const int MaxSignalTowersPlusStations = 96;
    public const int MaxCarsPerTrain = 8;
    public const int MaxCities = 100;
    public const int MaxPlayers = 4;      // 1 human + up to 3 AI railroads

    // --- stations -------------------------------------------------------------------------------
    public static readonly IReadOnlyList<StationKind> Stations = new[]
    {
        new StationKind("Signal Tower",  25_000, 0, "—",   1_000),
        new StationKind("Depot",         50_000, 1, "3×3", 2_000),
        new StationKind("Station",      100_000, 2, "5×5", 3_000),
        new StationKind("Terminal",     200_000, 3, "7×7", 4_000),
    };

    public static readonly IReadOnlyList<Improvement> Improvements = new[]
    {
        new Improvement("Maintenance Shop",  25_000, "−75% maintenance for that fiscal period."),
        new Improvement("Engine Shop",      100_000, "Required to build or upgrade trains (F7)."),
        new Improvement("Switching Yard",    50_000, "Lets a train pick up cars from adjacent tracks."),
        new Improvement("Cold Storage",      25_000, "Preserves food / perishables held at the station."),
        new Improvement("Goods/Arms Storage",25_000, "Preserves manufactured goods held at the station."),
        new Improvement("Livestock Pens",    25_000, "Holds livestock without spoilage."),
        new Improvement("Post Office",       50_000, "Raises mail revenue."),
        new Improvement("Restaurant",        25_000, "Raises passenger revenue."),
        new Improvement("Hotel",            100_000, "Raises passenger revenue further."),
    };

    // --- scenarios ------------------------------------------------------------------------------
    public static readonly IReadOnlyList<Scenario> Scenarios = new[]
    {
        new Scenario("Eastern (Northeast) US", 1830, "$", "Dense cities; the classic starter map."),
        new Scenario("Western US",             1866, "$", "Long east-west routes pay best; cheaper $1,000k bonds (subsidies)."),
        new Scenario("Great Britain",          1828, "£", "Compact, high demand; £ currency."),
        new Scenario("Continental Europe",     1900, "£", "Map is 2× scale (distances doubled); later, faster engines."),
    };

    // --- difficulty -----------------------------------------------------------------------------
    public static readonly IReadOnlyList<Difficulty> Difficulties = new[]
    {
        new Difficulty("Investor",  40, "10% +5%/reality level"),
        new Difficulty("Financier", 60, "30% +5%/reality level"),
        new Difficulty("Mogul",     80, "50% +10%/reality level"),
        new Difficulty("Tycoon",   100, "70% +10%/reality level"),
    };

    /// <summary>The three independent reality switches (easy ↔ hard), each adds to the difficulty factor.</summary>
    public static readonly IReadOnlyList<string> RealitySwitches = new[]
    {
        "No-Collision Operation ↔ Dispatcher Operation",
        "Basic Economy ↔ Complex Economy",
        "Friendly Competition ↔ Cut-throat Competition",
    };

    // --- retirement outcome ---------------------------------------------------------------------
    /// <summary>
    /// The best and worst ends of the post-retirement job-title ladder (the manual names these two
    /// endpoints explicitly). The exact title awarded is computed from final net worth, years served,
    /// difficulty factor, number of railroads controlled, and whether you were thrown out of office —
    /// a higher net worth and difficulty factor move you up the ladder toward the top title.
    /// </summary>
    public const string WorstRetirementTitle = "Hobo";
    public const string BestRetirementTitle = "President of the United States";
}
