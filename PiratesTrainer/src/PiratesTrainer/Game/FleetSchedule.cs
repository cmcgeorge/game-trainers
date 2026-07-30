namespace PiratesTrainer.Game;

/// <summary>One scheduled arrival: the convoy reaches <paramref name="City"/> in this half-month.</summary>
/// <param name="City">Settlement name as the game's table spells it.</param>
/// <param name="Month">Three-letter month, matching the game's own JAN..DEC table.</param>
/// <param name="Half">"early" (first half of the month) or "late" (second half).</param>
/// <param name="Slot">Index 0-15 into the era's sixteen half-month route table.</param>
public sealed record FleetStop(string City, string Month, string Half, int Slot)
{
    /// <summary>"Havana - Mar - early", the shape the 1987 manual's chart uses.</summary>
    public string Display => $"{City} - {Month} - {Half}";
}

/// <summary>
/// The Treasure Fleet and Silver Train itineraries — the same table the game sails the convoys along and
/// the answer key to the 1987 manual's date-lookup copy protection.
///
/// Decoded from <c>DISK1</c>: each era block ends with two sixteen-byte rows of city indices, one entry
/// per half-month (at file offset 0x54000 + 0x400 x era + 0x3E0 and + 0x3F0; the running game holds them
/// at <c>DGROUP:0x4620</c> and <c>DGROUP:0x4630</c>). A byte outside the era's settlement table is the
/// sentinel that ends the run.
///
/// Both the route sequences and their calendar phase come out of the binary. The game indexes each row
/// as <c>slot = dayOfYear / 15 − bias + 2 * (eraCode &amp; 1)</c>, with a bias of 18 for the Treasure
/// Fleet and 6 for the Silver Train — which puts slot 0 in the first half of October and of April
/// respectively, and shifts both a month earlier in the odd-coded eras (1620 and 1660). Reconstructed
/// that way, eleven of the twelve itineraries reproduce the shipped 1987 answer key entry for entry; see
/// <c>docs/Pirates-ReverseEngineering.md</c> for the single disagreement.
/// </summary>
public static class FleetSchedule
{
    /// <summary>Treasure Fleet itinerary for the 1560 era (slot 0 = Oct, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Fleet1560 = new[]
    {
        new FleetStop("CUMANA",        "Oct", "early",  0),
        new FleetStop("PR.CABELLO",    "Oct", "late",   1),
        new FleetStop("MARACAIBO",     "Nov", "early",  2),
        new FleetStop("RIO DE HACHA",  "Nov", "late",   3),
        new FleetStop("NOMBRE DIOS",   "Dec", "early",  4),
        new FleetStop("CARTAGENA",     "Dec", "late",   5),
        new FleetStop("CAMPECHE",      "Jan", "late",   7),
        new FleetStop("VERA CRUZ",     "Feb", "early",  8),
        new FleetStop("HAVANA",        "Mar", "early", 10),
        new FleetStop("SANTIAGO",      "Mar", "late",  11),
        new FleetStop("FLORIDA CHNL",  "Apr", "late",  13),
        new FleetStop("FLORIDA CHNL",  "May", "early", 14),
    };

    /// <summary>Silver Train itinerary for the 1560 era (slot 0 = Apr, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Train1560 = new[]
    {
        new FleetStop("CUMANA",        "Apr", "early",  0),
        new FleetStop("BORBURATA",     "Apr", "late",   1),
        new FleetStop("PR.CABELLO",    "May", "early",  2),
        new FleetStop("CORO",          "May", "late",   3),
        new FleetStop("GIBRALTAR",     "Jun", "early",  4),
        new FleetStop("MARACAIBO",     "Jun", "late",   5),
        new FleetStop("RIO DE HACHA",  "Jul", "early",  6),
        new FleetStop("SANTA MARTA",   "Jul", "late",   7),
        new FleetStop("CARTAGENA",     "Aug", "early",  8),
        new FleetStop("PANAMA",        "Aug", "late",   9),
        new FleetStop("NOMBRE DIOS",   "Oct", "early", 12),
    };

    /// <summary>Treasure Fleet itinerary for the 1600 era (slot 0 = Oct, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Fleet1600 = new[]
    {
        new FleetStop("CUMANA",        "Oct", "early",  0),
        new FleetStop("CARACAS",       "Oct", "late",   1),
        new FleetStop("MARACAIBO",     "Nov", "early",  2),
        new FleetStop("RIO DE HACHA",  "Nov", "late",   3),
        new FleetStop("SANTA MARTA",   "Dec", "early",  4),
        new FleetStop("PUERTO BELLO",  "Dec", "late",   5),
        new FleetStop("CARTAGENA",     "Jan", "early",  6),
        new FleetStop("CAMPECHE",      "Feb", "early",  8),
        new FleetStop("VERA CRUZ",     "Feb", "late",   9),
        new FleetStop("HAVANA",        "Mar", "late",  11),
        new FleetStop("FLORIDA CHNL",  "Apr", "late",  13),
        new FleetStop("FLORIDA CHNL",  "May", "early", 14),
    };

