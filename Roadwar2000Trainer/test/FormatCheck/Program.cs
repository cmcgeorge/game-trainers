using System.IO;
using GameTrainers.Common.Memory;
using Roadwar2000Trainer.Game;
using Roadwar2000Trainer.Memory;
using Roadwar2000Trainer.ViewModels;

namespace Roadwar2000Trainer.FormatCheck;

/// <summary>
/// Headless verification for the Roadwar 2000 trainer.
/// <para>
/// Everything here runs without the game: a synthetic slab is built from the reference tables and
/// driven through the same record views the GUI uses. When a real game folder is present (pass it
/// as the first argument, or let the default probe find it) the shipped <c>CHICAGO.RWS</c> and the
/// two <c>.MAP</c> files are checked as well, which is what pins the offsets to real data rather
/// than to the harness's own idea of them.
/// </para>
/// <para>
/// <c>--live</c> additionally attaches to a running DOSBox and verifies the locator end to end.
/// It is opt-in because it needs a game running, and it is skipped rather than failed when there
/// is none.
/// </para>
/// </summary>
internal static class Program
{
    private static int _checks;
    private static int _failures;

    private static void Check(bool condition, string what)
    {
        _checks++;
        if (condition) return;
        _failures++;
        Console.Error.WriteLine($"  FAIL  {what}");
    }

    private static void CheckEqual<T>(T expected, T actual, string what)
    {
        _checks++;
        if (EqualityComparer<T>.Default.Equals(expected, actual)) return;
        _failures++;
        Console.Error.WriteLine($"  FAIL  {what}: expected {expected}, got {actual}");
    }

    private static int Main(string[] args)
    {
        bool live = args.Contains("--live");
        string? folder = args.FirstOrDefault(a => !a.StartsWith("--")) ?? FindGameFolder();

        Console.WriteLine("Roadwar 2000 trainer - format checks");
        Console.WriteLine(new string('-', 60));

        CheckFormatConstants();
        CheckVehicleBook();
        CheckCityBook();
        CheckLootBook();
        CheckReferenceBooks();
        CheckSyntheticSlab();
        CheckOverlandGeometry();

        if (folder is not null && Directory.Exists(folder))
        {
            Console.WriteLine($"Game folder: {folder}");
            CheckShippedSave(folder);
            CheckShippedCityTable(folder);
            CheckShippedMaps(folder);
        }
        else
        {
            Console.WriteLine("Game folder not found - skipping the checks against the shipped data files.");
            Console.WriteLine($"Pass the Roadwar 2000 folder as the first argument, or set " +
                              $"{SaveEditorViewModel.FolderEnvironmentVariable}, to run them.");
        }

        if (live) CheckLive();

        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{_checks - _failures}/{_checks} checks passed.");
        if (_failures > 0) Console.Error.WriteLine($"{_failures} FAILED.");
        return _failures == 0 ? 0 : 1;
    }

    // ---- offline checks ------------------------------------------------------

    private static void CheckFormatConstants()
    {
        Console.WriteLine("Format constants");
        CheckEqual(6512, SaveFormat.SlabLength, "slab length");
        CheckEqual(0x21BA, SaveFormat.DsBase, "slab base in the data segment");
        CheckEqual(0x2254, SaveFormat.DsVehicleNames, "vehicle-name anchor address");
        CheckEqual(50, SaveFormat.VehicleRecordLength, "vehicle record length");
        CheckEqual(12, SaveFormat.CityRecordLength, "city record length");
        CheckEqual(24, SaveFormat.VehicleTypeRecordLength, "vehicle template length");

        // The tables have to fit inside the slab, in order, without overlapping.
        Check(SaveFormat.VehicleTypeTable + SaveFormat.VehicleTypeCount * SaveFormat.VehicleTypeRecordLength
              <= SaveFormat.LootTable, "vehicle template table ends before the loot table");
        Check(SaveFormat.CityTable + SaveFormat.CityCount * SaveFormat.CityRecordLength
              <= SaveFormat.GangName, "city table ends before the gang block");
        Check(SaveFormat.VehicleTable + SaveFormat.MaxVehicleSlots * SaveFormat.VehicleRecordLength
              <= SaveFormat.SlabLength, "vehicle records fit inside the slab");

        // The crew array is deliberately at an odd offset; Lattice C packs its structs.
        Check(SaveFormat.Crew % 2 == 1, "crew array sits at an odd offset (packed struct)");
        CheckEqual(SaveFormat.Crew + SaveFormat.CrewRankCount * 2, SaveFormat.Medical,
                   "medical supplies follow the five crew words");

        // Clock: the day starts at 6 AM.
        CheckEqual(6, GameFacts.HourOf(0), "hour index 0 is 6 AM");
        CheckEqual("6:00 AM", GameFacts.ClockOf(0), "clock at index 0");
        CheckEqual("2:00 PM", GameFacts.ClockOf(8), "clock at index 8");
        CheckEqual("12:00 AM", GameFacts.ClockOf(18), "clock at index 18 (midnight)");

        // TimeIndexOf has to wrap, not clamp: hours 0-5 are the tail of the game day, indices 18-23.
        for (int index = 0; index < 24; index++)
            CheckEqual(index, GameFacts.TimeIndexOf(GameFacts.HourOf(index) % 24),
                       $"time index {index} round-trips through the clock");
    }

