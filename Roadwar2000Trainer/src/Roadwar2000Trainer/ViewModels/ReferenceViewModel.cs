using GameTrainers.Common.Mvvm;
using Roadwar2000Trainer.Game;

namespace Roadwar2000Trainer.ViewModels;

/// <summary>A vehicle-table row, flattened for display.</summary>
public sealed record VehicleReferenceRow(
    string Name, int Mass, int Structure, string Speed, int Maneuver, int Braking, int Acceleration,
    string MissileFactors, string Protection, int Volleys, string Tires,
    string Boarding, int Interior, int Topside, int Fuel, int Capacity)
{
    public static VehicleReferenceRow From(VehicleType t) => new(
        t.Name, t.Mass, t.Structure, $"{t.MaxSpeedMph}", t.Maneuverability, t.Braking, t.Acceleration,
        $"{t.MissileLeft}/{t.MissileRight}/{t.MissileFront}/{t.MissileBack}",
        $"{t.ProtectLeft}/{t.ProtectRight}/{t.ProtectFront}/{t.ProtectBack}/{t.ProtectTop}",
        t.Volleys, t.Tires == 0 ? "treads" : t.Tires.ToString(),
        $"{t.BoardLeft}/{t.BoardRight}/{t.BoardFront}/{t.BoardBack}",
        t.DisplayInteriorCapacity, t.TopsideCapacity, t.FuelConsumption, t.CarryingCapacity);
}

/// <summary>A loot-table row, flattened for display.</summary>
public sealed record LootReferenceRow(string Name, string Frequency, string Yield)
{
    public static LootReferenceRow From(LootSite s)
    {
        var parts = new List<string>();
        if (s.Food > 0) parts.Add($"{s.Food} food");
        if (s.Guns > 0) parts.Add($"{s.Guns} guns");
        if (s.Tires > 0) parts.Add($"{s.Tires} tires");
        if (s.Fuel > 0) parts.Add($"{s.Fuel} fuel");
        if (s.Medical > 0) parts.Add($"{s.Medical} medical");
        if (s.PaysVehicle) parts.Add("a vehicle");
        return new(s.Name,
                   $"{s.FreqPlains}/{s.FreqFarmland}/{s.FreqCity}/{s.FreqRoad}",
                   parts.Count == 0 ? "(service or upgrade)" : string.Join(", ", parts));
    }
}

/// <summary>A city-gazetteer row.</summary>
public sealed record CityReferenceRow(int Id, string Name, int Size, string Map, int X, int Y)
{
    public static CityReferenceRow From(CityInfo c) => new(c.Id, c.Name, c.Size, c.MapName, c.X, c.Y);
}

/// <summary>
/// The Reference tab: everything the engine's own tables say, with no live game needed. All of it
/// is read out of START.EXE's data segment rather than typed in from the manual, so where the two
/// disagree this is what the game will actually do.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    public ReferenceViewModel()
    {
        Vehicles = VehicleBook.All.Select(VehicleReferenceRow.From).ToList();
        Loot = LootBook.All.Select(LootReferenceRow.From).ToList();
        Cities = CityBook.All.Select(CityReferenceRow.From).ToList();
        Terrain = TerrainBook.Names
            .Select((n, i) => $"{i,2}  {n}")
            .ToList();
        Ranks = RankBook.Names.ToList();
        Residents = ResidentBook.Names.ToList();
        Footgangs = FootgangBook.Names.ToList();
        RoadGangs = RoadGangBook.Names.ToList();
        Scientists = ScientistBook.Names.ToList();
        Improvements = ImprovementBook.Names.ToList();
    }

    public IReadOnlyList<VehicleReferenceRow> Vehicles { get; }
    public IReadOnlyList<LootReferenceRow> Loot { get; }
    public IReadOnlyList<CityReferenceRow> Cities { get; }
    public IReadOnlyList<string> Terrain { get; }
    public IReadOnlyList<string> Ranks { get; }
    public IReadOnlyList<string> Residents { get; }
    public IReadOnlyList<string> Footgangs { get; }
    public IReadOnlyList<string> RoadGangs { get; }
    public IReadOnlyList<string> Scientists { get; }
    public IReadOnlyList<string> Improvements { get; }
}
