namespace BardsTale1Trainer.Game;

/// <summary>
/// One reference map: a Bard's Tale 1 area and its grid size in game cells. Unlike the MM1
/// trainer there are no bundled scans — the view model renders a labelled grid of
/// <see cref="Width"/> × <see cref="Height"/> cells instead, and the user's two calibration
/// anchors tie the grid to the game's coordinates. Maps are grouped in the UI by
/// <see cref="Category"/>.
/// </summary>
public sealed record GameMap(string Category, string Name, int Width, int Height, string Description);

/// <summary>
/// Every area of Bard's Tale 1: the city of Skara Brae (30×30 cells) and the sixteen
/// 22×22 dungeon levels. Purely reference data — independent of any attached game —
/// mirroring how <see cref="Spellbook"/> backs the spell reference.
/// </summary>
public static class MapBook
{
    private const int City = 30;     // Skara Brae is a 30×30 street grid
    private const int Dungeon = 22;  // every dungeon level is a 22×22 maze

    private static GameMap M(string cat, string name, int w, int h, string desc) => new(cat, name, w, h, desc);

    public static readonly IReadOnlyList<GameMap> Maps = new[]
    {
        M("City", "Skara Brae", City, City,
            "The snowbound city — 30×30 cells of streets, shops, temples, taverns and the entrances to every dungeon."),

        M("Wine Cellar & Sewers", "Wine Cellar", Dungeon, Dungeon,
            "Under the Scarlet Bard inn on Rakhir Street; the gateway down to the sewers."),
        M("Wine Cellar & Sewers", "Sewers — level 1", Dungeon, Dungeon,
            "First sewer level below the Wine Cellar."),
        M("Wine Cellar & Sewers", "Sewers — level 2", Dungeon, Dungeon,
            "Second sewer level."),
        M("Wine Cellar & Sewers", "Sewers — level 3", Dungeon, Dungeon,
            "Deepest sewer level."),

        M("Catacombs", "Catacombs — level 1", Dungeon, Dungeon,
            "Beneath the temple of the Mad God; speak the dead god's name to descend."),
        M("Catacombs", "Catacombs — level 2", Dungeon, Dungeon,
            "Second catacomb level."),
        M("Catacombs", "Catacombs — level 3", Dungeon, Dungeon,
            "Deepest catacomb level."),

        M("Harkyn's Castle", "Harkyn's Castle — level 1", Dungeon, Dungeon,
            "The castle in the north of the city."),
        M("Harkyn's Castle", "Harkyn's Castle — level 2", Dungeon, Dungeon,
            "Second castle level."),
        M("Harkyn's Castle", "Harkyn's Castle — level 3", Dungeon, Dungeon,
            "Top castle level."),

        M("Kylearan's Tower", "Kylearan's Tower", Dungeon, Dungeon,
            "The Mad One's tower — a single maze level."),

        M("Mangar's Tower", "Mangar's Tower — level 1", Dungeon, Dungeon,
            "The dark wizard's tower; five levels to the top."),
        M("Mangar's Tower", "Mangar's Tower — level 2", Dungeon, Dungeon,
            "Second tower level."),
        M("Mangar's Tower", "Mangar's Tower — level 3", Dungeon, Dungeon,
            "Third tower level."),
        M("Mangar's Tower", "Mangar's Tower — level 4", Dungeon, Dungeon,
            "Fourth tower level."),
        M("Mangar's Tower", "Mangar's Tower — level 5", Dungeon, Dungeon,
            "The top of Mangar's Tower."),
    };
}