    private static void CheckVehicleBook()
    {
        Console.WriteLine("Vehicle table");
        CheckEqual(19, VehicleBook.All.Count, "19 vehicle types");
        CheckEqual("MOTORCYCLE", VehicleBook.All[0].Name, "type 0");
        CheckEqual("TRAILER TRUCK", VehicleBook.All[18].Name, "type 18");

        for (int i = 0; i < VehicleBook.All.Count; i++)
            CheckEqual(i, VehicleBook.All[i].Id, $"type {i} knows its own id");

        // Spot values against the printed manual table.
        var moto = VehicleBook.All[0];
        CheckEqual(1, moto.Mass, "motorcycle mass");
        CheckEqual(3, moto.Structure, "motorcycle structure");
        CheckEqual(100, moto.MaxSpeedMph, "motorcycle top speed");
        CheckEqual(1, moto.Volleys, "motorcycle fires one volley");
        CheckEqual(2, moto.Tires, "motorcycle tires");

        var rig = VehicleBook.All[18];
        CheckEqual(20, rig.Mass, "trailer truck mass");
        CheckEqual(50, rig.Structure, "trailer truck structure");
        CheckEqual(18, rig.Tires, "trailer truck tires");
        CheckEqual(51, rig.DisplayInteriorCapacity, "trailer truck interior capacity as displayed");
        CheckEqual(10, rig.FuelConsumption, "trailer truck fuel consumption");

        // Treads are stored as zero tires and cannot be shot out.
        CheckEqual(0, VehicleBook.All[15].Tires, "tractor runs on treads");
        CheckEqual(0, VehicleBook.All[16].Tires, "construction vehicle runs on treads");

        // Carrying capacity was measured to be exactly 5 x mass^2 for every type.
        var manualCapacity = new[] { 5, 20, 45, 45, 125, 125, 80, 80, 180, 320, 245, 405, 180, 180, 980, 500, 1620, 1280, 2000 };
        for (int i = 0; i < VehicleBook.All.Count; i++)
            CheckEqual(manualCapacity[i], VehicleBook.All[i].CarryingCapacity,
                       $"{VehicleBook.All[i].Name} carrying capacity matches the manual");
    }

    private static void CheckCityBook()
    {
        Console.WriteLine("City table");
        CheckEqual(120, CityBook.All.Count, "120 cities");
        CheckEqual("LOUISVILLE", CityBook.All[0].Name, "city 0");
        CheckEqual("CHICAGO", CityBook.All[52].Name, "city 52");

        var chicago = CityBook.All[52];
        CheckEqual(2, chicago.Map, "Chicago is on the east map");
        CheckEqual(12, chicago.X, "Chicago X");
        CheckEqual(13, chicago.Y, "Chicago Y");
        CheckEqual(chicago, CityBook.At(2, 12, 13), "lookup by square finds Chicago");
        Check(CityBook.At(2, 1, 0) is null, "an empty square has no city");

        // Every city must sit on exactly one map, and no two share a square.
        var seen = new HashSet<(int, int, int)>();
        foreach (var c in CityBook.All)
        {
            Check(c.Map is 1 or 2, $"{c.Name} is on a known map");
            // X = 0 is legal in the shipped table -- HOUSTON has it, and the engine's flat index
            // wraps it onto the previous row. See the HOUSTON checks in CheckShippedMaps.
            Check(c.X >= 0 && c.X <= OverlandMap.Width, $"{c.Name} X in range");
            Check(c.Y >= 0 && c.Y < OverlandMap.Height, $"{c.Name} Y in range");
            Check(seen.Add((c.Map, c.X, c.Y)), $"{c.Name} does not share a square with another city");
        }

        CheckEqual(228, CityBook.All.Max(c => c.Size), "New York is the largest city");

        // The size column has to be the pristine one out of START.EXE, not the shipped save's,
        // whose 30 looted towns would make "restock to the shipped level" restore a looted level
        // and make the two towns that were stripped to zero unrestockable. Spot-check the three
        // largest discrepancies; CheckShippedSave pins the whole column when the EXE is present.
        CheckEqual(178, CityBook.All[52].Size, "Chicago's shipped size (the save holds 150)");
        CheckEqual(77, CityBook.All[89].Size, "Washington DC's shipped size (the save holds 54)");
        CheckEqual(8, CityBook.All[74].Size, "Ottawa's shipped size (the save holds 0)");
        Check(CityBook.All.All(c => c.Size > 0), "no town ships with a zero supply level");
    }

