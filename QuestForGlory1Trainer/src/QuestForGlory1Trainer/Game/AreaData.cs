namespace QuestForGlory1Trainer.Game;

public static class AreaData
{
    public static readonly IReadOnlyList<AreaLevel> Levels = Build();

    private static IReadOnlyList<AreaLevel> Build() => new List<AreaLevel>
    {
        Parse(0, "Spielburg Town", "The valley's main hub for supplies, healing, gossip, and rest.", Town, Pois(
            (2, 2, "Town Gate", "The road into Spielburg Valley."),
            (5, 3, "Sheriff's Office", "Speak with Sheriff Schultz about the brigands."),
            (10, 3, "Acker Berg Tavern", "Rumours, food, and the famous brew."),
            (4, 8, "Bakery", "Buy fresh bread for the road."),
            (10, 8, "General Store", "Supplies and useful adventuring equipment."),
            (15, 8, "Healer's Hut", "Healing potions and advice from Healer Johann."),
            (10, 12, "Dry Grape Inn", "Rest safely and recover before travelling."))),
        Parse(1, "Spielburg Valley", "Forest roads connect the town, castle, and the valley's dangerous landmarks.", Valley, Pois(
            (2, 13, "Start", "The south road leading from Spielburg Town."),
            (8, 10, "Spielburg Town", "Return for supplies, rest, and quest information."),
            (15, 4, "Castle Spielburg", "The Baron's cursed castle."),
            (5, 4, "Erana's Peace", "A magical sanctuary in the forest."),
            (17, 13, "Brigand Trail", "A guarded route toward the brigand camp."))),
        Parse(2, "Castle Spielburg", "The Baron's cursed castle, sealed behind its drawbridge and its secrets.", Castle, Pois(
            (8, 14, "Drawbridge", "The entrance from the valley road."),
            (8, 8, "Courtyard", "The overgrown heart of the castle."),
            (4, 4, "Baron Stefan", "The grieving Baron seeks his daughter Elsa."),
            (12, 4, "Elsa's Room", "A key clue to the curse over Spielburg."))),
        Parse(3, "Erana's Peace", "A tranquil glade protected by the magic of Erana.", EranasPeace, Pois(
            (8, 13, "Forest Path", "The route back into the valley."),
            (8, 8, "Erana's Peace", "Rest here safely and recover your strength."),
            (12, 5, "Magic Acorn", "A magical acorn needed for the dispel potion."))),
        Parse(4, "Mead Maze", "A hedge maze whose brewer and puzzles favour a resourceful thief.", MeadMaze, Pois(
            (2, 13, "Maze Entrance", "The path from the valley."),
            (7, 7, "Mead Brewer", "The brewer knows the maze and its secrets."),
            (14, 3, "Green Ring", "A useful magical item hidden in the maze."))),
        Parse(5, "Brigand Camp", "The brigands' forest stronghold. Infiltration and disguise are safer than a frontal assault.", BrigandCamp, Pois(
            (2, 13, "Camp Approach", "The guarded forest trail."),
            (8, 9, "Barracks", "Brigands gather here."),
            (11, 5, "Brigand Leader", "The leader holds the key to the camp's secrets."),
            (14, 10, "Treasure", "Supplies and valuables taken by the brigands."))),
        Parse(6, "Baba Yaga's Hut", "The witch's moving hut is an endgame destination and demands careful preparation.", BabaYaga, Pois(
            (2, 13, "Swamp Path", "The difficult path through the forest."),
            (8, 8, "Baba Yaga's Hut", "The witch's hut and its dangerous owner."),
            (13, 5, "Dispel Potion", "Use the ingredients to break the curse."))),
        Parse(7, "Dryad's Tree", "A sacred tree watched by a dryad; kindness and the right item earn her trust.", DryadTree, Pois(
            (2, 13, "Forest Path", "The trail from the valley."),
            (8, 7, "Dryad", "The dryad guards her ancient tree."),
            (9, 7, "Dispel Ingredient", "A vital ingredient for the dispel potion."))),
        Parse(8, "Goblin Camp", "A small enemy camp with opportunities for combat, stealth, and scavenging.", GoblinCamp, Pois(
            (2, 13, "Forest Path", "The route back to the valley."),
            (8, 8, "Goblin Camp", "Goblins patrol the central clearing."),
            (13, 5, "Thief's Tools", "Useful equipment for locked doors and chests."))),
        Parse(9, "Caves and Fairy Ring", "Hidden caves and a magical fairy ring reward thorough exploration.", Caves, Pois(
            (2, 13, "Cave Entrance", "A concealed opening in the forest."),
            (7, 7, "Hidden Treasure", "A cache for explorers who search carefully."),
            (13, 5, "Fairy's Ring", "A magical glade with unusual effects."))),
    };

