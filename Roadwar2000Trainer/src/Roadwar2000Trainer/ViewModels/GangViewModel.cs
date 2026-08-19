using System.Collections.ObjectModel;
using GameTrainers.Common.Mvvm;
using Roadwar2000Trainer.Game;

namespace Roadwar2000Trainer.ViewModels;

/// <summary>One crew rank, bound as an editable row.</summary>
public sealed class CrewRowViewModel : ObservableObject
{
    private readonly GangViewModel _owner;
    private int _count;

    public CrewRowViewModel(GangViewModel owner, int rank)
    {
        _owner = owner;
        Rank = rank;
    }

    public int Rank { get; }

    public string Name => RankBook.NameOf(Rank);

    public int Count
    {
        get => _count;
        set
        {
            if (!SetField(ref _count, value)) return;
            _owner.WriteCrew(Rank, value);
        }
    }

    internal void SetQuietly(int value) => SetField(ref _count, value, nameof(Count));
}

/// <summary>
/// The Gang tab: supplies, crew, cronies, the clock and the quick actions. Everything here maps
/// to a field confirmed against the game's own Gang Status screen.
/// </summary>
public sealed class GangViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public GangViewModel(MainViewModel main)
    {
        _main = main;
        for (int r = 0; r < SaveFormat.CrewRankCount; r++) Crew.Add(new CrewRowViewModel(this, r));

        MaxSuppliesCommand = new RelayCommand(MaxSupplies, () => _main.CanEdit);
        MaxCrewCommand = new RelayCommand(MaxCrew, () => _main.CanEdit);
        HireCroniesCommand = new RelayCommand(HireCronies, () => _main.CanEdit);
        MaxEverythingCommand = new RelayCommand(MaxEverything, () => _main.CanEdit);
        RefillCommand = new RelayCommand(Refill, () => _main.CanEdit);
    }

    public ObservableCollection<CrewRowViewModel> Crew { get; } = new();

    public RelayCommand MaxSuppliesCommand { get; }
    public RelayCommand MaxCrewCommand { get; }
    public RelayCommand HireCroniesCommand { get; }
    public RelayCommand MaxEverythingCommand { get; }
    public RelayCommand RefillCommand { get; }

    private GangRecord? Gang => _main.GangRecord;

    // ---- bound values --------------------------------------------------------

    private string _name = "";
    public string GangName
    {
        get => _name;
        set { if (SetField(ref _name, value)) Write(g => g.Name = value); }
    }

    private int _food;
    public int Food { get => _food; set { if (SetField(ref _food, value)) Write(g => g.Food = value); } }

    private int _tires;
    public int Tires { get => _tires; set { if (SetField(ref _tires, value)) Write(g => g.Tires = value); } }

    private int _fuel;
    public int Fuel { get => _fuel; set { if (SetField(ref _fuel, value)) Write(g => g.Fuel = value); } }

    private int _ammo;
    public int Ammo { get => _ammo; set { if (SetField(ref _ammo, value)) Write(g => g.Ammo = value); } }

    private int _guns;
    public int Guns { get => _guns; set { if (SetField(ref _guns, value)) Write(g => g.Guns = value); } }

    private int _medical;
    public int Medical { get => _medical; set { if (SetField(ref _medical, value)) Write(g => g.Medical = value); } }

    private int _antitoxin;
    public int Antitoxin { get => _antitoxin; set { if (SetField(ref _antitoxin, value)) Write(g => g.Antitoxin = value); } }

    private int _doctor;
    public int Doctor { get => _doctor; set { if (SetField(ref _doctor, value)) Write(g => g.DoctorQuality = value); } }

    private int _drill;
    public int DrillSergeant { get => _drill; set { if (SetField(ref _drill, value)) Write(g => g.DrillSergeantQuality = value); } }

    private int _politician;
    public int Politician { get => _politician; set { if (SetField(ref _politician, value)) Write(g => g.PoliticianQuality = value); } }

    private bool _rdf;
    public bool HasRadioDirectionFinder
    {
        get => _rdf;
        set { if (SetField(ref _rdf, value)) Write(g => g.HasRadioDirectionFinder = value); }
    }

    private bool _snowTires;
    public bool HasSnowTires
    {
        get => _snowTires;
        set { if (SetField(ref _snowTires, value)) Write(g => g.HasSnowTires = value); }
    }

    private bool _fuelSpecial;
    public bool HasFuelSpecial
    {
        get => _fuelSpecial;
        set { if (SetField(ref _fuelSpecial, value)) Write(g => g.HasFuelSpecial = value); }
    }

    private int _maxVehicles;
    public int MaxVehicles
    {
        get => _maxVehicles;
        set { if (SetField(ref _maxVehicles, value)) Write(g => g.MaxVehicles = value); }
    }

    private int _day;
    public int Day { get => _day; set { if (SetField(ref _day, value)) Write(g => g.Day = value); } }

    private int _timeOfDay;
    public int TimeOfDay
    {
        get => _timeOfDay;
        set { if (SetField(ref _timeOfDay, value)) { Write(g => g.TimeOfDay = value); OnPropertyChanged(nameof(Clock)); } }
    }

    // ---- read-only summary ---------------------------------------------------

    private int _vehicleCount;
    public int VehicleCount { get => _vehicleCount; private set => SetField(ref _vehicleCount, value); }

    private int _totalCapacity;
    public int TotalCapacity { get => _totalCapacity; private set => SetField(ref _totalCapacity, value); }

    private int _passengerCapacity;
    public int PassengerCapacity { get => _passengerCapacity; private set => SetField(ref _passengerCapacity, value); }

    private int _fuelConsumption;
    public int FuelConsumption { get => _fuelConsumption; private set => SetField(ref _fuelConsumption, value); }

    private int _displayedFuel;
    /// <summary>Fuel as the game's own status screen shows it (stored less the tank reserve).</summary>
    public int DisplayedFuel { get => _displayedFuel; private set => SetField(ref _displayedFuel, value); }

    private int _totalCrew;
    public int TotalCrew { get => _totalCrew; private set => SetField(ref _totalCrew, value); }

    private int _suppliesCarried;
    public int SuppliesCarried { get => _suppliesCarried; private set => SetField(ref _suppliesCarried, value); }

    private string _location = "";
    public string Location { get => _location; private set => SetField(ref _location, value); }

    public string Clock => GameFacts.ClockOf(_timeOfDay);

    // ---- plumbing ------------------------------------------------------------

    /// <summary>
    /// Applies an edit and re-seeds the freeze snapshots. The re-seed is not optional: a ticked
    /// freeze re-applies its snapshot twice a second, so without it every deliberate write here --
    /// typed or from a quick action -- would be reverted on the next tick while the status line
    /// still claimed success.
    /// </summary>
    private void Write(Action<GangRecord> apply)
    {
        if (_main.SuppressWriteBack) return;
        if (!_main.CanEdit || Gang is not { } gang) return;
        apply(gang);
        _main.ReseedFreezes();
    }

    internal void WriteCrew(int rank, int value)
    {
        if (_main.SuppressWriteBack) return;
        if (!_main.CanEdit || Gang is not { } gang) return;
        gang.SetCrew(rank, value);
        _main.ReseedFreezes();
        TotalCrew = gang.TotalCrew;
    }

    /// <summary>Repopulates every bound value from the current slab snapshot.</summary>
    public void Reload()
    {
        if (Gang is not { } gang)
        {
            // Detached: blank the panel rather than leave the previous session's figures on screen
            // looking like live ones.
            SetField(ref _name, "", nameof(GangName));
            foreach (var row in Crew) row.SetQuietly(0);
            SetField(ref _food, 0, nameof(Food));
            SetField(ref _tires, 0, nameof(Tires));
            SetField(ref _fuel, 0, nameof(Fuel));
            SetField(ref _ammo, 0, nameof(Ammo));
            SetField(ref _guns, 0, nameof(Guns));
            SetField(ref _medical, 0, nameof(Medical));
            SetField(ref _antitoxin, 0, nameof(Antitoxin));
            SetField(ref _doctor, 0, nameof(Doctor));
            SetField(ref _drill, 0, nameof(DrillSergeant));
            SetField(ref _politician, 0, nameof(Politician));
            SetField(ref _rdf, false, nameof(HasRadioDirectionFinder));
            SetField(ref _snowTires, false, nameof(HasSnowTires));
            SetField(ref _fuelSpecial, false, nameof(HasFuelSpecial));
            SetField(ref _maxVehicles, 0, nameof(MaxVehicles));
            SetField(ref _day, 0, nameof(Day));
            SetField(ref _timeOfDay, 0, nameof(TimeOfDay));
            OnPropertyChanged(nameof(Clock));
            VehicleCount = TotalCapacity = PassengerCapacity = 0;
            FuelConsumption = DisplayedFuel = TotalCrew = SuppliesCarried = 0;
            Location = "";
            RaiseCommands();
            return;
        }

        SetField(ref _name, gang.Name, nameof(GangName));
        SetField(ref _food, gang.Food, nameof(Food));
        SetField(ref _tires, gang.Tires, nameof(Tires));
        SetField(ref _fuel, gang.Fuel, nameof(Fuel));
        SetField(ref _ammo, gang.Ammo, nameof(Ammo));
        SetField(ref _guns, gang.Guns, nameof(Guns));
        SetField(ref _medical, gang.Medical, nameof(Medical));
        SetField(ref _antitoxin, gang.Antitoxin, nameof(Antitoxin));
        SetField(ref _doctor, gang.DoctorQuality, nameof(Doctor));
        SetField(ref _drill, gang.DrillSergeantQuality, nameof(DrillSergeant));
        SetField(ref _politician, gang.PoliticianQuality, nameof(Politician));
        SetField(ref _rdf, gang.HasRadioDirectionFinder, nameof(HasRadioDirectionFinder));
        SetField(ref _snowTires, gang.HasSnowTires, nameof(HasSnowTires));
        SetField(ref _fuelSpecial, gang.HasFuelSpecial, nameof(HasFuelSpecial));
        SetField(ref _maxVehicles, gang.MaxVehicles, nameof(MaxVehicles));
        SetField(ref _day, gang.Day, nameof(Day));
        SetField(ref _timeOfDay, gang.TimeOfDay, nameof(TimeOfDay));
        OnPropertyChanged(nameof(Clock));

        for (int r = 0; r < Crew.Count; r++) Crew[r].SetQuietly(gang.GetCrew(r));

        VehicleCount = gang.VehicleCount;
        TotalCapacity = gang.TotalCapacity;
        PassengerCapacity = gang.PassengerCapacity;
        FuelConsumption = gang.FuelConsumption;
        DisplayedFuel = gang.DisplayedFuel;
        TotalCrew = gang.TotalCrew;
        SuppliesCarried = gang.SuppliesCarried;
        Location = gang.LocationName;

        RaiseCommands();
    }

    private void RaiseCommands()
    {
        MaxSuppliesCommand.RaiseCanExecuteChanged();
        MaxCrewCommand.RaiseCanExecuteChanged();
        HireCroniesCommand.RaiseCanExecuteChanged();
        MaxEverythingCommand.RaiseCanExecuteChanged();
        RefillCommand.RaiseCanExecuteChanged();
    }

    // ---- quick actions -------------------------------------------------------

    /// <summary>
    /// Sets each supply to 9,999 rather than 65,535. The engine's fields are 16-bit and would
    /// hold more, but the Gang Status screen prints supplies in a five-column field and the
    /// carried-supply total is what the game compares against cargo capacity -- a number large
    /// enough to overflow either is a cosmetic mess for no benefit.
    /// </summary>
    private void MaxSupplies()
    {
        if (Gang is not { } gang) return;
        gang.Food = 9999;
        gang.Tires = 9999;
        gang.Fuel = 9999;
        gang.Ammo = 30000;
        gang.Guns = 9999;
        gang.Medical = 999;
        gang.Antitoxin = 255;
        _main.ReseedFreezes();
        _main.Report("Supplies topped up.");
        _main.Refresh(force: false);
    }

    /// <summary>
    /// Fills the roster with 250 of each rank. The cap is deliberate: crew have to fit in the
    /// fleet's seats, and a gang far larger than its vehicles can carry starves and deserts.
    /// </summary>
    private void MaxCrew()
    {
        if (Gang is not { } gang) return;
        for (int r = 0; r < SaveFormat.CrewRankCount; r++) gang.SetCrew(r, 250);
        _main.ReseedFreezes();
        _main.Report("Crew raised to 250 of each rank (1,250 total). " +
                     "Check the fleet has seats for them -- see Passenger capacity.");
        _main.Refresh(force: false);
    }

    /// <summary>Gives the gang all three cronies at top skill plus the Radio Direction Finder.</summary>
    private void HireCronies()
    {
        if (Gang is not { } gang) return;
        gang.DoctorQuality = 9;
        gang.DrillSergeantQuality = 9;
        gang.PoliticianQuality = 9;
        gang.HasRadioDirectionFinder = true;
        _main.Report("Doctor, drill sergeant and politician hired at top skill; RDF fitted.");
        _main.Refresh(force: false);
    }

    /// <summary>Refills only what a long haul actually consumes, leaving the rest of the game alone.</summary>
    private void Refill()
    {
        if (Gang is not { } gang) return;
        gang.Food = Math.Max(gang.Food, gang.TotalCrew * 20);
        gang.Fuel = Math.Max(gang.Fuel, Math.Max(500, gang.FuelConsumption * 40));
        gang.Ammo = Math.Max(gang.Ammo, 20000);
        _main.ReseedFreezes();
        _main.Report("Topped up food for 20 nights and fuel for 40 moves.");
        _main.Refresh(force: false);
    }

    private void MaxEverything()
    {
        if (Gang is not { } gang) return;
        MaxSupplies();
        MaxCrew();
        HireCronies();
        gang.HasSnowTires = true;
        gang.HasFuelSpecial = true;
        gang.MaxVehicles = SaveFormat.MaxVehicleSlots;
        _main.Report("Supplies, crew, cronies, snow tires, fuel special and a 15-vehicle ceiling applied.");
        _main.Refresh(force: true);
    }
}
