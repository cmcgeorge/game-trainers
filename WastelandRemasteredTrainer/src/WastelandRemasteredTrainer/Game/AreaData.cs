namespace WastelandRemasteredTrainer.Game;

public static class AreaData
{
    public static readonly IReadOnlyList<AreaLevel> Areas = Build();

    private static IReadOnlyList<AreaLevel> Build() =>
    [
        Parse("Ranger Center", "Reference layout inspired by the Desert Rangers' home base; not extracted from live game geometry.", RangerCenter,
            (2, 2, "R", "Ranger Center", "Desert Rangers' home base."), (9, 3, "N", "Radio Room", "Promotion and field-contact hub."),
            (4, 12, "I", "Roster Room", "Character management and equipment."), (15, 15, "S", "Start", "Reference starting point.")),
        Parse("Highpool", "Reference layout for the reservoir settlement; landmark placement is guide material, not live map data.", Highpool,
            (3, 3, "T", "Highpool", "Reservoir settlement."), (13, 4, "N", "Water Pipe", "Repair the leak to gain trust."),
            (5, 14, "D", "Cave Entrance", "Underground route and early loot."), (15, 15, "I", "Chubby's Shop", "Early supplies and ammunition.")),
        Parse("Agricultural Center", "Reference layout for the besieged farming complex; this is not a confirmed coordinate atlas.", AgriculturalCenter,
            (2, 3, "T", "Agricultural Center", "Farming complex entrance."), (10, 5, "E", "Agro-Bot", "Malfunctioning harvester blocking a route."),
            (5, 14, "D", "Root Cellar", "Access to food storage."), (15, 15, "N", "Harry's House", "Mutant-pest quest lead.")),
        Parse("Rail Nomads Camp", "Reference layout for the divided railroad camp; symbols identify notable stops only.", RailNomads,
            (2, 10, "T", "Rail Nomads Camp", "Arrival from the rail line."), (8, 4, "N", "The Hobo", "Source of passwords and clues."),
            (15, 5, "I", "Casino Car", "Gambling and trade."), (13, 15, "E", "Clan Hall", "Focal point of the camp dispute.")),
        Parse("Quartz", "Reference layout for the bandit town; positions are illustrative and should not be treated as confirmed coordinates.", Quartz,
            (2, 2, "T", "Quartz", "Bandit-town approach."), (14, 3, "N", "Scott's Bar", "Contacts, brawls, and a stage challenge."),
            (5, 14, "E", "Courthouse", "Hostage and law-enforcement route."), (15, 15, "I", "Ugly's Hideout", "Fortified bandit base.")),
        Parse("Needles", "Reference layout for the ruined blood-cult city; it is a visual guide rather than decoded remaster terrain.", Needles,
            (2, 9, "T", "Needles", "Ruined city entrance."), (10, 4, "E", "Temple of Blood", "Cult headquarters and Bloodstaff lead."),
            (5, 15, "I", "Sphinx", "Monument with hidden equipment."), (15, 14, "N", "Waste Dump", "Radiation-suit lead.")),
        Parse("Las Vegas", "Reference layout for the casino city; landmark positions are intentionally non-authoritative.", LasVegas,
            (2, 3, "T", "Las Vegas", "City approach."), (9, 4, "N", "Brygo's Palace", "Robot-investigation and casino lead."),
            (4, 15, "E", "Mushroom Church", "Cult site with sewer routes."), (15, 14, "D", "Sewers", "Connection toward the Sleeper Base.")),
        Parse("Darwin", "Reference layout for Finster's hidden facility; use it for planning, not position tracking.", Darwin,
            (2, 2, "B", "Darwin", "Hidden endgame facility."), (10, 4, "N", "Science Lab", "Blackstar Key and experiment clues."),
            (5, 14, "E", "Mind Maze", "Finster's puzzle gauntlet."), (15, 15, "I", "Cloning Vats", "Cloning-equipment area.")),
        Parse("Base Cochise", "Reference layout for the final AI-controlled base; this trainer does not read or write your location.", BaseCochise,
            (2, 10, "B", "Base Cochise", "Endgame base access."), (9, 4, "E", "Assembly Line", "Robotic manufacturing zone."),
            (5, 15, "N", "Core Terminal", "Self-destruction objective."), (15, 14, "S", "Escape Pods", "Evacuation route.")),
    ];

    private static AreaLevel Parse(string name, string description, string layout,
        params (int x, int y, string symbol, string name, string description)[] pois)
    {
        var rows = layout.Trim().Split('\n').Select(row => row.Trim()).ToArray();
        foreach (var poi in pois)
        {
            var row = rows[poi.y].PadRight(20, '#').ToCharArray();
            row[poi.x] = poi.symbol[0];
            rows[poi.y] = new string(row);
        }
        var grid = new CellKind[20, 20];
        for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
            {
                char cell = y < rows.Length && x < rows[y].Length ? rows[y][x] : '#';
                grid[x, y] = cell == '#' ? CellKind.Wall : CellKind.Floor;
            }
        return new AreaLevel(name, description, grid,
            pois.Select(p => new AreaPoi(p.x, p.y, p.symbol, p.name, p.description)).ToArray());
    }

