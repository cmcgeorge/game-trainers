namespace AlternateRealityTrainer.Game;

/// <summary>What kind of building sits on a square.</summary>
public enum PlaceKind
{
    Inn,
    Tavern,
    Bank,
    Shop,
    Smithy,
    Healer,
    Guild,
}

/// <summary>
/// A location in The City.
/// </summary>
/// <param name="Kind">Building type.</param>
/// <param name="North">Squares north; 1 is the southern edge.</param>
/// <param name="East">Squares east; 1 is the western edge.</param>
/// <param name="Note">Prices, opening hours, or the approach you have to take to get in.</param>
public readonly record struct Place(PlaceKind Kind, int North, int East, string Note)
{
    /// <summary>Single-character glyph drawn inside the marker (also a colour-blind fallback).</summary>
    public char Symbol => Kind switch
    {
        PlaceKind.Inn => 'I',
        PlaceKind.Tavern => 'T',
        PlaceKind.Bank => 'B',
        PlaceKind.Shop => 'S',
        PlaceKind.Smithy => 'M',
        PlaceKind.Healer => 'H',
        _ => 'G',
    };

    public string Coordinate => $"{North}N, {East}E";
}

/// <summary>
/// Every known location in The City, with the coordinates from the <c>alternate.txt</c> hint file
/// shipped with the game, cross-checked against the published cluebook.
///
/// Coordinates are <c>&lt;north&gt;, &lt;east&gt;</c>, counting square 1N 1E as the south-west
/// corner — the convention both sources use.
/// </summary>
public static class CityBook
{
    public static readonly IReadOnlyList<Place> Places = new[]
    {
        // --- inns ------------------------------------------------------------
        new Place(PlaceKind.Inn, 26, 32, "Prices high (same inn as 25N 33E)"),
        new Place(PlaceKind.Inn, 25, 33, "Prices high (same inn as 26N 32E)"),
        new Place(PlaceKind.Inn, 24, 33, "Reasonable"),
        new Place(PlaceKind.Inn, 20, 10, "Reasonable"),
        new Place(PlaceKind.Inn,  4, 32, "Very expensive"),
        new Place(PlaceKind.Inn,  7, 61, "Cheap"),
        new Place(PlaceKind.Inn, 53, 34, "Reasonable"),
        new Place(PlaceKind.Inn, 55, 29, "Cheap"),

        // --- taverns ---------------------------------------------------------
        new Place(PlaceKind.Tavern, 30, 40, "Expensive"),
        new Place(PlaceKind.Tavern, 20, 33, "Reasonable, limited hours"),
        new Place(PlaceKind.Tavern, 25,  8, "Reasonable, limited hours, enter from the south"),
        new Place(PlaceKind.Tavern, 13, 14, "Reasonable, special song at midnight"),
        new Place(PlaceKind.Tavern, 10, 45, "Reasonable"),
        new Place(PlaceKind.Tavern,  3, 61, "Cheap"),
        new Place(PlaceKind.Tavern, 31, 61, "Reasonable — enter from the east: 32,59 → 32,60 → south to 31,60"),
        new Place(PlaceKind.Tavern, 34, 58, "Dues to join, expensive, enter from the north"),
        new Place(PlaceKind.Tavern, 36,  6, "Reasonable"),
        new Place(PlaceKind.Tavern, 36,  7, "Reasonable"),
        new Place(PlaceKind.Tavern, 55,  2, "Dues to join, limited hours"),
        new Place(PlaceKind.Tavern, 63, 21, "Cheapest, free water — north at 63,2, east to 64,21, then south"),
        new Place(PlaceKind.Tavern, 54, 34, "Dues to join, limited hours"),
        new Place(PlaceKind.Tavern, 57, 53, "Reasonable, enter from the south or the west"),

        // --- banks -----------------------------------------------------------
        new Place(PlaceKind.Bank, 28, 39, "Low interest, safe"),
        new Place(PlaceKind.Bank,  7, 31, "Higher interest, more likely to fail"),
        new Place(PlaceKind.Bank, 62,  3, "Highest interest, most risky — enter from the south at 61,2"),

        // --- shops -----------------------------------------------------------
        new Place(PlaceKind.Shop, 25, 36, ""),
        new Place(PlaceKind.Shop, 31, 36, ""),
        new Place(PlaceKind.Shop, 14,  1, "Enter going west from 15,6"),
        new Place(PlaceKind.Shop, 13,  4, "Enter going west from 15,6"),
        new Place(PlaceKind.Shop,  6, 20, ""),
        new Place(PlaceKind.Shop, 16, 26, ""),
        new Place(PlaceKind.Shop,  9, 52, ""),
        new Place(PlaceKind.Shop, 10, 53, ""),
        new Place(PlaceKind.Shop, 19, 56, ""),
        new Place(PlaceKind.Shop, 37, 47, ""),
        new Place(PlaceKind.Shop, 56, 34, ""),
        new Place(PlaceKind.Shop, 57, 38, "Enter from the north"),
        new Place(PlaceKind.Shop, 62, 61, ""),
        new Place(PlaceKind.Shop, 60, 27, ""),
        new Place(PlaceKind.Shop, 44, 21, ""),
        new Place(PlaceKind.Shop, 44, 22, ""),
        new Place(PlaceKind.Shop, 38, 10, ""),

        // --- smithies --------------------------------------------------------
        new Place(PlaceKind.Smithy, 28, 33, ""),
        new Place(PlaceKind.Smithy, 10, 55, ""),
        new Place(PlaceKind.Smithy, 35, 51, ""),
        new Place(PlaceKind.Smithy, 33, 20, "Enter from the north"),

        // --- healers ---------------------------------------------------------
        new Place(PlaceKind.Healer, 20, 5, "Open mostly on odd hours"),
        new Place(PlaceKind.Healer, 30, 30, "Open mostly on odd hours"),

        // --- guilds (first visit raises the listed stat, free) ----------------
        new Place(PlaceKind.Guild,  5,  3, "Light Wizards — Wisdom, enter from the west"),
        new Place(PlaceKind.Guild, 15,  6, "Physicians — Hit Points, enter from the west"),
        new Place(PlaceKind.Guild, 43, 12, "Green Wizards Academy — Stamina, enter from the north"),
        new Place(PlaceKind.Guild, 12, 28, "Star Wizards — Hit Points and Strength"),
        new Place(PlaceKind.Guild, 22, 34, "Dark Wizards — Charm"),
        new Place(PlaceKind.Guild, 44, 35, "Thieves — Skill, enter from the west (the cluebook transposes this to 35N 44E)"),
        new Place(PlaceKind.Guild, 15, 48, "Red Wizards — Strength; north from 13,47, east to 14,48, then north"),
        new Place(PlaceKind.Guild, 48, 19, "Blue Wizards — Speed, enter from the west"),
        new Place(PlaceKind.Guild, 50, 58, "Guild of the Order — Intelligence"),
        new Place(PlaceKind.Guild, 50, 62, "Wizards of Law — Wisdom"),
        new Place(PlaceKind.Guild, 60, 51, "Wizards of Chaos — Charm, enter from the east"),
        new Place(PlaceKind.Guild,  3, 56, "Assassins — stealth; north from 2,57, then south from 4,56"),
    };

    /// <summary>True when no two locations share a square, so a drawn marker is never ambiguous.</summary>
    public static bool AllSquaresDistinct =>
        Places.Select(p => (p.North, p.East)).Distinct().Count() == Places.Count;
}
