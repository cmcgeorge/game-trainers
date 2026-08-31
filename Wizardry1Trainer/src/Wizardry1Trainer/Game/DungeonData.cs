namespace Wizardry1Trainer.Game;

/// <summary>
/// The ten dungeon levels of Wizardry 1, each a 20×20 grid of wall and floor cells with
/// points of interest (stairs, elevator, the Blue Ribbon, the Amulet). The wall layout is
/// taken from the strategy guide's ASCII maps, which were cross-checked against the
/// reconstructed Pascal source and the Wizardry Wiki.
///
/// <para>Each level is a 20×20 grid. <c>'#'</c> is a wall, <c>'.'</c> is open floor, and
/// the letters mark POIs on floor squares: <c>U</c> stairs up, <c>D</c> stairs down,
/// <c>E</c> elevator, <c>@</c> party start, <c>A</c> the Amulet, <c>B</c> the Blue Ribbon.</para>
/// </summary>
public static class DungeonData
{
    private const int N = GameFacts.MazeSize;

    /// <summary>All ten dungeon levels, indexed 0..9 (level 1 = index 0).</summary>
    private static IReadOnlyList<DungeonLevel>? _levels;
    public static IReadOnlyList<DungeonLevel> Levels => _levels ??= Build();

    private static IReadOnlyList<DungeonLevel> Build()
    {
        var list = new List<DungeonLevel>(10);
        list.Add(Parse(0, "Castle Level",
            "Entrance from the Edge of Town. Weak monsters (kobolds, orcs, skeletons). Grind here until level 2-3.",
            Level1, Pois(
                (10, 1, "Party Start", "Where the party enters from the Edge of Town."),
                (3, 17, "Stairs Down", "Descends to Level 2 at (3, 17)."))));

        list.Add(Parse(1, "Upper Maze",
            "First tough encounters. Stairs up to Level 1, stairs down to Level 3.",
            Level2, Pois(
                (3, 17, "Stairs Up", "Ascends to Level 1 at (3, 17)."),
                (16, 3, "Stairs Down", "Descends to Level 3 at (16, 3)."))));

        list.Add(Parse(2, "The Elevator Level",
            "The critical transition level. The elevator can send the party to any previously visited level.",
            Level3, Pois(
                (16, 3, "Stairs Up", "Ascends to Level 2 at (16, 3)."),
                (3, 16, "Stairs Down", "Descends to Level 4 at (3, 16)."),
                (10, 10, "Elevator", "Sends the party to any previously visited level."))));

        list.Add(Parse(3, "Middle Maze",
            "Good treasure. The Blue Ribbon is required to access the deeper levels.",
            Level4, Pois(
                (3, 16, "Stairs Up", "Ascends to Level 3 at (3, 16)."),
                (16, 16, "Stairs Down", "Descends to Level 5 at (16, 16)."),
                (10, 3, "Blue Ribbon", "Required to access the deeper levels."))));

        list.Add(Parse(4, "Lower Middle",
            "Strong monsters (trolls, ogres, wights). Stairs up to Level 4, stairs down to Level 6.",
            Level5, Pois(
                (16, 16, "Stairs Up", "Ascends to Level 4 at (16, 16)."),
                (3, 3, "Stairs Down", "Descends to Level 6 at (3, 3)."))));

        list.Add(Parse(5, "Deep Maze",
            "Dangerous traps (pits, teleporters, darkness). Stairs up to Level 5, stairs down to Level 7.",
            Level6, Pois(
                (3, 3, "Stairs Up", "Ascends to Level 5 at (3, 3)."),
                (16, 3, "Stairs Down", "Descends to Level 7 at (16, 3)."))));

        list.Add(Parse(6, "Werdna's Domain Begins",
            "Very dangerous. Guarded by high-level monsters. Stairs up to Level 6, stairs down to Level 8.",
            Level7, Pois(
                (16, 3, "Stairs Up", "Ascends to Level 6 at (16, 3)."),
                (3, 16, "Stairs Down", "Descends to Level 8 at (3, 16)."))));

        list.Add(Parse(7, "The Deeps",
            "Among the hardest levels. Powerful undead and demons. Stairs up to Level 7, stairs down to Level 9.",
            Level8, Pois(
                (3, 16, "Stairs Up", "Ascends to Level 7 at (3, 16)."),
                (16, 3, "Stairs Down", "Descends to Level 9 at (16, 3)."))));

        list.Add(Parse(8, "Near the Bottom",
            "Final approach to Werdna. Stairs up to Level 8, stairs down to Level 10.",
            Level9, Pois(
                (16, 3, "Stairs Up", "Ascends to Level 8 at (16, 3)."),
                (10, 17, "Stairs Down", "Descends to Level 10 at (10, 17)."))));

        list.Add(Parse(9, "Werdna's Lair",
            "The bottom level. Confront Werdna and claim the Amulet to win the game.",
            Level10, Pois(
                (10, 17, "Stairs Up", "Ascends to Level 9 at (10, 17)."),
                (10, 10, "The Amulet", "Defeat Werdna and claim the Amulet to win the game."))));

        return list;
    }

