namespace HillsfarTrainer.Game;

public static class AreaData
{
    public static readonly IReadOnlyList<AreaLevel> Areas = Build();

    private static IReadOnlyList<AreaLevel> Build() => new[]
    {
        Parse(0, "Market District", "Shops and services around Hillsfar's central market square.", Market, Pois(
            (3, 3, "Shop", "Weapons and armor merchants."),
            (11, 3, "Shop", "Magic shop and general goods."),
            (7, 8, "NPC", "Merchant with local rumors."),
            (12, 12, "Item", "A searchable market stall."))),
        Parse(1, "Tavern District", "Four pubs offer rumors, meals, drinks, and quest leads after dark.", Tavern, Pois(
            (3, 3, "Tavern", "Dragon's Lair."),
            (11, 3, "Tavern", "Rat's Nest."),
            (3, 12, "Tavern", "Hydra's Den."),
            (11, 12, "Tavern", "Bugbear's Cave."),
            (7, 8, "NPC", "A patron with a mission clue."))),
        Parse(2, "Temple and Guilds", "The class guilds and Temple of Tempus are the reliable mission hubs.", Temple, Pois(
            (7, 3, "Temple", "Temple of Tempus: healing and cleric missions."),
            (3, 8, "NPC", "Fighter's Guild master."),
            (11, 8, "NPC", "Mage's Guild master."),
            (7, 12, "NPC", "Rogue's Guild master."))),
        Parse(3, "Arena District", "The arena hosts melee competitions, chariot events, and required duels.", Arena, Pois(
            (7, 4, "Arena", "Arena entrance and combat competition."),
            (3, 11, "NPC", "Arena master."),
            (11, 11, "Enemy", "A challenging arena opponent."))),
        Parse(4, "Government District", "Maalthiir's officials, the castle, and the jail dominate this guarded quarter.", Government, Pois(
            (7, 3, "Government", "Government offices and quest assignments."),
            (3, 8, "NPC", "An official with a mission briefing."),
            (11, 8, "Government", "Castle approach."),
            (7, 12, "Enemy", "Jail guards patrol after dark."))),
        Parse(5, "Dock District", "Warehouses, waterside taverns, and contacts tied to shipping and smuggling.", Docks, Pois(
            (3, 3, "Docks", "Wharf and ships."),
            (11, 3, "Tavern", "Dockside tavern."),
            (7, 8, "NPC", "Sailor with smuggling rumors."),
            (12, 12, "Item", "A crate worth searching."))),
        Parse(6, "Crypts and Sewers", "Dark underground passages with chests, locks, and dangerous encounters.", Crypts, Pois(
            (3, 3, "Crypt", "Sewer entrance from the city."),
            (12, 4, "Item", "Locked chest."),
            (7, 8, "Enemy", "Crypt guardian."),
            (3, 12, "NPC", "A trapped or hidden contact."))),
        Parse(7, "Wilderness Roads", "Riding trails connect Hillsfar to camp, the Trading Post, ruins, and hidden paths.", Wilderness, Pois(
            (2, 3, "NPC", "Trader on the road."),
            (7, 7, "Item", "A trail-side discovery."),
            (12, 3, "Enemy", "Bandits on the road."),
            (12, 12, "NPC", "Hermit or traveller."))),
        Parse(8, "Bandit Camp", "An enemy stronghold in the hills, reached through the surrounding wilderness.", BanditCamp, Pois(
            (7, 3, "Enemy", "Bandit captain."),
            (3, 8, "Enemy", "Camp guard."),
            (11, 8, "Item", "Stolen supplies."),
            (7, 12, "NPC", "A captive with information."))),
        Parse(9, "Mage's Tower", "A magical challenge area with maze-like halls, secret rooms, and arcane treasures.", MagesTower, Pois(
            (7, 3, "NPC", "Wizard or magical guardian."),
            (3, 8, "Item", "Arcane chest."),
            (11, 8, "Enemy", "Summoned guardian."),
            (7, 12, "Shop", "Magical supplies."))),
    };

    private static IReadOnlyList<AreaPoi> Pois(params (int X, int Y, string Name, string Description)[] items) =>
        items.Select(item => new AreaPoi(item.X, item.Y, item.Name, item.Description)).ToList();

