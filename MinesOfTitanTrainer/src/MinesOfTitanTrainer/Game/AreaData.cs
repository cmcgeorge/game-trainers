namespace MinesOfTitanTrainer.Game;

public static class AreaData
{
    private static IReadOnlyList<AreaLevel>? _areas;
    public static IReadOnlyList<AreaLevel> Areas => _areas ??= Build();

    private static IReadOnlyList<AreaLevel> Build() =>
    [
        Parse(0, "Landing Site", "Your arrival point on Titan. Search the wreckage before entering the station.", LandingSite, Pois((2, 1, "Start", "The expedition begins here."), (12, 2, "Supply Cache", "Emergency oxygen and basic tools."), (14, 9, "Station Gate", "The route into the abandoned station."))),
        Parse(1, "Abandoned Station", "A deserted research outpost containing equipment, records, and a route into the tunnels.", Station, Pois((3, 2, "Medical Locker", "Supplies for surviving Titan's hazards."), (6, 6, "Station Survivor", "An NPC with information about the research outpost."), (9, 5, "Research Terminal", "A puzzle terminal containing clues."), (13, 9, "Tunnel Lift", "Descends to the underground tunnels."))),
        Parse(2, "Underground Tunnels", "A maze of mining passages below the surface. Keep enough oxygen for the return trip.", Tunnels, Pois((2, 2, "Lift Exit", "Connection back to the abandoned station."), (8, 5, "Cave-In", "A hazardous blocked passage."), (13, 9, "Crystal Vein", "A valuable mineral deposit."))),
        Parse(3, "Mine Shafts", "Excavation galleries and machinery beneath the tunnels.", MineShafts, Pois((2, 2, "Freight Lift", "Access from the tunnels."), (9, 4, "Mining Robot", "A malfunctioning robot blocks the machinery."), (13, 9, "Power Console", "Restores power to deeper routes."))),
        Parse(4, "Ice Caverns", "Frozen caverns where thin ice and cold make every crossing dangerous.", IceCaverns, Pois((3, 2, "Frozen Cache", "An important item preserved in ice."), (8, 5, "Ice Bridge", "A hazardous crossing."), (13, 9, "City Passage", "Leads toward the alien ruins."))),
        Parse(5, "Alien City", "Ancient streets and sealed structures built by Titan's vanished inhabitants.", AlienCity, Pois((2, 2, "Ruin Entrance", "Arrival from the ice caverns."), (8, 4, "Alien Guardian", "A hostile defender patrols the plaza."), (13, 9, "Temple Door", "Requires the correct crystal or command."))),
        Parse(6, "Observation Dome", "A high vantage point overlooking the city and the distant crash site.", ObservationDome, Pois((3, 2, "Dome Access", "Elevator from the alien city."), (9, 4, "Navigation Display", "Reveals the crash site route."), (13, 9, "Signal Console", "A puzzle point for contacting rescue."))),
        Parse(7, "Shuttle Crash Site", "The damaged rescue shuttle and scattered equipment lie in a hostile ravine.", CrashSite, Pois((2, 2, "Ravine Entry", "Route from the observation dome."), (8, 5, "Shuttle Wreck", "Search for repair parts and a distress beacon."), (13, 9, "Hostile Creature", "An enemy guarding the exit."))),
        Parse(8, "Alien Temple", "The central structure of the ruins, filled with ancient mechanisms and defenses.", Temple, Pois((2, 2, "Temple Entrance", "The sealed door from the alien city."), (8, 4, "Crystal Socket", "Place recovered crystals to unlock the inner chamber."), (13, 9, "Inner Sanctum", "The path to the control center."))),
        Parse(9, "Control Center", "The final complex where Titan's systems and the escape route can be restored.", ControlCenter, Pois((2, 2, "Sanctum Access", "Entrance from the alien temple."), (8, 5, "Main Control", "Solve the final systems puzzle."), (13, 9, "Launch Link", "Activate the route to escape Titan."))),
    ];

    private static IReadOnlyList<AreaPoi> Pois(params (int x, int y, string name, string desc)[] items) =>
        items.Select(item => new AreaPoi(item.x, item.y, item.name, item.desc)).ToList();

    private static AreaLevel Parse(int index, string name, string description, string[] rows,
        IReadOnlyList<AreaPoi> pois)
    {
        int height = rows.Length;
        int width = rows.Max(row => row.Length);
        var grid = new CellKind[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[x, y] = x < rows[y].Length && rows[y][x] != '#' ? CellKind.Floor : CellKind.Wall;
        return new AreaLevel(index, name, description, grid, pois);
    }

    private static readonly string[] LandingSite =
    ["################", "#S.............#", "#.####.#####.I.#", "#....#.....#..#", "####.#####.#.##", "#.......#.....#", "#.#####.####..#", "#.#...#....#..#", "#.#.#.####.#..#", "#...#......#..#", "#.##########.P#", "################"];
    private static readonly string[] Station =
    ["################", "#..............#", "#..I.#####.##..#", "#.##.#...#..#.#", "#....#.P.#..#.#", "#.######.####.#", "#.#...N......##", "#.#.########..#", "#...#......#..#", "#.###.####.#.P#", "#..............#", "################"];
    private static readonly string[] Tunnels =
    ["################", "#..............#", "#.P.######.##.#", "#.#......#..#.#", "#.#.####.##.#.#", "#...#..X....#.#", "#####.######..#", "#......#......#", "#.####.#.####.#", "#.#....#....I.#", "#..............#", "################"];
    private static readonly string[] MineShafts =
    ["################", "#..............#", "#.P.######.##.#", "#.#......#..#.#", "#.#.####.E#.#.#", "#...#......#..#", "#####.######..#", "#......#......#", "#.####.#.####.#", "#.#....#....P.#", "#..............#", "################"];
    private static readonly string[] IceCaverns =
    ["################", "#..............#", "#..I.#####.##.#", "#.##.#...#..#.#", "#....#.X.#..#.#", "#.######.####.#", "#.#..........##", "#.#.########..#", "#...#......#..#", "#.###.####.#.P#", "#..............#", "################"];
    private static readonly string[] AlienCity =
    ["################", "#..............#", "#.P.######.##.#", "#.#......#..#.#", "#.#.####.E#.#.#", "#...#......#..#", "#####.######..#", "#......#......#", "#.####.#.####.#", "#.#....#....P.#", "#..............#", "################"];
    private static readonly string[] ObservationDome =
    ["################", "#..............#", "#..P.#####.##.#", "#.##.#...#..#.#", "#....#.I.#..#.#", "#.######.####.#", "#.#..........##", "#.#.########..#", "#...#......#..#", "#.###.####.#.P#", "#..............#", "################"];
    private static readonly string[] CrashSite =
    ["################", "#..............#", "#.P.######.##.#", "#.#......#..#.#", "#.#.####.##.#.#", "#...#..I....#.#", "#####.######..#", "#......#......#", "#.####.#.####.#", "#.#....#....E.#", "#..............#", "################"];
    private static readonly string[] Temple =
    ["################", "#..............#", "#.P.######.##.#", "#.#......#..#.#", "#.#.####.I#.#.#", "#...#......#..#", "#####.######..#", "#......#......#", "#.####.#.####.#", "#.#....#....P.#", "#..............#", "################"];
    private static readonly string[] ControlCenter =
    ["################", "#..............#", "#..P.#####.##.#", "#.##.#...#..#.#", "#....#.P.#..#.#", "#.######.####.#", "#.#..........##", "#.#.########..#", "#...#......#..#", "#.###.####.#.P#", "#..............#", "################"];
}
