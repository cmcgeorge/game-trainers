namespace Roadwar2000Trainer.Game;

/// <summary>
/// Typed, mutable view of the gang: name, supplies, crew, cronies, position and clock.
/// Every property reads out of the slab cache and writes straight back through it, so the
/// same class serves the live editor and the save editor without knowing which it is on.
/// </summary>
public sealed class GangRecord
{
    private readonly GameSlab _slab;

    public GangRecord(GameSlab slab) => _slab = slab;

    // ---- identity and inventory ---------------------------------------------

    public string Name
    {
        get => _slab.GetString(SaveFormat.GangName, SaveFormat.GangNameLength);
        set => _slab.SetString(SaveFormat.GangName, SaveFormat.GangNameLength, value);
    }

    public int Food
    {
        get => _slab.GetUInt16(SaveFormat.Food);
        set => _slab.SetUInt16(SaveFormat.Food, value);
    }

    public int Tires
    {
        get => _slab.GetUInt16(SaveFormat.Tires);
        set => _slab.SetUInt16(SaveFormat.Tires, value);
    }

    /// <summary>Fuel as stored. See <see cref="DisplayedFuel"/> for what the Gang Status screen shows.</summary>
    public int Fuel
    {
        get => _slab.GetUInt16(SaveFormat.Fuel);
        set => _slab.SetUInt16(SaveFormat.Fuel, value);
    }

    public int Ammo
    {
        get => _slab.GetUInt16(SaveFormat.Ammo);
        set => _slab.SetUInt16(SaveFormat.Ammo, value);
    }

    public int Guns
    {
        get => _slab.GetUInt16(SaveFormat.Guns);
        set => _slab.SetUInt16(SaveFormat.Guns, value);
    }

    public int Medical
    {
        get => _slab.GetUInt16(SaveFormat.Medical);
        set => _slab.SetUInt16(SaveFormat.Medical, value);
    }

    public int Antitoxin
    {
        get => _slab.GetUInt16(SaveFormat.Antitoxin);
        set => _slab.SetUInt16(SaveFormat.Antitoxin, value);
    }

    // ---- crew ----------------------------------------------------------------

    /// <summary>Crew of one rank, 0 = armsmaster through 4 = escort.</summary>
    public int GetCrew(int rank) => _slab.GetUInt16(SaveFormat.Crew + rank * 2);

    public bool SetCrew(int rank, int value) => _slab.SetUInt16(SaveFormat.Crew + rank * 2, value);

    public int TotalCrew
    {
        get
        {
            int sum = 0;
            for (int r = 0; r < SaveFormat.CrewRankCount; r++) sum += GetCrew(r);
            return sum;
        }
    }

    // ---- cronies and specials ------------------------------------------------

    public int DoctorQuality
    {
        get => _slab.GetByte(SaveFormat.DoctorQuality);
        set => _slab.SetByte(SaveFormat.DoctorQuality, value);
    }

    public int DrillSergeantQuality
    {
        get => _slab.GetByte(SaveFormat.DrillSergeantQuality);
        set => _slab.SetByte(SaveFormat.DrillSergeantQuality, value);
    }

    public int PoliticianQuality
    {
        get => _slab.GetByte(SaveFormat.PoliticianQuality);
        set => _slab.SetByte(SaveFormat.PoliticianQuality, value);
    }

    public bool HasRadioDirectionFinder
    {
        get => _slab.GetByte(SaveFormat.RadioDirectionFinder) != 0;
        set => _slab.SetByte(SaveFormat.RadioDirectionFinder, value ? 1 : 0);
    }

    /// <summary>
    /// The snow-tire special -- the '*' the Gang Status screen prints beside TIRES.
    /// <para>
    /// Setting it writes 8 rather than 1 because 8 is the value the shipped save carries and the
    /// value that was confirmed live. The engine only ever tested it for non-zero in everything
    /// observed, but writing back the one value known to work beats relying on an encoding that
    /// was never exercised.
    /// </para>
    /// </summary>
    public bool HasSnowTires
    {
        get => _slab.GetByte(SaveFormat.SnowTires) != 0;
        set => _slab.SetByte(SaveFormat.SnowTires, value ? 8 : 0);
    }

    public bool HasFuelSpecial
    {
        get => _slab.GetByte(SaveFormat.FuelSpecial) != 0;
        set => _slab.SetByte(SaveFormat.FuelSpecial, value ? 1 : 0);
    }

    // ---- fleet ---------------------------------------------------------------

    public int VehicleCount
    {
        get => _slab.GetByte(SaveFormat.VehicleCount);
        set => _slab.SetByte(SaveFormat.VehicleCount, Math.Clamp(value, 0, SaveFormat.MaxVehicleSlots));
    }

