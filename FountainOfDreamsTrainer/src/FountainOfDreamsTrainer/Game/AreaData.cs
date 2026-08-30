namespace FountainOfDreamsTrainer.Game;

public static class AreaData
{
    public static readonly IReadOnlyList<AreaLevel> Levels = Build();

    private static IReadOnlyList<AreaLevel> Build() =>
    [
        Parse(0, "Miami", "The main city hub, with services and routes into the ruins.",
            [
                "####################", "#........T.........#", "#.####.#####.####..#", "#.#..#.....#....#..#", "#.#N.#####.####.#..#",
                "#.#..............#.#", "#.######.#########.#", "#......#...........#", "#.####.#####.#####.#", "#.#....#...#.....#.#",
                "#.#.####.N.#####.#.#", "#.#......#.......#.#", "#.######.#######.#.#", "#......#.......#...#", "#.####.#######.###.#",
                "#....T.......N.....#", "####################"
            ], Pois((9, 1, "North Gate", "Exit toward the Florida wasteland."), (3, 4, "Tavern", "A place to trade rumors and recruit help."), (10, 10, "Temple", "A refuge for healing and treatment."), (5, 15, "Market Gate", "Route toward the southern districts."), (13, 15, "Mechanic", "Repairs and technical services."))),
        Parse(1, "Miami Sewers", "Flooded tunnels below Miami, occupied by vermin, mutants, and scavengers.",
            [
                "####################", "#S....#............#", "#####.#.##########.#", "#.....#....#.....#.#", "#.########.#.###.#.#",
                "#.#......#.#.#...#.#", "#.#.####.#.#.#.###.#", "#...#..#.#...#...#.#", "#####..#.#####.###.#", "#...#..#.....#.....#",
                "#.#.#########.#####.", "#.#.....I...#.....#.", "#.#####.###.#####.#.", "#.....#...#.....#.#.", "#.###.###.#####.#.#.",
                "#...#.......E...#..T", "####################"
            ], Pois((1, 1, "Sewer Entry", "Access from Miami."), (8, 11, "Cache", "Supplies left by earlier scavengers."), (15, 15, "Mutant Nest", "A dangerous concentration of hostile mutants."), (19, 15, "Drain Exit", "A route back to the surface."))),
        Parse(2, "Quartz", "A small settlement where cautious travelers barter for supplies.",
            [
                "####################", "#...........T......#", "#.#####.#####.###.#", "#.#...#.....#...#.#", "#.#.N.#####.###.#.#",
                "#.#.............#.#", "#.#########.#####.#", "#.....#...........#", "#.###.#.#########.#", "#...#.#.#.......#.#",
                "###.#.#.#.#####.#.#", "#...#...#...N.#...#", "#.###########.###.#", "#.......T.........#", "####################"
            ], Pois((13, 1, "East Road", "Leads into the desert."), (4, 4, "Trading Post", "Basic equipment and provisions."), (12, 11, "Town Elder", "Knows local routes and dangers."), (8, 13, "South Gate", "Road toward the swamp."))),
        Parse(3, "Needles", "A desert settlement near rumors of the Fountain of Dreams.",
            [
                "####################", "#T.................#", "#.#####.#####.###.#", "#.#...#.....#...#.#", "#.#.N.#####.###.#.#",
                "#.#.............#.#", "#.#########.#####.#", "#.....#.....#.....#", "#.###.#.###.#.###.#", "#...#.#.#I#.#...#.#",
                "###.#.#.###.###.#.#", "#...#...N.....#...#", "#.###########.###.#", "#...............T.#", "####################"
            ], Pois((1, 1, "West Gate", "Road back toward Quartz."), (4, 4, "Guide", "A local who knows the desert paths."), (9, 9, "Water Cache", "A valuable reserve for desert travel."), (8, 11, "Fountain Rumors", "Travelers describe a garden beyond the dunes."), (16, 13, "East Gate", "Route toward the irradiated desert."))),
        Parse(4, "Irradiated Desert", "Open wasteland where radiation and raiders make every crossing hazardous.",
            [
                "####################", "#T....X....X.......#", "#.####.####.#####.#", "#....X....#.....#.#", "#####.###.#####.#.#",
                "#...#...#.....#.#.#", "#.X.###.#####.#.#.#", "#...#...#...#.#...#", "###.#.#####.#.###.#", "#...#...X.#.#...#.#",
                "#.#####.###.###.#.#", "#.....#...#...#.#.#", "#.###.###.###.#.#.#", "#...#...X.....#...#", "#.E.#####.#####.###", "#.............T....#", "####################"
            ], Pois((1, 1, "Needles Road", "The safer western approach."), (6, 3, "Radiation Field", "Hazardous ground; protective gear is recommended."), (2, 6, "Ruined Convoy", "Search for useful salvage."), (8, 13, "Hot Zone", "Severe irradiation blocks the direct route."), (2, 14, "Raider Ambush", "Hostile raiders patrol this passage."), (14, 15, "Garden Trail", "A narrow route toward the Fountain."))),
        Parse(5, "The Garden", "An overgrown sanctuary surrounding the Fountain of Dreams.",
            [
                "####################", "#........T.........#", "#.#####.#####.###.#", "#.#...#.....#...#.#", "#.#.N.#####.###.#.#",
                "#.#.............#.#", "#.#########.#####.#", "#.....#.....#.....#", "#.###.#.###.#.###.#", "#...#.#.#F#.#...#.#",
                "###.#.#.###.###.#.#", "#...#...I.....#...#", "#.###########.###.#", "#.................#", "####################"
            ], Pois((9, 1, "Desert Entrance", "The path from the irradiated desert."), (4, 4, "Guardian", "A wary inhabitant of the garden."), (9, 9, "Fountain of Dreams", "The central mystery and possible cure for mutations."), (8, 11, "Garden Relic", "An important clue or quest item."))),
        Parse(6, "Robot Factory", "An automated pre-war facility with hostile machinery and locked workshops.",
            [
                "####################", "#T.....#............#", "#.#####.#.#########.#", "#.....#.#.#.......#.#", "#####.#.#.#.#####.#.#",
                "#...#.#.#.#.#E..#.#.#", "#.###.#.#.#.###.#.#.#", "#.#...#...#...#.#...#", "#.#.#########.#.#####", "#.#.....I.....#.....#",
                "#.#####.###########.#", "#.....#.......#.....#", "#.###.#######.#.###.#", "#...#.........#...#.#", "#.###############.#.#", "#.................#.#", "####################"
            ], Pois((1, 1, "Factory Gate", "Entry from the wasteland."), (13, 5, "Security Robots", "Automated defenders patrol the production floor."), (7, 9, "Workshop Cache", "Tools and machine parts may be useful."), (1, 15, "Service Exit", "A maintenance route to the swamp."))),
        Parse(7, "Mutant Swamp", "Flooded wetlands where toxic pools conceal dangerous creatures.",
            [
                "####################", "#T....X...........#", "#.####.#####.###.#", "#....#.....#...#.#", "#.##.#####.###.#.#",
                "#.#..X....#.....#.#", "#.#.#####.#######.#", "#.#.....#.........#", "#.#####.#####.###.#", "#.....#.....#...#.#",
                "#.###.#####.#.#.#.#", "#...#....X..#.#...#", "###.#########.###.#", "#...E...........I.#", "####################"
            ], Pois((1, 1, "Swamp Edge", "The road from Quartz and the factory."), (5, 5, "Toxic Pool", "Irradiated water; avoid without protection."), (9, 11, "Sunken Hazard", "Unstable ground and toxic sludge."), (4, 13, "Mutant Pack", "Aggressive mutants defend this territory."), (16, 13, "Lost Supplies", "Searchable remains near the eastern trail."))),
        Parse(8, "Florida Wasteland", "Open routes between settlements, ruins, and hostile territory.",
            [
                "####################", "#S.................#", "#.#####.#####.###.#", "#.....#.....#...#.#", "#####.#####.###.#.#",
                "#...#.....#.....#.#", "#.#######.#######.#", "#.......#.........#", "#.#####.#####.###.#", "#.#...#.....#...#.#",
                "#.#.E.#####.###.#.#", "#.#.............#.#", "#.#########.#####.#", "#.....T.....T.....#", "####################"
            ], Pois((1, 1, "Starting Road", "The first route beyond the settled areas."), (4, 10, "Raider Patrol", "Avoid or prepare for combat."), (6, 13, "Miami Route", "Return to the city hub."), (12, 13, "Quartz Route", "Road to the small settlement."))),
        Parse(9, "Ruined Coast", "Collapsed buildings and contaminated shorelines hide both salvage and danger.",
            [
                "####################", "#T..............X.#", "#.#####.#####.##.#", "#.....#.....#....#", "#####.#####.####.#",
                "#...#.....#....#.#", "#.#######.####.#.#", "#.......#......#.#", "#.#####.#####.##.#", "#.#...#.....#....#",
                "#.#.E.#####.####.#", "#.#......I......#.#", "#.#########.#####.#", "#.................#", "####################"
            ], Pois((1, 1, "Coastal Road", "Return route to the wasteland."), (17, 1, "Contaminated Shore", "Irradiated water and unstable ruins."), (4, 10, "Raider Camp", "A hostile camp among the wreckage."), (9, 11, "Pre-war Cache", "Important salvage in a collapsed building."))),
    ];

    private static IReadOnlyList<AreaPoi> Pois(params (int x, int y, string name, string desc)[] items) =>
        items.Select(i => new AreaPoi(i.x, i.y, i.name, i.desc)).ToList();

    private static AreaLevel Parse(int index, string name, string description, string[] rows,
        IReadOnlyList<AreaPoi> pois)
    {
        int height = rows.Length;
        int width = rows.Max(row => row.Length);
        var grid = new CellKind[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                char cell = x < rows[y].Length ? rows[y][x] : '#';
                grid[x, y] = cell == '#' ? CellKind.Wall : CellKind.Floor;
            }
        return new AreaLevel(index, name, description, grid, pois);
    }
}