    /// <summary>Silver Train itinerary for the 1600 era (slot 0 = Apr, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Train1600 = new[]
    {
        new FleetStop("ST.THOME",      "Apr", "early",  0),
        new FleetStop("CUMANA",        "Apr", "late",   1),
        new FleetStop("CARACAS",       "May", "early",  2),
        new FleetStop("PR.CABELLO",    "May", "late",   3),
        new FleetStop("CORO",          "Jun", "early",  4),
        new FleetStop("GIBRALTAR",     "Jun", "late",   5),
        new FleetStop("MARACAIBO",     "Jul", "early",  6),
        new FleetStop("RIO DE HACHA",  "Jul", "late",   7),
        new FleetStop("SANTA MARTA",   "Aug", "early",  8),
        new FleetStop("CARTAGENA",     "Aug", "late",   9),
        new FleetStop("PANAMA",        "Sep", "early", 10),
        new FleetStop("PUERTO BELLO",  "Oct", "late",  13),
    };

    /// <summary>Treasure Fleet itinerary for the 1620 era (slot 0 = Sep, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Fleet1620 = new[]
    {
        new FleetStop("CARACAS",       "Sep", "early",  0),
        new FleetStop("MARACAIBO",     "Sep", "late",   1),
        new FleetStop("RIO DE HACHA",  "Oct", "early",  2),
        new FleetStop("SANTA MARTA",   "Oct", "late",   3),
        new FleetStop("PUERTO BELLO",  "Nov", "early",  4),
        new FleetStop("CARTAGENA",     "Dec", "early",  6),
        new FleetStop("CAMPECHE",      "Jan", "early",  8),
        new FleetStop("VERA CRUZ",     "Jan", "late",   9),
        new FleetStop("HAVANA",        "Feb", "late",  11),
        new FleetStop("FLORIDA CHNL",  "Mar", "late",  13),
        new FleetStop("FLORIDA CHNL",  "Apr", "early", 14),
    };

    /// <summary>Silver Train itinerary for the 1620 era (slot 0 = Mar, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Train1620 = new[]
    {
        new FleetStop("ST.THOME",      "Mar", "early",  0),
        new FleetStop("CUMANA",        "Mar", "late",   1),
        new FleetStop("CARACAS",       "Apr", "early",  2),
        new FleetStop("PR.CABELLO",    "Apr", "late",   3),
        new FleetStop("GIBRALTAR",     "May", "early",  4),
        new FleetStop("MARACAIBO",     "May", "late",   5),
        new FleetStop("RIO DE HACHA",  "Jun", "early",  6),
        new FleetStop("SANTA MARTA",   "Jun", "late",   7),
        new FleetStop("CARTAGENA",     "Jul", "early",  8),
        new FleetStop("PANAMA",        "Jul", "late",   9),
        new FleetStop("PUERTO BELLO",  "Sep", "early", 12),
    };

    /// <summary>Treasure Fleet itinerary for the 1640 era (slot 0 = Oct, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Fleet1640 = new[]
    {
        new FleetStop("CARACAS",       "Oct", "early",  0),
        new FleetStop("MARACAIBO",     "Oct", "late",   1),
        new FleetStop("RIO DE HACHA",  "Nov", "early",  2),
        new FleetStop("SANTA MARTA",   "Nov", "late",   3),
        new FleetStop("PUERTO BELLO",  "Dec", "early",  4),
        new FleetStop("CARTAGENA",     "Jan", "early",  6),
        new FleetStop("CAMPECHE",      "Feb", "early",  8),
        new FleetStop("VERA CRUZ",     "Feb", "late",   9),
        new FleetStop("HAVANA",        "Mar", "late",  11),
        new FleetStop("FLORIDA CHNL",  "Apr", "late",  13),
        new FleetStop("FLORIDA CHNL",  "May", "early", 14),
    };

    /// <summary>Silver Train itinerary for the 1640 era (slot 0 = Apr, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Train1640 = new[]
    {
        new FleetStop("CUMANA",        "Apr", "early",  0),
        new FleetStop("CARACAS",       "Apr", "late",   1),
        new FleetStop("GIBRALTAR",     "May", "early",  2),
        new FleetStop("MARACAIBO",     "May", "late",   3),
        new FleetStop("RIO DE HACHA",  "Jun", "early",  4),
        new FleetStop("SANTA MARTA",   "Jul", "early",  6),
        new FleetStop("CARTAGENA",     "Jul", "late",   7),
        new FleetStop("PANAMA",        "Aug", "late",   9),
        new FleetStop("PUERTO BELLO",  "Oct", "early", 12),
        new FleetStop("BARBADOS",      "Nov", "late",  15),
    };

