namespace Roadwar2000Trainer.Game;

/// <summary>
/// Every offset the trainer knows about, in one place.
/// <para>
/// Roadwar 2000 keeps its whole mutable world in one contiguous slab of the Lattice C data
/// segment, and its <c>.RWS</c> save file is a byte-for-byte image of exactly that slab:
/// <c>DS:0x21BA</c> through <c>DS:0x3B29</c>, 6,512 bytes, no header and no checksum. That is
/// why one offset table serves both the live-memory editor and the save editor -- a
/// <see cref="SlabOffset"/> is a file offset, and <c>DsBase + slabOffset</c> is the address of
/// the same field in the running game.
/// </para>
/// <para>
/// The equivalence was not assumed. It was measured: recalling the shipped CHICAGO.RWS and
/// dumping the emulator's guest RAM gave a slab that matched the file in 6,509 of 6,512 bytes,
/// and a save written back out after nine trainer pokes carried every one of them.
/// </para>
/// </summary>
public static class SaveFormat
{
    /// <summary>Bytes in a save file, and in the live slab.</summary>
    public const int SlabLength = 0x1970;   // 6,512

    /// <summary>Offset in the data segment where the slab starts, i.e. save offset 0.</summary>
    public const int DsBase = 0x21BA;

    /// <summary>DS offset of the loaded overland map (2,016 bytes read verbatim out of WEST/EAST.MAP).</summary>
    public const int DsOverlandMap = 0x03C7;

    /// <summary>
    /// The locator's anchor: the vehicle-type name block, which sits at <c>DS:0x2254</c>
    /// (slab 0x009A) and is the same in every build and every save.
    /// </summary>
    public const int DsVehicleNames = DsBase + VehicleNames;

    // ---- header / world block ------------------------------------------------

    /// <summary>uint16. Which overland map the gang is on: 1 = WEST.MAP, 2 = EAST.MAP.</summary>
    public const int CurrentMap = 0x0004;

    /// <summary>uint16. Day of the year, 1-based; the game's own display prints it as "DAY n".</summary>
    public const int Day = 0x001C;

    /// <summary>uint16. Hour of the day as an index; the clock reads <c>6 + value</c> (0 = 6:00 AM).</summary>
    public const int TimeOfDay = 0x001E;

    /// <summary>byte. Party column on the overland map, 1-based (see <see cref="OverlandMap"/>).</summary>
    public const int PartyX = 0x0022;

    /// <summary>byte. Party row on the overland map, 0-based.</summary>
    public const int PartyY = 0x0023;

    // ---- reference tables (read-only; the trainer never writes these) ---------

    /// <summary>19 NUL-terminated vehicle type names.</summary>
    public const int VehicleNames = 0x009A;

    /// <summary>19 word pointers into <see cref="VehicleNames"/>; the locator's second validator.</summary>
    public const int VehicleNamePointers = 0x01AC;

    /// <summary>19 vehicle-type templates of <see cref="VehicleTypeRecordLength"/> bytes.</summary>
    public const int VehicleTypeTable = 0x01D2;
    public const int VehicleTypeRecordLength = 24;
    public const int VehicleTypeCount = 19;

    /// <summary>28 loot-site definitions of 12 bytes.</summary>
    public const int LootTable = 0x0520;
    public const int LootRecordLength = 12;
    public const int LootCount = 28;

    // ---- gang globals --------------------------------------------------------

    /// <summary>byte. Terrain code of the square the gang is standing on (see <see cref="TerrainBook"/>).</summary>
    public const int CurrentTerrain = 0x0678;

    /// <summary>byte. Non-zero once the gang holds a Radio Direction Finder.</summary>
    public const int RadioDirectionFinder = 0x068E;

    /// <summary>byte. Doctor's skill; 0 means the gang has no doctor.</summary>
    public const int DoctorQuality = 0x068F;

    /// <summary>uint16. Antitoxin doses. One dose inoculates 50 crew.</summary>
    public const int Antitoxin = 0x0690;

    /// <summary>byte. Drill sergeant's skill; 0 means none.</summary>
    public const int DrillSergeantQuality = 0x0692;

    /// <summary>byte. Politician's skill; 0 means none.</summary>
    public const int PoliticianQuality = 0x0693;

    /// <summary>byte. Non-zero when the gang's tires are snow tires (the '*' beside TIRES).</summary>
    public const int SnowTires = 0x0694;

    /// <summary>byte. Non-zero for the fuel special, which roughly halves fuel consumption.</summary>
    public const int FuelSpecial = 0x0695;

