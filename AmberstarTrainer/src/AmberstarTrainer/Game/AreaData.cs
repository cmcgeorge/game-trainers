namespace AmberstarTrainer.Game;

public static class AreaData
{
    private static IReadOnlyList<AreaLevel>? _levels;
    public static IReadOnlyList<AreaLevel> Levels => _levels ??= Build();

    private static IReadOnlyList<AreaLevel> Build() =>
    [
        Parse(0, "Twinlake City", "The starting city and the main hub for trade, healing, training, and guild business.", Twinlake,
            Pois((2, 2, "City Gate", "Main entrance to Twinlake."), (8, 2, "Temple", "Healing and spiritual services."),
                (13, 4, "Tavern", "Rumours, companions, and rest."), (4, 8, "Shops", "Equipment and supplies."),
                (11, 9, "Guilds", "Training and faction contacts."))),
        Parse(1, "Twinlake Overworld", "The roads, forests, lakes, and mountain passes linking Twinlake's settlements and dungeons.", Overworld,
            Pois((2, 2, "Twinlake", "Starting city."), (13, 2, "Haste", "Village to the east."),
                (4, 8, "Crystal", "Ice and water dungeon."), (14, 9, "Grim-path", "Mountain village."),
                (9, 5, "Elven Ruins", "Ruins hidden in the forest."))),
        Parse(2, "Haste", "A small village with a tavern and shop, useful as a stop on the eastern roads.", Haste,
            Pois((2, 5, "West Road", "Road back toward Twinlake."), (8, 3, "Tavern", "Rest and local information."),
                (13, 7, "Shop", "Supplies and equipment."))),
        Parse(3, "Grim-path", "A mountain settlement close to the mines and high passes.", GrimPath,
            Pois((2, 8, "Mountain Pass", "Route toward the lower roads."), (8, 3, "Village Hall", "Local services."),
                (13, 6, "Mine Road", "Leads toward dwarven workings."))),
        Parse(4, "Crystal", "An ice and water themed dungeon where narrow passages surround crystalline chambers.", Crystal,
            Pois((2, 9, "Entrance", "Return route to the overworld."), (8, 3, "Crystal Chamber", "A major crystalline cavern."),
                (14, 8, "Lower Passage", "Route deeper into the dungeon."))),
        Parse(5, "Elven Ruins", "Forest ruins with old halls, concealed paths, and magical remnants.", ElvenRuins,
            Pois((2, 9, "Forest Entrance", "Trail back to the overworld."), (8, 4, "Ancient Hall", "Central ruin chamber."),
                (14, 3, "Hidden Shrine", "A secluded elven site."))),
        Parse(6, "Dwarven Mines", "Underground mine galleries beneath the mountains, full of shafts and worked stone.", DwarvenMines,
            Pois((2, 2, "Upper Shaft", "Exit toward Grim-path."), (8, 6, "Mine Works", "Central working area."),
                (14, 9, "Deep Tunnel", "Passage toward lower caverns."))),
        Parse(7, "Desert Temples", "Ancient temples stand among the desert routes south of the settled lands.", DesertTemples,
            Pois((2, 9, "Desert Road", "Route back across the sands."), (8, 4, "Sun Temple", "Main temple complex."),
                (14, 7, "Burial Chamber", "Ancient inner chamber."))),
        Parse(8, "Underground Caves", "Natural cave systems connect remote dungeon routes below Umajin.", UndergroundCaves,
            Pois((2, 2, "Upper Cave", "Passage to the mines."), (8, 7, "Underground Lake", "Water-filled cavern."),
                (14, 3, "Fortress Route", "Approach to Lord Chile's domain."))),
        Parse(9, "Lord Chile's Fortress", "The final fortress: a defended stronghold where the campaign reaches its climax.", ChilesFortress,
            Pois((2, 9, "Outer Gate", "Fortress entrance."), (8, 5, "Great Hall", "Central fortress chamber."),
                (14, 2, "Lord Chile", "Final confrontation.")))
    ];

