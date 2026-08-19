namespace Roadwar2000Trainer.Game;

/// <summary>
/// Typed view of one of the fifteen 50-byte vehicle slots.
/// <para>
/// The layout was recovered by reading the game's own Vehicle Stats screen for four vehicles of
/// three different types and matching every printed figure to a byte -- which is why the
/// current/maximum pairs are the right way round (the engine stores maximum first) and why
/// mass, fuel consumption and facing are where they are rather than where a guess would put them.
/// </para>
/// </summary>
public sealed class VehicleRecord
{
    private readonly GameSlab _slab;

    public VehicleRecord(GameSlab slab, int slot)
    {
        _slab = slab;
        Slot = slot;
        Base = SaveFormat.VehicleTable + slot * SaveFormat.VehicleRecordLength;
    }

    /// <summary>0-based slot; the game numbers vehicles from 1.</summary>
    public int Slot { get; }

    /// <summary>Slab offset of this record's first byte.</summary>
    public int Base { get; }

    private int Get(int field) => _slab.GetByte(Base + field);
    private bool Set(int field, int value) => _slab.SetByte(Base + field, value);

    public int TypeId
    {
        get => Get(SaveFormat.VehType);
        set => Set(SaveFormat.VehType, value);
    }

    public VehicleType? Type => VehicleBook.ById(TypeId);

    public string TypeName => VehicleBook.NameOf(TypeId);

    /// <summary>Mass drives ramming damage and cargo capacity; the engine copies it from the template.</summary>
    public int Mass
    {
        get => Get(SaveFormat.VehMass);
        set => Set(SaveFormat.VehMass, value);
    }

    public int Structure
    {
        get => Get(SaveFormat.VehStructure);
        set => Set(SaveFormat.VehStructure, value);
    }

    public int StructureMax
    {
        get => Get(SaveFormat.VehStructureMax);
        set => Set(SaveFormat.VehStructureMax, value);
    }

    public int Maneuver
    {
        get => Get(SaveFormat.VehManeuver);
        set => Set(SaveFormat.VehManeuver, value);
    }

    public int ManeuverMax
    {
        get => Get(SaveFormat.VehManeuverMax);
        set => Set(SaveFormat.VehManeuverMax, value);
    }

    public int Braking
    {
        get => Get(SaveFormat.VehBraking);
        set => Set(SaveFormat.VehBraking, value);
    }

    public int Acceleration
    {
        get => Get(SaveFormat.VehAcceleration);
        set => Set(SaveFormat.VehAcceleration, value);
    }

    public int Tires
    {
        get => Get(SaveFormat.VehTires);
        set => Set(SaveFormat.VehTires, value);
    }

    public int TiresMax
    {
        get => Get(SaveFormat.VehTiresMax);
        set => Set(SaveFormat.VehTiresMax, value);
    }

    /// <summary>Top speed in tens of MPH, as stored.</summary>
    public int MaxSpeed
    {
        get => Get(SaveFormat.VehMaxSpeed);
        set => Set(SaveFormat.VehMaxSpeed, value);
    }

    /// <summary>Current speed in tens of MPH.</summary>
    public int Speed
    {
        get => Get(SaveFormat.VehSpeed);
        set => Set(SaveFormat.VehSpeed, value);
    }

    /// <summary>Facing 1..8, using the same rosette as the movement keys (1 = north).</summary>
    public int Facing
    {
        get => Get(SaveFormat.VehFacing);
        set => Set(SaveFormat.VehFacing, value);
    }

    public int FuelConsumption
    {
        get => Get(SaveFormat.VehFuelConsumption);
        set => Set(SaveFormat.VehFuelConsumption, value);
    }

    /// <summary>Missile protection, 0 = open air through 5 = solid metal. Index 0..4 = L, R, F, B, T.</summary>
    public int GetProtection(int facing) => Get(SaveFormat.VehProtection + facing);

