namespace AutoduelTrainer.Game;

public static class AreaData
{
    public static readonly IReadOnlyList<AreaLevel> Areas = Build();

    private static IReadOnlyList<AreaLevel> Build() => new List<AreaLevel>
    {
        Parse(0, "Eastern Highways", "The road network linking the eastern fortress towns.", new[]
        {
            "############", "#C.RRRR.C..#", "#.R####.R..#", "#.R.C..RR..#", "#.RRR.###R.#", "#C...R.C.R.#", "###.RRR.RR.#", "#C...#..C..#", "#.RRRRRRR..#", "#C....C....#", "#....C.....#", "############"
        }, Pois((1, 1, "Watertown", "Northern road junction."), (8, 1, "Manchester", "Road to Boston."), (4, 3, "Albany", "Central route hub."), (1, 5, "Buffalo", "Western salvage stop."), (7, 5, "Boston", "Gold Cross and arena."), (1, 7, "Pittsburgh", "Arena and assembly plant."), (8, 7, "New York", "Starting city and FBI hub."), (1, 9, "Baltimore", "Southern route."), (6, 9, "Philadelphia", "Courier route hub."), (5, 10, "Washington", "Southern terminus."))),
        Parse(1, "New York", "Starting city with a full service district and major arena.", new[]
        {
            "############", "#....R.....#", "#.##R##.###", "#.S..R..S.#", "#....R....#", "#RRRARRRRR#", "#....R....#", "#.T..R..I.#", "#.##R##.##", "#....R..N.#", "#....R....#", "############"
        }, Pois((5, 5, "New York Arena", "Fight for money and prestige."), (2, 3, "Body Shop", "Repair and upgrade your car."), (8, 3, "Weapons Shop", "Buy vehicle weapons and ammunition."), (2, 7, "Truck Stop", "Find courier missions."), (8, 7, "Cargo", "Courier pickup point."), (8, 9, "FBI Contact", "Quest and rumor lead."))),
        Parse(2, "Chicago", "A busy industrial city with an arena and vehicle services.", new[]
        {
            "############", "#....R.....#", "#.##R##.###", "#.S..R.S..#", "#....R....#", "#RRRARRR.R#", "#....R....#", "#.T..R..N.#", "#.##R##.##", "#....R..I.#", "#....R....#", "############"
        }, Pois((5, 5, "Chicago Arena", "A high-traffic deathmatch venue."), (2, 3, "Body Shop", "Industrial upgrades and repairs."), (7, 3, "Weapons Shop", "Stock up before the highway."), (2, 7, "Truck Stop", "Courier jobs west and east."), (8, 7, "Mechanic", "Local vehicle expert."), (8, 9, "Salvage", "Recover useful parts."))),
        Parse(3, "Los Angeles", "West coast destination with shops, arena fights, and long-haul jobs.", new[]
        {
            "############", "#....R.....#", "#.##R##.###", "#.S..R..S.#", "#....R....#", "#RRRARRRRR#", "#....R....#", "#.T..R..I.#", "#.##R##.##", "#..N.R....#", "#....R....#", "############"
        }, Pois((5, 5, "Los Angeles Arena", "Earn cash in west-coast events."), (2, 3, "Car Dealer", "Browse replacement vehicles."), (8, 3, "Weapons Shop", "Buy combat equipment."), (2, 7, "Truck Stop", "Long-haul courier contracts."), (8, 7, "Cargo", "West coast delivery pickup."), (3, 9, "Fixer", "Rumors and local work."))),
        Parse(4, "Boston", "East coast fortress town with cloning and vehicle support.", new[]
        {
            "############", "#....R.....#", "#.##R##.###", "#.S..R..S.#", "#....R....#", "#RRRARRRRR#", "#....R....#", "#.T..R..I.#", "#.##R##.##", "#....R..N.#", "#....R....#", "############"
        }, Pois((5, 5, "Boston Arena", "Prestige fights and championships."), (2, 3, "Assembly Plant", "Build or buy a vehicle."), (8, 3, "Gold Cross", "Clone and restore your driver."), (2, 7, "Truck Stop", "Courier jobs to New York and beyond."), (8, 7, "Cargo", "Delivery collection."), (8, 9, "Joe's Bar", "Hear rumors."))),
        Parse(5, "Detroit", "Industrial center offering stronger vehicle upgrades.", new[]
        {
            "############", "#....R.....#", "#.##R##.###", "#.S..R..S.#", "#....R....#", "#RRRARRRRR#", "#....R....#", "#.T..R..I.#", "#.##R##.##", "#..N.R....#", "#....R....#", "############"
        }, Pois((5, 5, "Detroit Arena", "Industrial city arena."), (2, 3, "Upgrade Shop", "Improve chassis, suspension, and plant."), (8, 3, "Weapons Shop", "Heavy weapons and ammo."), (2, 7, "Truck Stop", "Courier dispatch."), (8, 7, "Parts Cache", "Salvageable vehicle parts."), (3, 9, "Mechanic", "Expert repairs."))),
        Parse(6, "Houston", "Southern city with busy highways and lucrative courier contracts.", new[]
        {
            "############", "#....R.....#", "#.##R##.###", "#.S..R..S.#", "#....R....#", "#RRRARRRRR#", "#....R....#", "#.T..R..I.#", "#.##R##.##", "#....R..N.#", "#....R....#", "############"
        }, Pois((5, 5, "Houston Arena", "Southern deathmatch circuit."), (2, 3, "Body Shop", "Repair highway damage."), (8, 3, "Weapons Shop", "Restock combat gear."), (2, 7, "Truck Stop", "Courier contracts."), (8, 7, "Cargo", "Mission pickup."), (8, 9, "Contact", "Local information broker."))),
        Parse(7, "Outlaw Highway", "A dangerous long-distance highway where hostile vehicles and salvage appear.", new[]
        {
            "############", "#RRRRRRRRRR#", "#R###R###R#", "#R...R...R#", "#R.#####.R#", "#R...I...R#", "#R.#####.R#", "#R...N...R#", "#R###R###R#", "#RRRRRRRRRR#", "#C........#", "############"
        }, Pois((5, 5, "Wreckage", "Search for salvage after a battle."), (5, 7, "Outlaw Patrol", "Random hostile encounter."), (1, 10, "Fortress Town", "Return to city services."))),
    };

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
                grid[x, y] = cell switch { '#' => CellKind.Wall, 'R' => CellKind.Road, _ => CellKind.Open };
            }
        return new AreaLevel(index, name, description, grid, pois);
    }

    private static IReadOnlyList<AreaPoi> Pois(params (int x, int y, string name, string description)[] items) =>
        items.Select(item => new AreaPoi(item.x, item.y, item.name, item.description)).ToList();
}