    private static void CheckLootBook()
    {
        Console.WriteLine("Loot table");
        CheckEqual(28, LootBook.All.Count, "28 loot sites");
        CheckEqual("CONVENIENCE STORE", LootBook.All[0].Name, "site 0");
        CheckEqual("RACING TRACK", LootBook.All[27].Name, "site 27");

        // The two sites whose payout is unambiguous, which is what pinned the yield columns.
        var gunShop = LootBook.All.First(s => s.Name == "GUN SHOP");
        Check(gunShop.Guns > 0 && gunShop.Food == 0, "a gun shop pays guns and no food");
        var armory = LootBook.All.First(s => s.Name == "ARMORY");
        Check(armory.Guns > gunShop.Guns, "an armory out-pays a gun shop");

        var farm = LootBook.All.First(s => s.Name == "FARM");
        var ranch = LootBook.All.First(s => s.Name == "RANCH");
        Check(farm.FreqFarmland > farm.FreqPlains, "farms are commoner in farmland");
        Check(ranch.FreqPlains > ranch.FreqFarmland, "ranches are commoner on the plains");

        // Each payout column is pinned by a site that pays in it and in nothing else. These are the
        // assertions that would have caught the columns being labelled one position off.
        var tireStore = LootBook.All.First(s => s.Name == "TIRE STORE");
        Check(tireStore.Tires > 0, "a tire store pays tires");
        Check(tireStore.Fuel == 0 && tireStore.Food == 0 && tireStore.Guns == 0 && tireStore.Medical == 0,
              "and a tire store pays nothing else");

        // The food column, pinned the same way. SHELTER is deliberately not used: it pays 50 food
        // *and* 1 medical, and is the one site that pays in two columns without being a cache.
        foreach (var name in new[] { "SUPERMARKET", "CONVENIENCE STORE", "RESTAURANT" })
        {
            var site = LootBook.All.First(s => s.Name == name);
            Check(site.Food > 0, $"a {name.ToLowerInvariant()} pays food");
            Check(site.Guns == 0 && site.Tires == 0 && site.Fuel == 0 && site.Medical == 0,
                  $"and a {name.ToLowerInvariant()} pays nothing else");
        }
        var shelter = LootBook.All.First(s => s.Name == "SHELTER");
        Check(shelter.Food == 50 && shelter.Medical == 1,
              "a shelter pays 50 food and 1 medical - the one two-column site that is not a cache");

        var fuelTank = LootBook.All.First(s => s.Name == "FUEL STORAGE TANK");
        Check(fuelTank.Fuel > 0, "a fuel storage tank pays fuel");
        Check(fuelTank.Tires == 0 && fuelTank.Medical == 0, "and a fuel storage tank pays nothing else");

        foreach (var name in new[] { "MEDICAL CENTER", "HOSPITAL", "VETERINARIAN", "DRUG STORE" })
        {
            var site = LootBook.All.First(s => s.Name == name);
            Check(site.Medical > 0, $"a {name.ToLowerInvariant()} pays medical supplies");
            Check(site.Food == 0 && site.Guns == 0 && site.Tires == 0 && site.Fuel == 0,
                  $"and a {name.ToLowerInvariant()} pays nothing else");
        }

        // CACHE corroborates all five supply columns at once: a stash pays a little of everything.
        var cache = LootBook.All.First(s => s.Name == "CACHE");
        Check(cache.Food > 0 && cache.Guns > 0 && cache.Tires > 0 && cache.Fuel > 0 && cache.Medical > 0,
              "a cache pays in every supply column");

        // The vehicle flag is 1 at exactly the eight sites where a vehicle can turn up.
        var vehicleSites = LootBook.All.Where(s => s.PaysVehicle).Select(s => s.Name).ToHashSet();
        CheckEqual(8, vehicleSites.Count, "eight sites can turn up a vehicle");
        foreach (var name in new[] { "BODY SHOP", "AUTO DEALER", "BUS DEPOT", "TAXI GARAGE", "RACING TRACK" })
            Check(vehicleSites.Contains(name), $"{name.ToLowerInvariant()} is one of them");
    }

    private static void CheckReferenceBooks()
    {
        Console.WriteLine("Reference books");
        CheckEqual(23, TerrainBook.Names.Count, "23 terrain codes");
        CheckEqual("Plains", TerrainBook.NameOf(0), "terrain 0");
        CheckEqual("Farmland", TerrainBook.NameOf(1), "terrain 1");
        Check(TerrainBook.IsRoad(7) && TerrainBook.IsRoad(18) && !TerrainBook.IsRoad(19),
              "road codes are 7 through 18");
        Check(TerrainBook.IsCity(19) && TerrainBook.IsCity(21) && !TerrainBook.IsCity(22),
              "city codes are 19 through 21");
        Check(!TerrainBook.IsPassable(TerrainBook.Water), "water is impassable");
        Check(!TerrainBook.IsPassable(TerrainBook.Wilderness), "wilderness is impassable");
        Check(!TerrainBook.IsPassable(31), "scenery codes above the table are impassable");
        Check(TerrainBook.IsPassable(TerrainBook.Oilfield), "oilfields are passable");

        CheckEqual(5, RankBook.Names.Count, "five crew ranks");
        CheckEqual("Armsmaster", RankBook.NameOf(0), "best rank first");
        CheckEqual(10, ResidentBook.Names.Count, "ten resident factions");
        CheckEqual("No one", ResidentBook.NameOf(ResidentBook.NoOne), "faction 0 is nobody");
        Check(ResidentBook.NameOf(17).Contains("17"), "an unnamed faction is reported by number");
        CheckEqual(8, ScientistBook.Names.Count, "eight scientists");
        CheckEqual(11, RoadGangBook.Names.Count, "eleven named road gangs");
        CheckEqual(7, FootgangBook.Names.Count, "seven foot-gang types");
        CheckEqual(6, ImprovementBook.Names.Count, "six upgrade shops");
    }

