using System.Collections.ObjectModel;
using GameTrainers.Common.Mvvm;
using Roadwar2000Trainer.Game;

namespace Roadwar2000Trainer.ViewModels;

/// <summary>One of the fifteen vehicle slots, as a grid row plus an editing surface.</summary>
public sealed class VehicleSlotViewModel : ObservableObject
{
    private readonly VehiclesViewModel _owner;

    public VehicleSlotViewModel(VehiclesViewModel owner, int slot)
    {
        _owner = owner;
        Slot = slot;
    }

    public int Slot { get; }

    /// <summary>The game numbers vehicles from 1.</summary>
    public int Number => Slot + 1;

    private bool _inUse;
    /// <summary>True when the slot is within the gang's vehicle count and holds a sane record.</summary>
    public bool InUse { get => _inUse; private set => SetField(ref _inUse, value); }

    private string _typeName = "";
    public string TypeName { get => _typeName; private set => SetField(ref _typeName, value); }

    private int _typeId;
    public int TypeId
    {
        get => _typeId;
        set { if (SetField(ref _typeId, value)) _owner.WriteSlot(Slot, v => v.TypeId = value); }
    }

    private int _structure;
    public int Structure
    {
        get => _structure;
        set
        {
            if (!SetField(ref _structure, value)) return;
            // Raising current above maximum would fail VehicleRecord.LooksValid(), which is what
            // decides whether the slot shows as in use and whether the repair freeze touches it.
            _owner.WriteSlot(Slot, v => { v.StructureMax = Math.Max(v.StructureMax, value); v.Structure = value; });
        }
    }

    private int _structureMax;
    public int StructureMax
    {
        get => _structureMax;
        set
        {
            if (!SetField(ref _structureMax, value)) return;
            // Lowering the maximum below the current breaks the same LooksValid() invariant that
            // the Structure setter guards from the other side, so the current follows it down.
            _owner.WriteSlot(Slot, v => { v.StructureMax = value; v.Structure = Math.Min(v.Structure, value); });
        }
    }

    private int _tires;
    public int Tires
    {
        get => _tires;
        set
        {
            if (!SetField(ref _tires, value)) return;
            _owner.WriteSlot(Slot, v => { v.TiresMax = Math.Max(v.TiresMax, value); v.Tires = value; });
        }
    }

    private int _tiresMax;
    public int TiresMax
    {
        get => _tiresMax;
        set
        {
            if (!SetField(ref _tiresMax, value)) return;
            _owner.WriteSlot(Slot, v => { v.TiresMax = value; v.Tires = Math.Min(v.Tires, value); });
        }
    }

    private int _maxSpeed;
    /// <summary>Top speed in tens of MPH, as the engine stores it.</summary>
    public int MaxSpeed
    {
        get => _maxSpeed;
        set { if (SetField(ref _maxSpeed, value)) { _owner.WriteSlot(Slot, v => v.MaxSpeed = value); OnPropertyChanged(nameof(MaxSpeedMph)); } }
    }

    public int MaxSpeedMph => _maxSpeed * 10;

    private int _maneuver;
    public int Maneuver
    {
        get => _maneuver;
        set { if (SetField(ref _maneuver, value)) _owner.WriteSlot(Slot, v => { v.Maneuver = value; v.ManeuverMax = Math.Max(v.ManeuverMax, value); }); }
    }

    private int _braking;
    public int Braking
    {
        get => _braking;
        set { if (SetField(ref _braking, value)) _owner.WriteSlot(Slot, v => v.Braking = value); }
    }

    private int _acceleration;
    public int Acceleration
    {
        get => _acceleration;
        set { if (SetField(ref _acceleration, value)) _owner.WriteSlot(Slot, v => v.Acceleration = value); }
    }

    private int _protLeft, _protRight, _protFront, _protBack, _protTop;