    private const string RangerCenter = """
        ####################
        #........#.........#
        #.######.#.#######.#
        #.#......#.....#...#
        #.#.#########.#.####
        #.#.........#.#....#
        #.#########.#.###.#
        #.........#.#...#.#
        #######.#.#.###.#.#
        #.....#.#.#.....#.#
        #.###.#.#.#######.#
        #.#...#.#.........#
        #.#.###.#########.#
        #.#.....#.......#.#
        #.#######.#####.#.#
        #.............#...#
        #.#############.###
        #..................#
        #..................#
        ####################
        """;

    private const string Highpool = """
        ####################
        #..................#
        #.######.#########.#
        #.#....#.#.......#.#
        #.#.##.#.#.#####.#.#
        #...##...#.....#...#
        #####.########.#####
        #.......#..........#
        #.#####.#.########.#
        #.#.....#.#......#.#
        #.#.#####.#.####.#.#
        #.#.......#.#....#.#
        #.#########.#.####.#
        #.........#.#......#
        #.#######.#.######.#
        #.......#.#........#
        #######.#.##########
        #..................#
        #..................#
        ####################
        """;

    private const string AgriculturalCenter = """
        ####################
        #....#.............#
        #.##.#.###########.#
        #.#..#.#.........#.#
        #.#.##.#.#######.#.#
        #.#....#.#.....#.#.#
        #.######.#.###.#.#.#
        #........#.#...#...#
        ########.##.#######
        #......#....#......#
        #.####.######.####.#
        #.#....#....#....#.#
        #.#.####.##.####.#.#
        #.#......##......#.#
        #.################.#
        #..................#
        #.##################
        #..................#
        #..................#
        ####################
        """;

    private const string RailNomads = """
        ####################
        #..................#
        #.################.#
        #.#..............#.#
        #.#.############.#.#
        #.#.#..........#.#.#
        #...#.########.#...#
        #####.#......#.#####
        #.....#.####.#.....#
        #.#####.#..#.#####.#
        #.#.....#..#.....#.#
        #.#.############.#.#
        #.#..............#.#
        #.################.#
        #..................#
        #.################.#
        #..................#
        #.################.#
        #..................#
        ####################
        """;

    private const string Quartz = """
        ####################
        #.........#........#
        #.#######.#.######.#
        #.#.....#.#.#....#.#
        #.#.###.#.#.#.##.#.#
        #...#...#...#..#...#
        #####.########.#####
        #........#.........#
        #.######.#.#######.#
        #.#....#.#.#.....#.#
        #.#.##.#.#.#.###.#.#
        #.#..#.#...#.#...#.#
        #.##.#.#####.#.###.#
        #....#.......#.....#
        #.################.#
        #..................#
        #.################.#
        #..................#
        #..................#
        ####################
        """;

    private const string Needles = """
        ####################
        #..................#
        #.########.#######.#
        #.#......#.#.....#.#
        #.#.####.#.#.###.#.#
        #.#.#....#.#...#.#.#
        #...#.####.###.#...#
        #####.#......#.#####
        #.....#.####.#.....#
        #.#####.#..#.#####.#
        #.#.....#..#.....#.#
        #.#.############.#.#
        #.#..............#.#
        #.################.#
        #..................#
        #.################.#
        #..................#
        #.################.#
        #..................#
        ####################
        """;

    private const string LasVegas = """
        ####################
        #.....#............#
        #.###.#.##########.#
        #.#...#.#........#.#
        #.#.###.#.######.#.#
        #.#.....#.#....#.#.#
        #.#######.#.##.#.#.#
        #.........#.#..#...#
        ########.##.#.######
        #......#....#......#
        #.####.######.####.#
        #.#....#....#....#.#
        #.#.####.##.####.#.#
        #.#......##......#.#
        #.################.#
        #..................#
        #.################.#
        #..................#
        #..................#
        ####################
        """;

    private const string Darwin = """
        ####################
        #..................#
        #.######.#########.#
        #.#....#.#.......#.#
        #.#.##.#.#.#####.#.#
        #.#.##...#.....#...#
        #.#.###########.####
        #.#.........#......#
        #.#########.#.####.#
        #.........#.#.#....#
        ########.##.#.#.####
        #......#....#.#....#
        #.####.######.####.#
        #.#....#....#....#.#
        #.#.####.##.####.#.#
        #.#......##......#.#
        #.################.#
        #..................#
        #..................#
        ####################
        """;

    private const string BaseCochise = """
        ####################
        #..................#
        #.################.#
        #.#..............#.#
        #.#.############.#.#
        #.#.#..........#.#.#
        #.#.#.########.#.#.#
        #.#.#.#......#.#.#.#
        #...#.#.####.#.#...#
        #####.#.#..#.#.#####
        #.....#.#..#.#.....#
        #.#####.####.#####.#
        #.#................#
        #.#.##############.#
        #.#.#............#.#
        #...#.##########.#.#
        #####............#.#
        #..................#
        #..................#
        ####################
        """;
}
