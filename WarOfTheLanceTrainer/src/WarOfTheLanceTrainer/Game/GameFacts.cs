namespace WarOfTheLanceTrainer.Game;

/// <summary>
/// The reverse-engineered, verified-against-the-shipped-files knowledge layer for War of the Lance
/// (SSI, 1989). Everything here was recovered by static analysis of the game's data files in
/// <c>.game/</c> — the byte-exact string tables the engine loads verbatim (NAT.DAT, WL2.DAT,
/// MENU.DAT) and the unit tables (WL.UNT/SCEN.UNT, WL.DAT/SCEN.DAT). These string tables are the
/// stable anchors the live-memory locator scans for; the engine copies them into guest RAM unchanged.
/// </summary>
public static class GameFacts
{
    /// <summary>Full campaign length: 5 turns/year (seasons) × 6 game years.</summary>
    public const int TurnsPerYear = 5;
    public const int GameYears = 6;
    public const int TotalTurns = TurnsPerYear * GameYears;   // 30

    /// <summary>Highest strength value the engine's base numbers reach (Griffon = 240).</summary>
    public const byte MaxStrength = 240;

    /// <summary>0xFF in a WL.UNT slot's X/Y means the slot is empty.</summary>
    public const byte EmptySlot = 0xFF;

    /// <summary>
    /// The 28 nation / place names exactly as they appear (in order) in NAT.DAT's payload, decoded
    /// from the high-bit ASCII encoding. Index order is the engine's nation ordinal. The trailing
    /// three ("CLERIST TOWER", "SOTH", "-") are the special map objectives / placeholder.
    /// </summary>
    public static readonly string[] NationNames =
    {
        "BLODE", "CAERGOTH", "GOODLUND", "GUNTHAR", "HYLO", "KAOLYN", "KERN", "KHUR",
        "KOTHAS", "LEMISH", "MAELSTROM", "MITHAS", "NERAKA", "NORDMAAR", "N. ERGOTH",
        "PALANTHUS", "QUALINESTI", "SANCTION", "SILVANESTI", "SOLANTHUS", "TARSIS",
        "THORBARDIN", "THROTYL", "VINGAARD", "ZHAKAR", "CLERIST TOWER", "SOTH", "-",
    };

    /// <summary>The three allegiances, in WL2.DAT order (Highlord = evil, Whitestone = good).</summary>
    public static readonly string[] SideNames = { "HIGHLORD", "WHITESTONE", "NEUTRAL" };

    /// <summary>Two-letter side codes, in WL2.DAT order.</summary>
    public static readonly string[] SideCodes = { "HL", "WS", "NE" };

    /// <summary>
    /// Unit-type labels, in WL2.DAT order. Index is the engine's unit-type ordinal used across the
    /// data tables. INF/CAV/FLEET are the common land/sea types; DRAGON/CITADEL/LEADER/WIZARD/
    /// DIPLOMAT/HERO are the specials.
    /// </summary>
    public static readonly string[] UnitTypeNames =
    {
        "INF", "CAV", "FLEET", "PEGASUS", "GRIFFON", "DRAGON", "CITADEL",
        "LEADER", "DRACONIAN", "WIZARD", "DIPLOMAT", "HERO",
    };

    /// <summary>
    /// The 21 terrain names in WL2.DAT order (index = terrain code used by the .MAP tiles).
    /// </summary>
    public static readonly string[] TerrainNames =
    {
        "GRASSLAND", "STEPPE", "FOREST", "MOUNTAIN", "MTN. PASS", "TUNNEL", "TUNNEL ENTR.",
        "RIVER", "STREAM", "PORT CITY", "COAST", "SEA", "MAELSTROM", "GLACIER", "TOWER",
        "DWARVEN FORT", "FORTRESS", "BRIDGE", "FORT. CITY", "MARSH", "DESERT",
    };

    /// <summary>
    /// The five season labels per game year, in MENU.DAT order. The fifth ("WINTER") is the
    /// recovery/replacement turn, confirming the 5-turns-per-year cadence.
    /// </summary>
    public static readonly string[] SeasonNames = { "MAR/APR", "MAY/JUN", "JUL/AUG", "SEP/OCT", "WINTER" };
}