    private static AreaLevel Parse(int index, string name, string description, string[] rows,
        IReadOnlyList<AreaPoi> pois)
    {
        int height = rows.Length;
        int width = rows.Max(r => r.Length);
        var grid = new AreaCellKind[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[x, y] = x < rows[y].Length ? Cell(rows[y][x]) : AreaCellKind.Wall;
        return new AreaLevel(index, name, description, grid, pois);
    }

    private static AreaCellKind Cell(char value) => value switch
    {
        '.' => AreaCellKind.Floor,
        '~' => AreaCellKind.Water,
        '^' => AreaCellKind.Mountain,
        'f' => AreaCellKind.Forest,
        's' => AreaCellKind.Desert,
        _ => AreaCellKind.Wall,
    };

    private static IReadOnlyList<AreaPoi> Pois(params (int x, int y, string name, string description)[] items) =>
        items.Select(item => new AreaPoi(item.x, item.y, item.name, item.description)).ToList();

    private static readonly string[] Twinlake =
    [
        "#################", "#...............#", "#...............#", "#...###...###...#", "#...#.......#...#", "#...#.......#...#", "#...###...###...#", "#...............#", "#...............#", "#...............#", "#################"
    ];
    private static readonly string[] Overworld =
    [
        "^^^^^^^^^^^^^^^^^", "^^^fffff...ffff^^", "^^......~......^^", "^^.ffff.~.ffff.^^", "^^.f....~....f.^^", "^^.f.fffffff.f.^^", "^^...fff.fff...^^", "^^.fff.....fff.^^", "^^..sssss.sss..^^", "^^..ssss...ss..^^", "^^^^^^^^^^^^^^^^^"
    ];
    private static readonly string[] Haste =
    [
        "#################", "#...............#", "#...#####.......#", "#...#...#.......#", "#...#...#.......#", "#...............#", "#.......#####...#", "#.......#...#...#", "#.......#####...#", "#...............#", "#################"
    ];
    private static readonly string[] GrimPath =
    [
        "^^^^^^^^^^^^^^^^^", "^^^.........^^^^", "^^...^^^^^...^^", "^...^...^....^^", "^...^...^.....^", "^...^^^^^.....^", "^..............^", "^^.....^^^^....^", "^^^...........^^", "^^^^^^^^^^^^^^^^^"
    ];
    private static readonly string[] Crystal =
    [
        "#################", "#~~~~#.....#~~~~#", "#~##~#.###.#~##~#", "#~#..#.....#..#~#", "#~#.#####.###.#~#", "#...#.......#...#", "###.#.#####.#.###", "#...#.#...#.#...#", "#.###.#...#.###.#", "#...............#", "#################"
    ];
    private static readonly string[] ElvenRuins =
    [
        "fffffffffffffffff", "fff.....ffff....f", "ff..###.fff.###.f", "f...#.......#...f", "f.#####.#####...f", "f.....#.#.......f", "fff.#.#.#.###.fff", "f...#...#...#...f", "f.#########.#...f", "f...............f", "fffffffffffffffff"
    ];
    private static readonly string[] DwarvenMines =
    [
        "#################", "#.....#.....#...#", "#.###.#.###.#.#.#", "#.#...#...#...#.#", "#.#.#####.#####.#", "#.#.....#.......#", "#.#####.#.#####.#", "#.....#.#.#.....#", "#####.#.#.#.#####", "#...............#", "#################"
    ];
    private static readonly string[] DesertTemples =
    [
        "sssssssssssssssss", "sss...........sss", "ss..#########..ss", "s...#.......#...s", "s.###.#####.###.s", "s.#...#...#...#.s", "s.#.###...###.#.s", "s.#...........#.s", "s.#############.s", "s...............s", "sssssssssssssssss"
    ];
    private static readonly string[] UndergroundCaves =
    [
        "#################", "#...#.......#...#", "#.#.#.#####.#.#.#", "#.#...#~~~#...#.#", "#.#####~#~#####.#", "#.....#~~~#.....#", "###.#.#####.#.###", "#...#.......#...#", "#.#############.#", "#...............#", "#################"
    ];
    private static readonly string[] ChilesFortress =
    [
        "#################", "#.......#.......#", "#.#####.#.#####.#", "#.#...#...#...#.#", "#.#.#.#####.#.#.#", "#...#...#...#...#", "#####.#.#.#.#####", "#.....#...#.....#", "#.###########.#.#", "#...............#", "#################"
    ];
}
