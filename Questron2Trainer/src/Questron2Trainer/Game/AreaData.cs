namespace Questron2Trainer.Game;

public static class AreaData
{
    private const int Size = 20;

    public static readonly IReadOnlyList<AreaLevel> Areas = Build();

    private static IReadOnlyList<AreaLevel> Build() =>
    [
        Parse(0, "Redstone Castle", "The king's stronghold and the beginning of the quest.",
            "####################", "#........C.........#", "#.####.#####.####..#", "#.#..#.......#..#..#", "#.#..#.#####.#..#..#", "#....#...N...#.....#", "#.######.########..#", "#..................#", "####.##########.####", "#..................#", "#.####.######.###.#", "#.#..............#.#", "#.#.############.#.#", "#.#..............#.#", "#.######.########.#", "#..................#", "#.################.#", "#..................#", "#.........T........#", "####################",
            (9, 1, "Castle", "Speak with the king to begin the quest."),
            (9, 5, "Quest Giver", "A royal advisor provides guidance."),
            (10, 18, "Castle Gate", "Exit to the surrounding wilderness.")),
        Parse(1, "Hidden Rock", "A frontier town with supplies and services.",
            "####################", "#........T.........#", "#.######.######.##.#", "#.#....#......#....#", "#.#.##.######.#.##.#", "#...##....N...#....#", "######.######.######", "#..................#", "#.####.######.####.#", "#.#..#.#....#.#..#.#", "#.#..#.#.I..#.#..#.#", "#.####.######.####.#", "#..................#", "####.##########.####", "#..................#", "#.################.#", "#..................#", "#..................#", "#........T.........#", "####################",
            (9, 1, "Town Entrance", "Road to the wilderness."),
            (10, 5, "Town Elder", "Offers local advice."),
            (10, 10, "Supply Cache", "A useful early-game item."),
            (9, 18, "South Gate", "Road to Redstone Castle.")),
        Parse(2, "Bay View", "A coastal town and a place to prepare for sea travel.",
            "####################", "#........T.........#", "#.######.######.##.#", "#.#....#......#....#", "#.#.##.######.#.##.#", "#...##........#....#", "######.######.######", "#..................#", "#.####.######.####.#", "#.#..#.#....#.#..#.#", "#.#..#.#.N..#.#..#.#", "#.####.######.####.#", "#..................#", "####.##########.####", "#..................#", "#.################.#", "#..................#", "#........S.........#", "#........T.........#", "####################",
            (9, 1, "Town Entrance", "Road to the western coast."),
            (10, 10, "Harbormaster", "Knows the nearby waters."),
            (9, 17, "Shore", "Board a ship for island travel."),
            (9, 18, "South Gate", "Return to the wilderness.")),
        Parse(3, "Great Plains", "An exposed wilderness route between the towns and old ruins.",
            "####################", "#....#........#....#", "#.##.#.######.#.##.#", "#....#.#....#.#....#", "######.#.##.#.######", "#......#....#......#", "#.##############.#.#", "#.#..............#.#", "#.#.############.#.#", "#.#.#..........#.#.#", "#...#.########.#...#", "#####.#......#.#####", "#.....#.####.#.....#", "#.#####.#..#.#####.#", "#.......#..#.......#", "#.##########.#####.#", "#...............D..#", "#.################.#", "#........T.........#", "####################",
            (16, 16, "Dungeon Entrance", "A stairway descends into the Dungeon of Despair."),
            (9, 18, "Road Marker", "The route to the nearest town.")),
        Parse(4, "Dungeon of Despair", "A dangerous underground maze filled with traps and treasure.",
            "####################", "#........U.........#", "#.######.#########.#", "#.#....#.........#.#", "#.#.##.#########.#.#", "#...##.........#...#", "#####.########.#####", "#........#.........#", "#.######.#.#######.#", "#.#....#.#.#.....#.#", "#.#.##.#...#.###.#.#", "#...##.#####.#I#...#", "#####........#.#####", "#...#.########.#...#", "#.#.#....N.....#.#.#", "#.#.############.#.#", "#.#..............#.#", "#.##############.#.#", "#.........D........#", "####################",
            (9, 1, "Stairs Up", "Return to the Great Plains."),
            (12, 11, "Ancient Cache", "A guarded treasure chamber."),
            (9, 14, "Dungeon Keeper", "A hostile guardian blocks the route."),
            (10, 18, "Stairs Down", "Descend toward the Hall of the Gargoyle.")),
        Parse(5, "Hall of the Gargoyle", "A sealed dungeon hall guarded by a powerful gargoyle.",
            "####################", "#........U.........#", "#.######.#########.#", "#.#....#.........#.#", "#.#.##.#########.#.#", "#...##.........#...#", "#####.########.#####", "#........#.........#", "#.######.#.#######.#", "#.#....#.#.#.....#.#", "#.#.##.#...#.###.#.#", "#...##.#####.#.#...#", "#####........#.#####", "#...#.########.#...#", "#.#.#....B.....#.#.#", "#.#.############.#.#", "#.#..............#.#", "#.##############.#.#", "#.........D........#", "####################",
            (9, 1, "Stairs Up", "Return to the Dungeon of Despair."),
            (9, 14, "Gargoyle Boss", "Defeat the guardian to claim the passage."),
            (10, 18, "Stairs Down", "A route toward Grelminar's tomb.")),
        Parse(6, "Tomb of Grelminar", "A crypt of old magic, hidden passages, and essential artifacts.",
            "####################", "#........U.........#", "#.######.#########.#", "#.#....#.........#.#", "#.#.##.#########.#.#", "#...##.........#...#", "#####.########.#####", "#........#.........#", "#.######.#.#######.#", "#.#....#.#.#.....#.#", "#.#.##.#...#.###.#.#", "#...##.#####.#.#...#", "#####........#.#####", "#...#.########.#...#", "#.#.#....I.....#.#.#", "#.#.############.#.#", "#.#..............#.#", "#.##############.#.#", "#.........D........#", "####################",
            (9, 1, "Stairs Up", "Return to the Hall of the Gargoyle."),
            (9, 14, "Grelminar's Relic", "An artifact needed for the final journey."),
            (10, 18, "Stairs Down", "Passage to the ancient Pyramid.")),
        Parse(7, "The Pyramid", "An ancient structure where magic and stone conceal the final route.",
            "####################", "#........U.........#", "#.######.#########.#", "#.#....#.........#.#", "#.#.##.#########.#.#", "#...##.........#...#", "#####.########.#####", "#........#.........#", "#.######.#.#######.#", "#.#....#.#.#.....#.#", "#.#.##.#...#.###.#.#", "#...##.#####.#.#...#", "#####........#.#####", "#...#.########.#...#", "#.#.#....N.....#.#.#", "#.#.############.#.#", "#.#..............#.#", "#.##############.#.#", "#.........D........#", "####################",
            (9, 1, "Stairs Up", "Return to the Tomb of Grelminar."),
            (9, 14, "Pyramid Sage", "An ancient guardian presents a final warning."),
            (10, 18, "Stairs Down", "A hidden route to the final castle.")),
        Parse(8, "Island Shore", "A remote shore reached by ship, with paths leading inland.",
            "####################", "#........S.........#", "#.######.#########.#", "#.#....#.........#.#", "#.#.##.#########.#.#", "#...##.........#...#", "#####.########.#####", "#........#.........#", "#.######.#.#######.#", "#.#....#.#.#.....#.#", "#.#.##.#...#.###.#.#", "#...##.#####.#.#...#", "#####........#.#####", "#...#.########.#...#", "#.#.#....I.....#.#.#", "#.#.############.#.#", "#.#..............#.#", "#.##############.#.#", "#.........T........#", "####################",
            (9, 1, "Shore", "Disembark from your ship."),
            (9, 14, "Island Treasure", "A hidden cache rewards exploration."),
            (10, 18, "Island Trail", "The path leads toward inland ruins.")),
        Parse(9, "Final Castle", "The endgame stronghold where the final enemy awaits.",
            "####################", "#........U.........#", "#.######.#########.#", "#.#....#.........#.#", "#.#.##.#########.#.#", "#...##.........#...#", "#####.########.#####", "#........#.........#", "#.######.#.#######.#", "#.#....#.#.#.....#.#", "#.#.##.#...#.###.#.#", "#...##.#####.#.#...#", "#####........#.#####", "#...#.########.#...#", "#.#.#....B.....#.#.#", "#.#.############.#.#", "#.#..............#.#", "#.##############.#.#", "#.........C........#", "####################",
            (9, 1, "Entrance", "The final approach."),
            (9, 14, "Final Boss", "Prepare before confronting the final guardian."),
            (10, 18, "Final Castle", "The conclusion of the quest.")),
    ];