    public bool SetProtection(int facing, int value) =>
        Set(SaveFormat.VehProtection + facing, Math.Clamp(value, 0, 5));

    /// <summary>Crew that may fire through a facing. Index 0..3 = L, R, F, B.</summary>
    public int GetMissileFactor(int facing) => Get(SaveFormat.VehMissile + facing);

    public bool SetMissileFactor(int facing, int value) => Set(SaveFormat.VehMissile + facing, value);

    /// <summary>Crew that may board through a facing. Index 0..3 = L, R, F, B.</summary>
    public int GetBoarding(int facing) => Get(SaveFormat.VehBoarding + facing);

    public int InteriorCapacity
    {
        get => Get(SaveFormat.VehInteriorCapacity);
        set => Set(SaveFormat.VehInteriorCapacity, value);
    }

    /// <summary>What the game prints; the stored byte excludes the driver.</summary>
    public int DisplayInteriorCapacity => InteriorCapacity + 1;

    public int TopsideCapacity
    {
        get => Get(SaveFormat.VehTopsideCapacity);
        set => Set(SaveFormat.VehTopsideCapacity, value);
    }

    public int GetInteriorCrew(int rank) => Get(SaveFormat.VehInteriorCrew + rank);

    public bool SetInteriorCrew(int rank, int value) => Set(SaveFormat.VehInteriorCrew + rank, value);

    public int GetTopsideCrew(int rank) => Get(SaveFormat.VehTopsideCrew + rank);

    public bool SetTopsideCrew(int rank, int value) => Set(SaveFormat.VehTopsideCrew + rank, value);

    public int CrewAboard
    {
        get
        {
            int sum = 0;
            for (int r = 0; r < SaveFormat.CrewRankCount; r++) sum += GetInteriorCrew(r) + GetTopsideCrew(r);
            return sum;
        }
    }

    /// <summary>Cargo spaces this vehicle contributes, from its mass.</summary>
    public int CarryingCapacity => GameFacts.CarryingCapacity(Mass);

    /// <summary>Raises structure, manoeuvrability and tires back to their maxima.</summary>
    public void Repair()
    {
        Structure = StructureMax;
        Maneuver = ManeuverMax;
        Tires = TiresMax;
    }

    /// <summary>
    /// Applies every upgrade the game can bestow: solid-metal armour on all five facings, full
    /// structure, and a speed and handling package. These are the same fields the foundry,
    /// welding, speed, brake, performance and underbody shops raise during play.
    /// </summary>
    public void Maximize()
    {
        for (int f = 0; f < 5; f++) SetProtection(f, Math.Max(GetProtection(f), 5));
        StructureMax = Math.Min(255, Math.Max(StructureMax, (Type?.Structure ?? StructureMax) * 2));
        Structure = StructureMax;
        ManeuverMax = Raise(ManeuverMax, 2, 9);
        Maneuver = ManeuverMax;
        Braking = Raise(Braking, 2, 9);
        Acceleration = Raise(Acceleration, 2, 9);
        MaxSpeed = Raise(MaxSpeed, 4, 25);
        Tires = TiresMax;
    }

    /// <summary>
    /// Adds <paramref name="by"/> up to <paramref name="cap"/>, but never returns less than it was
    /// given. A plain <c>Math.Min(cap, current + by)</c> would *downgrade* anything already above
    /// the cap, and vehicles above it are exactly the ones worth keeping -- speed and performance
    /// shops raise these fields during play, and captured road-gang vehicles often arrive improved
    /// (the shipped save's first vehicle is already at 140 MPH against a template 120).
    /// </summary>
    private static int Raise(int current, int by, int cap) => Math.Max(current, Math.Min(cap, current + by));