    public int ProtectLeft { get => _protLeft; set { if (SetField(ref _protLeft, value)) _owner.WriteSlot(Slot, v => v.SetProtection(0, value)); } }
    public int ProtectRight { get => _protRight; set { if (SetField(ref _protRight, value)) _owner.WriteSlot(Slot, v => v.SetProtection(1, value)); } }
    public int ProtectFront { get => _protFront; set { if (SetField(ref _protFront, value)) _owner.WriteSlot(Slot, v => v.SetProtection(2, value)); } }
    public int ProtectBack { get => _protBack; set { if (SetField(ref _protBack, value)) _owner.WriteSlot(Slot, v => v.SetProtection(3, value)); } }
    public int ProtectTop { get => _protTop; set { if (SetField(ref _protTop, value)) _owner.WriteSlot(Slot, v => v.SetProtection(4, value)); } }

    private int _crewAboard;
    public int CrewAboard { get => _crewAboard; private set => SetField(ref _crewAboard, value); }

    private int _capacity;
    public int CarryingCapacity { get => _capacity; private set => SetField(ref _capacity, value); }

    private string _summary = "";
    public string Summary { get => _summary; private set => SetField(ref _summary, value); }

    /// <summary>Pulls every bound value from the record without echoing writes back.</summary>
    internal void Load(VehicleRecord v, bool inUse)
    {
        InUse = inUse && v.LooksValid();
        SetField(ref _typeId, v.TypeId, nameof(TypeId));
        TypeName = v.TypeName;
        SetField(ref _structure, v.Structure, nameof(Structure));
        SetField(ref _structureMax, v.StructureMax, nameof(StructureMax));
        SetField(ref _tires, v.Tires, nameof(Tires));
        SetField(ref _tiresMax, v.TiresMax, nameof(TiresMax));
        SetField(ref _maxSpeed, v.MaxSpeed, nameof(MaxSpeed));
        OnPropertyChanged(nameof(MaxSpeedMph));
        SetField(ref _maneuver, v.Maneuver, nameof(Maneuver));
        SetField(ref _braking, v.Braking, nameof(Braking));
        SetField(ref _acceleration, v.Acceleration, nameof(Acceleration));
        SetField(ref _protLeft, v.GetProtection(0), nameof(ProtectLeft));
        SetField(ref _protRight, v.GetProtection(1), nameof(ProtectRight));
        SetField(ref _protFront, v.GetProtection(2), nameof(ProtectFront));
        SetField(ref _protBack, v.GetProtection(3), nameof(ProtectBack));
        SetField(ref _protTop, v.GetProtection(4), nameof(ProtectTop));
        CrewAboard = v.CrewAboard;
        CarryingCapacity = v.CarryingCapacity;
        Summary = InUse
            ? $"{v.TypeName} - structure {v.Structure}/{v.StructureMax}, tires {v.Tires}/{v.TiresMax}, " +
              $"{v.MaxSpeed * 10} MPH, crew {v.CrewAboard}, {v.CarryingCapacity} spaces"
            : "(empty slot)";
    }

    /// <summary>Blanks the row when the trainer detaches.</summary>
    internal void Clear()
    {
        InUse = false;
        TypeName = "";
        Summary = "(not attached)";
    }
}

