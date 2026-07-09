using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoduelTrainer.Memory;

/// <summary>
/// One entry in the car's 20-slot component table. Editable fields raise change
/// notifications so the weapon rows can be bound two-way and their derived labels
/// (name, ammo) stay in step while the user edits.
/// </summary>
public sealed class ComponentInfo : INotifyPropertyChanged
{
    public int Slot { get; init; }

    private byte _type;
    public byte Type
    {
        get => _type;
        set
        {
            if (!Set(ref _type, value)) return;
            Raise(nameof(WeaponLabel));
            Raise(nameof(SelfPowered));
            Raise(nameof(AmmoEditable));
            Raise(nameof(AmmoLabel));
        }
    }

    private byte _currentDp;
    public byte CurrentDp { get => _currentDp; set => Set(ref _currentDp, value); }

    private byte _maxDp;
    public byte MaxDp { get => _maxDp; set => Set(ref _maxDp, value); }

    private byte _location;
    public byte Location
    {
        get => _location;
        set
        {
            if (!Set(ref _location, value)) return;
            Raise(nameof(WeaponLabel));
            Raise(nameof(ArmorLabel));
        }
    }

    private int _ammo;
    public int Ammo
    {
        get => _ammo;
        set { if (Set(ref _ammo, value)) Raise(nameof(AmmoLabel)); }
    }

    private bool _present;
    public bool Present { get => _present; set => Set(ref _present, value); }

    public string WeaponLabel
    {
        get
        {
            string facing = Location switch
            {
                0 => "Front", 1 => "Back", 2 => "Left", 3 => "Right", _ => $"loc {Location}"
            };
            return $"{GameData.WeaponName(Type),-16} {facing}";
        }
    }

    public string ArmorLabel
    {
        get
        {
            int facet = Location;
            return facet >= 0 && facet < GameData.ArmorFacets.Length
                ? GameData.ArmorFacets[facet] : $"facet {facet}";
        }
    }

    /// <summary>Label for a power-plant / tire row (slots 0–4), derived from the fixed slot.</summary>
    public string DrivetrainLabel
    {
        get
        {
            if (Slot == GameData.PlantSlot) return "Power plant";
            int tire = Slot - GameData.FirstTireSlot;   // 0..3
            return tire >= 0 && tire < GameData.TireLocations.Length
                ? $"{GameData.TireLocations[tire]} tire" : $"slot {Slot}";
        }
    }

    public bool SelfPowered => Type is GameData.LaserType or GameData.HeavyRocketType;

    /// <summary>False for self-powered weapons (laser / heavy rocket) whose magazine is infinite.</summary>
    public bool AmmoEditable => !SelfPowered;

    public string AmmoLabel => SelfPowered ? "∞" : Ammo.ToString();

    /// <summary>Copy the mutable fields of a fresh read into this (same-slot) instance,
    /// so the bound rows update without being torn down and rebuilt each refresh.</summary>
    public void UpdateFrom(ComponentInfo other)
    {
        Type = other.Type;
        CurrentDp = other.CurrentDp;
        MaxDp = other.MaxDp;
        Location = other.Location;
        Ammo = other.Ammo;
        Present = other.Present;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class GameSnapshot
{
    public string DriverName { get; set; } = "";
    public int Money { get; set; }
    public int Prestige { get; set; }
    public int Health { get; set; }
    public int BodyArmor { get; set; }
    public int Driving { get; set; }
    public int Marksmanship { get; set; }
    public int Mechanic { get; set; }
    public int CityId { get; set; }
    public int Day { get; set; }
    public int DayOfWeek { get; set; }
    public int TimeOfDay { get; set; }
    public bool HasCarWithPlayer { get; set; }

    public string CarName { get; set; } = "";
    public int MaxWeight { get; set; }
    public int WeightLeft { get; set; }
    public int MaxSpaces { get; set; }
    public int SpacesLeft { get; set; }
    public int Handling { get; set; }
    public int Acceleration { get; set; }
    public int Suspension { get; set; }
    public int Chassis { get; set; }
    public int CarValue { get; set; }
    public int Battery { get; set; }
    public int BatteryMax { get; set; }
    public bool HasCar { get; set; }

    public List<ComponentInfo> Weapons { get; } = new();
    public List<ComponentInfo> Armor { get; } = new();
    public List<ComponentInfo> Drivetrain { get; } = new();   // power plant + tires (slots 0–4)

    public string CityName =>
        CityId >= 0 && CityId < GameData.Cities.Length ? GameData.Cities[CityId] : $"#{CityId}";

    public string DayOfWeekName =>
        DayOfWeek >= 0 && DayOfWeek < GameData.DaysOfWeek.Length
            ? GameData.DaysOfWeek[DayOfWeek] : $"#{DayOfWeek}";
}
