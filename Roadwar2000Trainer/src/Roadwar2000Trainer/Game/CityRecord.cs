namespace Roadwar2000Trainer.Game;

/// <summary>
/// Typed view of one of the 120 twelve-byte city records.
/// <para>
/// The first four bytes never change during a game -- size, which map, and where -- and are the
/// same numbers baked into <see cref="CityBook"/>. What does change is the cache the gang stashes
/// there and who holds the town, and those are the two fields the trainer edits.
/// </para>
/// </summary>
public sealed class CityRecord
{
    /// <summary>Cache slot order, fixed by the engine's record layout.</summary>
    public const int CacheFood = 0;
    public const int CacheTires = 1;
    public const int CacheFuel = 2;
    public const int CacheGuns = 3;
    public const int CacheMedical = 4;
    public const int CacheSlots = 5;

    /// <summary>Labels for the five cache slots, in record order.</summary>
    public static readonly IReadOnlyList<string> CacheNames = new[]
    {
        "Food", "Tires", "Fuel", "Guns", "Medical",
    };

    private readonly GameSlab _slab;

    public CityRecord(GameSlab slab, int index)
    {
        _slab = slab;
        Index = index;
        Base = SaveFormat.CityTable + index * SaveFormat.CityRecordLength;
    }

    public int Index { get; }

    public int Base { get; }

    public CityInfo? Info => CityBook.ById(Index);

    public string Name => Info?.Name ?? $"City {Index}";

    /// <summary>Supply level; large cities start high and fall as the town is stripped.</summary>
    public int Size
    {
        get => _slab.GetByte(Base + SaveFormat.CitySize);
        set => _slab.SetByte(Base + SaveFormat.CitySize, value);
    }

    public int Map => _slab.GetByte(Base + SaveFormat.CityMap);

    public int X => _slab.GetByte(Base + SaveFormat.CityX);

    public int Y => _slab.GetByte(Base + SaveFormat.CityY);

    /// <summary>Who holds the town; index into <see cref="ResidentBook"/>.</summary>
    public int Resident
    {
        get => _slab.GetByte(Base + SaveFormat.CityResident);
        set => _slab.SetByte(Base + SaveFormat.CityResident, value);
    }

    public string ResidentName => ResidentBook.NameOf(Resident);

    /// <summary>How strongly the residents hold it. Zero alongside a zero resident means an open town.</summary>
    public int Strength
    {
        get => _slab.GetByte(Base + SaveFormat.CityStrength);
        set => _slab.SetByte(Base + SaveFormat.CityStrength, value);
    }

    public int GetCache(int slot) => _slab.GetByte(Base + SaveFormat.CityCache + slot);

    public bool SetCache(int slot, int value) =>
        _slab.SetByte(Base + SaveFormat.CityCache + slot, Math.Clamp(value, 0, SaveFormat.MaxCacheItem));

    public int CacheTotal
    {
        get
        {
            int sum = 0;
            for (int s = 0; s < CacheSlots; s++) sum += GetCache(s);
            return sum;
        }
    }

    /// <summary>Fills all five cache slots to the engine's per-slot ceiling of 255.</summary>
    public void FillCache()
    {
        for (int s = 0; s < CacheSlots; s++) SetCache(s, SaveFormat.MaxCacheItem);
    }

    /// <summary>Empties the town of residents, which is what stops residential encounters there.</summary>
    public void Clear()
    {
        Resident = ResidentBook.NoOne;
        Strength = 0;
    }
}