    public int MaxVehicles
    {
        get => _slab.GetByte(SaveFormat.MaxVehicles);
        set => _slab.SetByte(SaveFormat.MaxVehicles, Math.Clamp(value, 1, SaveFormat.MaxVehicleSlots));
    }

    // ---- world position and clock -------------------------------------------

    public int CurrentMap
    {
        get => _slab.GetUInt16(SaveFormat.CurrentMap);
        set => _slab.SetUInt16(SaveFormat.CurrentMap, value);
    }

    /// <summary>
    /// Party column, 1-based. Setting it updates both copies the engine keeps -- the world
    /// header and the gang block -- because both were seen to move together on every step, and
    /// writing only one leaves the game disagreeing with itself.
    /// </summary>
    public int X
    {
        get => _slab.GetByte(SaveFormat.PartyX);
        set { _slab.SetByte(SaveFormat.PartyX, value); _slab.SetByte(SaveFormat.GangX, value); }
    }

    /// <summary>Party row, 0-based. Written to both copies, as for <see cref="X"/>.</summary>
    public int Y
    {
        get => _slab.GetByte(SaveFormat.PartyY);
        set { _slab.SetByte(SaveFormat.PartyY, value); _slab.SetByte(SaveFormat.GangY, value); }
    }

    /// <summary>Terrain code under the gang. The engine recomputes it on the next redraw.</summary>
    public int CurrentTerrain => _slab.GetByte(SaveFormat.CurrentTerrain);

    public int Day
    {
        get => _slab.GetUInt16(SaveFormat.Day);
        set => _slab.SetUInt16(SaveFormat.Day, value);
    }

    /// <summary>
    /// Time of day as the engine stores it; the clock reads <c>6 + this</c>. Clamped to a day,
    /// because a larger value leaves the engine holding a time past the end of its own -- every
    /// other bounded field on this class clamps, and this one has no reason not to.
    /// </summary>
    public int TimeOfDay
    {
        get => _slab.GetUInt16(SaveFormat.TimeOfDay);
        set => _slab.SetUInt16(SaveFormat.TimeOfDay, Math.Clamp(value, 0, 23));
    }

    // ---- derived -------------------------------------------------------------

    /// <summary>Total fuel the fleet burns per overland move; the sum over the crewed vehicles.</summary>
    public int FuelConsumption
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < VehicleCount && i < SaveFormat.MaxVehicleSlots; i++)
                sum += _slab.GetByte(SaveFormat.VehicleTable + i * SaveFormat.VehicleRecordLength +
                                     SaveFormat.VehFuelConsumption);
            return HasFuelSpecial ? (sum + 1) / 2 : sum;
        }
    }

    /// <summary>
    /// What the game's Gang Status screen prints for fuel: the stored figure less the two moves'
    /// worth every vehicle keeps in its tank, which does not occupy cargo space.
    /// </summary>
    public int DisplayedFuel => Math.Max(0, Fuel - 2 * FuelConsumption);

    /// <summary>Total cargo spaces the fleet has, 5 x mass^2 per vehicle.</summary>
    public int TotalCapacity
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < VehicleCount && i < SaveFormat.MaxVehicleSlots; i++)
            {
                int mass = _slab.GetByte(SaveFormat.VehicleTable + i * SaveFormat.VehicleRecordLength +
                                         SaveFormat.VehMass);
                sum += GameFacts.CarryingCapacity(mass);
            }
            return sum;
        }
    }

    /// <summary>Seats the fleet has, counting the driver each interior adds.</summary>
    public int PassengerCapacity
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < VehicleCount && i < SaveFormat.MaxVehicleSlots; i++)
            {
                int b = SaveFormat.VehicleTable + i * SaveFormat.VehicleRecordLength;
                sum += _slab.GetByte(b + SaveFormat.VehInteriorCapacity) + 1
                     + _slab.GetByte(b + SaveFormat.VehTopsideCapacity);
            }
            return sum;
        }
    }

    /// <summary>Spaces in use: everything except ammo and antitoxin, which are weightless.</summary>
    public int SuppliesCarried => Food + Tires + DisplayedFuel + Guns + Medical;

    public string Clock => GameFacts.ClockOf(TimeOfDay);

    /// <summary>Name of the square the gang is on -- the city if there is one, else the terrain.</summary>
    public string LocationName
    {
        get
        {
            var city = CityBook.At(CurrentMap, X, Y);
            return city?.Name ?? TerrainBook.NameOf(CurrentTerrain);
        }
    }
}
