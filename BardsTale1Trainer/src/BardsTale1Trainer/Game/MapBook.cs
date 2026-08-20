namespace BardsTale1Trainer.Game;

/// <summary>
/// One reference map: a Bard's Tale 1 area, its grid size in game cells, and the per-square
/// wall/barrier grid the view model draws. Unlike the MM1 trainer there are no bundled scans —
/// the view model renders <see cref="Terrain"/> as a labelled W×H maze instead, and the user's
/// two calibration anchors tie it to the game's coordinates. Maps are grouped in the UI by
/// <see cref="Category"/>.
/// </summary>
public sealed record GameMap(string Category, string Name, int Width, int Height, string Description,
    BoardSquare[,] Terrain);

/// <summary>
/// Every area of Bard's Tale 1: the city of Skara Brae (30×30 cells) and the sixteen
/// 22×22 dungeon levels, each with the walls, doors, secret doors and one-way passages that
/// <see cref="MapTerrainData"/> holds. Purely reference data — independent of any attached
/// game — mirroring how <see cref="Spellbook"/> backs the spell reference.
/// </summary>
public static class MapBook
{
    private const int City = 30;     // Skara Brae is a 30×30 street grid
    private const int Dungeon = 22;  // every dungeon level is a 22×22 maze

    private static GameMap D(string cat, string name, string[] terrain, string desc) =>
        new(cat, name, Dungeon, Dungeon, desc, MapAscii.Parse(terrain, Dungeon, Dungeon));

    public static readonly IReadOnlyList<GameMap> Maps = new[]
    {
        new GameMap("City", "Skara Brae", City, City,
            "The snowbound city — 30×30 cells of streets, shops, temples, taverns and the entrances to every dungeon.",
            MapAscii.ParseCity(MapTerrainData.SkaraBraeTerrain, City, City)),

        D("Wine Cellar & Sewers", "Wine Cellar", MapTerrainData.WineCellarTerrain,
            "Under the Scarlet Bard inn on Rakhir Street; the gateway down to the sewers."),
        D("Wine Cellar & Sewers", "Sewers — level 1", MapTerrainData.Sewers1Terrain,
            "First sewer level below the Wine Cellar."),
        D("Wine Cellar & Sewers", "Sewers — level 2", MapTerrainData.Sewers2Terrain,
            "Second sewer level."),
        D("Wine Cellar & Sewers", "Sewers — level 3", MapTerrainData.Sewers3Terrain,
            "Deepest sewer level."),

        D("Catacombs", "Catacombs — level 1", MapTerrainData.Catacombs1Terrain,
            "Beneath the temple of the Mad God; speak the dead god's name to descend."),
        D("Catacombs", "Catacombs — level 2", MapTerrainData.Catacombs2Terrain,
            "Second catacomb level."),
        D("Catacombs", "Catacombs — level 3", MapTerrainData.Catacombs3Terrain,
            "Deepest catacomb level."),

        D("Harkyn's Castle", "Harkyn's Castle — level 1", MapTerrainData.Harkyns1Terrain,
            "The castle in the north of the city."),
        D("Harkyn's Castle", "Harkyn's Castle — level 2", MapTerrainData.Harkyns2Terrain,
            "Second castle level."),
        D("Harkyn's Castle", "Harkyn's Castle — level 3", MapTerrainData.Harkyns3Terrain,
            "Top castle level."),

        D("Kylearan's Tower", "Kylearan's Tower", MapTerrainData.KylearansTerrain,
            "The Mad One's tower — a single maze level."),

        D("Mangar's Tower", "Mangar's Tower — level 1", MapTerrainData.Mangars1Terrain,
            "The dark wizard's tower; five levels to the top."),
        D("Mangar's Tower", "Mangar's Tower — level 2", MapTerrainData.Mangars2Terrain,
            "Second tower level."),
        D("Mangar's Tower", "Mangar's Tower — level 3", MapTerrainData.Mangars3Terrain,
            "Third tower level."),
        D("Mangar's Tower", "Mangar's Tower — level 4", MapTerrainData.Mangars4Terrain,
            "Fourth tower level."),
        D("Mangar's Tower", "Mangar's Tower — level 5", MapTerrainData.Mangars5Terrain,
            "The top of Mangar's Tower."),
    };
}