    private static AreaLevel Parse(int index, string name, string description, string[] rows,
        IReadOnlyList<AreaPoi> pois)
    {
        int width = rows.Max(row => row.Length);
        var grid = new CellKind[width, rows.Length];
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < width; x++)
                grid[x, y] = x < rows[y].Length && rows[y][x] != '#' ? CellKind.Open : CellKind.Wall;
        return new AreaLevel(index, name, description, grid, pois);
    }

    private static readonly string[] Market =
    {
        "################",
        "#..............#",
        "#.###.####.###.#",
        "#.S...#....S...#",
        "#.###.#.##.###.#",
        "#.....#..#.....#",
        "#####.##.##.####",
        "#..............#",
        "#......N.......#",
        "#.####.##.####.#",
        "#.....#..#.....#",
        "#.###.#.##.###.#",
        "#...#.......I..#",
        "#.############.#",
        "#..............#",
        "################",
    };

    private static readonly string[] Tavern =
    {
        "################",
        "#..............#",
        "#.####.##.####.#",
        "#.T..#....#..T.#",
        "#.####.##.####.#",
        "#..............#",
        "#####.####.#####",
        "#..............#",
        "#......N.......#",
        "#####.####.#####",
        "#..............#",
        "#.####.##.####.#",
        "#.T..#....#..T.#",
        "#.####.##.####.#",
        "#..............#",
        "################",
    };

    private static readonly string[] Temple =
    {
        "################",
        "#..............#",
        "#.#####..#####.#",
        "#.#....M.....#.#",
        "#.#.###..###.#.#",
        "#...#......#...#",
        "###.#.####.#.###",
        "#..............#",
        "#.N...######..N#",
        "#.....#....#...#",
        "#.###.#.##.#.###",
        "#...#........#.#",
        "#.###...N....#.#",
        "#..............#",
        "#..............#",
        "################",
    };

    private static readonly string[] Arena =
    {
        "################",
        "#..............#",
        "#.############.#",
        "#.#..........#.#",
        "#.#....AAA...#.#",
        "#.#....AAA...#.#",
        "#.#....AAA...#.#",
        "#.#..........#.#",
        "#.#..........#.#",
        "#.#..........#.#",
        "#.#..........#.#",
        "#.N..........E.#",
        "#.############.#",
        "#..............#",
        "#..............#",
        "################",
    };

    private static readonly string[] Government =
    {
        "################",
        "#..............#",
        "#.############.#",
        "#.#....GGG...#.#",
        "#.#....GGG...#.#",
        "#.#..........#.#",
        "#.#.N......G.#.#",
        "#.#..........#.#",
        "#.#..........#.#",
        "#.#..........#.#",
        "#.#.########.#.#",
        "#.#....E.....#.#",
        "#.############.#",
        "#..............#",
        "#..............#",
        "################",
    };

    private static readonly string[] Docks =
    {
        "################",
        "#..............#",
        "#.DDDD....TTTT.#",
        "#.D..D....T..T.#",
        "#.DDDD....TTTT.#",
        "#..............#",
        "#####.####.#####",
        "#..............#",
        "#......N.......#",
        "#####.####.#####",
        "#..............#",
        "#.####.##.####.#",
        "#...#.......I..#",
        "#.############.#",
        "#..............#",
        "################",
    };

    private static readonly string[] Crypts =
    {
        "################",
        "#C....#........#",
        "#####.#.######.#",
        "#.....#.....#I.#",
        "#.#########.#..#",
        "#.........#.#..#",
        "#.#######.#.####",
        "#.#.....#.#....#",
        "#.#....E#......#",
        "#.#.#####.######",
        "#.#.....#......#",
        "#.#####.######.#",
        "#..N...........#",
        "#.############.#",
        "#..............#",
        "################",
    };

    private static readonly string[] Wilderness =
    {
        "################",
        "#..............#",
        "#..N......##E..#",
        "#.......#......#",
        "#.#####.#.####.#",
        "#.....#.#....#.#",
        "#####.#.####.#.#",
        "#.....#.I....#.#",
        "#.#####.####.#.#",
        "#.#..........#.#",
        "#.#.##########.#",
        "#.#..........#.#",
        "#.#.........N#.#",
        "#.############.#",
        "#..............#",
        "################",
    };

    private static readonly string[] BanditCamp =
    {
        "################",
        "#..............#",
        "#.############.#",
        "#.#....E.....#.#",
        "#.#.########.#.#",
        "#.#.#......#.#.#",
        "#.#.#.####.#.#.#",
        "#.#E#.I..#.#E#.#",
        "#.#.#.####.#.#.#",
        "#.#.#......#.#.#",
        "#.#.########.#.#",
        "#.#....N.....#.#",
        "#.############.#",
        "#..............#",
        "#..............#",
        "################",
    };

    private static readonly string[] MagesTower =
    {
        "################",
        "#..............#",
        "#.############.#",
        "#.#....N.....#.#",
        "#.#.########.#.#",
        "#.#.#......#.#.#",
        "#.#.#.####.#.#.#",
        "#.#I#.#..#.E.#.#",
        "#.#.#.####.#.#.#",
        "#.#.#......#.#.#",
        "#.#.########.#.#",
        "#.#....S.....#.#",
        "#.############.#",
        "#..............#",
        "#..............#",
        "################",
    };
}
