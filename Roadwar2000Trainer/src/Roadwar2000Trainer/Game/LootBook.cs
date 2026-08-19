// The 28 lootable location types and their yield table, out of START.EXE's data segment
// (names at DS:0x2554 / slab 0x039A, records at DS:0x26DA / slab 0x0520, 28 x 12 bytes).
//
// Bytes +0..+3 are the site's relative frequency in the four terrain classes the engine
// distinguishes when you press L. The reading is forced by the two agricultural sites, which are
// mirror images: FARM is 26/80/1/6 and RANCH is 80/26/1/6, and the manual says farms are common
// in farmland and ranches on the plains.
//
// Bytes +4..+9 are the payout, and each column is pinned by a site that pays in it and nothing
// else: GUN SHOP and ARMORY pay only +5; TIRE STORE and JUNKYARD only +7; FUEL STORAGE TANK only
// +8; MEDICAL CENTER, HOSPITAL, VETERINARIAN and DRUG STORE only +9. So +4 is food, +5 guns,
// +7 tires, +8 fuel and +9 medical. CACHE, which pays a little of everything (10/10/0/10/10/2),
// corroborates all five at once. There is no ammunition column -- ammo arrives with guns.
//
// Byte +6 is 1 at exactly the eight sites where a vehicle can turn up -- shopping mall, military
// base, body shop, high school/college, auto dealer, bus depot, taxi garage, racing track -- and
// 0 everywhere else, so it is read as a vehicle flag. That one is Inferred; the five supply
// columns are Measured. See docs/reverse-engineering.md section 7.
namespace Roadwar2000Trainer.Game;

/// <summary>A place the L)oot command can turn up, and what it is worth.</summary>
public sealed record LootSite(
    int Id, string Name,
    int FreqPlains, int FreqFarmland, int FreqCity, int FreqRoad,
    int Food, int Guns, int Vehicle, int Tires, int Fuel, int Medical)
{
    /// <summary>True for sites that hand out a supply rather than a service or a vehicle.</summary>
    public bool PaysSupplies => Food + Guns + Tires + Fuel + Medical > 0;

    /// <summary>True for the eight sites that can turn up a vehicle.</summary>
    public bool PaysVehicle => Vehicle > 0;

    public override string ToString() => Name;
}

/// <summary>The engine's loot-site table, in engine order.</summary>
public static class LootBook
{
    /// <summary>The 28 sites; the index is the stored site id.</summary>
    public static readonly IReadOnlyList<LootSite> All = new LootSite[]
    {
        new( 0, "CONVENIENCE STORE", 6, 6, 30, 6, 10, 0, 0, 0, 0, 0),
        new( 1, "SUPERMARKET", 4, 4, 18, 2, 50, 0, 0, 0, 0, 0),
        new( 2, "SHOPPING MALL", 2, 2, 2, 2, 30, 10, 1, 10, 2, 0),
        new( 3, "MILITARY BASE", 4, 2, 1, 2, 40, 60, 1, 20, 2, 2),
        new( 4, "FARM", 26, 80, 1, 6, 20, 2, 0, 0, 0, 0),
        new( 5, "RANCH", 80, 26, 1, 6, 15, 2, 0, 0, 0, 0),
        new( 6, "SPORTING GOODS STORE", 2, 2, 6, 2, 0, 5, 0, 0, 0, 0),
        new( 7, "GUN SHOP", 4, 4, 2, 2, 0, 20, 0, 0, 0, 0),
        new( 8, "ARMORY", 4, 2, 1, 2, 0, 40, 0, 0, 0, 0),
        new( 9, "RESTAURANT", 8, 10, 30, 10, 10, 0, 0, 0, 0, 0),
        new(10, "BODY SHOP", 2, 2, 6, 2, 0, 0, 1, 0, 0, 0),
        new(11, "HIGH SCHOOL/COLLEGE", 8, 10, 6, 6, 30, 0, 1, 0, 0, 0),
        new(12, "AUTO DEALER", 2, 2, 6, 2, 0, 0, 1, 15, 2, 0),
        new(13, "TIRE STORE", 2, 2, 6, 2, 0, 0, 0, 10, 0, 0),
        new(14, "JUNKYARD", 2, 2, 2, 2, 0, 0, 0, 30, 0, 0),
        new(15, "GAS STATION", 6, 6, 30, 2, 0, 0, 0, 2, 2, 0),
        new(16, "PARKING LOT", 6, 6, 18, 6, 0, 0, 0, 10, 1, 0),
        new(17, "FUEL STORAGE TANK", 2, 2, 1, 100, 0, 0, 0, 0, 100, 0),
        new(18, "MEDICAL CENTER", 4, 4, 6, 2, 0, 0, 0, 0, 0, 2),
        new(19, "HOSPITAL", 2, 2, 2, 6, 0, 0, 0, 0, 0, 3),
        new(20, "VETERINARIAN", 6, 6, 6, 2, 0, 0, 0, 0, 0, 1),
        new(21, "CACHE", 2, 2, 6, 6, 10, 10, 0, 10, 10, 2),
        new(22, "POLICE STATION", 2, 2, 1, 2, 0, 20, 0, 0, 2, 0),
        new(23, "BUS DEPOT", 2, 2, 2, 2, 0, 0, 1, 2, 2, 0),
        new(24, "TAXI GARAGE", 2, 2, 2, 2, 0, 0, 1, 3, 2, 0),
        new(25, "SHELTER", 2, 2, 1, 2, 50, 0, 0, 0, 0, 1),
        new(26, "DRUG STORE", 6, 6, 6, 6, 0, 0, 0, 0, 0, 2),
        new(27, "RACING TRACK", 2, 2, 1, 2, 0, 0, 1, 5, 2, 0),
    };

    public static LootSite? ById(int id) => id >= 0 && id < All.Count ? All[id] : null;
}