    private static AreaLevel Parse(int index, string name, string description, params string[] rows)
    {
        var poiRows = rows.TakeWhile(row => row.Length == Size).ToArray();
        var pois = rows.Skip(poiRows.Length)
            .Select(row => row.Split('\u001f'))
            .Select(parts => new AreaPoi(int.Parse(parts[0]), int.Parse(parts[1]), parts[2], parts[3]))
            .ToList();
        var grid = new CellKind[Size, Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                grid[x, y] = poiRows[y][x] == '#' ? CellKind.Wall : CellKind.Floor;
        return new AreaLevel(index, name, description, grid, pois);
    }

    private static AreaLevel Parse(int index, string name, string description, string row1, string row2, string row3, string row4, string row5, string row6, string row7, string row8, string row9, string row10, string row11, string row12, string row13, string row14, string row15, string row16, string row17, string row18, string row19, string row20, params (int x, int y, string name, string description)[] pois)
    {
        var grid = new CellKind[Size, Size];
        var rows = new[] { row1, row2, row3, row4, row5, row6, row7, row8, row9, row10, row11, row12, row13, row14, row15, row16, row17, row18, row19, row20 };
        for (int y = 0; y < Size; y++)
        {
            var row = rows[y];
            for (int x = 0; x < Size; x++)
            {
                char c = x < row.Length ? row[x] : '#';
                grid[x, y] = c == '#' ? CellKind.Wall : CellKind.Floor;
            }
        }
        return new AreaLevel(index, name, description, grid,
            pois.Select(p => new AreaPoi(p.x, p.y, p.name, p.description)).ToList());
    }
}