/// <summary>The Vehicles tab: fifteen slots, an editor for the selected one, and fleet actions.</summary>
public sealed class VehiclesViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public VehiclesViewModel(MainViewModel main)
    {
        _main = main;
        for (int i = 0; i < SaveFormat.MaxVehicleSlots; i++) Slots.Add(new VehicleSlotViewModel(this, i));
        Selected = Slots[0];
        VehicleTypes = VehicleBook.All;

        RepairSelectedCommand = new RelayCommand(RepairSelected, () => _main.CanEdit && Selected is { InUse: true });
        MaximizeSelectedCommand = new RelayCommand(MaximizeSelected, () => _main.CanEdit && Selected is { InUse: true });
        RepairAllCommand = new RelayCommand(RepairAll, () => _main.CanEdit);
        MaximizeAllCommand = new RelayCommand(MaximizeAll, () => _main.CanEdit);
        AddVehicleCommand = new RelayCommand(AddVehicle, CanAddVehicle);
        RemoveLastCommand = new RelayCommand(RemoveLast, () => _main.CanEdit && VehicleCount > 0);
        FillFleetCommand = new RelayCommand(FillFleet, () => _main.CanEdit);
    }

    public ObservableCollection<VehicleSlotViewModel> Slots { get; } = new();

    public IReadOnlyList<VehicleType> VehicleTypes { get; }

    private VehicleSlotViewModel? _selected;
    public VehicleSlotViewModel? Selected
    {
        get => _selected;
        set
        {
            if (!SetField(ref _selected, value)) return;
            RepairSelectedCommand?.RaiseCanExecuteChanged();
            MaximizeSelectedCommand?.RaiseCanExecuteChanged();
        }
    }

    // Trailer truck: the biggest thing on the road, and the sane default for "add a vehicle".
    private VehicleType _newVehicleType = VehicleBook.All[18];
    public VehicleType NewVehicleType
    {
        get => _newVehicleType;
        set => SetField(ref _newVehicleType, value);
    }

    private int _vehicleCount;
    public int VehicleCount { get => _vehicleCount; private set => SetField(ref _vehicleCount, value); }

    private int _maxVehicles;
    public int MaxVehicles { get => _maxVehicles; private set => SetField(ref _maxVehicles, value); }

    public RelayCommand RepairSelectedCommand { get; }
    public RelayCommand MaximizeSelectedCommand { get; }
    public RelayCommand RepairAllCommand { get; }
    public RelayCommand MaximizeAllCommand { get; }
    public RelayCommand AddVehicleCommand { get; }
    public RelayCommand RemoveLastCommand { get; }
    public RelayCommand FillFleetCommand { get; }

    private GameSlab? Slab => _main.Slab;

    internal void WriteSlot(int slot, Action<VehicleRecord> apply)
    {
        if (_main.SuppressWriteBack) return;
        if (!_main.CanEdit || Slab is not { } slab) return;
        apply(new VehicleRecord(slab, slot));
    }

    /// <summary>Rebuilds the whole list -- used on attach, detach and after structural changes.</summary>
    public void Reload()
    {
        if (Slab is not { } slab || _main.GangRecord is not { } gang)
        {
            foreach (var s in Slots) s.Clear();
            VehicleCount = 0;
            MaxVehicles = 0;
            RaiseAll();
            return;
        }

        VehicleCount = gang.VehicleCount;
        MaxVehicles = gang.MaxVehicles;
        for (int i = 0; i < Slots.Count; i++)
            Slots[i].Load(new VehicleRecord(slab, i), i < VehicleCount);
        RaiseAll();
    }

    /// <summary>Cheaper refresh for the polling tick: values only, no rebinding.</summary>
    public void RefreshValues() => Reload();

    private void RaiseAll()
    {
        RepairSelectedCommand.RaiseCanExecuteChanged();
        MaximizeSelectedCommand.RaiseCanExecuteChanged();
        RepairAllCommand.RaiseCanExecuteChanged();
        MaximizeAllCommand.RaiseCanExecuteChanged();
        AddVehicleCommand.RaiseCanExecuteChanged();
        RemoveLastCommand.RaiseCanExecuteChanged();
        FillFleetCommand.RaiseCanExecuteChanged();
    }

    private bool CanAddVehicle() =>
        _main.CanEdit && _main.GangRecord is { } g && g.VehicleCount < SaveFormat.MaxVehicleSlots;

    private void RepairSelected()
    {
        if (Selected is not { } sel) return;
        WriteSlot(sel.Slot, v => v.Repair());
        _main.Report($"Vehicle {sel.Number} repaired.");
        _main.Refresh(force: false);
    }

    private void MaximizeSelected()
    {
        if (Selected is not { } sel) return;
        WriteSlot(sel.Slot, v => v.Maximize());
        _main.Report($"Vehicle {sel.Number} upgraded to solid-metal armour and a full speed package.");
        _main.Refresh(force: false);
    }

    /// <summary>
    /// How many slots to act on. <see cref="GangRecord.VehicleCount"/> is a raw byte out of guest
    /// RAM -- the setter clamps, but the getter cannot -- so a garbage or half-loaded slab could
    /// name more slots than the fifteen that exist, and indexing slot 19 would run off the end of
    /// the 6,512-byte cache.
    /// </summary>
    private int LiveSlots => Math.Clamp(_main.GangRecord?.VehicleCount ?? 0, 0, SaveFormat.MaxVehicleSlots);

    private void RepairAll()
    {
        int n = LiveSlots;
        for (int i = 0; i < n; i++) WriteSlot(i, v => v.Repair());
        _main.Report($"Repaired {n} vehicle(s).");
        _main.Refresh(force: false);
    }

    private void MaximizeAll()
    {
        int n = LiveSlots;
        for (int i = 0; i < n; i++) WriteSlot(i, v => v.Maximize());
        _main.Report($"Upgraded {n} vehicle(s).");
        _main.Refresh(force: false);
    }

    /// <summary>
    /// Writes a factory-fresh vehicle into the next free slot and raises the gang's vehicle
    /// count. The ceiling is raised too when needed, because the engine will not let the gang
    /// hold more vehicles than the ceiling allows and would otherwise drop the new one.
    /// </summary>
    private void AddVehicle()
    {
        if (Slab is not { } slab || _main.GangRecord is not { } gang) return;
        int slot = gang.VehicleCount;
        if (slot >= SaveFormat.MaxVehicleSlots) return;

        var record = new VehicleRecord(slab, slot);
        if (!record.Fill(NewVehicleType))
        {
            _main.Report("Could not write the new vehicle; the game may have moved on.");
            return;
        }

        gang.VehicleCount = slot + 1;
        if (gang.MaxVehicles < gang.VehicleCount) gang.MaxVehicles = gang.VehicleCount;

        _main.Refresh(force: true);
        if (gang.VehicleCount != slot + 1)
        {
            _main.Report($"The {NewVehicleType.Name} record was written but the vehicle count was not, " +
                         "so the game cannot see it. Attach again and retry.");
            return;
        }

        _main.Report($"Added a {NewVehicleType.Name} as vehicle {slot + 1}. " +
                     "It has no crew yet -- move some aboard in-game, or it cannot fight.");
    }

    private void RemoveLast()
    {
        if (_main.GangRecord is not { } gang || gang.VehicleCount == 0) return;
        int wanted = gang.VehicleCount - 1;
        gang.VehicleCount = wanted;
        _main.Refresh(force: true);
        _main.Report(gang.VehicleCount == wanted
            ? $"Vehicle count reduced to {wanted}. Any crew that was aboard the dropped vehicle is gone with it."
            : "The vehicle count could not be written; attach again and retry.");
    }

    /// <summary>Fills every remaining slot up to the engine's fifteen with the chosen type.</summary>
    private void FillFleet()
    {
        if (Slab is not { } slab || _main.GangRecord is not { } gang) return;
        int added = 0;
        for (int slot = gang.VehicleCount; slot < SaveFormat.MaxVehicleSlots; slot++)
        {
            if (!new VehicleRecord(slab, slot).Fill(NewVehicleType)) break;
            added++;
        }
        if (added == 0)
        {
            _main.Report(gang.VehicleCount >= SaveFormat.MaxVehicleSlots
                ? "The fleet is already full."
                : "Could not write a vehicle record; attach again and retry.");
            return;
        }

        int reached = Math.Min(SaveFormat.MaxVehicleSlots, gang.VehicleCount + added);
        gang.VehicleCount = reached;
        gang.MaxVehicles = Math.Max(gang.MaxVehicles, reached);
        _main.Refresh(force: true);
        _main.Report(reached >= SaveFormat.MaxVehicleSlots
            ? $"Added {added} x {NewVehicleType.Name}; the fleet is now at the engine's ceiling of 15."
            : $"Added {added} x {NewVehicleType.Name}; the fleet stopped at {reached} because a write " +
              "was refused. Attach again and retry for the rest.");
    }
}