    /// <summary>
    /// Builds a slab from the reference tables, then drives every record view over it. This is
    /// what proves the write path: a value set through a view has to read back through the raw
    /// bytes at the offset the format table claims.
    /// </summary>
    private static void CheckSyntheticSlab()
    {
        Console.WriteLine("Synthetic slab round-trip");
        var bytes = BuildSyntheticSlab();
        Check(GameSlab.LooksLikeSlab(bytes), "a synthetic slab passes the structural check");

        var target = new BufferTarget(bytes);
        var slab = new GameSlab(target);
        Check(slab.Refresh(), "slab refresh");
        Check(slab.LooksValid(), "refreshed slab still validates");

        var gang = new GangRecord(slab);
        gang.Name = "THE NEON KNIGHTS";
        gang.Food = 4321;
        gang.Tires = 763;
        gang.Fuel = 2035;
        gang.Ammo = 12033;
        gang.Guns = 852;
        gang.Medical = 36;
        gang.Antitoxin = 25;
        gang.Day = 261;
        gang.TimeOfDay = 8;
        gang.MaxVehicles = 9;
        gang.VehicleCount = 2;
        gang.X = 12;
        gang.Y = 13;
        gang.CurrentMap = 2;
        gang.DoctorQuality = 5;
        gang.DrillSergeantQuality = 4;
        gang.PoliticianQuality = 1;
        gang.HasRadioDirectionFinder = true;
        gang.HasSnowTires = true;
        gang.HasFuelSpecial = false;
        int[] crew = { 22, 51, 100, 100, 99 };
        for (int r = 0; r < crew.Length; r++) gang.SetCrew(r, crew[r]);

        CheckEqual("THE NEON KNIGHTS", gang.Name, "gang name round-trips");
        CheckEqual(4321, gang.Food, "food round-trips");
        CheckEqual(2035, gang.Fuel, "fuel round-trips");
        CheckEqual(12033, gang.Ammo, "ammo round-trips");
        CheckEqual(25, gang.Antitoxin, "antitoxin round-trips");
        CheckEqual(372, gang.TotalCrew, "crew total");
        CheckEqual(261, gang.Day, "day round-trips");
        CheckEqual("2:00 PM", gang.Clock, "clock reads back");
        Check(gang.HasRadioDirectionFinder, "RDF flag round-trips");
        Check(gang.HasSnowTires, "snow-tire flag round-trips");

        // Both copies of the position have to move, or the game disagrees with itself.
        CheckEqual(12, bytes[SaveFormat.PartyX], "world-header X was written");
        CheckEqual(12, bytes[SaveFormat.GangX], "gang-block X was written");
        CheckEqual(13, bytes[SaveFormat.PartyY], "world-header Y was written");
        CheckEqual(13, bytes[SaveFormat.GangY], "gang-block Y was written");

        // The raw bytes must land where the format table says.
        CheckEqual(4321, bytes[SaveFormat.Food] | (bytes[SaveFormat.Food + 1] << 8), "food at the documented offset");
        CheckEqual(22, bytes[SaveFormat.Crew] | (bytes[SaveFormat.Crew + 1] << 8), "crew[0] at the documented offset");
        CheckEqual(36, bytes[SaveFormat.Medical] | (bytes[SaveFormat.Medical + 1] << 8), "medical at the documented offset");
        CheckEqual(25, bytes[SaveFormat.Antitoxin], "antitoxin at the documented offset");
        CheckEqual(9, bytes[SaveFormat.MaxVehicles], "max vehicles at the documented offset");

        // Vehicles.
        var sports = VehicleBook.All[7];
        var rig = VehicleBook.All[18];
        var v0 = new VehicleRecord(slab, 0);
        var v1 = new VehicleRecord(slab, 1);
        Check(v0.Fill(sports), "filling slot 0");
        Check(v1.Fill(rig), "filling slot 1");
        CheckEqual(SaveFormat.VehicleTable, v0.Base, "slot 0 base");
        CheckEqual(SaveFormat.VehicleTable + 50, v1.Base, "slot 1 base");
        CheckEqual("SPORTS CAR HARDTOP", v0.TypeName, "slot 0 type");
        CheckEqual("TRAILER TRUCK", v1.TypeName, "slot 1 type");
        Check(v0.LooksValid() && v1.LooksValid(), "filled slots validate");
        CheckEqual(rig.Mass, v1.Mass, "mass copied from the template");
        CheckEqual(2000, v1.CarryingCapacity, "trailer truck carries 2,000 spaces");
        CheckEqual(80 + 2000, gang.TotalCapacity, "fleet capacity is the sum of the two");

        v1.Structure = 25;
        CheckEqual(25, v1.Structure, "damage applied");
        v1.Tires = 9;
        v1.Repair();
        CheckEqual(v1.StructureMax, v1.Structure, "repair restores structure");
        CheckEqual(v1.TiresMax, v1.Tires, "repair restores tires");

        v0.Maximize();
        for (int f = 0; f < 5; f++) CheckEqual(5, v0.GetProtection(f), $"upgrade sets armour facing {f} to 5");
        Check(v0.MaxSpeed > sports.MaxSpeed, "upgrade raises top speed");

        // An upgrade must never make a vehicle worse. A captured vehicle can already sit above the
        // caps Maximize applies, and a naive Math.Min would silently downgrade it.
        v0.MaxSpeed = 30;          // above the cap Maximize applies, as an improved capture can be
        v0.Braking = 9;
        v0.ManeuverMax = 9;
        int structureBefore = v0.StructureMax;
        v0.Maximize();
        CheckEqual(30, v0.MaxSpeed, "upgrade leaves an already-faster-than-cap vehicle alone");
        CheckEqual(9, v0.Braking, "upgrade leaves already-capped braking alone");
        CheckEqual(9, v0.ManeuverMax, "upgrade leaves already-capped manoeuvrability alone");
        Check(v0.StructureMax >= structureBefore, "upgrade never lowers structure");

        // Filling a one-volley type must not hand it a weapon for a volley it does not have.
        var motorcycle = VehicleBook.All[0];
        CheckEqual(1, motorcycle.Volleys, "the motorcycle fires one volley");
        var v2 = new VehicleRecord(slab, 2);
        Check(v2.Fill(motorcycle), "filling slot 2 with a motorcycle");
        CheckEqual(2, bytes[v2.Base + SaveFormat.VehWeaponTypes], "first volley is a firearm");
        CheckEqual(0, bytes[v2.Base + SaveFormat.VehWeaponTypes + 1], "second volley slot is left empty");
        Check(v1.Fill(rig), "refilling slot 1 with a trailer truck");
        CheckEqual(2, bytes[v1.Base + SaveFormat.VehWeaponTypes + 1], "a two-volley type gets both");

        // An empty slot must not read as a vehicle.
        var v9 = new VehicleRecord(slab, 9);
        Check(!v9.LooksValid(), "an all-zero slot is rejected");

        // Cities.
        var chicago = new CityRecord(slab, 52);
        CheckEqual("CHICAGO", chicago.Name, "city record 52 is Chicago");
        chicago.SetCache(CityRecord.CacheFood, 255);
        chicago.SetCache(CityRecord.CacheTires, 255);
        CheckEqual(255, chicago.GetCache(CityRecord.CacheFood), "cache food round-trips");
        CheckEqual(510, chicago.CacheTotal, "cache total");
        chicago.SetCache(CityRecord.CacheFuel, 999);
        CheckEqual(255, chicago.GetCache(CityRecord.CacheFuel), "cache is clamped to the engine's 255");
        chicago.Resident = 8;
        CheckEqual("Invaders", chicago.ResidentName, "resident 8 is the invaders");
        chicago.Clear();
        CheckEqual(0, chicago.Resident, "clearing a town empties it");
        CheckEqual(0, chicago.Strength, "clearing a town zeroes its strength");
        chicago.FillCache();
        CheckEqual(255 * 5, chicago.CacheTotal, "fill cache tops up all five slots");

        // A refusing target must leave the cache untouched, or the UI would show a lie.
        var readOnly = new GameSlab(new RefusingTarget(bytes));
        Check(readOnly.Refresh(), "read-only target still reads");
        var refused = new GangRecord(readOnly);
        int before = refused.Food;
        refused.Food = 1;
        CheckEqual(before, refused.Food, "a refused write does not change the cached value");
    }

