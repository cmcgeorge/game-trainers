namespace HillsfarTrainer.Game;

/// <summary>A city location and the hours it is open.</summary>
/// <param name="Name">The name exactly as the game's own table spells it.</param>
/// <param name="OpenHour">First hour it is open, 1..24, or null when it never opens.</param>
/// <param name="CloseHour">Last hour it is open, 1..24, or null when it never opens.</param>
/// <param name="AlwaysOpen">True when the location has no closing time.</param>
/// <param name="Note">Why you go there.</param>
public readonly record struct LocationInfo(
    string Name, int? OpenHour, int? CloseHour, bool AlwaysOpen, string Note)
{
    /// <summary>True when the location never opens and has to be broken into.</summary>
    public bool NeverOpen => !AlwaysOpen && OpenHour is null;

    /// <summary>The hours as the manual prints them.</summary>
    public string Hours =>
        AlwaysOpen ? "Always open"
        : NeverOpen ? "Never open"
        : $"{GameFacts.FormatHour(OpenHour!.Value)} – {GameFacts.FormatHour(CloseHour!.Value)}";

    /// <summary>
    /// True when the location is open at <paramref name="hour"/> (1..24). Ranges that wrap past
    /// midnight — the pubs at 5 pm to 7 am, the cemetery at midnight to 7 am — are handled.
    /// </summary>
    public bool IsOpenAt(int hour)
    {
        if (AlwaysOpen) return true;
        if (NeverOpen) return false;
        int open = OpenHour!.Value, close = CloseHour!.Value;
        return open <= close
            ? hour >= open && hour <= close
            : hour >= open || hour <= close;   // wraps past midnight
    }
}

/// <summary>
/// An overland destination and how it is reached.
///
/// <para>This is a record rather than a tuple deliberately: a <c>ValueTuple</c> exposes its members
/// as <b>fields</b>, and WPF's binding engine resolves paths through property descriptors only — so
/// a <c>DataGrid</c> bound to a tuple list renders every cell blank, with nothing but a binding
/// error in the debug output to say why. A positional record generates real properties.</para>
/// </summary>
/// <param name="Name">The destination as the strategy guide names it.</param>
/// <param name="ReachedFrom">
/// The road or trail that leads there. Begins with <see cref="LocationBook.HiddenPrefix"/> when the
/// destination is only reachable by an unmarked trail.
/// </param>
/// <param name="Why">What the missions send you there for.</param>
public readonly record struct OverlandInfo(string Name, string ReachedFrom, string Why)
{
    /// <summary>True when this destination is only reachable by an unmarked trail.</summary>
    public bool IsHidden =>
        ReachedFrom.StartsWith(LocationBook.HiddenPrefix, StringComparison.Ordinal);
}

/// <summary>
/// The eighteen city locations. The names are the game's own — read out of its internal table at
/// <c>DGROUP:0x3D1D</c>, misspelling of "Cemetary" included — and the hours come from the manual,
/// whose list is exactly these eighteen entries.
///
/// <para>Hours are stored on the game's 1..24 clock so they can be compared directly against the
/// hour byte in the character record. Midnight is hour 24.</para>
/// </summary>
public static class LocationBook
{
    private const int Midnight = CharacterFormat.HoursPerDay;

    /// <summary>Every location, with its opening hours and what it is for.</summary>
    public static readonly IReadOnlyList<LocationInfo> Locations = new[]
    {
        new LocationInfo("Arena", 8, 23, false, "Fight for gold, fame, or as a sentence"),
        new LocationInfo("Archery", 8, 15, false, "Tanna's Target Range — levels gate five mission steps"),
        new LocationInfo("Bank", 8, 15, false, "Deposit gold so pubs and mazes cannot cost you it"),
        new LocationInfo("Book store", 8, 15, false, "Read a book; talk to the owner"),
        new LocationInfo("Castle", null, null, false, "Maalthiir's castle — break in; capture here always means the Arena"),
        new LocationInfo("Cemetary", Midnight, 7, false, "Chests; open only in the small hours"),
        new LocationInfo("Temple of Tempus", null, null, true, "The cleric's guild and mission hub"),
        new LocationInfo("Stable", null, null, true, "Where you arrive and the only way out of the city"),
        new LocationInfo("Fighter's Guild", null, null, true, "The fighter's mission hub"),
        new LocationInfo("Haunted Mansion", null, null, false, "Break in; has a secret room"),
        new LocationInfo("Healer", 8, 15, false, "Healing potions, or a 500-gold cure critical wounds"),
        new LocationInfo("Jail", null, null, false, "Break in — pick the first tumblers, force the last with F"),
        new LocationInfo("Mage's Guild", null, null, true, "The magic-user's mission hub"),
        new LocationInfo("Magic shop", 8, 15, false, "Buy and sell knock rings; two missions need it after hours"),
        new LocationInfo("Mages Tower", 8, 15, false, "Maze with a secret room in the top-left"),
        new LocationInfo("Pub", 17, 7, false, "Gossip — the main way the plot advances"),
        new LocationInfo("Sewer", null, null, true, "Maze with chests; several mission objects"),
        new LocationInfo("Rogue's Guild", null, null, true, "The thief's mission hub"),
    };

    /// <summary>
    /// The four named pubs. Missions often name one specifically; when a step says "any pub", any of
    /// these will do.
    /// </summary>
    public static readonly IReadOnlyList<string> Pubs = new[]
    {
        "Dragon's Lair", "Rat's Nest", "Hydra's Den", "Bugbear's Cave",
    };

    /// <summary>
    /// Overland destinations, and how each is reached. The three marked as hidden are only reachable
    /// by riding the parent location until a <c>?</c> appears and pressing Space.
    /// </summary>
    public static readonly IReadOnlyList<OverlandInfo> Overland = new[]
    {
        new OverlandInfo("Camp", "—", "Save, rest, view the character sheet"),
        new OverlandInfo("Hillsfar", "Main road", "The city — guilds, pubs, shops, arena, mazes"),
        new OverlandInfo("Trading Post", "Road", "The Trader tracks people's movements"),
        new OverlandInfo("Big Tree", "Road", "Maze with chests; a body in one of them"),
        new OverlandInfo("Hermit's House", "Road", "Holy Scriptures, a Poster, a White Liquid, his Diary"),
        new OverlandInfo("Rock Quarry", "HIDDEN — secret path from the Hermit's House",
                         "A dead woman, a Bonnet, a Rusty Old Pick"),
        new OverlandInfo("Hut", "Road", "An Old Man with a clue"),
        new OverlandInfo("Old Ruins", "Road", "A Gold Pendant, a bottle of Incense, Ariana"),
        new OverlandInfo("Wizard's Lair", "HIDDEN — hidden path from the Ruins", "Mage mission three chests"),
        new OverlandInfo("Shipwreck", "Coast road", "Mage missions one and three"),
        new OverlandInfo("Dead Dragon", "HIDDEN — hidden trail from the Shipwreck",
                         "The Squid's remains, a strange Pick"),
    };

    /// <summary>Prefix marking a destination that is only reachable by an unmarked trail.</summary>
    public const string HiddenPrefix = "HIDDEN";

    /// <summary>Every location that is open at <paramref name="hour"/> (1..24).</summary>
    public static IEnumerable<LocationInfo> OpenAt(int hour)
    {
        foreach (var l in Locations) if (l.IsOpenAt(hour)) yield return l;
    }
}