    private static IReadOnlyList<DungeonPoi> Pois(params (int x, int y, string name, string desc)[] items) =>
        items.Select(i => new DungeonPoi(i.x, i.y, i.name, i.desc)).ToList();

    private static DungeonLevel Parse(int index, string name, string desc, string[] rows,
        IReadOnlyList<DungeonPoi> pois)
    {
        var grid = new CellKind[N, N];
        for (int y = 0; y < N; y++)
        {
            string row = y < rows.Length ? rows[y] : "";
            for (int x = 0; x < N; x++)
            {
                char c = x < row.Length ? row[x] : '#';
                grid[x, y] = c == '#' ? CellKind.Wall : CellKind.Floor;
            }
        }
        return new DungeonLevel(index, name, desc, grid, pois);
    }

    // --- ASCII grids (20x20, '#' = wall, '.' = floor) -----------------------

    private static readonly string[] Level1 =
    {
        "####################",
        "#.........@........#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#..D............#..#",
        "#..................#",
        "####################",
    };

    private static readonly string[] Level2 =
    {
        "####################",
        "#..................#",
        "#..................#",
        "#...####....####.D.#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#..U............#..#",
        "#..................#",
        "####################",
    };

    private static readonly string[] Level3 =
    {
        "####################",
        "#..................#",
        "#..................#",
        "#...####....####.U.#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#..........E.......#",
        "#..........E.......#",
        "#..................#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#..D............#..#",
        "#..................#",
        "#..................#",
        "####################",
    };

    private static readonly string[] Level4 =
    {
        "####################",
        "#..................#",
        "#..................#",
        "#...####....B####..#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#.D.#",
        "#..U............#..#",
        "#..................#",
        "####################",
    };

    private static readonly string[] Level5 =
    {
        "####################",
        "#..................#",
        "#..................#",
        "#.D.####....####...#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#.U.#",
        "#..................#",
        "#..................#",
        "####################",
    };

    private static readonly string[] Level6 =
    {
        "####################",
        "#..................#",
        "#..................#",
        "#.U.####....####.D.#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#..................#",
        "#..................#",
        "####################",
    };

    private static readonly string[] Level7 =
    {
        "####################",
        "#..................#",
        "#..................#",
        "#...####....####.D.#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#..U............#..#",
        "#..................#",
        "####################",
    };

    private static readonly string[] Level8 =
    {
        "####################",
        "#..................#",
        "#..................#",
        "#...####....####.D.#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#..U............#..#",
        "#..................#",
        "####################",
    };

    private static readonly string[] Level9 =
    {
        "####################",
        "#..................#",
        "#..................#",
        "#...####....####.U.#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#..........D.......#",
        "#..................#",
        "####################",
    };

    private static readonly string[] Level10 =
    {
        "####################",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#..........A.......#",
        "#...#..........#...#",
        "#...####....####...#",
        "#..................#",
        "#..................#",
        "#...####....####...#",
        "#...#..........#...#",
        "#..........U.......#",
        "#..................#",
        "####################",
    };
}