    private static void CheckOverlandGeometry()
    {
        Console.WriteLine("Overland geometry");
        CheckEqual(2016, OverlandMap.CellCount, "2,016 squares");
        CheckEqual(2024, OverlandMap.FileLength, "map file length");
        CheckEqual(0, OverlandMap.Index(1, 0), "square 1,0 is index 0");
        CheckEqual(635, OverlandMap.Index(12, 13), "Chicago's square is index 635");
        CheckEqual(2015, OverlandMap.Index(48, 41), "the last square");
        Check(OverlandMap.IsInside(1, 0) && OverlandMap.IsInside(48, 41), "corners are inside");
        Check(!OverlandMap.IsInside(0, 0), "column 0 does not exist - X is 1-based");
        Check(!OverlandMap.IsInside(49, 0), "column 49 does not exist");
        Check(!OverlandMap.IsInside(1, 42), "row 42 does not exist");

        // A synthetic map exercises the party-marker mask.
        var cells = new byte[OverlandMap.CellCount];
        cells[OverlandMap.Index(12, 13)] = TerrainBook.Farmland | OverlandMap.PartyMarker;
        var map = OverlandMap.FromBytes(cells, 2, "synthetic");
        Check(map is not null, "synthetic map parses");
        CheckEqual(TerrainBook.Farmland, map![12, 13],
                   "the party marker is masked off when reading terrain");
        Check(OverlandMap.FromBytes(new byte[10], 2, "too short") is null, "a short buffer is rejected");
    }