    /// <summary>byte. Vehicle ceiling, 1..15. Starts at 6 and rises by 1 per tactical road battle.</summary>
    public const int MaxVehicles = 0x0698;

    // ---- city table ----------------------------------------------------------

    /// <summary>120 city records of <see cref="CityRecordLength"/> bytes.</summary>
    public const int CityTable = 0x0CB8;
    public const int CityRecordLength = 12;
    public const int CityCount = 120;

    // City record field offsets.
    public const int CitySize = 0x00;      // byte, depletes as the town is looted
    public const int CityMap = 0x01;       // byte, 1 = west, 2 = east
    public const int CityX = 0x02;         // byte
    public const int CityY = 0x03;         // byte
    public const int CityCache = 0x04;     // 5 bytes: food, tires, fuel, guns, medical
    public const int CityResident = 0x09;  // byte, who holds the town
    public const int CityStrength = 0x0A;  // byte, how strongly

    // ---- the gang ------------------------------------------------------------

    /// <summary>Gang name, NUL-terminated, at most 20 characters.</summary>
    public const int GangName = 0x1570;
    public const int GangNameLength = 20;

    /// <summary>byte. Vehicles the gang currently owns, 0..15.</summary>
    public const int VehicleCount = 0x1584;

    public const int Food = 0x1586;        // uint16
    public const int Tires = 0x1588;       // uint16

    /// <summary>
    /// uint16. Fuel as the engine stores it. The Gang Status screen prints
    /// <c>stored - 2 * fuel consumption</c>, because every vehicle carries two moves' worth in
    /// its tank and that reserve does not occupy cargo space. X)amine Supplies prints the raw value.
    /// </summary>
    public const int Fuel = 0x158A;

    public const int Ammo = 0x158C;        // uint16
    public const int Guns = 0x158E;        // uint16

    /// <summary>byte. Party column, mirrored from <see cref="PartyX"/>; both copies move together.</summary>
    public const int GangX = 0x1591;

    /// <summary>byte. Party row, mirrored from <see cref="PartyY"/>.</summary>
    public const int GangY = 0x1592;

    /// <summary>
    /// Five uint16 crew counts by rank -- armsmaster, bodyguard, commando, dragoon, escort.
    /// Note the odd address: Lattice C packs its structs, so these words are not word-aligned.
    /// </summary>
    public const int Crew = 0x1595;
    public const int CrewRankCount = 5;

    /// <summary>uint16. Medical supplies (what healers charge for their services).</summary>
    public const int Medical = 0x159F;

    /// <summary>15 vehicle records of <see cref="VehicleRecordLength"/> bytes.</summary>
    public const int VehicleTable = 0x15B2;
    public const int VehicleRecordLength = 50;
    public const int MaxVehicleSlots = 15;

    // Vehicle record field offsets.
    public const int VehType = 0x00;
    public const int VehMass = 0x01;
    public const int VehStructureMax = 0x02;
    public const int VehStructure = 0x03;
    public const int VehManeuverMax = 0x04;
    public const int VehManeuver = 0x05;
    public const int VehBraking = 0x06;
    public const int VehAcceleration = 0x07;
    public const int VehMissile = 0x08;        // 4 bytes L, R, F, B
    public const int VehWeaponTypes = 0x0C;    // 2 bytes, one per volley; 2 = firearm, 1 = crossbow
    public const int VehProtection = 0x0E;     // 5 bytes L, R, F, B, T
    public const int VehTiresMax = 0x15;
    public const int VehTires = 0x16;
    public const int VehBoarding = 0x17;       // 4 bytes L, R, F, B
    public const int VehInteriorCapacity = 0x1B;
    public const int VehInteriorCrew = 0x1C;   // 5 bytes by rank
    public const int VehTopsideCapacity = 0x21;
    public const int VehTopsideCrew = 0x22;    // 5 bytes by rank
    public const int VehFuelConsumption = 0x2C;
    public const int VehMaxSpeed = 0x2D;       // tens of MPH
    public const int VehSpeed = 0x2E;          // tens of MPH
    public const int VehFacing = 0x2F;         // 1..8, same rosette as the movement keys

    /// <summary>
    /// The three bytes the save/load routine itself rewrites, so a file and the memory it came
    /// from always differ here. The save editor leaves them alone and the verifier ignores them.
    /// </summary>
    public static readonly int[] VolatileOffsets = { 0x0008, 0x0697, 0x1262 };

    /// <summary>Absolute limit the engine's own byte-wide fields impose.</summary>
    public const int MaxSupply = 65535;
    public const int MaxCrewPerRank = 65535;
    public const int MaxCacheItem = 255;
}