    /// <summary>Treasure Fleet itinerary for the 1660 era (slot 0 = Sep, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Fleet1660 = new[]
    {
        new FleetStop("CARACAS",       "Sep", "early",  0),
        new FleetStop("MARACAIBO",     "Sep", "late",   1),
        new FleetStop("RIO DE HACHA",  "Oct", "early",  2),
        new FleetStop("SANTA MARTA",   "Oct", "late",   3),
        new FleetStop("PUERTO BELLO",  "Nov", "early",  4),
        new FleetStop("CARTAGENA",     "Dec", "early",  6),
        new FleetStop("CAMPECHE",      "Jan", "early",  8),
        new FleetStop("VERA CRUZ",     "Jan", "late",   9),
        new FleetStop("HAVANA",        "Feb", "late",  11),
        new FleetStop("FLORIDA CHNL",  "Mar", "late",  13),
        new FleetStop("FLORIDA CHNL",  "Apr", "early", 14),
    };

    /// <summary>Silver Train itinerary for the 1660 era (slot 0 = Mar, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Train1660 = new[]
    {
        new FleetStop("CUMANA",        "Mar", "early",  0),
        new FleetStop("CARACAS",       "Mar", "late",   1),
        new FleetStop("GIBRALTAR",     "Apr", "early",  2),
        new FleetStop("MARACAIBO",     "Apr", "late",   3),
        new FleetStop("RIO DE HACHA",  "May", "early",  4),
        new FleetStop("SANTA MARTA",   "Jun", "early",  6),
        new FleetStop("CARTAGENA",     "Jun", "late",   7),
        new FleetStop("PANAMA",        "Jul", "late",   9),
        new FleetStop("PUERTO BELLO",  "Sep", "early", 12),
        new FleetStop("BARBADOS",      "Oct", "late",  15),
    };

    /// <summary>Treasure Fleet itinerary for the 1680 era (slot 0 = Oct, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Fleet1680 = new[]
    {
        new FleetStop("CARACAS",       "Oct", "early",  0),
        new FleetStop("RIO DE HACHA",  "Oct", "late",   1),
        new FleetStop("SANTA MARTA",   "Nov", "early",  2),
        new FleetStop("PUERTO BELLO",  "Nov", "late",   3),
        new FleetStop("CARTAGENA",     "Dec", "late",   5),
        new FleetStop("CAMPECHE",      "Jan", "late",   7),
        new FleetStop("VERA CRUZ",     "Feb", "early",  8),
        new FleetStop("HAVANA",        "Mar", "early", 10),
        new FleetStop("FLORIDA CHNL",  "Apr", "late",  13),
        new FleetStop("FLORIDA CHNL",  "May", "early", 14),
    };

    /// <summary>Silver Train itinerary for the 1680 era (slot 0 = Apr, first half).</summary>
    public static readonly IReadOnlyList<FleetStop> Train1680 = new[]
    {
        new FleetStop("CUMANA",        "Apr", "early",  0),
        new FleetStop("CARACAS",       "Apr", "late",   1),
        new FleetStop("MARACAIBO",     "May", "late",   3),
        new FleetStop("RIO DE HACHA",  "Jun", "late",   5),
        new FleetStop("SANTA MARTA",   "Jul", "early",  6),
        new FleetStop("CARTAGENA",     "Jul", "late",   7),
        new FleetStop("PANAMA",        "Aug", "late",   9),
        new FleetStop("PUERTO BELLO",  "Oct", "early", 12),
        new FleetStop("BARBADOS",      "Nov", "late",  15),
    };

    /// <summary>Treasure Fleet itineraries, indexed by era 0-5.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<FleetStop>> TreasureFleetByEra = new[]
    { Fleet1560, Fleet1600, Fleet1620, Fleet1640, Fleet1660, Fleet1680 };

    /// <summary>Silver Train itineraries, indexed by era 0-5.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<FleetStop>> SilverTrainByEra = new[]
    { Train1560, Train1600, Train1620, Train1640, Train1660, Train1680 };

    /// <summary>Every stop of both convoys in every era, flattened for a single searchable grid.</summary>
    public static readonly IReadOnlyList<ScheduleRow> All = Build();

    private static ScheduleRow[] Build()
    {
        var rows = new List<ScheduleRow>();
        for (int era = 0; era < CityBook.EraYears.Count; era++)
        {
            foreach (var s in TreasureFleetByEra[era])
                rows.Add(new ScheduleRow(CityBook.EraYears[era], "Treasure Fleet", s));
            foreach (var s in SilverTrainByEra[era])
                rows.Add(new ScheduleRow(CityBook.EraYears[era], "Silver Train", s));
        }
        return rows.ToArray();
    }
}

/// <summary>A convoy stop tagged with its era and which convoy it belongs to, for a flat grid.</summary>
/// <param name="Year">Era start year (1560..1680).</param>
/// <param name="Convoy">"Treasure Fleet" or "Silver Train".</param>
/// <param name="Stop">The scheduled arrival.</param>
public sealed record ScheduleRow(int Year, string Convoy, FleetStop Stop)
{
    public string City => Stop.City;
    public string Month => Stop.Month;
    public string Half => Stop.Half;
    public int Slot => Stop.Slot;
}