    // ---- checks against the shipped data files -------------------------------

    private static void CheckShippedSave(string folder)
    {
        string path = Path.Combine(folder, "CHICAGO.RWS");
        if (!File.Exists(path))
        {
            Console.WriteLine("  CHICAGO.RWS not present - skipping.");
            return;
        }

        Console.WriteLine("Shipped CHICAGO.RWS");
        var save = SaveGame.Load(path, out string error);
        Check(save is not null, $"CHICAGO.RWS loads ({error})");
        if (save is null) return;

        var g = save.Gang;
        // These are the figures the game's own Gang Status screen prints for this save.
        CheckEqual("The Neon Knights", g.Name, "gang name");
        CheckEqual(640, g.Food, "food");
        CheckEqual(763, g.Tires, "tires");
        CheckEqual(2035, g.Fuel, "stored fuel");
        CheckEqual(12033, g.Ammo, "ammo");
        CheckEqual(852, g.Guns, "guns");
        CheckEqual(36, g.Medical, "medical supplies");
        CheckEqual(25, g.Antitoxin, "antitoxin");
        CheckEqual(9, g.VehicleCount, "vehicles");
        CheckEqual(9, g.MaxVehicles, "vehicle ceiling");
        CheckEqual(261, g.Day, "day");
        CheckEqual(8, g.TimeOfDay, "time of day (2:00 PM)");
        CheckEqual(2, g.CurrentMap, "on the east map");
        CheckEqual(12, g.X, "party X");
        CheckEqual(13, g.Y, "party Y");
        CheckEqual("CHICAGO", g.LocationName, "the gang is standing in Chicago");
        CheckEqual(372, g.TotalCrew, "crew total");
        CheckEqual(22, g.GetCrew(0), "armsmasters");
        CheckEqual(99, g.GetCrew(4), "escorts");
        CheckEqual(6700, g.TotalCapacity, "cargo capacity");
        CheckEqual(372, g.PassengerCapacity, "passenger capacity");
        CheckEqual(49, g.FuelConsumption, "fuel per move");
        CheckEqual(1937, g.DisplayedFuel, "fuel as the status screen prints it");
        CheckEqual(4228, g.SuppliesCarried, "total supplies as the status screen prints it");
        Check(g.DoctorQuality > 0 && g.DrillSergeantQuality > 0 && g.PoliticianQuality > 0,
              "all three cronies present");
        Check(!g.HasRadioDirectionFinder, "no RDF in this save");
        Check(g.HasSnowTires, "snow tires fitted");

        // Vehicle 1 is a sports car hardtop; vehicle 2 a trailer truck.
        var v1 = new VehicleRecord(save.Slab, 0);
        CheckEqual("SPORTS CAR HARDTOP", v1.TypeName, "vehicle 1 type");
        CheckEqual(15, v1.Structure, "vehicle 1 structure");
        CheckEqual(15, v1.StructureMax, "vehicle 1 structure max");
        CheckEqual(4, v1.Tires, "vehicle 1 tires");
        CheckEqual(14, v1.MaxSpeed, "vehicle 1 top speed (140 MPH, improved)");
        CheckEqual(4, v1.Facing, "vehicle 1 facing");
        CheckEqual(4, v1.DisplayInteriorCapacity, "vehicle 1 interior capacity");
        CheckEqual(8, v1.CrewAboard, "vehicle 1 crew");

        var v2 = new VehicleRecord(save.Slab, 1);
        CheckEqual("TRAILER TRUCK", v2.TypeName, "vehicle 2 type");
        CheckEqual(25, v2.Structure, "vehicle 2 structure");
        CheckEqual(55, v2.StructureMax, "vehicle 2 structure max (improved)");
        CheckEqual(18, v2.Tires, "vehicle 2 tires");
        CheckEqual(51, v2.DisplayInteriorCapacity, "vehicle 2 interior capacity");
        CheckEqual(50, v2.TopsideCapacity, "vehicle 2 topside capacity");

        // Chicago's own record: the save was taken standing in it, with a stocked cache.
        var chicago = new CityRecord(save.Slab, 52);
        CheckEqual(2, chicago.Map, "Chicago record map");
        CheckEqual(12, chicago.X, "Chicago record X");
        CheckEqual(13, chicago.Y, "Chicago record Y");
        CheckEqual(255, chicago.GetCache(CityRecord.CacheFood), "Chicago cache food");
        CheckEqual(255, chicago.GetCache(CityRecord.CacheTires), "Chicago cache tires");
    }

