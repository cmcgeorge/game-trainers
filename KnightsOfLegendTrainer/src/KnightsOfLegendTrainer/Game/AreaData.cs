namespace KnightsOfLegendTrainer.Game;

public static class AreaData
{
    public static readonly IReadOnlyList<AreaLevel> Levels = Build();

    private static IReadOnlyList<AreaLevel> Build() =>
    [
        Parse(0, "Brettle", "Starting town and the main early-game hub.",
            [
                "############", "#....G.....#", "#..........#", "#.S......N.#", "#....##....#", "#....##....#", "#..........#", "#..I....A..#", "#..........#", "#....C.....#", "#..........#", "############",
            ], Pois((2, 3, "Start", "The party begins in Brettle."), (5, 1, "White Pearl Guild", "Learn basic White Pearl magic before joining an order."), (9, 3, "Quest Givers", "Stephanie, Stephen, and Hegissa begin the first quests."), (3, 7, "Trading Post", "Purchase equipment and supplies."), (8, 7, "Arena", "Practice tactical combat."), (5, 9, "Fortress of Brettle", "Hvrad Myth trains four weapon types."))),
        Parse(1, "Northern Tower", "A tower north of Brettle where Fistan Stockhard trains heavy weapons.",
            [
                "############", "#..........#", "#..######..#", "#..#....#..#", "#..#.N..#..#", "#..#....#..#", "#..####.#..#", "#......#...#", "#..C...#...#", "#..........#", "#..........#", "############",
            ], Pois((4, 4, "Fistan Stockhard", "Trains axes and heavy crossbows."), (3, 8, "Tower Keep", "A fortified training location."))),
        Parse(2, "Htron", "Town with training grounds and several quest contacts.",
            [
                "############", "#..........#", "#.N....G...#", "#..........#", "#....####..#", "#....#..#..#", "#..A.#..#..#", "#....####..#", "#..........#", "#....T.....#", "#..........#", "############",
            ], Pois((2, 2, "Quest Givers", "Biblik, Sam, and Tulliana offer quests here."), (7, 2, "Training Grounds", "Zachary Bladeshure and Mornag the Merciless train weapons."), (3, 6, "Arena", "Practice before the harder quests."), (5, 9, "Town Gate", "Road to the surrounding wilderness."))),
        Parse(3, "Tegal Forest", "Forest routes lead to Monvin the Elder, the Blue Gem order, and quests.",
            [
                "############", "#....#.....#", "#....#..N..#", "#....#.....#", "#..........#", "#.######...#", "#......#...#", "#..G...#...#", "#......#...#", "#....D.....#", "#..........#", "############",
            ], Pois((8, 2, "Monvin the Elder", "Trains halberds, flails, and other weapons."), (3, 7, "Blue Gem Guild", "Kelden and Dwarven characters can join this magic order."), (5, 9, "Forest Dungeon", "Quest battles and treasure lie beyond."))),
        Parse(4, "Poitle Lock", "A river town and the home of the Secret Storm magic order.",
            [
                "############", "#..........#", "#..~~~~~~..#", "#..~....~..#", "#..~.G..~..#", "#..~....~..#", "#..~~~~~~..#", "#.....N....#", "#..........#", "#....T.....#", "#..........#", "############",
            ], Pois((5, 4, "Secret Storm Guild", "Learn giant-themed magic before committing to an order."), (6, 7, "Quest Givers", "Orofin and Sedfrey begin quests here."), (5, 9, "Lock Gate", "Crossing point for river travel."))),
        Parse(5, "Thimblewald", "A forest town tied to Red Mist magic and cloak-related quests.",
            [
                "############", "#..........#", "#.G.....N..#", "#..........#", "#...####...#", "#...#..#...#", "#...#I.#...#", "#...####...#", "#..........#", "#....T.....#", "#..........#", "############",
            ], Pois((2, 2, "Red Mist Guild", "Legendary creature-themed spells are taught here."), (8, 2, "Quest Givers", "Milinya, Trimrose, and Keldinarr offer quests."), (6, 6, "Quest Item", "A landmark on the quest route."), (5, 9, "Town Gate", "Route toward Downing Swamp and Windy Run."))),
        Parse(6, "Olanthen Barrier", "Eastern city with Dark Stone magic, major quests, and the final assembly.",
            [
                "############", "#....C.....#", "#..........#", "#..G....N..#", "#..........#", "#.####.....#", "#.#..#..A..#", "#.#..#.....#", "#.####.....#", "#....T.....#", "#..........#", "############",
            ], Pois((5, 1, "Assembly Building", "Speak with Dundle here before the final quest."), (3, 3, "Dark Stone Guild", "Undead-themed magic order."), (8, 3, "Quest Givers", "Belinda and Denswurth offer quests."), (8, 6, "Arena", "Combat practice in the eastern city."), (5, 9, "Barrier Gate", "Road to the eastern wilderness."))),
        Parse(7, "Shellernoon", "Southern stronghold with Black Onyx magic and the final quest chain.",
            [
                "############", "#..........#", "#....G.....#", "#..........#", "#..C....N..#", "#..........#", "#....####..#", "#....#..#..#", "#..A.#..#..#", "#....####..#", "#..........#", "############",
            ], Pois((5, 2, "Black Onyx Guild", "Elemental magic order."), (3, 4, "Lord Norgan's Keep", "Begin the Ward quest here."), (8, 4, "Quest Contacts", "Sheller Bridge leads to the final quest chain."), (3, 8, "Arena", "Practice tactical formations."))),
        Parse(8, "Krag Keep", "Fortress near dangerous hills and the Mist Giant quest.",
            [
                "############", "#..........#", "#.########.#", "#.#......#.#", "#.#.N....#.#", "#.#......#.#", "#.#.####.#.#", "#.#......#.#", "#.#..C...#.#", "#..........#", "#..........#", "############",
            ], Pois((4, 4, "Ballaster", "Ask for scalfeth to begin the Millet quest."), (6, 8, "Krag Keep", "A fortress on the route to Wesswald."))),
        Parse(9, "Ghor Hills", "Final hostile area where Cyclops guard Seggallion's rescue.",
            [
                "############", "#....E.....#", "#..######..#", "#..#....#..#", "#..#.D..#..#", "#..#....#..#", "#..######..#", "#..........#", "#....I.....#", "#....E.....#", "#..........#", "############",
            ], Pois((5, 1, "Cyclops Patrol", "Prepare for the final battle."), (5, 4, "Ghor Dungeon", "Final quest destination."), (5, 8, "Seggallion's Trail", "The rescue route."), (5, 9, "Enemy Guard", "Hostile defenders near the objective."))),
    ];

    private static IReadOnlyList<AreaPoi> Pois(params (int x, int y, string name, string description)[] items) =>
        items.Select(item => new AreaPoi(item.x, item.y, item.name, item.description)).ToList();

    private static AreaLevel Parse(int index, string name, string description, string[] rows, IReadOnlyList<AreaPoi> pois)
    {
        int height = rows.Length;
        int width = rows.Max(row => row.Length);
        var grid = new CellKind[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[x, y] = x < rows[y].Length && rows[y][x] != '#' ? CellKind.Floor : CellKind.Wall;
        return new AreaLevel(index, name, description, grid, pois);
    }
}
