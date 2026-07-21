namespace ColonizationTrainer.Game;

/// <summary>A terrain type: its name, base food yield, and its notable non-food yields.</summary>
public sealed record Terrain(string Name, string Food, string Yields);

/// <summary>
/// The terrain types and their base yields (before roads, rivers, plowing, specialists, or
/// Sons-of-Liberty bonuses). Names follow the pedia (<c>.games/PEDIA.TXT @TERRAIN0…</c>); yield
/// numbers are the verified StrategyWiki base table — see <c>docs/Colonization-Strategy-Guide.md §5</c>.
/// </summary>
public static class TerrainBook
{
    public static readonly IReadOnlyList<Terrain> Terrains = new[]
    {
        new Terrain("Plains",          "5", "2 cotton, 1 ore"),
        new Terrain("Grassland",       "3", "3 tobacco"),
        new Terrain("Prairie",         "3", "3 cotton"),
        new Terrain("Savannah",        "4", "3 sugar"),
        new Terrain("Marsh",           "3", "2 tobacco, 2 ore"),
        new Terrain("Swamp",           "3", "2 sugar, 2 ore"),
        new Terrain("Tundra",          "3", "2 ore"),
        new Terrain("Desert",          "2", "1 cotton, 2 ore"),
        new Terrain("Hills",           "2", "4 ore (+2 with an ore deposit)"),
        new Terrain("Mountains",       "—", "4 ore, 1 silver"),
        new Terrain("Ocean",           "—", "4 fish (needs Docks)"),
        new Terrain("Sea Lane",        "—", "4 fish; the route to Europe"),
        new Terrain("Arctic",          "—", "nothing"),
        new Terrain("Boreal Forest",   "2", "3 furs, 4 lumber, 1 ore"),
        new Terrain("Scrub Forest",    "2", "1 cotton, 2 furs, 2 lumber, 1 ore"),
        new Terrain("Mixed Forest",    "3", "1 cotton, 3 furs, 6 lumber"),
        new Terrain("Broadleaf Forest","2", "1 cotton, 2 furs, 4 lumber"),
        new Terrain("Conifer Forest",  "2", "1 tobacco, 2 furs, 6 lumber"),
        new Terrain("Tropical Forest", "3", "1 sugar, 2 furs, 4 lumber"),
        new Terrain("Wetland Forest",  "2", "1 tobacco, 2 furs, 4 lumber, 1 ore"),
        new Terrain("Rain Forest",     "2", "1 sugar, 1 furs, 4 lumber, 1 ore"),
    };
}