    /// <summary>
    /// Pins every baked-in city figure against START.EXE's own initialised data. This is the check
    /// that would have caught the size column having been lifted from a played save.
    /// </summary>
    private static void CheckShippedCityTable(string folder)
    {
        string path = Path.Combine(folder, "START.EXE");
        if (!File.Exists(path)) { Console.WriteLine("  START.EXE not present - skipping."); return; }

        Console.WriteLine("Shipped START.EXE city table");
        var exe = File.ReadAllBytes(path);
        const int slabInExe = 0xA48A;      // file offset of slab byte 0; see the RE notes, section 1
        int table = slabInExe + SaveFormat.CityTable;
        if (exe.Length < table + SaveFormat.CityCount * SaveFormat.CityRecordLength)
        {
            Check(false, "START.EXE is long enough to hold the city table");
            return;
        }

        int mismatches = 0;
        for (int i = 0; i < SaveFormat.CityCount; i++)
        {
            int b = table + i * SaveFormat.CityRecordLength;
            var c = CityBook.All[i];
            if (exe[b + SaveFormat.CitySize] == c.Size &&
                exe[b + SaveFormat.CityMap] == c.Map &&
                exe[b + SaveFormat.CityX] == c.X &&
                exe[b + SaveFormat.CityY] == c.Y) continue;
            mismatches++;
            Console.Error.WriteLine(
                $"  {c.Name}: book {c.Size}/{c.Map}/{c.X}/{c.Y}, exe {exe[b + SaveFormat.CitySize]}/" +
                $"{exe[b + SaveFormat.CityMap]}/{exe[b + SaveFormat.CityX]}/{exe[b + SaveFormat.CityY]}");
        }
        CheckEqual(0, mismatches, "CityBook matches START.EXE's initial table in size, map and position");

        // And confirm the distinction is real: the shipped save has been played, so its size column
        // must differ from the EXE's. If these ever agree, the two sources have been conflated.
        string savePath = Path.Combine(folder, "CHICAGO.RWS");
        if (!File.Exists(savePath)) return;
        var sav = File.ReadAllBytes(savePath);
        if (sav.Length != SaveFormat.SlabLength) return;
        int looted = 0;
        for (int i = 0; i < SaveFormat.CityCount; i++)
        {
            int e = exe[table + i * SaveFormat.CityRecordLength + SaveFormat.CitySize];
            int s = sav[SaveFormat.CityTable + i * SaveFormat.CityRecordLength + SaveFormat.CitySize];
            if (e != s) looted++;
        }
        CheckEqual(30, looted, "the shipped save has 30 towns looted below their starting level");
    }

    private static void CheckShippedMaps(string folder)
    {
        Console.WriteLine("Shipped overland maps");
        var (west, east) = OverlandMap.LoadPair(folder);
        Check(west is not null, "WEST.MAP loads");
        Check(east is not null, "EAST.MAP loads");
        if (west is null || east is null) return;

        // The decisive geometry check: every one of the 120 cities has to land on a city tile of
        // its own map under the engine's index rule. Nothing else about the layout can be off if
        // this passes.
        int landed = 0;
        foreach (var c in CityBook.All)
        {
            var map = c.Map == 1 ? west : east;
            if (TerrainBook.IsCity(map[c.X, c.Y])) landed++;
            else Console.Error.WriteLine($"  {c.Name} at {c.Map}:{c.X},{c.Y} sits on terrain {map[c.X, c.Y]}");
        }
        CheckEqual(120, landed, "all 120 cities land on a city tile");

        // HOUSTON is the one record whose X is 0, which the engine's flat index wraps onto the
        // previous row's last column. It is called out here so that if the rule is ever changed,
        // the anomaly is re-examined rather than quietly re-broken.
        var houston = CityBook.All.First(c => c.Name == "HOUSTON");
        CheckEqual(0, houston.X, "HOUSTON is the X = 0 record");
        CheckEqual(31 * OverlandMap.Width + 47, OverlandMap.Index(houston.X, houston.Y),
                   "HOUSTON's index wraps onto row 31, column 47");
        Check(TerrainBook.IsCity(east[houston.X, houston.Y]), "the wrapped square carries a city tile");
        Check(!OverlandMap.IsInside(houston.X, houston.Y), "but it is not offered as a teleport target");

        CheckEqual("CHICAGO", east.DescribeSquare(12, 13), "square 12,13 of the east map is Chicago");
        Check(east.ToAscii().Length == OverlandMap.Height, "ASCII render has one line per row");
        Check(east.ToAscii()[0].Length == OverlandMap.Width, "ASCII render has one column per square");
    }

    // ---- live check ----------------------------------------------------------

    private static void CheckLive()
    {
        Console.WriteLine("Live game");
        var emulators = GameLocator.FindEmulators();
        if (emulators.Count == 0)
        {
            Console.WriteLine("  No DOSBox running - skipped.");
            return;
        }

        try
        {
            foreach (var p in emulators)
                if (CheckOneEmulator(p)) return;
            Console.WriteLine("  No emulator had Roadwar 2000 loaded - skipped.");
        }
        finally
        {
            foreach (var p in emulators) p.Dispose();
        }
    }