    private static IReadOnlyList<AreaPoi> Pois(params (int x, int y, string name, string desc)[] items) =>
        items.Select(i => new AreaPoi(i.x, i.y, i.name, i.desc)).ToList();

    private static AreaLevel Parse(int index, string name, string desc, string[] rows,
        IReadOnlyList<AreaPoi> pois)
    {
        int height = rows.Length;
        int width = rows.Max(r => r.Length);
        var grid = new CellKind[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[x, y] = x < rows[y].Length && rows[y][x] != '#' ? CellKind.Floor : CellKind.Wall;
        return new AreaLevel(index, name, desc, grid, pois);
    }

    private static readonly string[] Town =
    {
        "##################", "#................#", "#.T..T....T......#", "#................#", "#..####....####..#", "#..#..........#..#", "#..#..........#..#", "#..####....####..#", "#...T.....T....T.#", "#................#", "#..####....####..#", "#..#..........#..#", "#..#.....T....#..#", "#................#", "##################",
    };
    private static readonly string[] Valley =
    {
        "####################", "#..................#", "#..######....######", "#..#..............#", "#..#.E.........C..#", "#..#..............#", "#..######....######", "#..................#", "#....##########....#", "#....#........#....#", "#....#..T.....#....#", "#....#........#....#", "#....##########....#", "#.S..............B.#", "####################",
    };
    private static readonly string[] Castle =
    {
        "#################", "#...............#", "#.#####...#####.#", "#.#...........#.#", "#.#.N.......I.#.#", "#.#...........#.#", "#.#...........#.#", "#.#####...#####.#", "#.......C.......#", "#.#####...#####.#", "#.#...........#.#", "#.#...........#.#", "#.#...........#.#", "#.......C.......#", "########C########", "#################",
    };
    private static readonly string[] EranasPeace =
    {
        "#################", "#...............#", "#....#######....#", "#...#.......#I..#", "#..#.........#..#", "#..#.........#..#", "#..#.........#..#", "#...#...E...#...#", "#..#.........#..#", "#..#.........#..#", "#..#.........#..#", "#...#.......#...#", "#....#######....#", "#.......S.......#", "#################",
    };
    private static readonly string[] MeadMaze =
    {
        "#################", "#...............#", "#.#####.#######.#", "#.#...#.....#.I.#", "#.#.#.#####.#.#.#", "#...#.....#...#.#", "#####.###.#####.#", "#.....#...#.....#", "#.#####.#####.##", "#.#.....N.....#.#", "#.#.#########.#.#", "#.#...........#.#", "#.#############.#", "#S..............#", "#################",
    };
    private static readonly string[] BrigandCamp =
    {
        "#################", "#...............#", "#.#############.#", "#.#...B.....N.#.#", "#.#.#########.#.#", "#.#.#.......#.#.#", "#...#...I...#...#", "#####.#####.#####", "#.....#...#.....#", "#..B..#...#..B..#", "#.....#.....I...#", "#.#############.#", "#...............#", "#S..............#", "#################",
    };
    private static readonly string[] BabaYaga =
    {
        "#################", "#...............#", "#.#####.#######.#", "#.#...........#.#", "#.#.#########.#.#", "#...#.......#...#", "#####..H..#####.#", "#.....#####.....#", "#.###.......###.#", "#.#...........#.#", "#.#.#########.#.#", "#.#.......I...#.#", "#.#############.#", "#S..............#", "#################",
    };
    private static readonly string[] DryadTree =
    {
        "#################", "#...............#", "#....#######....#", "#...#.......#...#", "#..#.........#..#", "#..#....N....#..#", "#..#.........#..#", "#..#.....I...#..#", "#..#.........#..#", "#...#.......#...#", "#....#######....#", "#...............#", "#...............#", "#S..............#", "#################",
    };
    private static readonly string[] GoblinCamp =
    {
        "#################", "#...............#", "#.#############.#", "#.#...B.......#.#", "#.#.#########.#.#", "#.#.#.......#.#.#", "#...#...I...#...#", "#####.#####.#####", "#.....#...#.....#", "#..B..#...#..B..#", "#.....#####.....#", "#.#############.#", "#...............#", "#S..............#", "#################",
    };
    private static readonly string[] Caves =
    {
        "#################", "#...............#", "#.#####.#######.#", "#.#...#.......F.#", "#.#.#.#######.#.#", "#...#.....#...#.#", "#####.###.#.###.#", "#.....#...#.....#", "#.#####.#####.##", "#.#.....I.....#.#", "#.#.#########.#.#", "#.#...........#.#", "#.#############.#", "#S..............#", "#################",
    };
}
