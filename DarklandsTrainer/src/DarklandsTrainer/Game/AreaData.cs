namespace DarklandsTrainer.Game;

public static class AreaData
{
    public static readonly IReadOnlyList<AreaLevel> Levels = Build();

    private static IReadOnlyList<AreaLevel> Build() => new List<AreaLevel>
    {
        Parse(0, "Nuremberg and Franconia", "A central starting region of roads, trade towns, and noble estates.", new[]
        {
            "####################", "#....F....T....F...#", "#..S....F.....I....#", "#....F......C......#", "#..V......F........#", "#......N......T....#", "#...F..........F..#", "#....T....M.......#", "#.............V...#", "####################",
        }, Pois((3, 2, "Starting Road", "A safe place to begin gathering rumours and supplies."), (12, 3, "Nuremberg", "A major market city with guilds, inns, and merchants."), (7, 7, "Monastery", "A religious refuge for healing and learning."))),
        Parse(1, "Rhine Valley", "Rich river country from Mainz toward Cologne, where travel and trade converge.", new[]
        {
            "####################", "#...F..............#", "#..T....F....C.....#", "#......F...........#", "#....I.....F....T..#", "#..M.......F.......#", "#.......C......N...#", "#...F..............#", "#......V....F......#", "####################",
        }, Pois((12, 2, "Mainz", "A major Rhine city and useful supply stop."), (8, 6, "Cologne", "A powerful trading city on the Rhine."), (16, 6, "Rhine Castle", "A fortified noble holding overlooking the river."))),
        Parse(2, "Black Forest", "Dense woodland, isolated hamlets, and hidden dangers in southwest Germany.", new[]
        {
            "####################", "#FFFFFFFFFFFFFFFFFF#", "#FF...FFFFF....FFFF#", "#FF.T.FFFF..D..FFFF#", "#FFF..FFFFF....FFFF#", "#F...FFFF..I...FFFF#", "#F.V.FFFFFF....FFFF#", "#FFF...FFFFF..MFFFF#", "#FFFFFFFFFFFFFFFFFF#", "####################",
        }, Pois((3, 3, "Forest Town", "A remote settlement with local rumours."), (12, 3, "Hidden Cave", "A dangerous cave rumoured to shelter creatures and treasure."), (14, 7, "Forest Monastery", "A secluded religious community."))),
        Parse(3, "Swabia and Augsburg", "Prosperous southern roads connect Augsburg with villages and fortified estates.", new[]
        {
            "####################", "#......F...........#", "#..C......T....F...#", "#.......F..........#", "#..I.......N.......#", "#......V.......T...#", "#...F......M.......#", "#.............F....#", "#....T.............#", "####################",
        }, Pois((3, 2, "Augsburg", "A wealthy commercial city in southern Germany."), (11, 4, "Swabian Castle", "A noble stronghold on the road."), (10, 6, "Monastery", "A place to seek aid and religious lore."))),
        Parse(4, "Bavaria and Regensburg", "Danube-side settlements, old castles, and roads east into Bavaria.", new[]
        {
            "####################", "#......F...........#", "#..T.......C.......#", "#.....F.......N....#", "#...V.....I........#", "#.......F.....T....#", "#..M...............#", "#.....F....D.......#", "#.............F....#", "####################",
        }, Pois((10, 2, "Regensburg", "An important Bavarian city beside the Danube."), (14, 3, "Danube Castle", "A fortified river crossing."), (11, 7, "Old Mine", "A mine whose depths may conceal more than ore."))),
        Parse(5, "Hanseatic North", "Northern trade routes lead between Hamburg, towns, and coastal fortifications.", new[]
        {
            "####################", "#..................#", "#..F....T......F..#", "#.......F....C....#", "#..I..............#", "#.....N......T....#", "#...F......M......#", "#..............V..#", "#....F.............#", "####################",
        }, Pois((13, 3, "Hamburg", "A major northern port and mercantile centre."), (6, 5, "Northern Castle", "A strategic fortress on the trade route."), (10, 6, "Monastery", "A northern religious house."))),
        Parse(6, "Frankfurt and Hesse", "Busy central routes, market towns, and forested hills west of Franconia.", new[]
        {
            "####################", "#...F..............#", "#.....T....F.......#", "#..C.......I.......#", "#......F.......N...#", "#...V..............#", "#.........M....F...#", "#..F.....T..........#", "#.............D....#", "####################",
        }, Pois((3, 3, "Frankfurt", "A wealthy crossroads city for trade and information."), (14, 4, "Hessian Castle", "A regional noble stronghold."), (14, 8, "Ruined Temple", "An ancient site associated with unsettling stories."))),
        Parse(7, "Alpine Passes", "High southern mountains force travel through a few exposed roads and valleys.", new[]
        {
            "####################", "####################", "#...#....#....#...#", "#...#..T.#.I..#...#", "#......#....#.....#", "#..M...#..D.#..V..#", "#....#......#.....#", "#..C....#....#....#", "####################", "####################",
        }, Pois((7, 3, "Pass Town", "A hard mountain settlement serving travellers."), (11, 3, "Alpine Inn", "A welcome resting place before a mountain crossing."), (10, 5, "Mountain Cave", "A cave in the high country."))),
        Parse(8, "Satanic Cult Lands", "Remote forest roads conceal dangerous shrines, caves, and cult activity.", new[]
        {
            "####################", "#FFFFFFFFFFFFFFFFFF#", "#FF...FFFFF...FFFFF#", "#FF.D.FFFF..N.FFFFF#", "#FFFF.FFFF....FFFFF#", "#F..I..FFF.D..FFFFF#", "#F.FFF.FFFF....FFFF#", "#F....FFFFF..MFFFFF#", "#FFFFFFFFFFFFFFFFFF#", "####################",
        }, Pois((4, 3, "Draconite Cave", "A cave associated with dragon lore and rare materials."), (12, 3, "Cult Castle", "A fortified site linked to the satanic conspiracy."), (11, 5, "Satanic Temple", "An endgame cult location; approach only when prepared."))),
        Parse(9, "Final Fortress", "The remote final approach to the conspiracy's strongest redoubt.", new[]
        {
            "####################", "#..................#", "#..F..........F....#", "#.....########.....#", "#.....#......#.....#", "#..I..#..N...#..D..#", "#.....#......#.....#", "#.....########.....#", "#....F..........F..#", "####################",
        }, Pois((10, 5, "Final Fortress", "The final stronghold of the satanic conspiracy."), (16, 5, "Fortress Dungeon", "The dangerous inner dungeon."), (3, 5, "Last Inn", "A final resting point before the fortress."))),
    };

    private static IReadOnlyList<AreaPoi> Pois(params (int x, int y, string name, string desc)[] items) =>
        items.Select(item => new AreaPoi(item.x, item.y, item.name, item.desc)).ToList();

    private static AreaLevel Parse(int index, string name, string description, string[] rows, IReadOnlyList<AreaPoi> pois)
    {
        int height = rows.Length;
        int width = rows.Max(row => row.Length);
        var grid = new CellKind[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[x, y] = x < rows[y].Length ? ParseCell(rows[y][x]) : CellKind.Wall;
        return new AreaLevel(index, name, description, grid, pois);
    }

    private static CellKind ParseCell(char value) => value switch
    {
        '#' => CellKind.Wall,
        'C' => CellKind.City,
        'T' => CellKind.Town,
        'V' => CellKind.Village,
        'M' => CellKind.Monastery,
        'F' => CellKind.Forest,
        'I' => CellKind.Inn,
        'N' => CellKind.Castle,
        'D' => CellKind.Dungeon,
        'S' => CellKind.Start,
        _ => CellKind.Road,
    };
}