    /// <summary>
    /// Checks one emulator; returns true once a Roadwar data segment has been found and exercised.
    /// Opening a process can fail for reasons that are not a test failure -- the harness is not
    /// elevated, or the process is a service -- so those are reported as skips, which is what
    /// README.md and AGENTS.md promise.
    /// </summary>
    private static bool CheckOneEmulator(System.Diagnostics.Process p)
    {
        ProcessMemory memory;
        try { memory = ProcessMemory.Open(p.Id); }
        catch (Exception ex)
        {
            Console.WriteLine($"  pid {p.Id}: cannot open ({ex.Message.Split('.')[0]}) - skipped. " +
                              "Run the harness elevated to include it.");
            return false;
        }

        using (memory)
        {
            var found = new GameLocator().Locate(memory);
            if (found is null) { Console.WriteLine($"  pid {p.Id}: no Roadwar data segment."); return false; }

            Console.WriteLine($"  pid {p.Id}: data segment 0x{found.DataSegmentHost:X}, " +
                              $"{found.Detail}, {found.ElapsedMilliseconds} ms");
            var target = new LiveSlabTarget(memory, found.DataSegmentHost);
            var slab = new GameSlab(target);
            Check(slab.Refresh(), "live slab reads");
            Check(slab.LooksValid(), "live slab validates");

            var gang = new GangRecord(slab);
            Console.WriteLine($"    gang '{gang.Name}', day {gang.Day} {gang.Clock}, " +
                              $"{gang.VehicleCount} vehicle(s), at {gang.LocationName}");
            Check(gang.VehicleCount <= SaveFormat.MaxVehicleSlots, "vehicle count in range");
            Check(gang.X >= 1 && gang.X <= OverlandMap.Width, "party X in range");
            Check(gang.Y < OverlandMap.Height, "party Y in range");
            Check(target.ReadOverlandMap()?.Length == OverlandMap.CellCount, "overland map reads");
            Check(GameLocator.StillValid(memory, found.DataSegmentHost), "re-validation passes");
            return true;
        }
    }

    // ---- helpers -------------------------------------------------------------

    /// <summary>
    /// Builds the parts of a slab the structural check and the record views need: the vehicle
    /// name block, its pointer table, and the vehicle-type templates.
    /// </summary>
    private static byte[] BuildSyntheticSlab()
    {
        var bytes = new byte[SaveFormat.SlabLength];

        int nameOffset = SaveFormat.VehicleNames;
        for (int i = 0; i < VehicleBook.All.Count; i++)
        {
            int pointer = SaveFormat.DsBase + nameOffset;
            bytes[SaveFormat.VehicleNamePointers + i * 2] = (byte)(pointer & 0xFF);
            bytes[SaveFormat.VehicleNamePointers + i * 2 + 1] = (byte)(pointer >> 8);
            foreach (char ch in VehicleBook.All[i].Name) bytes[nameOffset++] = (byte)ch;
            bytes[nameOffset++] = 0;
        }

        for (int i = 0; i < VehicleBook.All.Count; i++)
        {
            var t = VehicleBook.All[i];
            int b = SaveFormat.VehicleTypeTable + i * SaveFormat.VehicleTypeRecordLength;
            bytes[b + 0] = (byte)t.Mass;
            bytes[b + 1] = (byte)t.Structure;
            bytes[b + 2] = (byte)t.MaxSpeed;
            bytes[b + 3] = (byte)t.Maneuverability;
        }

        // City records carry their immutable half, so CityRecord reads the same map/X/Y as CityBook.
        for (int i = 0; i < CityBook.All.Count; i++)
        {
            var c = CityBook.All[i];
            int b = SaveFormat.CityTable + i * SaveFormat.CityRecordLength;
            bytes[b + SaveFormat.CitySize] = (byte)Math.Min(255, c.Size);
            bytes[b + SaveFormat.CityMap] = (byte)c.Map;
            bytes[b + SaveFormat.CityX] = (byte)c.X;
            bytes[b + SaveFormat.CityY] = (byte)c.Y;
        }

        return bytes;
    }

    /// <summary>A target that reads but refuses every write, standing in for a game that went away.</summary>
    private sealed class RefusingTarget : ISlabTarget
    {
        private readonly byte[] _bytes;
        public RefusingTarget(byte[] bytes) => _bytes = bytes;
        public bool IsAvailable => true;
        public byte[]? Read(int slabOffset, int count)
        {
            if (slabOffset < 0 || count < 0 || slabOffset > _bytes.Length - count) return null;
            var slice = new byte[count];
            Array.Copy(_bytes, slabOffset, slice, 0, count);
            return slice;
        }
        public bool Write(int slabOffset, byte[] data) => false;
    }

    /// <summary>
    /// Where the shipped data files are. Uses the trainer's own probe, so the harness and the app
    /// agree: the <c>ROADWAR2000_DIR</c> environment variable first, then the conventional
    /// locations. No machine-specific path is baked in -- pass the folder as the first argument or
    /// set the variable.
    /// </summary>
    private static string? FindGameFolder() => SaveEditorViewModel.GuessGameFolder();
}