    /// <summary>
    /// Overwrites the slot with a factory-fresh vehicle of <paramref name="type"/>, crewless and
    /// facing north. This is what "add a vehicle" writes; the gang's vehicle count still has to
    /// be raised for the engine to look at the slot.
    /// </summary>
    public bool Fill(VehicleType type)
    {
        var r = new byte[SaveFormat.VehicleRecordLength];
        r[SaveFormat.VehType] = (byte)type.Id;
        r[SaveFormat.VehMass] = (byte)type.Mass;
        r[SaveFormat.VehStructureMax] = (byte)type.Structure;
        r[SaveFormat.VehStructure] = (byte)type.Structure;
        r[SaveFormat.VehManeuverMax] = (byte)type.Maneuverability;
        r[SaveFormat.VehManeuver] = (byte)type.Maneuverability;
        r[SaveFormat.VehBraking] = (byte)type.Braking;
        r[SaveFormat.VehAcceleration] = (byte)type.Acceleration;
        r[SaveFormat.VehMissile + 0] = (byte)type.MissileLeft;
        r[SaveFormat.VehMissile + 1] = (byte)type.MissileRight;
        r[SaveFormat.VehMissile + 2] = (byte)type.MissileFront;
        r[SaveFormat.VehMissile + 3] = (byte)type.MissileBack;
        // One byte per volley, 2 = firearm. The four one-volley types (motorcycle, sidecar,
        // tractor, construction vehicle) get nothing in the second slot rather than a weapon for a
        // volley the template says they do not have.
        r[SaveFormat.VehWeaponTypes + 0] = 2;
        r[SaveFormat.VehWeaponTypes + 1] = (byte)(type.Volleys > 1 ? 2 : 0);
        r[SaveFormat.VehProtection + 0] = (byte)type.ProtectLeft;
        r[SaveFormat.VehProtection + 1] = (byte)type.ProtectRight;
        r[SaveFormat.VehProtection + 2] = (byte)type.ProtectFront;
        r[SaveFormat.VehProtection + 3] = (byte)type.ProtectBack;
        r[SaveFormat.VehProtection + 4] = (byte)type.ProtectTop;
        // The two unidentified bytes between protection and tires. They read 2/2 on every crewed
        // vehicle inspected except one, which read 0/2 -- see docs/reverse-engineering.md sections
        // 4 and 10. The majority reading is written rather than a zero, but it is a copy of an
        // observation, not something understood.
        r[0x13] = 2;
        r[0x14] = 2;
        r[SaveFormat.VehTiresMax] = (byte)type.Tires;
        r[SaveFormat.VehTires] = (byte)type.Tires;
        r[SaveFormat.VehBoarding + 0] = (byte)type.BoardLeft;
        r[SaveFormat.VehBoarding + 1] = (byte)type.BoardRight;
        r[SaveFormat.VehBoarding + 2] = (byte)type.BoardFront;
        r[SaveFormat.VehBoarding + 3] = (byte)type.BoardBack;
        r[SaveFormat.VehInteriorCapacity] = (byte)type.InteriorCapacity;
        r[SaveFormat.VehTopsideCapacity] = (byte)type.TopsideCapacity;
        r[SaveFormat.VehFuelConsumption] = (byte)type.FuelConsumption;
        r[SaveFormat.VehMaxSpeed] = (byte)type.MaxSpeed;
        r[SaveFormat.VehSpeed] = 0;
        r[SaveFormat.VehFacing] = 1;
        r[0x30] = 1;    // the engine holds 0x0001 here in every live vehicle seen
        r[0x31] = 0;
        return _slab.SetBytes(Base, r);
    }

    /// <summary>
    /// Does this slot hold something the engine would recognise? Used to reject a slot that was
    /// never filled, so the UI does not offer to edit 50 bytes of zeroes as if they were a car.
    /// </summary>
    public bool LooksValid() =>
        TypeId < SaveFormat.VehicleTypeCount &&
        Mass > 0 && StructureMax > 0 &&
        Structure <= StructureMax &&
        Tires <= TiresMax &&
        MaxSpeed > 0;
}
