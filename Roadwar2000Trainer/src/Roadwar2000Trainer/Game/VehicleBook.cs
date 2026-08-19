// The 19 vehicle-type templates, transcribed byte-for-byte out of START.EXE's data segment
// (DS:0x238C, save-slab offset 0x01D2, 19 records of 24 bytes). Every field was cross-checked
// against the printed Vehicle Table in the game manual; where the two disagree the engine's
// numbers win here and the discrepancy is recorded in docs/reverse-engineering.md.
namespace Roadwar2000Trainer.Game;

/// <summary>One of the 19 vehicle types the game can hand you, exactly as the engine stores it.</summary>
/// <param name="Id">Index into the engine's table; this is the byte written at vehicle record +0x00.</param>
public sealed record VehicleType(
    int Id,
    string Name,
    int Mass,
    int Structure,
    int MaxSpeed,          // tens of MPH, as stored
    int Maneuverability,
    int Braking,
    int Acceleration,
    int MissileLeft, int MissileRight, int MissileFront, int MissileBack,
    int ProtectLeft, int ProtectRight, int ProtectFront, int ProtectBack, int ProtectTop,
    int Volleys,
    int Tires,             // 0 = treads, which cannot be shot out
    int BoardLeft, int BoardRight, int BoardFront, int BoardBack,
    int InteriorCapacity,  // as stored; the game's own display adds 1 for the driver
    int TopsideCapacity,
    int FuelConsumption)
{
    /// <summary>Spaces of supply the type carries. Measured to be exactly 5 x Mass^2 for all 19 types.</summary>
    public int CarryingCapacity => 5 * Mass * Mass;

    /// <summary>What the game's own Vehicle Stats screen prints for interior capacity.</summary>
    public int DisplayInteriorCapacity => InteriorCapacity + 1;

    /// <summary>Top speed in MPH (the engine stores tens).</summary>
    public int MaxSpeedMph => MaxSpeed * 10;

    public override string ToString() => Name;
}

/// <summary>The engine's vehicle-type table, in engine order.</summary>
public static class VehicleBook
{
    /// <summary>The 19 types. The order is load-bearing: the index is the stored type id.</summary>
    public static readonly IReadOnlyList<VehicleType> All = new VehicleType[]
    {
        new( 0, "MOTORCYCLE", 1, 3, 10, 4, 2, 2, 2, 2, 2, 2, 0, 0, 1, 0, 0, 1, 2, 1, 1, 0, 1, 1, 0, 1),
        new( 1, "SIDECAR", 2, 5, 6, 4, 2, 2, 3, 3, 3, 3, 0, 1, 1, 1, 0, 1, 3, 1, 1, 0, 2, 2, 0, 1),
        new( 2, "COMPACT CONVERTIBLE", 3, 8, 8, 3, 2, 1, 3, 3, 2, 2, 1, 1, 1, 1, 0, 2, 4, 1, 2, 0, 2, 5, 0, 2),
        new( 3, "COMPACT HARDTOP", 3, 8, 7, 3, 2, 1, 4, 4, 4, 4, 2, 2, 2, 2, 0, 2, 4, 0, 1, 2, 0, 3, 4, 2),
        new( 4, "MIDSIZE CONVERTIBLE", 5, 13, 9, 2, 2, 1, 3, 3, 2, 3, 1, 1, 1, 1, 0, 2, 4, 2, 3, 0, 3, 7, 0, 3),
        new( 5, "MIDSIZE HARDTOP", 5, 13, 8, 2, 2, 1, 4, 4, 5, 6, 2, 2, 2, 2, 0, 2, 4, 1, 2, 2, 0, 4, 6, 3),
        new( 6, "SPORTS CAR CONVERTIBLE", 4, 10, 12, 3, 2, 2, 3, 3, 2, 3, 1, 1, 1, 1, 0, 2, 4, 2, 3, 0, 2, 5, 0, 4),
        new( 7, "SPORTS CAR HARDTOP", 4, 10, 12, 3, 2, 2, 4, 4, 4, 4, 2, 2, 2, 2, 0, 2, 4, 0, 1, 2, 0, 3, 4, 4),
        new( 8, "STATION WAGON", 6, 15, 8, 2, 2, 1, 6, 6, 5, 6, 2, 2, 2, 2, 0, 2, 4, 2, 3, 3, 3, 7, 9, 3),
        new( 9, "LIMOUSINE", 8, 20, 10, 2, 2, 1, 6, 6, 5, 6, 2, 2, 2, 2, 0, 2, 4, 1, 2, 3, 3, 7, 9, 4),
        new(10, "VAN", 7, 18, 7, 2, 2, 1, 8, 8, 5, 6, 2, 2, 2, 2, 0, 2, 4, 0, 3, 3, 3, 10, 12, 3),
        new(11, "PICKUP TRUCK", 9, 23, 8, 2, 2, 1, 6, 6, 4, 3, 1, 1, 2, 1, 0, 2, 4, 4, 5, 0, 3, 13, 2, 4),
        new(12, "OFF ROAD CONVERTIBLE", 6, 15, 7, 2, 2, 1, 2, 2, 2, 2, 1, 1, 1, 1, 0, 2, 4, 1, 2, 0, 2, 3, 0, 4),
        new(13, "OFF ROAD HARDTOP", 6, 15, 7, 2, 2, 1, 3, 3, 3, 3, 2, 2, 2, 2, 0, 2, 4, 0, 1, 2, 0, 3, 2, 4),
        new(14, "BUS", 14, 35, 7, 1, 1, 1, 26, 26, 3, 5, 2, 2, 2, 2, 0, 2, 6, 0, 2, 10, 0, 50, 50, 10),
        new(15, "TRACTOR", 10, 25, 4, 2, 1, 1, 3, 3, 3, 3, 0, 0, 1, 0, 0, 1, 0, 2, 2, 0, 2, 2, 0, 6),
        new(16, "CONSTRUCTION VEHICLE", 18, 45, 3, 2, 1, 1, 4, 4, 4, 4, 0, 0, 1, 0, 0, 1, 0, 3, 3, 0, 3, 3, 0, 10),
        new(17, "FLATBED TRUCK", 16, 40, 8, 1, 1, 1, 14, 14, 4, 4, 0, 0, 2, 0, 0, 2, 14, 6, 7, 0, 4, 50, 2, 8),
        new(18, "TRAILER TRUCK", 20, 50, 8, 1, 1, 1, 14, 14, 4, 8, 5, 5, 2, 0, 0, 2, 18, 0, 1, 10, 5, 50, 50, 10),
    };

    public static VehicleType? ById(int id) => id >= 0 && id < All.Count ? All[id] : null;

    /// <summary>Name for a stored type byte; an out-of-range id reads as obviously wrong rather than blank.</summary>
    public static string NameOf(int id) => ById(id)?.Name ?? $"(type {id}?)";
}
